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
    // 数据结构 (保持引用兼容)
    // ===================================================================================
    [Serializable]
    public class FileData { public string fileName; public string md5; public long size; }

    [Serializable]
    public class FileManifest
    {
        public string version;
        public List<FileData> files = new List<FileData>();
        [NonSerialized] private Dictionary<string, FileData> _lookup;
        public Dictionary<string, FileData> Lookup {
            get {
                if (_lookup == null) {
                    _lookup = new Dictionary<string, FileData>();
                    foreach (var f in files) _lookup[f.fileName] = f;
                }
                return _lookup;
            }
        }
    }

    public struct SemVer
    {
        public int Major; public int Minor; public int Patch;
        public static SemVer Parse(string version) {
            SemVer sv = new SemVer();
            if (string.IsNullOrEmpty(version)) return sv;
            string[] parts = version.Split('.');
            if (parts.Length >= 1) int.TryParse(parts[0], out sv.Major);
            if (parts.Length >= 2) int.TryParse(parts[1], out sv.Minor);
            if (parts.Length >= 3) int.TryParse(parts[2], out sv.Patch);
            return sv;
        }
    }

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

        private string PlatformName => 
#if UNITY_ANDROID
            "Android";
#elif UNITY_IOS
            "iOS";
#else
            "StandaloneWindows64";
#endif

        private string RemoteUrl => $"{serverRoot}/{PlatformName}/";
        private const string BundleDirName = "Bundles";
        private const string FileListName = "filelist.json";
        private const string VersionFileName = "version.txt";

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
            string json = null;
            yield return RequestLocalText(Path.Combine(InnerPath, FileListName), res => json = res);
            
            if (!string.IsNullOrEmpty(json))
            {
                try {
                    var manifest = JsonUtility.FromJson<FileManifest>(json);
                    foreach (var f in manifest.files) _innerFileLookup[f.fileName] = f;
                } catch { }
            }
            
            yield return RequestLocalText(Path.Combine(OuterPath, FileListName), res => json = res);
            
            if (!string.IsNullOrEmpty(json))
            {
                try {
                    var manifest = JsonUtility.FromJson<FileManifest>(json);
                    foreach (var f in manifest.files) _outerFileLookup[f.fileName] = f;
                } catch { }
            }
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
            string localVerPath = Path.Combine(OuterPath, VersionFileName);
            string localVerTag = File.Exists(localVerPath) ? File.ReadAllText(localVerPath).Trim() : "";

            if (localVerTag == remoteVerTag)
            {
                Debug.Log("<color=green>[Boot]</color> 资源已是最新");
                LocateLatestCatalogFromLocal();
                onComplete?.Invoke(true);
                yield break;
            }

            // C. 执行下载序列
            string verStr = remoteVerTag.Split('_')[0];
            string remoteManifestUrl = RemoteUrl + $"filelist_{verStr}.json";
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
                    string relativePath = idx >= 0 ? location.InternalId.Substring(idx + platformKey.Length) : Path.GetFileName(location.InternalId);

                    string sandBoxPath = Path.Combine(OuterPath, relativePath);
                    if (File.Exists(sandBoxPath)) return "file:////" + sandBoxPath;

                    if (_innerFileLookup.ContainsKey(relativePath))
                    {
                        string innerPath = Path.Combine(InnerPath, relativePath);
                        if (Application.platform != RuntimePlatform.Android) innerPath = "file:////" + innerPath;
                        return innerPath;
                    }
                }
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
                bool isNotInOuter = !_outerFileLookup.TryGetValue(rf.fileName, out var outerF) || outerF.md5 != rf.md5;
                bool isNotInInner = !_innerFileLookup.TryGetValue(rf.fileName, out var interF) || interF.md5 != rf.md5;

                if (isNotInOuter && isNotInInner) {
                    downloadList.Add(rf);
                    totalSize += rf.size;
                }
            }

            if (downloadList.Count == 0) yield break;

            long finishedSize = 0;
            foreach (var file in downloadList)
            {
                string tempPath = Path.Combine(OuterPath, file.fileName + ".tmp");
                using (UnityWebRequest www = UnityWebRequest.Get(RemoteUrl + file.fileName)) {
                    www.downloadHandler = new DownloadHandlerFile(tempPath);
                    yield return www.SendWebRequest();

                    if (www.result == UnityWebRequest.Result.Success && GetFileMD5(tempPath) == file.md5) {
                        string dest = Path.Combine(OuterPath, file.fileName);
                        if (File.Exists(dest)) File.Delete(dest);
                        File.Move(tempPath, dest);
                        finishedSize += file.size;
                        UpdateUI((float)finishedSize / totalSize, $"更新资源: {file.fileName}", true);
                    }
                }
            }
        }
        
        private IEnumerator CleanUpOuterFiles(FileManifest remoteManifest)
        {
            if (!Directory.Exists(OuterPath)) yield break;
            try
            {
                var allLocalFiles = Directory.GetFiles(OuterPath, "*.*", SearchOption.AllDirectories);
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
                    if (f.fileName.StartsWith("catalog_") && f.fileName.EndsWith(".json")) {
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
                string typeName = $"Game.Runtime.Hotfix.GameEntry, {assemblyName}";
                Type.GetType(typeName)?.GetMethod("StartGame")?.Invoke(null, null);
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
            UpdateVersionDisplay();
        }

        // 获取版本号
        private void UpdateVersionDisplay()
        {
            if (!versionText) versionText = GameObject.Find("Version_Text")?.GetComponent<TextMeshProUGUI>();
            if (versionText)
            {
                string resVersion = "";
                string innerVerPath = Path.Combine(InnerPath, VersionFileName);
                string outerVerPath = Path.Combine(OuterPath, VersionFileName);
                string fullVersion = File.Exists(outerVerPath) ? File.ReadAllText(outerVerPath).Trim() : File.ReadAllText(innerVerPath).Trim();
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
                versionText.text = $"App:v{Application.version} Res:{resVersion}";
            }
        }

        // ===================================================================================
        // MD5 及 IO 基础工具
        // ===================================================================================
        
        private static string GetFileMD5(string filePath)
        {
            try
            {
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(filePath))
                    {
                        byte[] hash = md5.ComputeHash(stream);
                        StringBuilder sb = new StringBuilder();
                        for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                        return sb.ToString();
                    }
                }
            }
            catch { return ""; }
        }
        
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
