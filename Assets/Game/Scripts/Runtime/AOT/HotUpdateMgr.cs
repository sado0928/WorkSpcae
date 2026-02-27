using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HybridCLR;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace Game.Runtime.AOT
{
    // ===================================================================================
    // 热更新管理器 (开发者重构版：MD5 强校验与语义化驱动)
    // ===================================================================================
    public class HotUpdateMgr : MonoBehaviour
    {
        [Header("UI 引用")]
        public Slider progressSlider;
        public TextMeshProUGUI infoText;
        public TextMeshProUGUI versionText;

        [Header("配置")]
        public string serverRoot = "http://localhost:8888/";
        public string hotfixAssemblyName = "Game.Runtime.Hotfix.dll";
        public string gameEntryName = "Game.Runtime.Hotfix.GameEntry";
        public string gameEntryStartFuncName = "StartGame";

        private string PlatformName => 
#if UNITY_ANDROID
            "Android";
#elif UNITY_IOS
            "iOS";
#else
            "StandaloneWindows64";
#endif

        private string RemoteUrl => $"{serverRoot}/{PlatformName}/";
        private const string BundleDirName = HotUpdateConsts.BundleDirName;
        private const string FileListName = HotUpdateConsts.FileListName;
        private const string VersionFileName = HotUpdateConsts.VersionFileName;

        private string InnerPath => Path.Combine(Application.streamingAssetsPath, BundleDirName);
        private string OuterPath => 
#if !UNITY_EDITOR
            Path.Combine(Application.persistentDataPath, BundleDirName);
#else
            Path.Combine(Application.persistentDataPath, "EditorDev", BundleDirName);
#endif

        // 内置资源快照
        private Dictionary<string, FileData> _innerFileLookup = new Dictionary<string, FileData>();
        // 外置资源快照
        private Dictionary<string, FileData> _outerFileLookup = new Dictionary<string, FileData>();
        private string _latestCatalogFilename;
        private string _fullVersionTag;
        private void Start()
        {
            InitializeUI();
            StartCoroutine(BootSequence());
        }

        // ===================================================================================
        // 核心引导序列 (语义逻辑块支撑)
        // ===================================================================================
        private IEnumerator BootSequence()
        {
            Debug.Log("<color=cyan>[Boot]</color> 引导程序开始运行...");
            
            // [资源分析] 建立包内外资源快照
            yield return ScanLocalAssets();

            // [网络同步] 热更新校验
            bool isReady = false;
            yield return SyncRemoteAssets(ready => isReady = ready);
            if (!isReady) yield break;

            // [引擎激活] 初始化 Addressables 资源系统
            yield return BootResourceEngine();

            // [环境注入] 加载元数据并启动热更新程序集
            yield return InjectHotfixRuntime();

            // [生命周期移交]
            LaunchGame();
        }
        
        // ===================================================================================
        // 语义方法块 : 分析本地资源
        // ===================================================================================
        private IEnumerator ScanLocalAssets()
        {
            UpdateUI(0, "正在分析本地资源...");
            // filelist
            string json = null;
            yield return RequestLocalText(Path.Combine(InnerPath, FileListName), res => json = res);
            if (!string.IsNullOrEmpty(json))
            {
                try {
                    var manifest = JsonUtility.FromJson<FileManifest>(json);
                    foreach (var f in manifest.files) _innerFileLookup[f.fileName] = f;
                } catch { }
            }
            // 版本号
            string fullVersionTag = null;
            yield return RequestLocalText(Path.Combine(InnerPath, VersionFileName), res => fullVersionTag = res?.Trim());
            if (!string.IsNullOrEmpty(fullVersionTag))
            {
                _fullVersionTag = fullVersionTag;
            }
            // 包外路径
            if (!Directory.Exists(OuterPath)) Directory.CreateDirectory(OuterPath);
            
            yield return RequestLocalText(Path.Combine(OuterPath, FileListName), res => json = res);
            if (!string.IsNullOrEmpty(json))
            {
                try {
                    var manifest = JsonUtility.FromJson<FileManifest>(json);
                    foreach (var f in manifest.files) _outerFileLookup[f.fileName] = f;
                } catch { }
            }

            yield return RequestLocalText(Path.Combine(OuterPath, VersionFileName), res => fullVersionTag = res?.Trim());
            if (!string.IsNullOrEmpty(fullVersionTag))
            {
                _fullVersionTag = fullVersionTag;
            }

            UpdateVersionDisplay();
        }
        
        // ===================================================================================
        // 语义方法块 : 远端同步逻辑
        // ===================================================================================
        private IEnumerator SyncRemoteAssets(Action<bool> onComplete)
        {
            UpdateUI(0, "检查版本更新...");

            // A. 获取远端信任标识 (1.0.0_md5)
            string remoteVerTag = null;
            yield return RequestRemoteText(RemoteUrl + VersionFileName, res => remoteVerTag = res);

            if (string.IsNullOrEmpty(remoteVerTag))
            {
                Debug.Log("<color=yellow>[Boot]</color> 离线运行模式");
                LocateLatestCatalogFromLocal();
                onComplete?.Invoke(true);
                yield break;
            }
            // B. 差异化判定
            string[] remoteParts = remoteVerTag.Split('_');
            SemVer remoteSemVer = SemVer.Parse(remoteParts[0]);
            
            string localVerPath = Path.Combine(OuterPath, VersionFileName);
            string localVerTag = File.Exists(localVerPath) ? File.ReadAllText(localVerPath).Trim() : "";
            
            if (string.IsNullOrEmpty(localVerTag))
            {
                // 引擎版本号
                SemVer appSemVer = SemVer.Parse(Application.version);
                // 强制更新
                if (remoteSemVer.Major > appSemVer.Major)
                {
                    Debug.Log("<color=yellow>[Boot]</color> 检测到重大版本更新，请前往下载最新版本包");
                    onComplete?.Invoke(false); 
                    yield break;
                }
            }
            else
            {
                string[] localParts = localVerTag.Split('_');
                SemVer localSemVer = SemVer.Parse(localParts[0]);
              
                // 强制更新
                if (remoteSemVer.Major > localSemVer.Major)
                {
                    Debug.Log("<color=yellow>[Boot]</color> 检测到重大版本更新，请前往下载最新版本包");
                    onComplete?.Invoke(false); 
                    yield break;
                }
            }
            
            // C. 获取远端资源清单
            string verStr = remoteVerTag.Split('_')[0];
            string remoteManifestUrl = RemoteUrl + string.Format(HotUpdateConsts.RemoteFileListName,verStr);
            string remoteJson = null;
            yield return RequestRemoteText(remoteManifestUrl, res => remoteJson = res);

            if (!string.IsNullOrEmpty(remoteJson))
            {
                FileManifest remoteManifest = JsonUtility.FromJson<FileManifest>(remoteJson);
                yield return DownloadMissingAssets(remoteManifest);
                yield return CleanUpOuterFiles(remoteManifest);
                // D. 写入校验文件 (全部成功后再写入，保证原子性)
                File.WriteAllText(Path.Combine(OuterPath, FileListName), remoteJson);
                File.WriteAllText(localVerPath, remoteVerTag);
                LocateLatestCatalogFromLocal();
                
                // E. 同步版本号
                _fullVersionTag = remoteVerTag;
                UpdateVersionDisplay();
            }
            
            onComplete?.Invoke(true);
        }

        // ===================================================================================
        // 语义方法块 : 资源系统激活
        // ===================================================================================
        private IEnumerator BootResourceEngine()
        {
            UpdateUI(0, "正在激活资源引擎...");

            // 重定向：如果是热更地址且沙盒存在，否则走本地
            Addressables.InternalIdTransformFunc = (location) => {
                if (location.InternalId.StartsWith("http") || location.InternalId.StartsWith("ftp"))
                {
                    string platformKey = $"/{PlatformName}/";
                    int idx = location.InternalId.LastIndexOf(platformKey);
                    // 提取相对路径（如 Hotfix/Game.Runtime.Hotfix.dll.bytes）
                    string relativePath = idx >= 0 
                        ? location.InternalId.Substring(idx + platformKey.Length) 
                        : Path.GetFileName(location.InternalId);

                    // 1. 优先检查沙盒路径（PersistentDataPath）
                    string sandBoxPath = Path.Combine(OuterPath, relativePath);
                    sandBoxPath = sandBoxPath.Replace("\\", "/"); // 统一分隔符为 /
                    if (File.Exists(sandBoxPath))
                    {
                        // 安卓沙盒文件用 file:/// 前缀（沙盒是本地可读写目录，支持该协议）
                        return "file:///" + sandBoxPath;
                    }

                    // 2. 沙盒无则检查首包 StreamingAssets
                    if (_innerFileLookup.ContainsKey(relativePath))
                    {
                        string innerPath = Path.Combine(InnerPath, relativePath);
                        innerPath = innerPath.Replace("\\", "/"); // 统一分隔符

                        // 安卓专属：StreamingAssets 必须用 jar:file 协议
                        if (Application.platform == RuntimePlatform.Android)
                        {
                            // 拼接安卓 StreamingAssets 标准路径：jar:file:///应用APK路径!/assets/相对路径
                            innerPath = $"jar:file://{Application.dataPath}!/assets/{BundleDirName}/{relativePath}";
                        }
                        else
                        {
                            // 其他平台（Windows/iOS/macOS）用 file:/// 前缀
                            innerPath = "file:///" + innerPath;
                        }
                        return innerPath;
                    }
                }
                // 非远程地址，返回原 InternalId
                return location.InternalId;
            };

            yield return Addressables.InitializeAsync();

            // 彻底清理默认 Locator，确保热更索引表是全局唯一的
            Addressables.ClearResourceLocators();
            
            if (!string.IsNullOrEmpty(_latestCatalogFilename))
            {
                string outerCatalogPath = Path.Combine(OuterPath, _latestCatalogFilename);
                string innerCatalogPath = Path.Combine(InnerPath, _latestCatalogFilename);
                string catalogPath =  File.Exists(outerCatalogPath) ? outerCatalogPath: innerCatalogPath;
                if (File.Exists(catalogPath))
                {
                    Debug.Log($"<color=cyan>[Boot]</color> 显式加载索引: {_latestCatalogFilename}");
                    yield return Addressables.LoadContentCatalogAsync(catalogPath);
                }
            }
        }

        // ===================================================================================
        // 语义方法块 : 运行时注入 (HybridCLR)
        // ===================================================================================
        private IEnumerator InjectHotfixRuntime()
        {
#if !UNITY_EDITOR
            UpdateUI(0, "正在加载代码集...");
            // 补充 AOT 元数据
            foreach (var locator in Addressables.ResourceLocators) {
                foreach (var key in locator.Keys) {
                    string ks = key.ToString();
                    if (ks.StartsWith("Metadata/") && ks.EndsWith(".dll.bytes")) {
                        var handle = Addressables.LoadAssetAsync<TextAsset>(ks);
                        yield return handle;
                        if (handle.Status == AsyncOperationStatus.Succeeded)
                            RuntimeApi.LoadMetadataForAOTAssembly(handle.Result.bytes, HomologousImageMode.SuperSet);
                    }
                }
            }
            // 加载主 DLL
            var hotfixHandle = Addressables.LoadAssetAsync<TextAsset>($"Hotfix/{hotfixAssemblyName}.bytes");
            yield return hotfixHandle;
            if (hotfixHandle.Status == AsyncOperationStatus.Succeeded)
                Assembly.Load(hotfixHandle.Result.bytes);
#endif
            yield return null;
        }

        // ===================================================================================
        // 辅助逻辑块 (工具函数)
        // ===================================================================================

        private IEnumerator DownloadMissingAssets(FileManifest remoteManifest)
        {
            List<FileData> downloadList = new List<FileData>();
            long totalSize = 0;

            foreach (var rf in remoteManifest.files)
            {
                bool isCatalogFile = rf.fileName.StartsWith(HotUpdateConsts.CatalogPrefix);
                bool isNotInOuter = !_outerFileLookup.TryGetValue(rf.fileName, out var outerF) || outerF.md5 != rf.md5;
                bool isNotInInner = !_innerFileLookup.TryGetValue(rf.fileName, out var interF) || interF.md5 != rf.md5;
                // catalog文件必须包内都包外存在
                if (isCatalogFile && isNotInOuter)
                {
                    downloadList.Add(rf);
                    totalSize += rf.size;
                }
                else if (isNotInOuter && isNotInInner) 
                {
                    downloadList.Add(rf);
                    totalSize += rf.size;
                }
            }

            if (downloadList.Count == 0)
            {
                Debug.Log("<color=green>[Boot]</color> 资源已是最新");
                yield break;
            }
            
            long finishedSize = 0;
            foreach (var file in downloadList)
            {
                string tempPath = Path.Combine(OuterPath, file.fileName + ".tmp");
                using (UnityWebRequest www = UnityWebRequest.Get(RemoteUrl + file.fileName)) {
                    www.downloadHandler = new DownloadHandlerFile(tempPath);
                    yield return www.SendWebRequest();

                    if (www.result == UnityWebRequest.Result.Success && HotUpdateUtils.GetFileMD5(tempPath) == file.md5) {
                        string dest = Path.Combine(OuterPath, file.fileName);
                        if (File.Exists(dest)) File.Delete(dest);
                        File.Move(tempPath, dest);
                        finishedSize += file.size;
                        Debug.Log($"[HotUpdateManager] 更新资源文件: {file.fileName}");
                        UpdateUI((float)finishedSize / totalSize, $"更新资源中...", true);
                    }
                }
            }
        }
        
        private IEnumerator CleanUpOuterFiles(FileManifest remoteManifest)
        {
            if (!Directory.Exists(OuterPath)) yield break;
            try
            {
                IEnumerable<string> allLocalFiles = Directory.EnumerateFiles(
                    OuterPath, 
                    "*", // 替代 *.*，适配安卓/iOS 文件匹配规则
                    SearchOption.AllDirectories
                );
                foreach (var filePath in allLocalFiles)
                {
                    string fileName = Path.GetFileName(filePath);
                    if (fileName == FileListName || fileName == VersionFileName) continue;
                    
                    if (!remoteManifest.Lookup.ContainsKey(fileName))
                    {
                        Debug.Log($"[HotUpdateManager] 删除废弃文件: {fileName}");
                        File.Delete(filePath);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HotUpdateManager] 清理文件失败: {e.Message}");
            }
        }

        private void LocateLatestCatalogFromLocal()
        {
            string localListPath = Path.Combine(OuterPath, FileListName);
            if (!File.Exists(localListPath)) return;
            try {
                var manifest = JsonUtility.FromJson<FileManifest>(File.ReadAllText(localListPath));
                foreach (var f in manifest.files) {
                    if (f.fileName.StartsWith(HotUpdateConsts.CatalogPrefix) && f.fileName.EndsWith(".json")) {
                        _latestCatalogFilename = f.fileName;
                        break;
                    }
                }
            } catch { }
        }

        private void LaunchGame()
        {
            UpdateUI(1f, "准备就绪...");
            try {
                string assemblyName = hotfixAssemblyName.Replace(".dll", "");
                string typeName = $"{gameEntryName}, {assemblyName}";
                Type.GetType(typeName)?.GetMethod(gameEntryStartFuncName)?.Invoke(null, null);
            } catch (Exception e) { Debug.LogError($"[Boot] 启动失败: {e.Message}"); }
        }

        // ===================================================================================
        // UI 逻辑块
        // ===================================================================================

        private void UpdateUI(float progress, string msg, bool showSlider = false)
        {
            if (progressSlider) {
                progressSlider.gameObject.SetActive(showSlider);
                progressSlider.value = progress;
            }
            if (infoText) infoText.text = msg;
        }

        private void InitializeUI()
        {
            if (!progressSlider) progressSlider = GameObject.Find("Progress_Slider")?.GetComponent<Slider>();
            if (!infoText) infoText = GameObject.Find("Loading_Info_Text")?.GetComponent<TextMeshProUGUI>();
            if (progressSlider) progressSlider.gameObject.SetActive(false);
            if (!versionText) versionText = GameObject.Find("Version_Text")?.GetComponent<TextMeshProUGUI>();
            if (versionText) versionText.gameObject.SetActive(false);
        }

        // 获取版本号
        private void UpdateVersionDisplay()
        {
            if (versionText)
            {
                string resVersion = "";
                string fullVersion = _fullVersionTag;
                if (fullVersion.Contains("_"))
                {
                    string[] parts = fullVersion.Split('_');
                    string hash = parts[1];
                    // 只取 MD5 的后六位
                    string shortHash = hash.Length > 6 ? hash.Substring(hash.Length - 6) : hash;
                    resVersion = $"{parts[0]}_{shortHash}";
                }
                else
                {
                    resVersion = fullVersion.Length > 6 ? fullVersion.Substring(fullVersion.Length - 6) : fullVersion;
                }
                versionText.text = $"App:v{resVersion}";
                versionText.gameObject.SetActive(true);
            }
        }

        // ===================================================================================
        // IO 方法
        // ===================================================================================
        
        private IEnumerator RequestLocalText(string path, Action<string> onResult)
        {
            if (path.Contains("://") || Application.platform == RuntimePlatform.Android) {
                using (UnityWebRequest www = UnityWebRequest.Get(path)) {
                    yield return www.SendWebRequest();
                    onResult?.Invoke(www.result == UnityWebRequest.Result.Success ? www.downloadHandler.text : null);
                }
            } else onResult?.Invoke(File.Exists(path) ? File.ReadAllText(path) : null);
        }

        private IEnumerator RequestRemoteText(string url, Action<string> onResult)
        {
            using (UnityWebRequest www = UnityWebRequest.Get(url)) {
                www.timeout = 5; yield return www.SendWebRequest();
                onResult?.Invoke(www.result == UnityWebRequest.Result.Success ? www.downloadHandler.text.Trim() : null);
            }
        }
    }
}
