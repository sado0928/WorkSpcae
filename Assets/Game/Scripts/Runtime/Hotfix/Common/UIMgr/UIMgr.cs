using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// UI 管理器
    /// 职责：管理 UI 的加载、层级、缓存、互斥逻辑及队列加载
    /// </summary>
    public class UIMgr:IUpdate
    {
        private Transform m_UIRoot;
        
        // 资源缓存: Key -> Prefab (只存资源，不存实例)
        private Dictionary<string, GameObject> m_ResCache = new Dictionary<string, GameObject>();
        
        // 活动 UI 缓存: Key -> UIBase (当前正在打开的实例)
        private Dictionary<string, UIBase> m_ActiveCache = new Dictionary<string, UIBase>();
        
        // --- UI 栈管理 (存储当前 Active 的 UI，按打开顺序排序，最末尾为最顶层) ---
        private List<UIBase> m_ActiveStack = new List<UIBase>();
        
        // 层级根节点管理: Layer -> Transform
        private Dictionary<UILayer, Transform> m_LayerRoots = new Dictionary<UILayer, Transform>();
        
        // 关闭时间记录: Key -> Time (用于 LRU 资源释放)
        private Dictionary<string, float> m_LastCloseTime = new Dictionary<string, float>();
        
        // 正在加载中的 UI (防止短时间内重复 Open 导致重复加载)
        // Key: UI Key
        private HashSet<string> m_LoadingSet = new HashSet<string>();
        
        // 层级基准 Order
        private Dictionary<UILayer, int> m_LayerBaseOrder = new Dictionary<UILayer, int>()
        {
            { UILayer.Main, 0 },
            { UILayer.Normal, 1000 },
            { UILayer.PopUp, 2000 },
            { UILayer.Top, 3000 },
            { UILayer.System, 4000 }
        };

        // sorting设置
        public int m_OrderDefalut { get; private set; } = 0;
        public int m_OrderStep { get; private set; } = 5;
        public float m_DistanceDefault { get; private set; } = 100f;
        public float m_DistanceStep { get; private set; } = 5f;
        
        // 动态引用的组件
        public Canvas m_RootCanvas { get; private set; }
        public Camera m_UICamera { get; private set; }

        // --- 队列加载相关 ---
        private Queue<Action> m_OpenQueue = new Queue<Action>();
        private bool m_IsQueueRunning = false;

        // --- 防止按钮多点打开相关 ---
        // Flag -> 占用的 UI Key
        private List<string> m_OpenFlags = new List<string>();
        
        // --- 卸载资源
        private float m_AutoReleaseResTime = 0;

        private float m_AutoReleaseResTimeLeft = 60f;//自动卸载 计时器
        public void Init(Transform keepNode)
        {
            // 1. 获取 RootCanvas 和 UICamera
            m_RootCanvas = Global.gApp.gCanvas;
            if (m_RootCanvas == null)
            {
                Debug.LogError("[UIMgr] KeepNode must have a Canvas component for reference!");
            }
            
            m_UICamera = Global.gApp.gUICamera;
            if (m_UICamera == null)
            {
                // 如果 KeepNode 没有相机，尝试找 MainCamera
                m_UICamera = Camera.main;
            }

            //设置 RenderMode 和 Camera
            if (m_RootCanvas.renderMode != RenderMode.WorldSpace)
            {
                m_RootCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                m_RootCanvas.worldCamera = m_UICamera;
            }

            m_RootCanvas.sortingOrder = m_OrderDefalut;
            m_RootCanvas.planeDistance = m_DistanceDefault;
            
            // 2. 创建 UIRoot
            GameObject rootGO = new GameObject("UIRoot");
            m_UIRoot = rootGO.transform;
            m_UIRoot.SetParent(keepNode, false);
            
            // 3. 初始化各层级根节点
            m_LayerRoots.Clear();
            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
            {
                GameObject layerGO = new GameObject(layer.ToString());
                RectTransform rect = layerGO.AddComponent<RectTransform>();
                
                layerGO.transform.SetParent(m_UIRoot, false);
                
                // 设置全屏撑开
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                
                // 确保 Layer 节点不阻挡射线 (仅作为容器)
                // 如果需要 CanvasGroup 可以在这里加，目前保持纯净
                
                m_LayerRoots.Add(layer, layerGO.transform);
            }
        }
        
        #region API - 状态查询与获取

        /// <summary>
        /// 获取最顶层 (最后打开) 的 UI
        /// </summary>
        /// <param name="layer">可选：指定层级。如果不传，则返回整个栈的最顶层。</param>
        /// <returns>UIBase 对象，如果找不到则返回 null</returns>
        public UIBase GetTopUI(UILayer? layer = null)
        {
            if (layer == null)
            {
                return m_ActiveStack.Count > 0 ? m_ActiveStack.Last() : null;
            }

            // 从栈顶向下查找，第一个符合层级的 UI 即为该层级的最顶层
            for (int i = m_ActiveStack.Count - 1; i >= 0; i--)
            {
                if (m_ActiveStack[i].Config.Layer == layer.Value)
                {
                    return m_ActiveStack[i];
                }
            }
            return null;
        }

        /// <summary>
        /// 判断指定 UI 是否处于打开 (Active) 状态
        /// </summary>
        /// <param name="key">UI Key</param>
        /// <returns>true 表示已打开且可见</returns>
        public bool IsOpen(string key)
        {
            return m_ActiveCache.ContainsKey(key);
        }

        /// <summary>
        /// 获取当前打开的 UI 实例
        /// </summary>
        /// <typeparam name="T">UI 类型</typeparam>
        /// <param name="key">UI Key</param>
        /// <returns>UI 实例，如果未打开则返回 null</returns>
        public T GetUI<T>(string key) where T : UIBase
        {
            if (m_ActiveCache.TryGetValue(key, out UIBase ui))
            {
                return ui as T;
            }
            return null;
        }

        /// <summary>
        /// 获取当前所有打开的 UI
        /// </summary>
        /// <param name="layer">可选：筛选指定层级。如果不传 (null)，则返回所有层级的 UI。</param>
        /// <returns>UI 列表</returns>
        public List<UIBase> GetOpenUIs(UILayer? layer = null)
        {
            List<UIBase> result = new List<UIBase>();
            for (int i = 0; i < m_ActiveStack.Count; i++)
            {
                var ui = m_ActiveStack[i];
                if (ui != null)
                {
                    if (layer == null || ui.Config.Layer == layer.Value)
                    {
                        result.Add(ui);
                    }
                }
            }
            return result;
        }

        #endregion

        #region API - 基础打开

        /// <summary>
        /// 异步打开 UI (泛型版本)
        /// </summary>
        /// <typeparam name="T">UI 脚本类型</typeparam>
        /// <param name="key">UIDefine 中定义的 UI Key</param>
        /// <returns>UIHandle 句柄</returns>
        public UIHandle<T> OpenUIAsync<T>(string key) where T : UIBase
        {
            UIHandle<T> handle = new UIHandle<T>();
            OpenInternal(key, typeof(T), (ui) => 
            {
                handle.Complete(ui as T);
            });
            return handle;
        }

        /// <summary>
        /// 异步打开 UI (无泛型，返回 UIBase)
        /// </summary>
        public UIHandle<UIBase> OpenUIAsync(string key)
        {
            UIHandle<UIBase> handle = new UIHandle<UIBase>();
            OpenInternal(key, null, (ui) => 
            {
                handle.Complete(ui);
            });
            return handle;
        }

        #endregion
        
        #region API - 队列加载 (Queue)

        /// <summary>
        /// 队列式打开 UI
        /// 作用：保证 UI 严格按照调用顺序一个接一个地打开，前一个加载并初始化完毕后，才开始加载下一个。
        /// 适用场景：新手引导、连续弹窗、剧情对话等对时序要求严格的模块。
        /// </summary>
        public UIHandle<T> OpenUIByQueue<T>(string key) where T : UIBase
        {
            UIHandle<T> handle = new UIHandle<T>();

            // 将请求封装为一个 Action，推入队列
            m_OpenQueue.Enqueue(() =>
            {
                // 真正执行加载
                OpenInternal(key, typeof(T), (ui) =>
                {
                    handle.Complete(ui as T);
                    
                    // 当前这个加载完成了，触发队列下一个
                    CheckNextInQueue();
                });
            });

            // 如果当前队列没在跑，启动它
            if (!m_IsQueueRunning)
            {
                CheckNextInQueue();
            }

            return handle;
        }

        private void CheckNextInQueue()
        {
            if (m_OpenQueue.Count > 0)
            {
                m_IsQueueRunning = true;
                Action nextAction = m_OpenQueue.Dequeue();
                nextAction?.Invoke();
            }
            else
            {
                m_IsQueueRunning = false;
            }
        }

        #endregion

        #region API - 互斥加载

        /// <summary>
        /// 互斥式打开 UI
        /// 作用：防止同一组功能的 UI 并发打开
        /// 逻辑：
        /// 1. 检查 flag 是否被占用。
        /// 2. 如果被占用，且占用者不是当前 key -> 拒绝打开 (返回 null handle 或 失败 handle)。
        /// 3. 如果未被占用，或占用者就是我自己 -> 允许打开，并标记占用。
        /// </summary>
        public UIHandle<T> OpenUIByFlag<T>(string key) where T : UIBase
        {
            // 检查是否已经有正在打开的UI
            if (m_OpenFlags.Count > 0)
            {
                // 如果当前 flag 已经被别人占用了 (ownerKey != key)，则拒绝本次请求
                return null;
            }
            // 添加标志位
            m_OpenFlags.Add(key);

            // 劫持回调释放标志位
            UIHandle<T> handle = new UIHandle<T>();
            OpenInternal(key,typeof(T),(ui) =>
            {
                m_OpenFlags.Remove(key);
                handle.Complete(ui as T);
            });
            return handle;
        }

        #endregion

        #region 内部核心逻辑

        private void OpenInternal(string key, Type scriptType, Action<UIBase> onComplete)
        {
            // 0. 获取配置
            UIConfig config = UIDefine.GetUIConfig(key);
            if (config == null)
            {
                onComplete?.Invoke(null);
                return;
            }

            // 1. 检查是否已经打开 (Active)
            if (m_ActiveCache.TryGetValue(key, out UIBase activeUI))
            {
                // 如果已经打开，将其挪到最上层 (Stack 逻辑)
                if (m_ActiveStack.Contains(activeUI)) m_ActiveStack.Remove(activeUI);
                
                // 重新计算 Sorting (基于当前其他 UI)
                SetupUIState(activeUI, config);
                
                m_ActiveStack.Add(activeUI);
                
                onComplete?.Invoke(activeUI);
                return;
            }

            // 2. 检查是否正在加载中 (防并发)
            if (m_LoadingSet.Contains(key))
            {
                Debug.LogWarning($"[UIMgr] UI {key} is already loading. Ignored duplicate request.");
                return;
            }

            // 内部实例化方法
            Action<GameObject> doInstantiate = (prefab) => 
            {
                GameObject instantiatedGO = null;
                try
                {
                    // 确定父节点
                    Transform parent = m_UIRoot;
                    if (m_LayerRoots.TryGetValue(config.Layer, out Transform layerRoot))
                    {
                        parent = layerRoot;
                    }

                    // 实例化
                    instantiatedGO = Global.gApp.gResMgr.Instantiate(prefab, parent);
                    instantiatedGO.name = key;

                    // 获取/挂载脚本
                    UIBase script = instantiatedGO.GetComponent<UIBase>();
                    if (script == null)
                    {
                        if (scriptType != null)
                        {
                            script = instantiatedGO.AddComponent(scriptType) as UIBase;
                        }
                        else
                        {
                            throw new Exception($"Prefab {config.Path} has no UIBase and no Type provided!");
                        }
                    }

                    // 初始化状态
                    SetupUIState(script, config);

                    // 初始化业务
                    script.OnInit(key, config);

                    // 加入 Active 缓存和栈
                    m_ActiveCache.Add(key, script);
                    m_ActiveStack.Add(script);
                    
                    // 移除上一轮的关闭时间记录 (如果存在)，因为它现在活过来了
                    if (m_LastCloseTime.ContainsKey(key)) m_LastCloseTime.Remove(key);

                    onComplete?.Invoke(script);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[UIMgr] Critical exception during UI Open ({key}): {e}");
                    if (instantiatedGO != null)
                    {
                        Global.gApp.gResMgr.Destroy(instantiatedGO);
                    }
                    onComplete?.Invoke(null);
                }
            };

            // 3. 检查资源缓存 (m_ResCache)
            if (m_ResCache.TryGetValue(key, out GameObject cachedPrefab))
            {
                // 资源已在内存，直接实例化
                if (cachedPrefab != null)
                {
                    doInstantiate(cachedPrefab);
                    return;
                }
                else
                {
                    // 防御：Prefab 引用丢失 (可能是被 Force Unload 了)，移除并重新加载
                    m_ResCache.Remove(key);
                }
            }

            // 4. 异步加载资源
            m_LoadingSet.Add(key);
            Global.gApp.gResMgr.LoadPrefabAsync(config.Path, (prefab) =>
            {
                m_LoadingSet.Remove(key);

                if (prefab == null)
                {
                    Debug.LogError($"[UIMgr] Failed to load UI prefab: {config.Path} (Key: {key})");
                    onComplete?.Invoke(null);
                    return;
                }
                
                // 存入资源缓存
                if (!m_ResCache.ContainsKey(key))
                {
                    m_ResCache.Add(key, prefab);
                }

                // 执行实例化
                doInstantiate(prefab);

            }, false); // false 表示不需要 ResMgr 强制缓存到 Global，因为 UIMgr 自己持有引用
        }

        /// <summary>
        /// 统一设置 UI 的状态 (Camera, Sorting, Active)
        /// </summary>
        private void SetupUIState(UIBase ui, UIConfig config)
        {
            ui.gameObject.SetActive(true);
            
            int finalOrder = m_RootCanvas != null ? m_RootCanvas.sortingOrder : m_OrderDefalut;
            float finalDistance = m_RootCanvas != null ? m_RootCanvas.planeDistance : m_DistanceDefault;
            List<UIBase> uiBases = GetOpenUIs(config.Layer);

            if (uiBases.Count > 0)
            {
                UIBase topUI = uiBases.Last();
                finalOrder = topUI.m_FinalOrder;
                finalDistance = topUI.m_FinalDistance;
            }
            else
            {
                // 先拿各自UI层级的基础值
                finalOrder = m_LayerBaseOrder[config.Layer];
            }
            // 将参数传进去，让 UIBase 为每个子 Canvas 独立计算 Distance
            ui.SetSorting(finalOrder, finalDistance);
        }

        #endregion

        #region 关闭与销毁

        public void Close(string key)
        {
            if (m_ActiveCache.TryGetValue(key, out UIBase ui))
            {
                var lastCloseTime = Time.realtimeSinceStartup;
                // 1. 生命周期
                ui.OnClose(lastCloseTime);
                
                // 2. 维护 UI 栈：从栈中移除
                if (m_ActiveStack.Contains(ui)) m_ActiveStack.Remove(ui);

                // 3. 记录关闭时间 (用于 LRU 释放资源)
                // 只有当配置了非 KeepCached 时才记录，KeepCached 的永远不记录时间（免死金牌）
                if (!ui.Config.KeepCached)
                {
                    m_LastCloseTime[key] = lastCloseTime;
                }

                // 4. 销毁实例
                Global.gApp.gResMgr.Destroy(ui.gameObject);
                
                // 5. 移除活动缓存
                m_ActiveCache.Remove(key);
                
            }
        }
        
        /// <summary>
        /// 关闭所有 UI
        /// </summary>
        /// <param name="keepLayer">可选：指定一个需要保留的层级 (例如 Main 层)。如果不传，则关闭所有。</param>
        public void CloseAll(UILayer? keepLayer = null)
        {
            // 复制 Keys 列表防止修改集合报错
            var keys = new List<string>(m_ActiveCache.Keys);
            foreach (var key in keys)
            {
                if (m_ActiveCache.TryGetValue(key, out UIBase ui))
                {
                    // 如果指定了保留层级，且当前 UI 属于该层级，则跳过
                    if (keepLayer.HasValue && ui.Config.Layer == keepLayer.Value)
                    {
                        continue;
                    }
                    Close(key);
                }
            }

            // 如果是全关，可以清理状态，但 Cache 由 LRU 管理，不强制 Clear
            if (keepLayer == null)
            {
                m_OpenFlags.Clear();
                m_OpenQueue.Clear();
                m_ActiveStack.Clear();
                m_IsQueueRunning = false;
            }
        }

        #endregion
        
        #region 卸载资源

        public void ReleaseUnUseUI(bool force = false)
        {
            m_AutoReleaseResTime = 0;
            float now = Time.realtimeSinceStartup;
            List<string> keysToRemove = new List<string>();

            // 遍历所有持有的资源
            foreach (var key in m_ResCache.Keys)
            {
                // 如果当前正在使用 (Active)，则跳过
                if (m_ActiveCache.ContainsKey(key)) continue;
                
                // 检查是否有关闭时间记录 (KeepCached 的没有记录，所以通过 ContainsKey 自动过滤)
                // 如果是刚 Close 的，m_LastCloseTime 应该有记录
                if (m_LastCloseTime.TryGetValue(key, out float closeTime))
                {
                    if (force || (now - closeTime > m_AutoReleaseResTimeLeft))
                    {
                        // 1. 获取配置以拿到 Path
                        UIConfig config = UIDefine.GetUIConfig(key);
                        if (config != null)
                        {
                            // 2. 告诉 ResMgr 卸载资源
                            Global.gApp.gResMgr.UnloadAsset(config.Path);
                        }
                        
                        // 3. 记录 Key 待移除
                        keysToRemove.Add(key);
                    }
                }
            }

            // 4. 从资源缓存和时间记录中移除
            foreach (string key in keysToRemove)
            {
                m_ResCache.Remove(key);
                m_LastCloseTime.Remove(key);
            }
        }

        #endregion
        
        public void OnIUpdate(float dt)
        {
            if (Global.gApp.gResMgr.CurrentTypeBySceneType is ResTypeByScene.Global)
            {
                m_AutoReleaseResTime += dt;
                if (m_AutoReleaseResTime >= m_AutoReleaseResTimeLeft)
                {
                    ReleaseUnUseUI();
                }
            }
        }
    }
}
