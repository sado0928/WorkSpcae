using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.Callbacks;

namespace Game.Editor
{
    public class UIBuildTool : EditorWindow
    {
        private UIBuildConfig _config;
        private static readonly string ConfigPath = "Assets/Game/Scripts/Editor/UIBuild/UIBuildConfig.asset";
        private const string PendingTaskKey = "UIBuild_PendingTask";
        private const string BaseClassName = "UIBase";
        private const string SubBaseClassName = "ResBase";
        
        private static readonly List<Type> UIComponentTypes = new List<Type>
        {
            typeof(RectTransform), typeof(Image), typeof(RawImage), typeof(Text), 
            typeof(TextMeshProUGUI), typeof(Button), typeof(Toggle), typeof(Slider), 
            typeof(ScrollRect), typeof(TMP_InputField), typeof(InputField), 
            typeof(Animator), typeof(CanvasGroup)
        };

        [MenuItem("FreamWork/UI Build Config")]
        public static void ShowWindow()
        {
            UIBuildTool window = GetWindow<UIBuildTool>("UI Build Tool");
            window.minSize = new Vector2(500, 350);
            window.Show();
        }

        #region 菜单与执行入口
        [MenuItem("Assets/FreamWork/Serialize UI", false, 0)]
        public static void SerializeUIFromContext() { if (Selection.activeGameObject != null) StaticGenerateScripts(Selection.activeGameObject); }
        
        [MenuItem("Assets/FreamWork/Serialize UI", true)]
        public static bool SerializeUIFromContextValidate() => Selection.activeGameObject != null && PrefabUtility.GetPrefabAssetType(Selection.activeGameObject) != PrefabAssetType.NotAPrefab;

        public static void ExecuteSerializeForObject(MonoBehaviour mb)
        {
            if (mb == null) return;
            GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(mb.gameObject);
            if (prefabAsset == null)
            {
                var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && stage.IsPartOfPrefabContents(mb.gameObject))
                    prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(stage.assetPath);
                else
                    prefabAsset = mb.gameObject;
            }
            if (prefabAsset != null) StaticGenerateScripts(prefabAsset);
        }
        #endregion

        private void OnEnable() => LoadConfig();
        private void LoadConfig()
        {
            _config = AssetDatabase.LoadAssetAtPath<UIBuildConfig>(ConfigPath);
            if (_config == null) { _config = CreateInstance<UIBuildConfig>(); AssetDatabase.CreateAsset(_config, ConfigPath); AssetDatabase.SaveAssets(); }
        }

        private void OnGUI()
        {
            if (_config == null) return;
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("UI 自动绑定配置 (路径相对于 Assets)", EditorStyles.boldLabel);
            DrawPathSelector("Logic 逻辑目录:", ref _config.uiLogicPath);
            DrawPathSelector("Serialize 序列化目录:", ref _config.uiSerializePath);
            DrawPathSelector("Container 容器目录:", ref _config.uiContainerPath);
            _config.uiNamespace = EditorGUILayout.TextField("命名空间:", _config.uiNamespace);
            if (GUI.changed) _config.Save();
            EditorGUILayout.Space(20);
            EditorGUILayout.HelpBox("配置完成后，可直接在 Project 视图右键 Prefab 或在 Inspector 面板中点击按钮执行序列化。", MessageType.Info);
        }

        private void DrawPathSelector(string label, ref string path)
        {
            EditorGUILayout.LabelField(label);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField(path);
            if (GUILayout.Button("选择", GUILayout.Width(60)))
            {
                string initialPath = string.IsNullOrEmpty(path) ? Application.dataPath : Path.Combine(Application.dataPath, path.Replace("Assets/", ""));
                string selectedPath = EditorUtility.OpenFolderPanel("选择目录", initialPath, "");
                if (!string.IsNullOrEmpty(selectedPath) && selectedPath.StartsWith(Application.dataPath))
                    path = "Assets" + selectedPath.Substring(Application.dataPath.Length);
            }
            EditorGUILayout.EndHorizontal();
        }

