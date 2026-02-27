using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using System.IO;
using Game.Runtime.AOT;
using UnityEngine;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

public class AddressableAutoGrouper
{
    private const string ResBundlePath = HotUpdateConsts.ResBundlePath;
    private const string ResBuildInPath = HotUpdateConsts.ResBuildInPath;

    [MenuItem("FreamWork/Addressables/Auto Group ResBundle")]
    public static void AutoGroup()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        if (settings == null)
        {
            Debug.LogError("[AddressableAutoGrouper] Addressable Asset Settings 未找到，请先初始化 Addressables。");
            return;
        }

        Debug.Log("[AddressableAutoGrouper] 开始自动分组...");

        if (Directory.Exists(ResBundlePath))
        {
            ProcessDirectoryRecursively(new DirectoryInfo(ResBundlePath), settings, ResBundlePath, HotUpdateConsts.GroupPrefixBundle);
        }
        else
        {
            Debug.LogWarning($"[AddressableAutoGrouper] 目录未找到: {ResBundlePath}");
        }

        if (Directory.Exists(ResBuildInPath))
        {
            ProcessDirectoryRecursively(new DirectoryInfo(ResBuildInPath), settings, ResBuildInPath, HotUpdateConsts.GroupPrefixBuildIn);
        }
        else
        {
            Debug.Log($"[AddressableAutoGrouper] (可选) 内置资源目录未找到: {ResBuildInPath}");
        }
        
        CleanUpGroups(settings);
        
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true);
        AssetDatabase.SaveAssets();

        Debug.Log("[AddressableAutoGrouper] 自动分组完成！");
    }

    private static void CleanUpGroups(AddressableAssetSettings settings)
    {
        for (int i = settings.groups.Count - 1; i >= 0; i--)
        {
            AddressableAssetGroup group = settings.groups[i];
            if (group == null) continue;

            if (group == settings.DefaultGroup) continue;
            if (group.Name == "Built In Data") continue;
            if (group.ReadOnly) continue;

            var entriesToRemove = new System.Collections.Generic.List<AddressableAssetEntry>();
            foreach (var entry in group.entries)
            {
                string path = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    entriesToRemove.Add(entry);
                }
            }

            foreach (var entry in entriesToRemove)
            {
                Debug.Log($"[AddressableAutoGrouper] 移除失效资源引用: {entry.address} (Group: {group.Name})");
                group.RemoveAssetEntry(entry);
            }

            if (group.entries.Count == 0)
            {
                Debug.Log($"[AddressableAutoGrouper] 移除空组: {group.Name}");
                settings.RemoveGroup(group);
            }
        }
    }

    private static void ProcessDirectoryRecursively(DirectoryInfo dirInfo, AddressableAssetSettings settings, string rootPath, string groupPrefix)
    {
        FileInfo[] files = dirInfo.GetFiles();
        bool hasAssets = false;
        foreach (var f in files)
        {
            if (!f.Name.EndsWith(".meta")) 
            {
                hasAssets = true;
                break;
            }
        }

        if (hasAssets)
        {
            string fullPath = dirInfo.FullName.Replace('\\', '/');
            string normalizedRootPath = Path.GetFullPath(rootPath).Replace('\\', '/');
            
            string relativePath = "";
            if (fullPath.Length > normalizedRootPath.Length)
            {
                relativePath = fullPath.Substring(normalizedRootPath.Length + 1);
            }
            else
            {
                relativePath = Path.GetFileName(rootPath);
            }

            string groupName = groupPrefix + relativePath.Replace('/', '_');
            
            AddressableAssetGroup group = FindOrCreateGroup(settings, groupName, "Remote.BuildPath", "Remote.LoadPath");
            
            foreach (var f in files)
            {
                if (f.Name.EndsWith(".meta")) continue;

                string assetPath = ConvertToProjectRelativePath(f.FullName);
                string guid = AssetDatabase.AssetPathToGUID(assetPath);

                if (!string.IsNullOrEmpty(guid))
                {
                    string address = assetPath.Replace(rootPath + "/", "");
                    
                    AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
                    entry.address = address;
                    entry.SetLabel(HotUpdateConsts.LabelDefault, true, true);
                }
            }
        }

        foreach (var subDir in dirInfo.GetDirectories())
        {
            ProcessDirectoryRecursively(subDir, settings, rootPath, groupPrefix);
        }
    }

    private static string ConvertToProjectRelativePath(string fullPath)
    {
        string projectRoot = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
        string rootParent = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
        return fullPath.Replace('\\', '/').Substring(rootParent.Length + 1);
    }

    private static AddressableAssetGroup FindOrCreateGroup(AddressableAssetSettings settings, string groupName, string buildPathVar, string loadPathVar)
    {
        AddressableAssetGroup group = settings.FindGroup(groupName);
        if (group == null)
        {
            Debug.Log($"[AddressableAutoGrouper] 创建新的 Addressable Group: {groupName}");
            group = settings.CreateGroup(groupName, false, false, true, null, typeof(BundledAssetGroupSchema));
        }

        var schema = group.GetSchema<BundledAssetGroupSchema>();
        if (schema != null)
        {
            schema.BuildPath.SetVariableByName(settings, buildPathVar);
            schema.LoadPath.SetVariableByName(settings, loadPathVar);
        }

        return group;
    }
}
