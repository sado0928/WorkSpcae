using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Hotfix
{
    public abstract class UIBase : ResBase
    {
        public RectTransform m_RectTransform { get; private set; }
        
        // 当前 UI 的配置信息
        public UIConfig Config { get; private set; }

        // 当前 UI 的 Key (UIDefine 中的常量)
        public string Key { get; private set; }

        // 最后一次关闭的时间 (用于 LRU 缓存清理)
        public float LastCloseTime { get; private set; }

        // 遮罩节点
        private GameObject m_MaskNode;

        // Canvas 管理 (使用并行列表简化结构)
        private List<Canvas> m_SubCanvases = new List<Canvas>();

        // 定时器
        private List<int> m_TimerIds = new List<int>();
        private List<int> m_FrameTimerIds = new List<int>();

        public int m_FinalOrder { get;private set; } 
        public float m_FinalDistance { get;private set; } 
        
        /// <summary>
        /// 获取主 Canvas (用于 UIMgr 设置 Camera 等)
        /// </summary>
        public Canvas MainCanvas
        {
            get
            {
                if (m_SubCanvases != null && m_SubCanvases.Count > 0)
                    return m_SubCanvases[0];
                return null;
            }
        }
        
        protected virtual void Awake()
        {
            var rectTransform = GetComponent<RectTransform>();
            if (rectTransform == null) rectTransform = gameObject.AddComponent<RectTransform>();
            m_RectTransform = rectTransform;
            // 规则检查：根节点不允许挂载 Canvas
            if (GetComponent<Canvas>() != null)
            {
                Debug.LogError(
                    $"[UIBase] Root node '{name}' should NOT have a Canvas! Please move it to a child node.");
            }

            // 获取所有子节点中的 Canvas (不包括根节点)
            Canvas[] childCanvases = GetComponentsInChildren<Canvas>(true);

            // 过滤掉误挂在根节点的 Canvas (如果有)
            m_SubCanvases.Clear();
            
            foreach (var c in childCanvases)
            {
                if (c.gameObject != this.gameObject) 
                {
                    m_SubCanvases.Add(c);
                }
            }

            if (m_SubCanvases.Count == 0)
            {
                Debug.LogError($"[UIBase] No Canvas found in children of '{name}'! UI will not render correctly.");
                return;
            }
            
        }

        protected override void OnDestroy()
        {
            // 1. 调用 ResBase 的资源自动释放逻辑
            base.OnDestroy();

            // 2. 清理 UI 特有的引用
            m_SubCanvases.Clear();
            m_TimerIds.Clear();
            m_FrameTimerIds.Clear();
        }
        
        /// <summary>
        /// 初始化 (仅第一次加载时调用)
        /// </summary>
        public void OnInit(string key, UIConfig config)
        {
            Key = key;
            Config = config;

            // 处理点击遮罩关闭逻辑
            if (Config != null && Config.ClickMaskClose)
            {
                CheckOrCreateTouchMask();
            }

            OnInit();
        }

        /// <summary>
        /// 关闭时调用
        /// </summary>
        public void OnClose(float lastCloseTime)
        {
            LastCloseTime = lastCloseTime;
            RemoveAllTimer();
            RemoveAllFrameTimer();
            OnClose();
        }

        // 定义抽象每个UI脚本实现
        protected abstract void OnInit();
        protected abstract void OnClose();

        public virtual void OnRefresh(){}
        public virtual void OnRefresh(int val){}
        public virtual void OnRefresh(string val){}
        public virtual void OnRefresh(UIDataBase val){}

        /// <summary>
        /// 统一设置所有子 Canvas 的排序、距离和相机
        /// </summary>
        public void SetAddSorting(int order, float distance)
        {
            if (m_SubCanvases == null) return;
            var distanceStep = Global.gApp.gUIMgr.m_DistanceStep;
            var orderStep = Global.gApp.gUIMgr.m_OrderStep;
            foreach (Canvas canvas in m_SubCanvases)
            {
                if (canvas != null)
                {
                    // 1. 设置 Order 
                    m_FinalOrder = order + orderStep;
                    canvas.sortingOrder = m_FinalOrder;
                    
                    // 2. 设置 RenderMode 和 Camera
                    if (canvas.renderMode != RenderMode.WorldSpace)
                    {
                        var uiCamera = Global.gApp.gUIMgr.m_UICamera;
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = uiCamera;
                    }
                    
                    // 3. 独立计算每个 SubCanvas 的 Distance
                    m_FinalDistance = distance - distanceStep;
                    canvas.planeDistance = m_FinalDistance;
                }
            }
        }

        /// <summary>
        /// 统一设置所有子 Canvas 的排序、距离和相机
        /// </summary>
        public void SetReduceSorting(int order, float distance)
        {
            if (m_SubCanvases == null) return;
            var distanceStep = Global.gApp.gUIMgr.m_DistanceStep;
            var orderStep = Global.gApp.gUIMgr.m_OrderStep;
            foreach (Canvas canvas in m_SubCanvases)
            {
                if (canvas != null)
                {
                    // 1. 设置 Order 
                    m_FinalOrder = order - orderStep;
                    canvas.sortingOrder = m_FinalOrder;
                    
                    // 2. 设置 RenderMode 和 Camera
                    if (canvas.renderMode != RenderMode.WorldSpace)
                    {
                        var uiCamera = Global.gApp.gUIMgr.m_UICamera;
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = uiCamera;
                    }
                    
                    // 3. 独立计算每个 SubCanvas 的 Distance
                    m_FinalDistance = distance + distanceStep;
                    canvas.planeDistance = m_FinalDistance;
                }
            }
        }
        /// <summary>
        /// 检查或创建点击遮罩
        /// 遮罩将挂载到第一个子 Canvas 节点下
        /// </summary>
        private void CheckOrCreateTouchMask()
        {
            Canvas main = MainCanvas;
            if (main == null) return;

            Transform parentNode = main.transform;
            Transform firstChild = parentNode.childCount > 0 ? parentNode.GetChild(0) : null;

            if (firstChild != null && firstChild.name == "Auto_TouchMask")
            {
                m_MaskNode = firstChild.gameObject;
                m_MaskNode.SetActive(true);
                return;
            }

            m_MaskNode = new GameObject("Auto_TouchMask", typeof(RectTransform), typeof(Image), typeof(Button));
            m_MaskNode.transform.SetParent(parentNode, false);
            m_MaskNode.transform.SetAsFirstSibling();

            RectTransform rect = m_MaskNode.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image img = m_MaskNode.GetComponent<Image>();
            img.color = new Color(0, 0, 0, 0.5f);
            img.raycastTarget = true;

            Button btn = m_MaskNode.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                if (!string.IsNullOrEmpty(Key))
                {
                    Global.gApp.gUIMgr.CloseUI(Key);
                }
            });
        }
        
        public int AddTimer(float dtTime, int callTimes, System.Action<float, bool> callBack)
        {
            int timerId = Global.gApp.gTimerMgr.AddTimer(dtTime, callTimes, callBack);
            m_TimerIds.Add(timerId);
            return timerId;
        }

        public void RemoveTimer(int timerId)
        {
            Global.gApp.gTimerMgr.RemoveTimer(timerId);
        }

        private void RemoveAllTimer()
        {
            foreach (int timerId in m_TimerIds)
            {
                RemoveTimer(timerId);
            }

            m_TimerIds.Clear();
        }
        
        public int AddFrameTimer(int dtFrame, int callTimes, System.Action<float, bool> callBack)
        {
            int frameTimerId = Global.gApp.gFrameTimerMgr.AddFrameTimer(dtFrame, callTimes, callBack);
            m_FrameTimerIds.Add(frameTimerId);
            return frameTimerId;
        }

        public void RemoveFrameTimer(int frameTimerId)
        {
            Global.gApp.gFrameTimerMgr.RemoveTimer(frameTimerId);
        }

        private void RemoveAllFrameTimer()
        {
            foreach (int timerId in m_FrameTimerIds)
            {
                RemoveFrameTimer(timerId);
            }

            m_FrameTimerIds.Clear();
        }
    }
}