        public static void StaticGenerateScripts(GameObject prefab)
        {
            UIBuildConfig config = AssetDatabase.LoadAssetAtPath<UIBuildConfig>(ConfigPath);
            if (config == null) return;

            string uiName = prefab.name;
            UIMap map = new UIMap(uiName, prefab.transform);
            ScanRecursive(prefab.transform, map, map.Root);

            string uiSerializeDir = Path.Combine(config.uiSerializePath, uiName);
            if (!Directory.Exists(uiSerializeDir)) Directory.CreateDirectory(uiSerializeDir);
            if (!Directory.Exists(config.uiContainerPath)) Directory.CreateDirectory(config.uiContainerPath);

            CleanObsoleteScripts(uiSerializeDir, map);
            GenerateGlobalContainers(map, config);

            string logicDir = Path.Combine(config.uiLogicPath, uiName);
            if (!Directory.Exists(logicDir)) Directory.CreateDirectory(logicDir);
            string logicPath = Path.Combine(logicDir, $"{uiName}_Logic.cs");
            if (!File.Exists(logicPath)) File.WriteAllText(logicPath, CreateLogicTemplate(uiName, config), Encoding.UTF8);

            string serializePath = Path.Combine(uiSerializeDir, $"{uiName}.cs");
            File.WriteAllText(serializePath, CreateMainSerializeTemplate(map, config), Encoding.UTF8);

            foreach (var item in map.Items)
            {
                string itemPath = Path.Combine(uiSerializeDir, $"{item.FullClassName}.cs");
                File.WriteAllText(itemPath, CreateSubSerializeTemplate(item, config), Encoding.UTF8);
            }

            EditorPrefs.SetString(PendingTaskKey, $"{AssetDatabase.GetAssetPath(prefab)}|{config.uiNamespace}.{uiName}");
            AssetDatabase.Refresh();
            Debug.Log($"<color=cyan>[UIBuild]</color> 代码已生成，待编译后自动注入引用：{uiName}");
        }

        private static void CleanObsoleteScripts(string serializeDir, UIMap map)
        {
            if (!Directory.Exists(serializeDir)) return;
            HashSet<string> validNames = new HashSet<string> { map.MainUIName };
            foreach (var item in map.Items) validNames.Add(item.FullClassName);
            string[] existingFiles = Directory.GetFiles(serializeDir, "*.cs");
            foreach (string filePath in existingFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                if (!validNames.Contains(fileName))
                {
                    File.Delete(filePath);
                    if (File.Exists(filePath + ".meta")) File.Delete(filePath + ".meta");
                    Debug.Log($"<color=red>[UIBuild]</color> 已清理失效脚本: {fileName}.cs");
                }
            }
        }

        private static void ScanRecursive(Transform trans, UIMap map, NodeData currentOwner)
        {
            foreach (Transform child in trans)
            {
                string childName = child.name;
                if (childName.StartsWith("i_"))
                {
                    string itemName = childName.Substring(2);
                    string fieldName = "m_" + itemName;
                    NodeData itemNode = new NodeData(fieldName, GetRelativePath(currentOwner.NodeTrans, child), true, child);
                    itemNode.FullClassName = $"{map.MainUIName}_{itemName}";
                    itemNode.PropertyName = itemName;
                    itemNode.ComponentTypes = GetUIComponents(child);
                    itemNode.ContainerType = $"RectTransform_{itemNode.FullClassName}_Container";
                    currentOwner.Children.Add(itemNode);
                    map.Items.Add(itemNode);
                    ScanRecursive(child, map, itemNode); 
                }
                else if (childName.StartsWith("m_") || childName.StartsWith("p_"))
                {
                    // p_ 视为外部通用组件，只生成基础容器引用，不进入递归
                    bool isPart = childName.StartsWith("p_");
                    string propertyName = childName.Substring(2);
                    NodeData mNode = new NodeData(childName, GetRelativePath(currentOwner.NodeTrans, child), false, child);
                    mNode.PropertyName = propertyName;
                    mNode.ComponentTypes = GetUIComponents(child);
                    mNode.ContainerType = string.Join("_", mNode.ComponentTypes) + "_Container";
                    currentOwner.Children.Add(mNode);
                    
                    // 如果不是 p_，则继续向下扫描寻找内部的 m_
                    if (!isPart) ScanRecursive(child, map, currentOwner); 
                }
                else
                {
                    ScanRecursive(child, map, currentOwner);
                }
            }
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (root == target) return "";
            string path = target.name;
            Transform current = target.parent;
            while (current != null && current != root) { path = current.name + "/" + path; current = current.parent; }
            return path;
        }

