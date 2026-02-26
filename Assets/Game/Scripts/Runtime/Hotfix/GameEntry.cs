using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace Game.Runtime.Hotfix
{
    public class GameEntry
    {
        // 游戏入口
        public static void StartGame()
        {
            Debug.Log("=== Hotfix: 开始进入游戏主逻辑 ===");
            // 加载并实例化 KeepNode (逻辑核心)
            Debug.Log("=== Hotfix: 正在加载 KeepNode... ===");
            var nodeHandle = Addressables.InstantiateAsync("Prefabs/KeepNode.prefab");
            GameObject keepNode = nodeHandle.WaitForCompletion();

            if (keepNode != null)
            {
                // 确保 KeepNode 不会被后续场景切换销毁（虽然它是常驻的，但显式声明更安全）
                Object.DontDestroyOnLoad(keepNode);
                Debug.Log("=== Hotfix: KeepNode 加载成功，游戏逻辑已接管 ===");
            }
            else
            {
                Debug.LogError("=== Hotfix: KeepNode 加载失败！ ===");
            }
        }
    }
}
