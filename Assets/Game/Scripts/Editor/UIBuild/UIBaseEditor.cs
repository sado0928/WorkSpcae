using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;

namespace Game.Editor
{
    /// <summary>
    /// 为所有 UI 脚本提供 Inspector 快捷操作。
    /// 使用反射匹配基类名，避免因为 asmdef 导致的编译报错。
    /// </summary>
    [CustomEditor(typeof(MonoBehaviour), true)]
    [CanEditMultipleObjects]
    public class UIBaseEditor : UnityEditor.Editor
    {
        private bool _isUIType = false;

        private void OnEnable()
        {
            if (target == null) return;
            
            // 向上遍历基类，检查是否包含 UIBase 或 ResBase
            Type type = target.GetType();
            while (type != null && type != typeof(MonoBehaviour))
            {
                if (type.Name == "UIBase" || type.Name == "ResBase")
                {
                    _isUIType = true;
                    break;
                }
                type = type.BaseType;
            }
        }

        public override void OnInspectorGUI()
        {
            if (_isUIType)
            {
                EditorGUILayout.Space();
                GUI.color = new Color(0.4f, 0.8f, 1f); // 亮蓝色
                if (GUILayout.Button("Serialize UI (同步代码与绑定)", GUILayout.Height(30)))
                {
                    ExecuteSerialize();
                }
                GUI.color = Color.white;
                EditorGUILayout.Space();
            }

            // 调用默认的 Inspector 绘制，确保原有变量显示不受影响
            base.OnInspectorGUI();
        }

        private void ExecuteSerialize()
        {
            MonoBehaviour mb = target as MonoBehaviour;
            if (mb == null) return;

            // 1. 获取对应的 Prefab 资源
            GameObject prefabAsset = GetPrefabAsset(mb.gameObject);

            if (prefabAsset != null)
            {
                // 2. 调用生成工具
                UIBuildTool.ExecuteSerializeForObject(mb);
            }
            else
            {
                Debug.LogError("[UIBuild] 无法定位 Prefab 资源。请确保该脚本挂载在 Prefab 实例上。");
            }
        }

        private GameObject GetPrefabAsset(GameObject go)
        {
            // 尝试获取场景物体的源 Prefab
            GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
            if (prefab != null) return prefab;

            // 检查是否在 Prefab 隔离编辑模式下
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.IsPartOfPrefabContents(go))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(stage.assetPath);
            }

            // 检查物体本身是否就是 Prefab 资源
            string path = AssetDatabase.GetAssetPath(go);
            if (!string.IsNullOrEmpty(path) && path.EndsWith(".prefab"))
            {
                return go;
            }

            return null;
        }
    }
}
