using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using Game.Runtime.AOT;

namespace Game.Editor
{
    /// <summary>
    /// 资源版本清单管理器
    /// 核心功能：
    /// 1. 解析 Addressables 构建产物。
    /// 2. 分离“内置资源(ResBuildIn)”与“热更新资源”。
    /// 3. 生成全量清单(Server)与精简清单(StreamingAssets)。
    /// </summary>
    public static class VersionFileTool
    {
        /// <summary>
        /// 生成 FileList 并处理首包资源拷贝 (支持空壳包/按需分包策略)
        /// </summary>
        public static void GenerateFileListAndClean()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[VersionFileManager] 无法加载 AddressableAssetSettings！");
                return;
            }

            // 校验三段式版本号格式 (x.y.z)
            string appVersion = Application.version;
            if (!Regex.IsMatch(appVersion, @"^\d+\.\d+\.\d+$"))
            {
                Debug.LogWarning($"[VersionFileManager] 当前版本号 '{appVersion}' 不符合三段式规范(x.y.z)，请检查 PlayerSettings。");
                // 自动纠正或警告，这里选择继续执行，但在生成时会比较丑
            }

            // 获取 Addressables 构建输出路径 (通常是 ServerData/[BuildTarget])
            string serverDataPath = settings.RemoteCatalogBuildPath.GetValue(settings);
            if (!Path.IsPathRooted(serverDataPath))
            {
                serverDataPath = Path.Combine(Directory.GetCurrentDirectory(), serverDataPath);
            }
            serverDataPath = serverDataPath.Replace('\\', '/');
            
            if (!Directory.Exists(serverDataPath))
            {
                Debug.LogWarning($"[VersionFileManager] ServerData 不存在: {serverDataPath}");
                return;
            }

            Debug.Log($"[VersionFileManager] 开始生成资源清单... (版本: {appVersion}, 路径: {serverDataPath})");

            // 1. 寻找最新的 Catalog 文件
            var catalogFiles = new DirectoryInfo(serverDataPath).GetFiles($"{HotUpdateConsts.CatalogPrefix}*.json")
                .OrderByDescending(f => f.LastWriteTime)
                .ToArray();

            if (catalogFiles.Length == 0)
            {
                Debug.LogError("[VersionFileManager] 未找到 catalog 文件。");
                return;
            }

            FileInfo latestCatalog = catalogFiles[0];
            string catalogHashName = latestCatalog.Name.Replace(".json", ".hash");

            // 用于存储分类结果
            HashSet<string> allBundles = new HashSet<string>();
            HashSet<string> builtInBundles = new HashSet<string>();
            
            // 2. 解析 Catalog 内容，通过文件名识别“内置资源”
            string jsonText = File.ReadAllText(latestCatalog.FullName);
            var catalogData = JsonUtility.FromJson<ContentCatalogData>(jsonText);

            if (catalogData != null && catalogData.m_InternalIds != null)
            {
                foreach (var id in catalogData.m_InternalIds)
                {
                    if (id.EndsWith(".bundle"))
                    {
                        string fileName = Path.GetFileName(id);
                        allBundles.Add(fileName);

                        // 关键字匹配识别内置 Bundle
                        string fileNameLower = fileName.ToLower();
                        if (fileNameLower.Contains(HotUpdateConsts.ResBuildInName) || 
                            fileNameLower.Contains(HotUpdateConsts.AddressableDataName) || 
                            fileNameLower.Contains(HotUpdateConsts.CatalogPrefix))
                        {
                            builtInBundles.Add(fileName);
                        }
                    }
                }
            }

            // 补充：Catalog 自身及其 Hash 必须内置
            builtInBundles.Add(latestCatalog.Name);
            if (File.Exists(Path.Combine(serverDataPath, catalogHashName)))
            {
                allBundles.Add(catalogHashName);
                builtInBundles.Add(catalogHashName);
            }
            allBundles.Add(latestCatalog.Name);

            // 3. 生成全量清单 (ServerData)
            FileManifest fullManifest = new FileManifest();
            fullManifest.version = appVersion; // 使用 App 版本作为资源版本基准

            foreach (var fileName in allBundles.Union(builtInBundles)) 
            {
                string fullPath = Path.Combine(serverDataPath, fileName);
                if (!File.Exists(fullPath)) continue;

                fullManifest.files.Add(new FileData
                {
                    fileName = fileName,
                    size = new FileInfo(fullPath).Length,
                    md5 =  HotUpdateUtils.GetFileMD5(fullPath)
                });
            }

            string serverJson = JsonUtility.ToJson(fullManifest, true);
            
            // 计算 filelist 的 MD5 用于 version.txt
            string jsonMd5 = "";
            using (var md5 = MD5.Create())
            {
                byte[] data = Encoding.UTF8.GetBytes(serverJson);
                byte[] hash = md5.ComputeHash(data);
                jsonMd5 = System.BitConverter.ToString(hash).Replace("-", "").ToLower();
            }

            // 核心规则：version.txt = "1.0.0_md5"
            string fileListName = string.Format(HotUpdateConsts.RemoteFileListName,appVersion);
            string versionContent = $"{appVersion}_{jsonMd5}";

            File.WriteAllText(Path.Combine(serverDataPath, fileListName), serverJson);
            File.WriteAllText(Path.Combine(serverDataPath, HotUpdateConsts.VersionFileName), versionContent);
            
            Debug.Log($"[VersionFileManager] 清单生成完毕: {fileListName} | 内容摘要: {versionContent}");

            // 4. 拷贝到 StreamingAssets (包内只放精简清单)
            string streamingAssetsPath = Path.Combine(Application.streamingAssetsPath, HotUpdateConsts.BundleDirName);
            if (Directory.Exists(streamingAssetsPath)) Directory.Delete(streamingAssetsPath, true);
            Directory.CreateDirectory(streamingAssetsPath);

            FileManifest builtInManifest = new FileManifest();
            builtInManifest.version = fullManifest.version;

            foreach (var file in fullManifest.files)
            {
                if (builtInBundles.Contains(file.fileName))
                {
                    string src = Path.Combine(serverDataPath, file.fileName);
                    string dst = Path.Combine(streamingAssetsPath, file.fileName);
                    File.Copy(src, dst, true);
                    builtInManifest.files.Add(file);
                }
            }

            File.WriteAllText(Path.Combine(streamingAssetsPath, HotUpdateConsts.FileListName), JsonUtility.ToJson(builtInManifest, true));
            File.WriteAllText(Path.Combine(streamingAssetsPath, HotUpdateConsts.VersionFileName), versionContent); 

            Debug.Log($"[VersionFileManager] 首包拷贝完成。内置资源数: {builtInManifest.files.Count}");
            AssetDatabase.Refresh();
        }

        [System.Serializable]
        private class ContentCatalogData
        {
            public string[] m_InternalIds;
        }
        
    }
}
