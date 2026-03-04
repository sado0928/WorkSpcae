using UnityEngine;
using UnityEditor;

namespace Game.Editor
{
    public class UIBuildConfig : ScriptableObject
    {
        [Header("路径设置 (相对于 Assets)")]
        public string uiLogicPath = "Assets/Game/Scripts/Runtime/Hotfix/UI/Logic";
        public string uiSerializePath = "Assets/Game/Scripts/Runtime/Hotfix/UI/Serialize";
        public string uiContainerPath = "Assets/Game/Scripts/Runtime/Hotfix/UI/Container";

        [Header("命名空间设置")]
        public string uiNamespace = "Game.Runtime.Hotfix.UI";

        public void Save()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }
}
