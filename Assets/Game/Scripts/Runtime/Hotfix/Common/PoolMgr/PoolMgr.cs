using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// 对象池管理器
    /// 职责：管理基于 PoolBase 的资源复用与生命周期托管。
    /// 特色：
    /// 1. 异步句柄模式：提供 Spawn<T> 返回 PoolHandle<T>，对齐 UIMgr 使用习惯。
    /// 2. 自动组件挂载：如果 Prefab 上缺失对应的 PoolBase 子类脚本，会自动动态挂载。
    /// 3. 自动生命周期：联动 ResMgr 进行资源加载，自动维护场景域。
    /// </summary>
    public class PoolMgr
    {
        // 等待回调的任务结构
        private struct SpawnTask
        {
            public Type ComponentType;
            public Action<GameObject> Callback;
        }

        // 闲置对象池：Key (资源路径) -> 闲置对象堆栈
        private Dictionary<string, Stack<GameObject>> m_PoolIdleQueues = new Dictionary<string, Stack<GameObject>>();
        
        // 活跃对象池：Key -> 活跃对象列表
        private Dictionary<string, List<GameObject>> m_PoolActiveLists = new Dictionary<string, List<GameObject>>();
        
        // 场景根节点容器
        private Dictionary<ResTypeByScene, Transform> m_SceneRoots = new Dictionary<ResTypeByScene, Transform>();

        // 正在加载中的资源 Key 集
        private HashSet<string> m_LoadingPools = new HashSet<string>();
        
        // 并发加载时的回调等待队列
        private Dictionary<string, List<SpawnTask>> m_WaitingTasks = new Dictionary<string, List<SpawnTask>>();

        public PoolMgr()
        {
            // 初始化全局根节点
            GetOrCreateSceneRoot(ResTypeByScene.Global);
            
            // 核心联动：监听场景切换事件，执行场景域清理
            Global.gApp.gDispatcherMgr.AddEventListener<ResTypeByScene, ResTypeByScene>(EventDefine.LoadingScene, OnLoadingScene);
        }

        #region 外部 API (Simplified API)

        /// <summary>
        /// 异步获取池对象 (返回句柄模式)
        /// </summary>
        /// <typeparam name="T">具体的 PoolBase 子类类型</typeparam>
        /// <param name="key">资源路径</param>
        /// <returns>池对象句柄</returns>
        public PoolHandle<T> Spawn<T>(string key) where T : PoolBase
        {
            PoolHandle<T> handle = new PoolHandle<T>();
            InternalSpawn(key, typeof(T), (go) =>
            {
                T comp = go != null ? go.GetComponent<T>() : null;
                handle.Complete(comp);
            });
            return handle;
        }

        /// <summary>
        /// 内部获取池对象逻辑
        /// </summary>
        private void InternalSpawn(string key, Type componentType, Action<GameObject> callback)
        {
            if (string.IsNullOrEmpty(key) || callback == null) return;

            // 1. 优先从闲置池获取
            if (m_PoolIdleQueues.TryGetValue(key, out var idleQueue) && idleQueue.Count > 0)
            {
                GameObject go = idleQueue.Pop();
                if (go != null)
                {
                    ActivateInstance(key, go, callback);
                    return;
                }
            }
            
            // 2. 处理并发加载请求
            if (m_LoadingPools.Contains(key))
            {
                if (!m_WaitingTasks.ContainsKey(key)) m_WaitingTasks[key] = new List<SpawnTask>();
                m_WaitingTasks[key].Add(new SpawnTask { ComponentType = componentType, Callback = callback });
                return;
            }

            // 3. 联动 ResMgr 加载 Prefab
            m_LoadingPools.Add(key);
            
            bool isGlobalScope = Global.gApp.gResMgr.CurrentTypeBySceneType == ResTypeByScene.Global;
            
            Global.gApp.gResMgr.LoadPrefabAsync(key, (prefab) =>
            {
                m_LoadingPools.Remove(key);
                if (prefab == null)
                {
                    callback?.Invoke(null);
                    return;
                }

                // 实例化
                CreateInstance(key, componentType, prefab, callback);

                // 处理等待队列中的任务
                if (m_WaitingTasks.TryGetValue(key, out var taskList))
                {
                    foreach (var task in taskList)
                    {
                        CreateInstance(key, task.ComponentType, prefab, task.Callback);
                    }
                    m_WaitingTasks.Remove(key);
                }

            }, isGlobalScope);
        }

        /// <summary>
        /// 回收对象至池
        /// </summary>
        public void Despawn(GameObject go)
        {
            if (go == null) return;

            PoolItemIdentity identity = go.GetComponent<PoolItemIdentity>();
            if (identity != null)
            {
                string key = identity.AssetPath;
                if (m_PoolActiveLists.TryGetValue(key, out var activeList))
                {
                    if (activeList.Contains(go))
                    {
                        activeList.Remove(go);
                        
                        if (!m_PoolIdleQueues.ContainsKey(key)) m_PoolIdleQueues[key] = new Stack<GameObject>();
                        m_PoolIdleQueues[key].Push(go);

                        // 根据 PoolBase 场景域归还至对应的根节点
                        var poolBase = go.GetComponent<PoolBase>();
                        ResTypeByScene scope = poolBase != null ? poolBase.ResTypeByScene : ResTypeByScene.Global;
                        
                        go.transform.SetParent(GetOrCreateSceneRoot(scope), false);
                        
                        if (poolBase != null) poolBase.InternalOnDespawn();
                    }
                }
            }
            else
            {
                Global.gApp.gResMgr.Destroy(go);
            }
        }

        #endregion

        #region 内部实例化与激活

        private void CreateInstance(string key, Type componentType, GameObject prefab, Action<GameObject> callback)
        {
            ResTypeByScene currentScope = Global.gApp.gResMgr.CurrentTypeBySceneType;
            Transform parent = GetOrCreateSceneRoot(currentScope);
            
            // 联动 ResMgr 实例化
            GameObject go = Global.gApp.gResMgr.Instantiate(prefab, parent);
            
            // 补齐身份标识（用于 Despawn 回溯）
            var identity = go.GetComponent<PoolItemIdentity>();
            if (identity == null) identity = go.AddComponent<PoolItemIdentity>();
            identity.AssetPath = key;

            // 核心修复：根据泛型类型动态挂载组件，而不是添加抽象类
            var poolBase = go.GetComponent(componentType) as PoolBase;
            if (poolBase == null)
            {
                poolBase = go.AddComponent(componentType) as PoolBase;
            }

            if (poolBase != null)
            {
                poolBase.OnInit(key, currentScope);
            }
            else
            {
                Debug.LogError($"[PoolMgr] Failed to attach component of type {componentType.Name} to {go.name}. Make sure it inherits from PoolBase.");
            }

            ActivateInstance(key, go, callback);
        }

        private void ActivateInstance(string key, GameObject go, Action<GameObject> callback)
        {
            if (!m_PoolActiveLists.ContainsKey(key)) m_PoolActiveLists[key] = new List<GameObject>();
            m_PoolActiveLists[key].Add(go);
            
            var poolBase = go.GetComponent<PoolBase>();
            if (poolBase != null) poolBase.InternalOnSpawn();

            callback?.Invoke(go);
        }

        #endregion

        #region 场景管理与清理

        private void OnLoadingScene(ResTypeByScene lastScene, ResTypeByScene nextScene)
        {
            ClearScenePool(lastScene);
        }

        public void ClearScenePool(ResTypeByScene scope)
        {
            // 清理闲置池
            List<string> idleKeysToRemove = new List<string>();
            foreach (var kv in m_PoolIdleQueues)
            {
                var stack = kv.Value;
                if (stack.Count > 0)
                {
                    var firstGo = stack.Peek();
                    var poolBase = firstGo != null ? firstGo.GetComponent<PoolBase>() : null;
                    if (poolBase != null && poolBase.ResTypeByScene == scope)
                    {
                        foreach (var go in stack) if (go) Global.gApp.gResMgr.Destroy(go);
                        stack.Clear();
                        idleKeysToRemove.Add(kv.Key);
                    }
                }
            }
            foreach (var key in idleKeysToRemove) m_PoolIdleQueues.Remove(key);

            // 清理活跃池
            List<string> activeKeysToRemove = new List<string>();
            foreach (var kv in m_PoolActiveLists)
            {
                var list = kv.Value;
                if (list.Count > 0)
                {
                    var firstGo = list[0];
                    var poolBase = firstGo != null ? firstGo.GetComponent<PoolBase>() : null;
                    if (poolBase != null && poolBase.ResTypeByScene == scope)
                    {
                        foreach (var go in list) if (go) Global.gApp.gResMgr.Destroy(go);
                        list.Clear();
                        activeKeysToRemove.Add(kv.Key);
                    }
                }
            }
            foreach (var key in activeKeysToRemove) m_PoolActiveLists.Remove(key);

            // 销毁物理根节点
            if (m_SceneRoots.TryGetValue(scope, out var root))
            {
                if (root != null) Global.gApp.gResMgr.Destroy(root.gameObject);
                m_SceneRoots.Remove(scope);
            }
        }

        #endregion

        private Transform GetOrCreateSceneRoot(ResTypeByScene scope)
        {
            if (!m_SceneRoots.TryGetValue(scope, out var root) || root == null)
            {
                GameObject rootGO = new GameObject($"PoolRoot_{scope}");
                root = rootGO.transform;
                m_SceneRoots[scope] = root;
                if (scope == ResTypeByScene.Global) UnityEngine.Object.DontDestroyOnLoad(rootGO);
            }
            return root;
        }

        public void OnDestroy()
        {
            Global.gApp.gDispatcherMgr.RemoveEventListener<ResTypeByScene, ResTypeByScene>(EventDefine.LoadingScene, OnLoadingScene);
            
            foreach (var queue in m_PoolIdleQueues.Values) foreach (var go in queue) if (go) Global.gApp.gResMgr.Destroy(go);
            m_PoolIdleQueues.Clear();

            foreach (var list in m_PoolActiveLists.Values) foreach (var go in list) if (go) Global.gApp.gResMgr.Destroy(go);
            m_PoolActiveLists.Clear();

            foreach (var root in m_SceneRoots.Values) if (root != null) Global.gApp.gResMgr.Destroy(root.gameObject);
            m_SceneRoots.Clear();
        }
    }
}
