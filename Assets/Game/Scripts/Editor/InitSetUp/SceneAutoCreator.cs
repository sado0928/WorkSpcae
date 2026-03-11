using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Game.Editor.InitSetUp
{
    public class SceneAutoCreator : EditorWindow
    {
        [MenuItem("FreamWork/Setup/Setup Logo Scene UI")]
        public static void CreateLevel1Scene()
        {
            // 1. 创建新场景
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            
            // 2. 配置主摄像机 (2D 优化)
            GameObject cameraObj = GameObject.Find("Main Camera");
            if (cameraObj != null)
            {
                Camera cam = cameraObj.GetComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 10f; // 视野范围
                cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f); // 暗色背景，突出特效
                cam.clearFlags = CameraClearFlags.SolidColor;
                cameraObj.transform.position = new Vector3(0, 0, -10);
            }

            // 3. 创建地图容器 (Map)
            GameObject mapContainer = new GameObject("Map_Level1");
            mapContainer.transform.position = Vector3.zero;
            
            // 创建一个背景平面作为地面 (用于临时占位，后续你可以换成 Tiled Sprite)
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floor.name = "Floor_Visual";
            floor.transform.SetParent(mapContainer.transform);
            floor.transform.localScale = new Vector3(200, 200, 1); // 巨大的地图
            floor.transform.rotation = Quaternion.Euler(0, 0, 0);
            
            // 移除 MeshCollider，2D 游戏不需要 3D 碰撞
            DestroyImmediate(floor.GetComponent<MeshCollider>());
            
            // 材质处理 (给地面一个简单的默认材质，防止它是粉色的)
            Renderer rend = floor.GetComponent<Renderer>();
            rend.material = new Material(Shader.Find("Sprites/Default"));
            rend.material.color = new Color(0.2f, 0.3f, 0.2f); // 深绿色背景

            // 4. 创建玩家起点 (Player_Spawn_Point)
            GameObject playerPoint = new GameObject("Player_Spawn_Point");
            playerPoint.transform.position = Vector3.zero;

            // 5. 创建敌人管理器占位符 (Enemy_Manager)
            GameObject enemyMgr = new GameObject("Enemy_Manager_Pool");
            enemyMgr.transform.position = Vector3.zero;

            // 6. 确保保存目录存在
            string sceneDir = "Assets/Game/Scenes";
            if (!System.IO.Directory.Exists(sceneDir))
            {
                System.IO.Directory.CreateDirectory(sceneDir);
            }
            
            // 7. 保存并刷新
            string path = "Assets/Game/Scenes/Level1Scene.unity";
            EditorSceneManager.SaveScene(newScene, path);
            AssetDatabase.Refresh();
            
            Debug.Log($"<color=green><b>[Success]</b></color> 场景已成功创建并保存至: {path}");
            Debug.Log("<color=yellow><b>[Hint]</b></color> 请点击 Unity 顶部的 Tools > Game Setup > Create Level1 Scene 来执行。");
        }
    }
}
