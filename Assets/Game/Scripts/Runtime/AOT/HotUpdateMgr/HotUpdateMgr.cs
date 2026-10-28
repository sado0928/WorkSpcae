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
        [Header("UI 引用")] public Slider progressSlider;
        public TextMeshProUGUI infoText;
        public TextMeshProUGUI versionText;

        [Header("配置")] public string serverRoot = "http://localhost:8888/";
        public string hotfixAssemblyName = "Game.Runtime.Hotfix.dll";
        public string gameEntryName = "Game.Runtime.Hotfix.GameEntry";
        public string gameEntryStartFuncName = "StartGame";

        [Tooltip("执行包内外扫描、真实下载和 Catalog 加载。需先构建资源，并使用 Addressables 的 Use Existing Build 模式。")]
        public bool simulateHotUpdateInEditor = false;

        private bool UseHotUpdate => !Application.isEditor || simulateHotUpdateInEditor;

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
        private bool _catalogFromInner;
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
            if (UseHotUpdate)
            {
                // [资源分析] 建立包内外资源快照
                yield return ScanLocalAssets();

                // [网络同步] 热更新校验
                bool isReady = false;
                yield return SyncRemoteAssets(ready => isReady = ready);
                if (!isReady) yield break;
            }

            // [引擎激活] 初始化 Addressables 资源系统
            bool resourceReady = false;
            yield return BootResourceEngine(ready => resourceReady = ready);
            if (!resourceReady) yield break;

            // [环境注入] 加载元数据并启动热更新程序集
            bool runtimeReady = false;
            yield return InjectHotfixRuntime(ready => runtimeReady = ready);
            if (!runtimeReady) yield break;

            // [生命周期移交]
            LaunchGame();
        }

        // ===================================================================================
        // 语义方法块 : 分析本地资源
        // ===================================================================================
        private IEnumerator ScanLocalAssets()
        {
            UpdateUI(0, "正在分析本地资源...");
            _innerFileLookup.Clear();
            _outerFileLookup.Clear();
            _fullVersionTag = Application.version;
            // filelist
            string json = null;
            yield return RequestLocalText(Path.Combine(InnerPath, FileListName), res => json = res);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var manifest = JsonUtility.FromJson<FileManifest>(json);
                    foreach (var f in manifest.files) _innerFileLookup[f.fileName] = f;
                }
                catch
                {
                }
            }

            // 版本号
            string fullVersionTag = null;
            yield return RequestLocalText(Path.Combine(InnerPath, VersionFileName),
                res => fullVersionTag = res?.Trim());
            if (!string.IsNullOrEmpty(fullVersionTag))
            {
                _fullVersionTag = fullVersionTag;
            }

            // 包外路径
            if (!Directory.Exists(OuterPath)) Directory.CreateDirectory(OuterPath);

            yield return RequestLocalText(Path.Combine(OuterPath, FileListName), res => json = res);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var manifest = JsonUtility.FromJson<FileManifest>(json);
                    foreach (var f in manifest.files) _outerFileLookup[f.fileName] = f;
                }
                catch
                {
                }
            }

            yield return RequestLocalText(Path.Combine(OuterPath, VersionFileName),
                res => fullVersionTag = res?.Trim());
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
                bool hasCatalog = !string.IsNullOrEmpty(_latestCatalogFilename);
                if (!hasCatalog) UpdateUI(0, "本地资源不可用，请连接网络后重启重试");
                onComplete?.Invoke(hasCatalog);
                yield break;
            }

            // B. 差异化判定
            string[] remoteParts = remoteVerTag.Split('_');
            if (remoteParts.Length != 2 || remoteParts[1].Length != 32)
            {
                Debug.LogError("[Boot] 远端版本信息格式错误，停止更新");
                onComplete?.Invoke(false);
                yield break;
            }

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
            string remoteManifestUrl = RemoteUrl + string.Format(HotUpdateConsts.RemoteFileListName, verStr);
            string remoteJson = null;
            yield return RequestRemoteText(remoteManifestUrl, res => remoteJson = res, false);

            if (string.IsNullOrEmpty(remoteJson))
            {
                Debug.LogError("[Boot] 获取资源清单失败，保留当前版本");
                onComplete?.Invoke(false);
                yield break;
            }

            // 清单使用原文校验，不能 Trim；MD5 用于完整性检查，不代表来源可信。
            using (var md5 = MD5.Create())
            {
                string manifestMd5 = BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(remoteJson)))
                    .Replace("-", "");
                if (!string.Equals(manifestMd5, remoteParts[1], StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError("[Boot] 资源清单 MD5 不匹配，保留当前版本");
                    onComplete?.Invoke(false);
                    yield break;
                }
            }

            if (!string.IsNullOrEmpty(remoteJson))
            {
                FileManifest remoteManifest = JsonUtility.FromJson<FileManifest>(remoteJson);
                bool downloadSucceeded = false;
                yield return DownloadMissingAssets(remoteManifest, success => downloadSucceeded = success);
                if (!downloadSucceeded)
                {
                    Debug.LogError("[Boot] 资源更新失败，不清理旧文件、不写入新版本");
                    UpdateUI(0, "资源更新失败，请检查网络后重启重试");
                    onComplete?.Invoke(false);
                    yield break;
                }

                // 暂不清理旧资源，保留恢复旧版本所需的文件。
                // yield return CleanUpOuterFiles(remoteManifest);
                // D. 写入校验文件 (全部成功后再写入，保证原子性)
                // 此处以全部下载成功为前提；多个文件的断电事务恢复需要另行设计。
                string localListPath = Path.Combine(OuterPath, FileListName);
                File.WriteAllText(localListPath + ".tmp", remoteJson);
                if (File.Exists(localListPath))
                    File.Replace(localListPath + ".tmp", localListPath, localListPath + ".bak");
                else File.Move(localListPath + ".tmp", localListPath);
                File.WriteAllText(localVerPath + ".tmp", remoteVerTag);
                if (File.Exists(localVerPath)) File.Replace(localVerPath + ".tmp", localVerPath, localVerPath + ".bak");
                else File.Move(localVerPath + ".tmp", localVerPath);
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
        private IEnumerator BootResourceEngine(Action<bool> onComplete)
        {
            UpdateUI(0, "正在激活资源引擎...");

            if (UseHotUpdate)
            {
                // 重定向：如果是热更地址且沙盒存在，否则走本地
                Addressables.InternalIdTransformFunc = (location) =>
                {
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
                        if (File.Exists(sandBoxPath) && (!_catalogFromInner ||
                                                         (_innerFileLookup.TryGetValue(relativePath,
                                                              out var innerFile) &&
                                                          HotUpdateUtils.GetFileMD5(sandBoxPath) == innerFile.md5)))
                        {
                            // 安卓沙盒文件用 file:/// 前缀（沙盒是本地可读写目录，支持该协议）
                            return new Uri(Path.GetFullPath(sandBoxPath)).AbsoluteUri;
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
                                innerPath = Path.Combine(Application.streamingAssetsPath, BundleDirName, relativePath)
                                    .Replace("\\", "/");
                            }
                            else
                            {
                                // 其他平台（Windows/iOS/macOS）用 file:/// 前缀
                                innerPath = new Uri(Path.GetFullPath(innerPath)).AbsoluteUri;
                            }

                            return innerPath;
                        }
                    }

                    // 非远程地址，返回原 InternalId
                    return location.InternalId;
                };
            }

            // 先设置重定向再初始化；失败时停止，不继续启动热更逻辑。
            var initHandle = Addressables.InitializeAsync(false);
            yield return initHandle;
            bool initialized = initHandle.Status == AsyncOperationStatus.Succeeded;
            Addressables.Release(initHandle);
            if (!initialized)
            {
                UpdateUI(0, "资源初始化失败，请重启重试");
                onComplete?.Invoke(false);
                yield break;
            }

            if (UseHotUpdate)
            {
                // 彻底清理默认 Locator，确保热更索引表是全局唯一的
                // 等新 Catalog 加载成功再移除旧 Locator，避免失败后定位器为空。
                var oldLocators = new List<IResourceLocator>(Addressables.ResourceLocators);

                if (!string.IsNullOrEmpty(_latestCatalogFilename))
                {
                    string outerCatalogPath = Path.Combine(OuterPath, _latestCatalogFilename);
                    string innerCatalogPath = Path.Combine(InnerPath, _latestCatalogFilename);
                    string catalogPath = _catalogFromInner ? innerCatalogPath : outerCatalogPath;
                    // Android 包内资源是 jar:file URL，交给 Addressables 读取，不用 File.Exists。
                    catalogPath = catalogPath.Replace("\\", "/");
                    if (!catalogPath.Contains("://")) catalogPath = new Uri(Path.GetFullPath(catalogPath)).AbsoluteUri;
                    {
                        Debug.Log($"<color=cyan>[Boot]</color> 显式加载索引: {_latestCatalogFilename}");
                        var catalogHandle = Addressables.LoadContentCatalogAsync(catalogPath, false);
                        yield return catalogHandle;
                        bool loaded = catalogHandle.Status == AsyncOperationStatus.Succeeded;
                        if (loaded)
                            foreach (var locator in oldLocators)
                                if (!ReferenceEquals(locator, catalogHandle.Result))
                                    Addressables.RemoveResourceLocator(locator);
                        Addressables.Release(catalogHandle);
                        if (!loaded)
                        {
                            UpdateUI(0, "资源目录加载失败，请联网后重启重试");
                            onComplete?.Invoke(false);
                            yield break;
                        }
                    }
                }
                else
                {
                    UpdateUI(0, "未找到可用资源目录，请联网后重启重试");
                    onComplete?.Invoke(false);
                    yield break;
                }
            }

            onComplete?.Invoke(true);
        }

        // ===================================================================================
        // 语义方法块 : 运行时注入 (HybridCLR)
        // ===================================================================================
        private IEnumerator InjectHotfixRuntime(Action<bool> onComplete)
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
                        else { UpdateUI(0, "本地元数据不可用，请联网后重启重试"); onComplete?.Invoke(false); yield break; }
                    }
                }
            }
            // 加载主 DLL
            var hotfixHandle = Addressables.LoadAssetAsync<TextAsset>($"Hotfix/{hotfixAssemblyName}.bytes");
            yield return hotfixHandle;
            if (hotfixHandle.Status == AsyncOperationStatus.Succeeded)
                Assembly.Load(hotfixHandle.Result.bytes);
            else { UpdateUI(0, "本地代码资源不完整，请联网后重启重试"); onComplete?.Invoke(false); yield break; }