        private static List<string> GetUIComponents(Transform t)
        {
            List<string> res = new List<string>();
            foreach (var type in UIComponentTypes) if (t.GetComponent(type) != null) res.Add(type.Name);
            if (res.Count == 0) res.Add("RectTransform");
            return res;
        }

        private class UIMap { public string MainUIName; public NodeData Root; public List<NodeData> Items = new List<NodeData>(); public UIMap(string n, Transform t) { MainUIName = n; Root = new NodeData(n, "", false, t); } }
        private class NodeData { public string Name, PropertyName, FullClassName, Path, ContainerType; public bool IsItem; public Transform NodeTrans; public List<string> ComponentTypes = new List<string>(); public List<NodeData> Children = new List<NodeData>(); public NodeData(string n, string p, bool i, Transform t) { Name = n; Path = p; IsItem = i; NodeTrans = t; } }

        private static void GenerateGlobalContainers(UIMap map, UIBuildConfig config)
        {
            List<NodeData> all = new List<NodeData> { map.Root }; all.AddRange(map.Items);
            HashSet<string> done = new HashSet<string>();
            foreach (var node in all)
            {
                foreach (var child in node.Children)
                {
                    if (child.IsItem || done.Contains(child.ContainerType)) continue;
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("using UnityEngine;\nusing UnityEngine.UI;\nusing TMPro;\n");
                    sb.AppendLine($"namespace {config.uiNamespace}\n{{\n    [System.Serializable]\n    public class {child.ContainerType}\n    {{");
                    sb.AppendLine("        [SerializeField] private GameObject m_gameObject;");
                    sb.AppendLine("        public GameObject gameObject => m_gameObject;");
                    foreach (var t in child.ComponentTypes)
                    {
                        string field = $"m_{char.ToLower(t[0]) + t.Substring(1)}";
                        string prop = char.ToLower(t[0]) + t.Substring(1);
                        sb.AppendLine($"        [SerializeField] private {t} {field};");
                        sb.AppendLine($"        public {t} {prop} => {field};");
                    }
                    sb.AppendLine($"        public {child.ContainerType}() {{ }}\n    }}\n}}");
                    File.WriteAllText(Path.Combine(config.uiContainerPath, $"{child.ContainerType}.cs"), sb.ToString(), Encoding.UTF8);
                    done.Add(child.ContainerType);
                }
            }
        }

        private static string CreateLogicTemplate(string n, UIBuildConfig c) => $@"using UnityEngine;
namespace {c.uiNamespace}
{{
    public partial class {n}
    {{
        protected override void OnInit() 
        {{
        }}
        protected override void OnClose()
        {{
        }}
    }}
}}";

        private static string CreateMainSerializeTemplate(UIMap map, UIBuildConfig config)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("using System.Collections.Generic;\nusing UnityEngine;\nusing UnityEngine.UI;\nusing TMPro;\n");
            sb.AppendLine($"namespace {config.uiNamespace}\n{{\n    public partial class {map.MainUIName} : {BaseClassName}\n    {{");
            foreach (var child in map.Root.Children)
            {
                sb.AppendLine($"        [SerializeField] private {child.ContainerType} {child.Name};");
                sb.AppendLine($"        public {child.ContainerType} {child.PropertyName} => {child.Name};");
            }
            foreach (var item in map.Items)
            {
                if (!map.Root.Children.Contains(item)) continue;
                sb.AppendLine($@"
        [System.Serializable]
        public class {item.ContainerType} {{
            [SerializeField] private GameObject m_gameObject;
            public GameObject gameObject => m_gameObject;
            [SerializeField] private RectTransform m_rectTransform;
            public RectTransform rectTransform => m_rectTransform;
            [SerializeField] private {item.FullClassName} {item.Name};
            public {item.FullClassName} {item.PropertyName} => {item.Name};

            [System.NonSerialized] public List<{item.FullClassName}> mCachedList = new List<{item.FullClassName}>();
            private Queue<{item.FullClassName}> mCachedInstances;
            public {item.FullClassName} GetInstance(bool ignoreSibling = false) {{
                {item.FullClassName} instance = null;
                if (mCachedInstances != null) while ((instance == null || instance.Equals(null)) && mCachedInstances.Count > 0) instance = mCachedInstances.Dequeue();
                if (instance == null || instance.Equals(null)) instance = Instantiate<{item.FullClassName}>({item.Name});
                Transform t0 = {item.Name}.transform; Transform t1 = instance.transform;
                t1.SetParent(t0.parent, false); t1.localPosition = t0.localPosition; t1.localRotation = t0.localRotation; t1.localScale = t0.localScale;
                if (!ignoreSibling) t1.SetSiblingIndex(t0.GetSiblingIndex() + 1); else t1.SetAsLastSibling();
                instance.gameObject.SetActive(true); mCachedList.Add(instance); return instance;
            }}
            public void CacheInstance({item.FullClassName} instance) {{
                if (instance == null) return;
                if (mCachedInstances == null) mCachedInstances = new Queue<{item.FullClassName}>();
                if (!mCachedInstances.Contains(instance)) {{ instance.gameObject.SetActive(false); mCachedInstances.Enqueue(instance); }}
            }}
            public void CacheInstanceList() {{ foreach (var instance in mCachedList) CacheInstance(instance); mCachedList.Clear(); }}
        }}");
            }
            sb.AppendLine("    }\n}");
            return sb.ToString();
        }

