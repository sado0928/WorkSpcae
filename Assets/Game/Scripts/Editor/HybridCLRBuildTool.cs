using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public class HybridCLRBuildTool
    {
        // 配置路径
        public static string HotfixDllSrcDir => HybridCLR.Editor.SettingsUtil.GetHotUpdateDllsOutputDirByTarget(EditorUserBuildSettings.activeBuildTarget);
        public static string AotDllSrcDir => HybridCLR.Editor.SettingsUtil.GetAssembliesPostIl2CppStripDir(EditorUserBuildSettings.activeBuildTarget);
        
        public const string ResBundlePath = "Assets/ResBundle";
        public const string ResBuildInPath = "Assets/ResBuildIn";
        public const string HotfixDstPath = ResBundlePath + "/Hotfix";
        public const string MetadataDstPath = ResBuildInPath + "/Metadata";

        /// <summary>
        /// 一键同步：包含编译、生成裁剪保护、同步元数据全流程
        /// </summary>
        [MenuItem("FreamWork/HyBridCLR/CopyDllsToAddressables")]
        public static void CopyDllsToAddressables()
        {
            Debug.Log("<b>[HybridCLR]</b> === 开始全自动同步流程 ===");

            // 1. 编译 (确保反射拿到的依赖是最新的)
            HybridCLR.Editor.Commands.CompileDllCommand.CompileDll(EditorUserBuildSettings.activeBuildTarget);

            // 2. 自动化生成 link.xml (解决 LNK2001 和 MissingMethodException)
            BuildProjectLinkXml();

            // 3. 执行 Generate All
            HybridCLR.Editor.Commands.PrebuildCommand.GenerateAll();

            // 4. 执行文件拷贝
            if (!Directory.Exists(HotfixDstPath)) Directory.CreateDirectory(HotfixDstPath);
            if (!Directory.Exists(MetadataDstPath)) Directory.CreateDirectory(MetadataDstPath);

            // 拷贝热更 DLL
            string srcHotfix = Path.Combine(HotfixDllSrcDir, "Game.Runtime.Hotfix.dll");
            if (File.Exists(srcHotfix))
            {
                File.Copy(srcHotfix, Path.Combine(HotfixDstPath, "Game.Runtime.Hotfix.dll.bytes"), true);
                Debug.Log("[HybridCLR] 已同步热更 DLL");
            }

            // 拷贝 AOT 元数据 (运行时补全)
            List<string> aotDlls = GetAotMetadataList();
            int count = 0;
            foreach (var dll in aotDlls)
            {
                string src = Path.Combine(AotDllSrcDir, dll);
                if (File.Exists(src))
                {
                    File.Copy(src, Path.Combine(MetadataDstPath, dll + ".bytes"), true);
                    count++;
                }
            }
            Debug.Log($"[HybridCLR] 已同步 {count} 个 AOT 元数据文件");

            AssetDatabase.Refresh();
            AddressableAutoGrouper.AutoGroup();
            
            Debug.Log("<b>[HybridCLR]</b> === 同步流程完成！ ===");
        }

        /// <summary>
        /// 自动化生成裁剪保护文件 (link.xml) - 稳定版
        /// 解决 ParallelQuery 等核心库成员被过度裁剪导致的打包链接错误
        /// </summary>
        public static void BuildProjectLinkXml()
        {
            string hotfixDllPath = Path.Combine(HotfixDllSrcDir, "Game.Runtime.Hotfix.dll");
            HashSet<string> referencedAssemblies = new HashSet<string>();

            if (File.Exists(hotfixDllPath))
            {
                try
                {
                    byte[] dllData = File.ReadAllBytes(hotfixDllPath);
                    Assembly hotfixAss = Assembly.Load(dllData);
                    foreach (var refAssName in hotfixAss.GetReferencedAssemblies())
                    {
                        referencedAssemblies.Add(refAssName.Name);
                    }
                }
                catch (Exception e) { Debug.LogWarning($"[HybridCLR] link.xml 识别热更引用失败: {e.Message}"); }
            }

            // 核心白名单列表：这些程序集必须使用 preserve="all" 保护，以防打包失败
            string[] forceFullPreserve = { 
                "mscorlib", 
                "System", 
                "System.Core", 
                "UnityEngine.CoreModule",
            };

            List<string> lines = new List<string> { "<?xml version=\"1.0\" encoding=\"utf-8\"?>", "<linker>" };

            // 对核心模块执行全量保护
            foreach (var ass in forceFullPreserve)
            {
                lines.Add($"  <assembly fullname=\"{ass}\" preserve=\"all\" />");
            }

            // 保护其他热更引用的模块
            foreach (var ass in referencedAssemblies.OrderBy(s => s))
            {
                if (forceFullPreserve.Contains(ass)) continue;
                lines.Add($"  <assembly fullname=\"{ass}\" preserve=\"all\" />");
            }

            lines.Add("</linker>");

            string linkXmlPath = Path.Combine(Application.dataPath, "link.xml");
            File.WriteAllLines(linkXmlPath, lines);
            Debug.Log($"[HybridCLR] link.xml 已修正生成: {linkXmlPath}");
        }

        /// <summary>
        /// 自动化获取需要补充元数据的 DLL 列表
        /// </summary>
        private static List<string> GetAotMetadataList()
        {
            HashSet<string> aotDlls = new HashSet<string>();
            
            // 基础必选
            aotDlls.Add("mscorlib.dll");
            aotDlls.Add("System.dll");
            aotDlls.Add("System.Core.dll");
            aotDlls.Add("UnityEngine.CoreModule.dll");
            
            // 识别热更直接依赖
            string hotfixDllPath = Path.Combine(HotfixDllSrcDir, "Game.Runtime.Hotfix.dll");
            if (File.Exists(hotfixDllPath))
            {
                try
                {
                    byte[] dllData = File.ReadAllBytes(hotfixDllPath);
                    Assembly hotfixAss = Assembly.Load(dllData);
                    foreach (var refAssName in hotfixAss.GetReferencedAssemblies())
                    {
                        string dllName = refAssName.Name + ".dll";
                        if (File.Exists(Path.Combine(AotDllSrcDir, dllName))) aotDlls.Add(dllName);
                    }
                }
                catch { }
            }

            // 扫描辅助生成的配置文件 (补充间接泛型引用)
            string refFile = Path.Combine(Application.dataPath, "HybridCLRGenerate/AOTGenericReferences.cs");
            if (File.Exists(refFile))
            {
                try
                {
                    string content = File.ReadAllText(refFile);
                    var matches = System.Text.RegularExpressions.Regex.Matches(content, @"""([\w\.]+\.dll)""");
                    foreach (System.Text.RegularExpressions.Match match in matches) aotDlls.Add(match.Groups[1].Value);
                }
                catch { }
            }

            return aotDlls.ToList();
        }
    }
}