#endif
            onComplete?.Invoke(true);
            yield return null;
        }

        // ===================================================================================
        // 辅助逻辑块 (工具函数)
        // ===================================================================================

        private IEnumerator DownloadMissingAssets(FileManifest remoteManifest, Action<bool> onComplete)
        {
            List<FileData> downloadList = new List<FileData>();
            long totalSize = 0;

            foreach (var rf in remoteManifest.files)
            {
                bool isCatalogFile = rf.fileName.StartsWith(HotUpdateConsts.CatalogPrefix);
                string outerFilePath = Path.Combine(OuterPath, rf.fileName);
                bool isNotInOuter = !_outerFileLookup.TryGetValue(rf.fileName, out var outerF) || outerF.md5 != rf.md5
                    || !File.Exists(outerFilePath) || HotUpdateUtils.GetFileMD5(outerFilePath) != rf.md5;
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
                onComplete?.Invoke(true);
                yield break;
            }

            long finishedSize = 0;
            UpdateUI((float)finishedSize / totalSize,
                $"更新资源中...{HotUpdateUtils.FormatBytes(finishedSize)}/{HotUpdateUtils.FormatBytes(totalSize)}", true);
            foreach (var file in downloadList)
            {
                string tempPath = Path.Combine(OuterPath, file.fileName + ".tmp");
                Directory.CreateDirectory(Path.GetDirectoryName(tempPath));
                using (UnityWebRequest www = UnityWebRequest.Get(RemoteUrl + file.fileName))
                {
                    www.downloadHandler = new DownloadHandlerFile(tempPath);
                    yield return www.SendWebRequest();

                    if (www.result == UnityWebRequest.Result.Success && HotUpdateUtils.GetFileMD5(tempPath) == file.md5)
                    {
                        finishedSize += file.size;
                        Debug.Log($"[HotUpdateManager] 更新资源文件: {file.fileName}");
                        UpdateUI((float)finishedSize / totalSize,
                            $"更新资源中...{HotUpdateUtils.FormatBytes(finishedSize)}/{HotUpdateUtils.FormatBytes(totalSize)}",
                            true);
                    }
                    else
                    {
                        Debug.LogError($"[Boot] 下载失败或 MD5 不匹配: {file.fileName}, {www.error}");
                        if (File.Exists(tempPath)) File.Delete(tempPath);
                        onComplete?.Invoke(false);
                        yield break;
                    }
                }
            }

            // 所有文件都下载并校验成功后，再替换正式文件；旧文件保留为 .bak。
            try
            {
                foreach (var file in downloadList)
                {
                    string dest = Path.Combine(OuterPath, file.fileName);
                    string tempPath = dest + ".tmp";
                    if (File.Exists(dest)) File.Replace(tempPath, dest, dest + ".bak");
                    else File.Move(tempPath, dest);
                }
            }
            catch (IOException e)
            {
                Debug.LogError($"[Boot] 替换资源失败，保留旧版本号: {e.Message}");
                onComplete?.Invoke(false);
                yield break;
            }
            catch (UnauthorizedAccessException e)
            {
                Debug.LogError($"[Boot] 无法写入资源: {e.Message}");
                onComplete?.Invoke(false);
                yield break;
            }

            onComplete?.Invoke(true);
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
                    if (fileName.EndsWith(".bak")) continue; // 保留更新前的文件，供排查/恢复。

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
            _latestCatalogFilename = null;
            _catalogFromInner = false;
            string localListPath = Path.Combine(OuterPath, FileListName);
            if (File.Exists(localListPath))
            {
                try
                {
                    var manifest = JsonUtility.FromJson<FileManifest>(File.ReadAllText(localListPath));
                    foreach (var f in manifest.files)
                    {
                        if (f.fileName.StartsWith(HotUpdateConsts.CatalogPrefix) && f.fileName.EndsWith(".json"))
                        {
                            string catalogPath = Path.Combine(OuterPath, f.fileName);
                            if (!File.Exists(catalogPath) || HotUpdateUtils.GetFileMD5(catalogPath) != f.md5) continue;
                            _latestCatalogFilename = f.fileName;
                            return;
                        }
                    }
                }
                catch
                {
                }
            }

            // 首次安装离线，或包外 Catalog 缺失/损坏时，回退到已扫描的包内清单。
            foreach (var f in _innerFileLookup.Values)
                if (f.fileName.StartsWith(HotUpdateConsts.CatalogPrefix) && f.fileName.EndsWith(".json"))
                {
                    _latestCatalogFilename = f.fileName;
                    _catalogFromInner = true;
                    return;
                }
        }

        private void LaunchGame()
        {
            UpdateUI(1f, "即将准备就绪...");
            try
            {
                string assemblyName = hotfixAssemblyName.Replace(".dll", "");
                string typeName = $"{gameEntryName}, {assemblyName}";
                Type.GetType(typeName)?.GetMethod(gameEntryStartFuncName)?.Invoke(null, null);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Boot] 启动失败: {e.Message}");
            }
        }

        // ===================================================================================
        // UI 逻辑块
        // ===================================================================================

        private void UpdateUI(float progress, string msg, bool showSlider = false)
        {
            if (progressSlider)
            {
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
            path = path.Replace("\\", "/");
            if (path.Contains("://"))
            {
                using (UnityWebRequest www = UnityWebRequest.Get(path))
                {
                    yield return www.SendWebRequest();
                    onResult?.Invoke(www.result == UnityWebRequest.Result.Success ? www.downloadHandler.text : null);
                }
            }
            else onResult?.Invoke(File.Exists(path) ? File.ReadAllText(path) : null);
        }

        private IEnumerator RequestRemoteText(string url, Action<string> onResult, bool trimResult = true)
        {
            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                www.timeout = 5;
                yield return www.SendWebRequest();
                string text = www.result == UnityWebRequest.Result.Success ? www.downloadHandler.text : null;
                onResult?.Invoke(trimResult ? text?.Trim() : text);
            }
        }
    }
}