        private static string CreateSubSerializeTemplate(NodeData item, UIBuildConfig config)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;\nusing UnityEngine.UI;\nusing TMPro;\n");
            sb.AppendLine($"namespace {config.uiNamespace}\n{{\n    public partial class {item.FullClassName} : {SubBaseClassName}\n    {{");
            foreach (var child in item.Children)
            {
                sb.AppendLine($"        [SerializeField] private {child.ContainerType} {child.Name};");
                sb.AppendLine($"        public {child.ContainerType} {child.PropertyName} => {child.Name};");
            }
            sb.AppendLine("    }\n}");
            return sb.ToString();
        }

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            string task = EditorPrefs.GetString(PendingTaskKey, "");
            if (string.IsNullOrEmpty(task)) return;
            EditorPrefs.DeleteKey(PendingTaskKey);
            string[] parts = task.Split('|');
            EditorApplication.delayCall += () => AttachAndBindScript(parts[0], parts[1]);
        }

        private static void AttachAndBindScript(string prefabPath, string fullClassName)
        {
            Type uiType = GetTypeFromAllAssemblies(fullClassName);
            if (uiType == null) return;
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            BindRecursive(root, root, uiType);
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log($"<color=green>[UIBuild]</color> 脚本级联注入完成: {fullClassName}");
        }

        private static void BindRecursive(GameObject rootGo, GameObject currentGo, Type scriptType)
        {
            Component comp = currentGo.GetComponent(scriptType);
            if (comp == null) comp = currentGo.AddComponent(scriptType);
            string ns = scriptType.Namespace;
            UIMap map = new UIMap(scriptType.Name, currentGo.transform);
            ScanRecursive(currentGo.transform, map, map.Root);
            foreach (var node in map.Root.Children)
            {
                Transform target = currentGo.transform.Find(node.Path);
                if (target == null) continue;
                if (node.IsItem)
                {
                    Type itemType = GetTypeFromAllAssemblies($"{ns}.{node.FullClassName}");
                    if (itemType != null)
                    {
                        BindRecursive(rootGo, target.gameObject, itemType);
                        Type ctType = scriptType.GetNestedType(node.ContainerType);
                        if (ctType != null)
                        {
                            object ct = Activator.CreateInstance(ctType);
                            SetField(ctType, ct, "m_gameObject", target.gameObject);
                            SetField(ctType, ct, "m_rectTransform", target.GetComponent<RectTransform>());
                            SetField(ctType, ct, node.Name, target.GetComponent(itemType));
                            SetField(scriptType, comp, node.Name, ct);
                        }
                    }
                }
                else
                {
                    Type ctType = GetTypeFromAllAssemblies($"{ns}.{node.ContainerType}");
                    if (ctType != null)
                    {
                        object ct = Activator.CreateInstance(ctType);
                        SetField(ctType, ct, "m_gameObject", target.gameObject);
                        foreach (var c in node.ComponentTypes) SetField(ctType, ct, $"m_{char.ToLower(c[0]) + c.Substring(1)}", target.GetComponent(c));
                        SetField(scriptType, comp, node.Name, ct);
                    }
                }
            }
        }

        private static Type GetTypeFromAllAssemblies(string n) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(n)).FirstOrDefault(t => t != null);
        private static void SetField(Type t, object o, string p, object v)
        {
            FieldInfo f = t.GetField(p, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f != null) f.SetValue(o, v);
        }
    }
}
