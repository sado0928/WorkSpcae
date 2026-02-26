using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.SceneManagement;
using UnityEngine;
using HybridCLR.Editor.Commands;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>
    /// 全新的构建工具，专注于“一键式”操作
    /// </summary>
    public class GameBuildTool : EditorWindow
    {
        private enum WindowMode
        {
            Main,
            VersionManagement,
            BuildSettings
        }

        private enum BuildType
        {
            FullBuild,
            IncrementalBuild
        }

        private WindowMode _currentMode = WindowMode.Main;
        private BuildType _pendingBuildType = BuildType.FullBuild;

        // 构建设置临时变量
        private int _tempMajor = 0;
        private int _tempMinor = 0;
        private int _tempPatch = 0;
        
        private int _tempAndroidCode;
        private string _tempiOSBuildNum;

        [MenuItem("FreamWork/Game Builder")]
        public static void ShowWindow()
        {
            var window = GetWindow<GameBuildTool>("Game Builder");
            window.minSize = new Vector2(400, 500);
        }

        private void OnGUI()
        {
            if (_currentMode == WindowMode.Main)
            {
                DrawMainInterface();
            }
            else if (_currentMode == WindowMode.VersionManagement)
            {
                DrawVersionManagementInterface();
            }
            else if (_currentMode == WindowMode.BuildSettings)
            {
                DrawBuildSettingsInterface();
            }
        }

        // =======================================================
        // 主界面
        // =======================================================
        private void DrawMainInterface()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Space(10);
            GUILayout.Label("FreamWork 构建中心", new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(20);

            // 按钮 1: 一键打整包 (进入设置页)
            GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
            if (GUILayout.Button("一键打整包 (Full Build)\n[设置版本 -> 编译 -> 资源 -> APK/EXE]", GUILayout.Height(60)))
            {
                PrepareBuild(BuildType.FullBuild);
            }
            GUI.backgroundColor = Color.white;
            GUILayout.Space(10);

            // 按钮 2: 一键增量包
            GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
            if (GUILayout.Button("一键增量包 (Incremental/Hotfix)\n[编译代码 -> 增量资源 -> 准备热更]", GUILayout.Height(60)))
            {
                PrepareBuild(BuildType.IncrementalBuild);
            }
            GUI.backgroundColor = Color.white;
            GUILayout.Space(10);

            // 按钮 3: 版本管理
            GUI.backgroundColor = new Color(1f, 0.9f, 0.6f);
            if (GUILayout.Button("远端资源版本管理\n[查看历史版本 -> 清理废弃资源]", GUILayout.Height(60)))
            {
                _currentMode = WindowMode.VersionManagement;
            }
            GUI.backgroundColor = Color.white;
            
            GUILayout.Space(20);
            GUILayout.Label("当前平台: " + EditorUserBuildSettings.activeBuildTarget, EditorStyles.helpBox);
            
            GUILayout.EndVertical();
        }

        private void PrepareBuild(BuildType type)
        {
            _pendingBuildType = type;
            // 初始化临时变量
            ParseCurrentVersion();
            _tempAndroidCode = PlayerSettings.Android.bundleVersionCode;
            _tempiOSBuildNum = PlayerSettings.iOS.buildNumber;
            _currentMode = WindowMode.BuildSettings;
        }

        // =======================================================
        // 构建设置子界面 (三段式版本号)
        // =======================================================
        private void DrawBuildSettingsInterface()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Space(10);

            // 顶部导航
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("← 返回主菜单", GUILayout.Height(30), GUILayout.Width(100)))
            {
                _currentMode = WindowMode.Main;
            }
            
            string title = _pendingBuildType == BuildType.FullBuild ? "构建全量包 (Full)" : "构建热更包 (Incremental)";
            GUILayout.Label(title, new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(100);
            GUILayout.EndHorizontal();

            GUILayout.Space(20);

            // 版本设置区域
            GUILayout.Label("Semantic Versioning (三段式版本)", EditorStyles.boldLabel);
            GUILayout.BeginVertical("helpBox");
            GUILayout.Space(10);

            // 1. 版本号输入区域
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            var numStyle = new GUIStyle(EditorStyles.numberField) { fontSize = 20, alignment = TextAnchor.MiddleCenter, fixedHeight = 30 };
            var dotStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleCenter, fixedWidth = 10 };

            // 增量构建时，Major 是只读的（防止热更包升级主版本导致不兼容）
            if (_pendingBuildType == BuildType.IncrementalBuild)
            {
                GUI.enabled = false; // 禁用 Major 编辑
                EditorGUILayout.IntField(_tempMajor, numStyle, GUILayout.Width(60));
                GUI.enabled = true;
            }
            else
            {
                _tempMajor = EditorGUILayout.IntField(_tempMajor, numStyle, GUILayout.Width(60));
            }
            
            GUILayout.Label(".", dotStyle);
            _tempMinor = EditorGUILayout.IntField(_tempMinor, numStyle, GUILayout.Width(60));
            GUILayout.Label(".", dotStyle);
            _tempPatch = EditorGUILayout.IntField(_tempPatch, numStyle, GUILayout.Width(60));
            
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // 2. 快捷升级按钮
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            
            // Major++ 仅在全量构建时可用
            if (_pendingBuildType == BuildType.FullBuild)
            {
                GUI.backgroundColor = new Color(1f, 0.8f, 0.8f);
                if (GUILayout.Button($"Major++\n({_tempMajor + 1}.0.0)", GUILayout.Height(40)))
                {
                    _tempMajor++; _tempMinor = 0; _tempPatch = 0;
                }
            }
            else
            {
                GUI.enabled = false;
                GUILayout.Button("Major++\n(Locked)", GUILayout.Height(40));
                GUI.enabled = true;
            }
            
            GUI.backgroundColor = new Color(1f, 0.9f, 0.7f);
            if (GUILayout.Button($"Minor++\n({_tempMajor}.{_tempMinor + 1}.0)", GUILayout.Height(40)))
            {
                _tempMinor++; _tempPatch = 0;
            }

            GUI.backgroundColor = new Color(0.8f, 1f, 0.8f);
            if (GUILayout.Button($"Patch++\n({_tempMajor}.{_tempMinor}.{_tempPatch + 1})", GUILayout.Height(40)))
            {
                _tempPatch++;
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            GUILayout.Label($"预览: v{_tempMajor}.{_tempMinor}.{_tempPatch}", EditorStyles.centeredGreyMiniLabel);
            GUILayout.EndVertical();
            
            GUILayout.Space(15);

            // 构建号设置
            GUILayout.Label("Build Number (构建号)", EditorStyles.boldLabel);
            GUILayout.BeginVertical("helpBox");
            GUILayout.Space(5);
            
            // Android Code
            GUILayout.BeginHorizontal();
            GUILayout.Label("Android Version Code:", GUILayout.Width(150));
            _tempAndroidCode = EditorGUILayout.IntField(_tempAndroidCode);
            if (GUILayout.Button("+1", GUILayout.Width(40))) _tempAndroidCode++;
            GUILayout.EndHorizontal();

            // iOS Build Number
            GUILayout.BeginHorizontal();
            GUILayout.Label("iOS Build Number:", GUILayout.Width(150));
            _tempiOSBuildNum = EditorGUILayout.TextField(_tempiOSBuildNum);
            if (GUILayout.Button("+1", GUILayout.Width(40)))
            {
                if (int.TryParse(_tempiOSBuildNum, out int val)) _tempiOSBuildNum = (val + 1).ToString();
                else _tempiOSBuildNum = "1";
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
            GUILayout.EndVertical();

            GUILayout.Space(30);

            // 确认构建按钮
            string newVersionStr = $"{_tempMajor}.{_tempMinor}.{_tempPatch}";
            Color btnColor = _pendingBuildType == BuildType.FullBuild ? new Color(0.7f, 1f, 0.7f) : new Color(0.7f, 0.9f, 1f);
            GUI.backgroundColor = btnColor;
            
            string btnText = _pendingBuildType == BuildType.FullBuild ? "应用设置并开始【全量构建】" : "应用设置并开始【增量热更】";

            if (GUILayout.Button($"{btnText} (v{newVersionStr})", GUILayout.Height(50)))
            {
                // 保存设置
                string oldVersion = PlayerSettings.bundleVersion;
                PlayerSettings.bundleVersion = newVersionStr;
                PlayerSettings.Android.bundleVersionCode = _tempAndroidCode;
                PlayerSettings.iOS.buildNumber = _tempiOSBuildNum;
                AssetDatabase.SaveAssets();

                string confirmTitle = _pendingBuildType == BuildType.FullBuild ? "全量构建确认" : "增量热更确认";
                string buildMsg = _pendingBuildType == BuildType.FullBuild ? "开始全量构建" : "开始增量构建";

                if (EditorUtility.DisplayDialog(confirmTitle, 
                    $"即将执行：{buildMsg}\n\n" +
                    $"App Version: {oldVersion}  ->  {newVersionStr}\n" +
                    $"Android Code: {PlayerSettings.Android.bundleVersionCode}\n" +
                    $"iOS Build: {PlayerSettings.iOS.buildNumber}\n\n" +
                    "确认信息无误？", 
                    "开始执行", "取消"))
                {
                    if (_pendingBuildType == BuildType.FullBuild)
                    {
                        EditorApplication.delayCall += () => RunFullBuild(false);
                    }
                    else
                    {
                        EditorApplication.delayCall += ExecuteIncrementalBuild;
                    }
                }
            }
            GUI.backgroundColor = Color.white;

            GUILayout.EndVertical();
        }

        private void ParseCurrentVersion()
        {
            string current = PlayerSettings.bundleVersion;
            _tempMajor = 0; _tempMinor = 0; _tempPatch = 0;

            if (!string.IsNullOrEmpty(current))
            {
                var parts = current.Split('.');
                if (parts.Length >= 1) int.TryParse(parts[0], out _tempMajor);
                if (parts.Length >= 2) int.TryParse(parts[1], out _tempMinor);
                if (parts.Length >= 3) int.TryParse(parts[2], out _tempPatch);
            }
        }

        // =======================================================
        // 版本管理子界面
        // =======================================================
        private Vector2 _scrollPos;
        private Dictionary<string, bool> _versionSelection = new Dictionary<string, bool>();

        private void DrawVersionManagementInterface()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Space(10);
            
            // 顶部导航栏
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("← 返回主菜单", GUILayout.Height(30), GUILayout.Width(100)))
            {
                _currentMode = WindowMode.Main;
            }
            GUILayout.Label("远端资源版本管理", new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(100); 
            GUILayout.EndHorizontal();
            
            GUILayout.Space(20);

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) 
            {
                EditorGUILayout.HelpBox("Addressable Settings 未找到", MessageType.Error);
                GUILayout.EndVertical();
                return;
            }

            string serverDataPath = settings.RemoteCatalogBuildPath.GetValue(settings);
            if (!Path.IsPathRooted(serverDataPath)) serverDataPath = Path.Combine(Directory.GetCurrentDirectory(), serverDataPath);
            
            if (!Directory.Exists(serverDataPath))
            {
                EditorGUILayout.HelpBox($"ServerData 目录不存在: {serverDataPath}", MessageType.Warning);
                GUILayout.EndVertical();
                return;
            }

            // 扫描版本
            var fileListFiles = Directory.GetFiles(serverDataPath, "filelist_*.json");
            if (fileListFiles.Length == 0)
            {
                GUILayout.Label("暂无历史版本记录。", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                GUILayout.Label($"找到 {fileListFiles.Length} 个历史版本:", EditorStyles.boldLabel);
                _scrollPos = GUILayout.BeginScrollView(_scrollPos, "box");
                
                foreach (var fFile in fileListFiles)
                {
                    string fileName = Path.GetFileName(fFile); 
                    string ver = fileName.Substring(9, fileName.Length - 14);
                    
                    if (!_versionSelection.ContainsKey(ver)) _versionSelection[ver] = false;

                    GUILayout.BeginHorizontal();
                    _versionSelection[ver] = GUILayout.Toggle(_versionSelection[ver], $" Version {ver}", GUILayout.Width(150));
                    GUILayout.Label(File.GetLastWriteTime(fFile).ToString(), EditorStyles.miniLabel);
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();

                GUILayout.Space(10);
                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("清理选中的旧版本资源", GUILayout.Height(40)))
                {
                    bool hasSelection = false;
                    foreach(var s in _versionSelection.Values) if(s) hasSelection = true;

                    if (!hasSelection)
                    {
                        EditorUtility.DisplayDialog("提示", "请先勾选需要删除的版本。", "OK");
                    }
                    else if (EditorUtility.DisplayDialog("确认清理", "警告：此操作将删除选中的版本记录及其【独占】的 AssetBundle 资源。\n\n被其他未选中版本共用的资源将保留。\n\n确定要执行吗？", "确定删除", "取消"))
                    {
                        CleanServerVersions(serverDataPath);
                    }
                }
                GUI.backgroundColor = Color.white;
            }
            
            GUILayout.EndVertical();
        }

        // =======================================================
        // 清理逻辑
        // =======================================================
        private void CleanServerVersions(string serverRoot)
        {
            HashSet<string> keepBundles = new HashSet<string>();
            var allFileListFiles = Directory.GetFiles(serverRoot, "filelist_*.json");
            foreach (var fFile in allFileListFiles)
            {
                string fileName = Path.GetFileName(fFile);
                string ver = fileName.Substring(9, fileName.Length - 14);

                if (!_versionSelection.ContainsKey(ver) || !_versionSelection[ver])
                {
                    try
                    {
                        var json = File.ReadAllText(fFile);
                        var matches = System.Text.RegularExpressions.Regex.Matches(json, "\"fileName\":\\s*\"(.*?)\"");
                        foreach (System.Text.RegularExpressions.Match m in matches)
                        {
                            keepBundles.Add(m.Groups[1].Value.Replace("\\", "/"));
                        }
                    }
                    catch {}
                }
            }

            int delCount = 0;
            List<string> deletedVersions = new List<string>();
            
            foreach (var kv in new Dictionary<string, bool>(_versionSelection))
            {
                if (kv.Value) 
                {
                    string ver = kv.Key;
                    string lPath = Path.Combine(serverRoot, $"filelist_{ver}.json");
                    if (File.Exists(lPath)) File.Delete(lPath);
                    deletedVersions.Add(ver);
                }
            }

            var allBundles = Directory.GetFiles(serverRoot, "*.bundle", SearchOption.AllDirectories);
            foreach (var bPath in allBundles)
            {
                string relPath = bPath.Substring(serverRoot.Length + 1).Replace("\\", "/");
                if (!keepBundles.Contains(relPath))
                {
                    File.Delete(bPath);
                    delCount++;
                }
            }
            
            var allCatalogs = Directory.GetFiles(serverRoot, "catalog_*.*", SearchOption.AllDirectories);
            foreach(var cPath in allCatalogs)
            {
                string relPath = cPath.Substring(serverRoot.Length + 1).Replace("\\", "/");
                if (!keepBundles.Contains(relPath))
                {
                    File.Delete(cPath);
                }
            }

            AssetDatabase.Refresh();
            foreach(var ver in deletedVersions) _versionSelection.Remove(ver);
            EditorUtility.DisplayDialog("清理完成", $"已移除 {deletedVersions.Count} 个版本记录。\n清理了 {delCount} 个废弃资源文件。", "OK");
        }

        // =======================================================
        // CI/CD 命令行入口
        // =======================================================

        public static void BuildAndroid()
        {
            PerformHeadlessBuild(BuildTarget.Android);
        }

        public static void BuildWindows()
        {
            PerformHeadlessBuild(BuildTarget.StandaloneWindows64);
        }

        private static void PerformHeadlessBuild(BuildTarget target)
        {
            Debug.Log($"[CI] 开始构建平台: {target}");
            
            ParseCLIArgs();
            
            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                Debug.Log($"[CI] 切换平台到 {target}...");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target);
            }

            RunFullBuild(true);
        }

        private static void ParseCLIArgs()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-bundleVersion" && i + 1 < args.Length)
                {
                    PlayerSettings.bundleVersion = args[i + 1];
                    Debug.Log($"[CI] Set BundleVersion: {args[i + 1]}");
                }
                else if (args[i] == "-androidVersionCode" && i + 1 < args.Length && int.TryParse(args[i + 1], out int code))
                {
                    PlayerSettings.Android.bundleVersionCode = code;
                    Debug.Log($"[CI] Set AndroidVersionCode: {code}");
                }
                else if (args[i] == "-iosBuildNumber" && i + 1 < args.Length)
                {
                    PlayerSettings.iOS.buildNumber = args[i + 1];
                    Debug.Log($"[CI] Set iOSBuildNumber: {args[i + 1]}");
                }
            }
            AssetDatabase.SaveAssets();
        }

        // =======================================================
        // 构建逻辑实现
        // =======================================================

        private static void RunFullBuild(bool headless)
        {
            try
            {
                long startTime = DateTime.Now.Ticks;
                Debug.Log("<b>[GameBuilder]</b> === 开始全量构建流程 ===");

                UpdateProgress("正在清理构建环境...", 0.1f);
                BuildCache.PurgeCache(false); 
                
                UpdateProgress("生成 HybridCLR 代码...", 0.2f);
                PrebuildCommand.GenerateAll();

                UpdateProgress("编译 Hotfix DLL...", 0.3f);
                CompileDllCommand.CompileDll(EditorUserBuildSettings.activeBuildTarget);

                UpdateProgress("检查 AOT 元数据环境...", 0.4f);
                EnsureAotDllsExist();

                UpdateProgress("同步 DLL 资源到工程...", 0.5f);
                HybridCLRBuildTool.CopyDllsToAddressables(); 
                AssetDatabase.Refresh();

                UpdateProgress("构建 Addressables 资源包...", 0.6f);
                var settings = AddressableAssetSettingsDefaultObject.Settings;

                // --- 修复版本号不一致的关键逻辑 ---
                // 强制刷新 OverridePlayerVersion，确保它读取到最新的 PlayerSettings.bundleVersion
                // 有时候直接构建可能读取到旧的内存值，这里显式 "抖动" 一下配置
                string correctMacro = "[UnityEditor.PlayerSettings.bundleVersion]";
                settings.OverridePlayerVersion = ""; 
                settings.OverridePlayerVersion = correctMacro;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets(); // 确保版本号变更已写入磁盘
                // ----------------------------------

                AddressableAssetSettings.CleanPlayerContent(settings.ActivePlayerDataBuilder);
                AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult aaResult);
                
                if (aaResult != null && !string.IsNullOrEmpty(aaResult.Error))
                {
                    throw new Exception($"Addressables 构建失败: {aaResult.Error}");
                }

                UpdateProgress("生成资源清单...", 0.8f);
                // 关键修正：必须在 BuildPlayer 之前生成清单，否则 APK 内置的 version.txt 是旧的！
                VersionFileMgr.GenerateFileListAndClean();

                UpdateProgress("构建终端 Player...", 0.9f);
                BuildPlayer();
                
                Debug.Log($"<b>[GameBuilder]</b> 全量构建成功！耗时: {TimeSpan.FromTicks(DateTime.Now.Ticks - startTime).TotalSeconds:F1}s");
                
                if (!headless) EditorUtility.DisplayDialog("成功", "全量构建成功！", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameBuilder] 全量构建失败: {e.Message}\n{e.StackTrace}");
                if (!headless) EditorUtility.DisplayDialog("失败", $"构建过程中发生错误:\n{e.Message}", "关闭");
                else throw; 
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void ExecuteIncrementalBuild()
        {
            try
            {
                long startTime = DateTime.Now.Ticks;
                Debug.Log("<b>[GameBuilder]</b> === 开始增量热更构建 ===");

                EditorUtility.DisplayProgressBar("Game Builder", "编译 Hotfix DLL...", 0.3f);
                CompileDllCommand.CompileDll(EditorUserBuildSettings.activeBuildTarget);

                EditorUtility.DisplayProgressBar("Game Builder", "同步 DLL 资源...", 0.5f);
                HybridCLRBuildTool.CopyDllsToAddressables();
                AssetDatabase.Refresh();

                EditorUtility.DisplayProgressBar("Game Builder", "更新 Addressables 资源...", 0.7f);
                AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult aaResult);

                if (aaResult != null && !string.IsNullOrEmpty(aaResult.Error))
                {
                    throw new Exception($"Addressables 增量构建失败: {aaResult.Error}");
                }

                UpdateProgress("生成资源清单...", 0.9f);
                VersionFileMgr.GenerateFileListAndClean();
                
                Debug.Log($"<b>[GameBuilder]</b> 增量构建完成！耗时: {TimeSpan.FromTicks(DateTime.Now.Ticks - startTime).TotalSeconds:F1}s\n请检查 ServerData 目录。");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameBuilder] 增量构建失败: {e.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // =======================================================
        // 辅助方法
        // =======================================================

        private static void EnsureAotDllsExist()
        {
            string aotDir = HybridCLRBuildTool.AotDllSrcDir;
            if (Directory.Exists(aotDir) && Directory.GetFiles(aotDir, "*.dll").Length > 0) return;

            Debug.LogWarning("[GameBuilder] 检测到 AOT 裁剪目录为空。正在执行【快速脚本构建】以刷新 AOT 缓存...");
            
            // 使用临时路径
            string tempBuildPath = Path.Combine("Temp", "PreBuild", "ScriptsOnly");

            BuildPlayerOptions opt = new BuildPlayerOptions
            {
                scenes = GetBuildScenes(),
                locationPathName = tempBuildPath,
                target = EditorUserBuildSettings.activeBuildTarget,
                // 【核心优化】: 只构建脚本。
                // 这会触发 IL2CPP 编译和 DLL 裁剪，但跳过极其耗时的资源打包过程。
                options = BuildOptions.BuildScriptsOnly 
            };

            BuildReport report = BuildPipeline.BuildPlayer(opt);

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception("脚本预构建失败，无法生成 AOT 裁剪环境。请检查控制台错误。");
            }
            Debug.Log("[GameBuilder] 脚本预构建完成，AOT 环境已刷新。");
        }

        private static string[] GetBuildScenes()
        {
            var levels = new string[EditorSceneManager.sceneCountInBuildSettings];
            for (int i = 0; i < levels.Length; i++) levels[i] = SceneUtility.GetScenePathByBuildIndex(i);
            return levels;
        }

        private static void BuildPlayer()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var originalPolicy = settings.BuildAddressablesWithPlayerBuild;
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;

            try
            {
                string extension = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ? ".apk" : ".exe";
                string buildPath = $"Builds/{EditorUserBuildSettings.activeBuildTarget}/Game{extension}";
                
                string buildDir = Path.GetDirectoryName(buildPath);
                if (!Directory.Exists(buildDir)) Directory.CreateDirectory(buildDir);

                BuildPlayerOptions opt = new BuildPlayerOptions
                {
                    scenes = GetBuildScenes(),
                    locationPathName = buildPath,
                    target = EditorUserBuildSettings.activeBuildTarget,
                    options = BuildOptions.None
                };

                if (EditorUserBuildSettings.development)
                    opt.options |= BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.ConnectWithProfiler;

                BuildReport report = BuildPipeline.BuildPlayer(opt); 
                
                if (report.summary.result != BuildResult.Succeeded) throw new Exception($"Player 构建失败: {report.summary.totalErrors}");
                
                EditorUtility.RevealInFinder(buildPath);
            }
            finally
            {
                settings.BuildAddressablesWithPlayerBuild = originalPolicy;
            }
        }

        private static void UpdateProgress(string info, float progress)
        {
            EditorUtility.DisplayProgressBar("Game Builder", info, progress);
        }
    }
}
