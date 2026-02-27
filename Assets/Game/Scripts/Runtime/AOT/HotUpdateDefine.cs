using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Game.Runtime.AOT
{
    /// <summary>
    /// 常量定义
    /// </summary>
    public static class HotUpdateConsts
    {
        // 目录路径
        public const string ResBundlePath = "Assets/ResBundle";
        public const string ResBuildInPath = "Assets/ResBuildIn";
        
        // Addressables Group 前缀
        public const string GroupPrefixBundle = "ResBundle_";
        public const string GroupPrefixBuildIn = "ResBuildIn_";
        
        // 资源标签 (Labels)
        public const string LabelDefault = "default";

        // 构建输出相关
        public const string BundleDirName = "Bundles";
        public const string FileListName = "filelist.json";
        public const string VersionFileName = "version.txt";

        public const string RemoteFileListName = "filelist_{0}.json";
        // 特殊资源识别
        public const string CatalogPrefix = "catalog_";
        public const string FileListPrefix = "filelist_";
        public const string AddressableDataName = "addressableassetsdata";
        public const string ResBuildInName = "resbuildin";
    }
    
    // ===================================================================================
    // 静态工具类
    // ===================================================================================
    public static class HotUpdateUtils
    {
        public static string GetFileMD5(string filePath)
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
    }
    
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

    /// <summary>
    /// 版本号 
    /// </summary>
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
    
}
