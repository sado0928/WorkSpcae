using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.U2D;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// 资源类型枚举
    /// 作用：定义资源的逻辑分类，解耦具体目录结构
    /// </summary>
    public enum ResType
    {
        Prefab = 0, //预制
        SpriteAtlas, //图集
        Sprite, //图片
        Audio, //音效
        Font, //字体
        Asset, //asset (ScriptableObject, Config, etc.)
        Material, //mat
        Scenes, //场景
    }

    public enum ResTypeByScene
    {
        Global, // 全局常驻资源
        Level1, // 示例：关卡1资源
        // Level2, 
        // Battle,
    }

    /// <summary>
    /// 资源管理器
    /// 职责：统一负责基于 Addressables 的资源加载与卸载。
    /// 特色：
    /// 1. 严格的生命周期控制：所有资源（Prefab/纹理/音频）都归属于特定的 ResTypeByScene 域。
    /// 2. 统一缓存：不再区分资源类型，统一管理 UnityEngine.Object。
    /// </summary>
    public class ResMgr
    {
        private Dictionary<ResType, string> m_ResTypeFormatDic;
        
        // --- 统一缓存管理 ---
        // Key1: 场景域 (Global, Level1...)
        // Key2: 资源路径 (Addressable Name/Path)
        // Value: 资源对象 (UnityEngine.Object)
        private Dictionary<ResTypeByScene, Dictionary<string, UnityEngine.Object>> m_CacheDic;

        /// <summary>
        /// 当前资源加载所属的场景域
        /// </summary>
        public ResTypeByScene CurrentTypeBySceneType { get; private set; } = ResTypeByScene.Global;

        public ResMgr()
        {
            m_ResTypeFormatDic = new Dictionary<ResType, string>
            {
                { ResType.Prefab, "{0}.prefab" },
                { ResType.SpriteAtlas, "{0}.spriteatlas" },
                { ResType.Sprite, "{0}.png" },
                { ResType.Audio, "{0}.mp3" },
                { ResType.Font, "{0}.ttf" },
                { ResType.Asset, "{0}.asset" },
                { ResType.Material, "{0}.mat" },
                { ResType.Scenes, "{0}.unity" }
            };

            // 初始化各场景域的缓存池
            m_CacheDic = new Dictionary<ResTypeByScene, Dictionary<string, UnityEngine.Object>>();
            foreach (ResTypeByScene type in Enum.GetValues(typeof(ResTypeByScene)))
            {
                m_CacheDic[type] = new Dictionary<string, UnityEngine.Object>();
            }
        }

        
        public virtual void OnDestroy()
        {
            UnLoadAssets();
        }
        
        private string GetAddress(string path, ResType resType)
        {
            if (m_ResTypeFormatDic.TryGetValue(resType, out string format))
                return string.Format(format, path);
            return path;
        }

        /// <summary>
        /// 通用加载接口
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径</param>
        /// <param name="resType">资源后缀类型</param>
        /// <param name="forceGlobal">是否强制加载到 Global 域（即使当前处于 Level 域）</param>
        /// <returns></returns>
        public T LoadAsset<T>(string path, ResType resType, bool forceGlobal = false) where T : UnityEngine.Object
        {
            // 确定目标缓存域：如果强制Global，则存入Global，否则存入当前场景域
            ResTypeByScene targetScope = forceGlobal ? ResTypeByScene.Global : CurrentTypeBySceneType;

            // 1. 检查目标域缓存
            if (m_CacheDic[targetScope].TryGetValue(path, out UnityEngine.Object cachedObj))
            {
                if (cachedObj is T result) return result;
                Debug.LogWarning($"[ResMgr] Path {path} cached but type mismatch. Expected {typeof(T)}, got {cachedObj?.GetType()}");
            }

            // 2. 如果目标域不是 Global，尝试去 Global 域找一下（防止重复加载通用资源）
            if (targetScope != ResTypeByScene.Global)
            {
                if (m_CacheDic[ResTypeByScene.Global].TryGetValue(path, out UnityEngine.Object globalObj))
                {
                    // 找到了，直接返回，不移动所有权，它仍然属于 Global
                    return globalObj as T;
                }
            }

            // 3. 真正加载 (同步等待)
            string address = GetAddress(path, resType);
            var op = Addressables.LoadAssetAsync<T>(address);
            T asset = op.WaitForCompletion();

            // 4. 存入缓存
            if (op.Status == AsyncOperationStatus.Succeeded && asset != null)
            {
                m_CacheDic[targetScope][path] = asset;
                return asset;
            }
            
            Debug.LogError($"[ResMgr] Load failed: {address}");
            return null;
        }

        /// <summary>
        /// 通用异步加载接口
        /// </summary>
        public void LoadAssetAsync<T>(string path, ResType resType, Action<T> callback, bool forceGlobal = false) where T : UnityEngine.Object
        {
            if (callback == null) return;

            ResTypeByScene targetScope = forceGlobal ? ResTypeByScene.Global : CurrentTypeBySceneType;

            // 1. 检查目标域缓存
            if (m_CacheDic[targetScope].TryGetValue(path, out UnityEngine.Object cachedObj))
            {
                if (cachedObj is T result)
                {
                    callback(result);
                    return;
                }
            }

            // 2. 检查 Global 域缓存
            if (targetScope != ResTypeByScene.Global)
            {
                if (m_CacheDic[ResTypeByScene.Global].TryGetValue(path, out UnityEngine.Object globalObj))
                {
                    callback(globalObj as T);
                    return;
                }
            }

            // 3. 真正异步加载
            string address = GetAddress(path, resType);
            Addressables.LoadAssetAsync<T>(address).Completed += (op) =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded && op.Result != null)
                {
                    // 放入目标域缓存（由于是异步，需再次判断防止并发冲突）
                    if (!m_CacheDic[targetScope].ContainsKey(path))
                    {
                        m_CacheDic[targetScope][path] = op.Result;
                    }
                    callback(op.Result);
                }
                else
                {
                    Debug.LogError($"[ResMgr] Async Load failed: {address}");
                    callback(null);
                }
            };
        }

        /// <summary>
        /// 手动卸载指定资源
        /// </summary>
        public void UnloadAsset(string path, ResTypeByScene? scope = null)
        {
            // 如果未指定域，优先在当前域找，找不到去 Global 找
            if (scope == null)
            {
                if (UnloadAssetFromScope(path, CurrentTypeBySceneType)) return;
                if (CurrentTypeBySceneType != ResTypeByScene.Global)
                {
                    UnloadAssetFromScope(path, ResTypeByScene.Global);
                }
            }
            else
            {
                UnloadAssetFromScope(path, scope.Value);
            }
        }

        /// <summary>
        /// 手动卸载全部资源
        /// </summary>
        public void UnLoadAssets()
        {
            Resources.UnloadUnusedAssets();
            GC.Collect();
        }
        
        private bool UnloadAssetFromScope(string path, ResTypeByScene scope)
        {
            if (m_CacheDic[scope].TryGetValue(path, out UnityEngine.Object obj))
            {
                if (obj != null) Addressables.Release(obj);
                m_CacheDic[scope].Remove(path);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 切换场景逻辑 (异步)
        /// </summary>
        public void LoadSceneAsync(string scenePath, ResTypeByScene newSceneType, Action onComplete = null)
        {
            if (CurrentTypeBySceneType != ResTypeByScene.Global && CurrentTypeBySceneType != newSceneType)
            {
                ClearSceneRes(CurrentTypeBySceneType);
            }

            CurrentTypeBySceneType = newSceneType;

            string address = GetAddress(scenePath, ResType.Scenes);
            Addressables.LoadSceneAsync(address, UnityEngine.SceneManagement.LoadSceneMode.Single).Completed += (op) =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded)
                {
                    UnLoadAssets();
                    onComplete?.Invoke();
                }
                else
                {
                    Debug.LogError($"[ResMgr] Async Load Scene failed: {address}");
                }
            };
        }

        /// <summary>
        /// 切换场景逻辑
        /// 1. 清理上一个场景的资源（如果是从非 Global 切走）
        /// 2. 加载新场景
        /// </summary>
        /// <param name="scenePath">场景资源路径</param>
        /// <param name="newSceneType">新场景的资源域类型</param>
        public void LoadScene(string scenePath, ResTypeByScene newSceneType)
        {
            // 如果当前场景类型不是 Global，且发生了类型变化，清理旧资源
            // 例如：Level1 -> Level2，清理 Level1
            // 例如：Global -> Level1，不清理 Global
            if (CurrentTypeBySceneType != ResTypeByScene.Global && CurrentTypeBySceneType != newSceneType)
            {
                ClearSceneRes(CurrentTypeBySceneType);
            }

            CurrentTypeBySceneType = newSceneType;

            string address = GetAddress(scenePath, ResType.Scenes);
            // Single 模式加载场景
            Addressables.LoadSceneAsync(address, UnityEngine.SceneManagement.LoadSceneMode.Single).WaitForCompletion();
            
            // 建议：切场景后手动 GC
            UnLoadAssets();
        }

        /// <summary>
        /// 清理指定资源域的所有资源
        /// </summary>
        public void ClearSceneRes(ResTypeByScene typeBySceneType)
        {
            if (typeBySceneType == ResTypeByScene.Global)
            {
                Debug.LogWarning("[ResMgr] Warning: Clearing Global cache! Make sure this is intended (e.g. Reboot).");
            }

            if (m_CacheDic.TryGetValue(typeBySceneType, out var subDic))
            {
                int count = 0;
                // 需要复制 Keys 列表，因为在遍历时会修改字典
                var keys = new List<string>(subDic.Keys);
                foreach (var key in keys)
                {
                    var obj = subDic[key];
                    if (obj != null)
                    {
                        Addressables.Release(obj);
                        count++;
                    }
                }
                subDic.Clear();
                Debug.Log($"[ResMgr] Cleared cache for scope [{typeBySceneType}]. Released {count} assets.");
            }
        }

        // --- 封装便捷接口 ---

        public GameObject LoadPrefab(string path, bool forceGlobal = false)
        {
            return LoadAsset<GameObject>(path, ResType.Prefab, forceGlobal);
        }

        public Sprite LoadSprite(string path, bool forceGlobal = false)
        {
            return LoadAsset<Sprite>(path, ResType.Sprite, forceGlobal);
        }

        public SpriteAtlas LoadSpriteAtlas(string path, bool forceGlobal = false)
        {
            return LoadAsset<SpriteAtlas>(path, ResType.SpriteAtlas, forceGlobal);
        }

        public AudioClip LoadAudio(string path, bool forceGlobal = false)
        {
            return LoadAsset<AudioClip>(path, ResType.Audio, forceGlobal);
        }

        public Font LoadFont(string path, bool forceGlobal = false)
        {
            return LoadAsset<Font>(path, ResType.Font, forceGlobal);
        }
        
        public Material LoadMaterial(string path, bool forceGlobal = false)
        {
            return LoadAsset<Material>(path, ResType.Material, forceGlobal);
        }

        public ScriptableObject LoadScriptableObject(string path, bool forceGlobal = false)
        {
            return LoadAsset<ScriptableObject>(path, ResType.Asset, forceGlobal);
        }

        // --- 异步封装接口 ---

        public void LoadPrefabAsync(string path, Action<GameObject> callback, bool forceGlobal = false)
        {
            LoadAssetAsync<GameObject>(path, ResType.Prefab, callback, forceGlobal);
        }

        public void LoadSpriteAsync(string path, Action<Sprite> callback, bool forceGlobal = false)
        {
            LoadAssetAsync<Sprite>(path, ResType.Sprite, callback, forceGlobal);
        }

        public void LoadSpriteAtlasAsync(string path, Action<SpriteAtlas> callback, bool forceGlobal = false)
        {
            LoadAssetAsync<SpriteAtlas>(path, ResType.SpriteAtlas, callback, forceGlobal);
        }

        public void LoadAudioAsync(string path, Action<AudioClip> callback, bool forceGlobal = false)
        {
            LoadAssetAsync<AudioClip>(path, ResType.Audio, callback, forceGlobal);
        }

        public void LoadFontAsync(string path, Action<Font> callback, bool forceGlobal = false)
        {
            LoadAssetAsync<Font>(path, ResType.Font, callback, forceGlobal);
        }

        public void LoadMaterialAsync(string path, Action<Material> callback, bool forceGlobal = false)
        {
            LoadAssetAsync<Material>(path, ResType.Material, callback, forceGlobal);
        }

        public void LoadScriptableObjectAsync(string path, Action<ScriptableObject> callback, bool forceGlobal = false)
        {
            LoadAssetAsync<ScriptableObject>(path, ResType.Asset, callback, forceGlobal);
        }
        
        /// <summary>
        /// 释放资源实例 (GameObject)
        /// 注意：如果是通过 ResMgr.Instantiate 创建的 (基于 Object.Instantiate)，请使用 ResMgr.Destroy。
        /// 如果是通过 Addressables.InstantiateAsync 创建的，才使用此方法。
        /// 目前 ResMgr 采用 Asset 模式，所以此方法主要用于兼容或特定 Addressable 实例。
        /// </summary>
        public void ReleaseInstance(GameObject instance)
        {
            if (instance != null)
            {
                Addressables.ReleaseInstance(instance);
            }
        }

        /// <summary>
        /// 实例化对象 (对 UnityEngine.Object.Instantiate 的封装)
        /// </summary>
        public GameObject Instantiate(GameObject prefab, Transform parent = null)
        {
            if (prefab == null) return null;
            return UnityEngine.Object.Instantiate(prefab, parent);
        }

        /// <summary>
        /// 销毁对象 (对 UnityEngine.Object.Destroy 的封装)
        /// </summary>
        public void Destroy(GameObject instance)
        {
            if (instance != null)
            {
                UnityEngine.Object.Destroy(instance);
            }
        }
        
        /// <summary>
        /// 重启游戏时彻底清理
        /// </summary>
        public void ClearAll()
        {
            foreach (ResTypeByScene type in Enum.GetValues(typeof(ResTypeByScene)))
            {
                ClearSceneRes(type);
            }
            CurrentTypeBySceneType = ResTypeByScene.Global;
        }
    }
}
