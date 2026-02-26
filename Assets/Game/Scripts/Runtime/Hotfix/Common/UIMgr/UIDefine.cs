using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// UI 层级枚举
    /// </summary>
    public enum UILayer
    {
        Main = 0,      // 场景层 (主界面，HUD) - BaseOrder: 0
        Normal = 1,     // 普通层 (全屏界面，如背包、角色) - BaseOrder: 1000
        PopUp = 2,      // 弹窗层 (确认框，提示框) - BaseOrder: 2000
        Top = 3,        // 顶层 (Loading，新手引导) - BaseOrder: 3000
        System = 4      // 系统层 (断线重连，跑马灯) - BaseOrder: 4000
    }

    /// <summary>
    /// UI 配置结构
    /// </summary>
    public class UIConfig
    {
        public string Path;                // 资源地址
        public UILayer Layer;              // 层级
        public ResTypeByScene ResTypeByScene;      // 资源归属场景
        public bool ClickMaskClose;        // 是否开启点击遮罩关闭
        public bool KeepCached;            // 是否缓存实例

        public UIConfig()
        {
        }
    }
    
    /// <summary>
    /// UI 异步加载句柄
    /// 作用：提供类似于 Task/Promise 的回调机制，让业务层可以在 UI 加载完成后获取实例
    /// </summary>
    /// <typeparam name="T">UIBase 的具体类型</typeparam>
    public class UIHandle<T> where T : UIBase
    {
        private Action<T> m_OnComplete;
        private T m_Result;
        private bool m_IsDone;

        public UIHandle()
        {
            m_IsDone = false;
        }

        /// <summary>
        /// 设置加载完成后的回调
        /// 如果已经加载完成，会立即执行回调
        /// </summary>
        public void SetCallback(Action<T> callback)
        {
            m_OnComplete = callback;
            if (m_IsDone && m_Result != null)
            {
                m_OnComplete?.Invoke(m_Result);
                m_OnComplete = null; // 触发一次后清空，避免重复
            }
        }

        /// <summary>
        /// 内部调用：标记加载完成
        /// </summary>
        public void Complete(T result)
        {
            m_Result = result;
            m_IsDone = true;
            m_OnComplete?.Invoke(m_Result);
            m_OnComplete = null;
        }
    }

    /// <summary>
    /// UI 定义文件
    /// </summary>
    public class UIDefine
    {
        public static string LoadingUI = "LoadingUI";
        public static string MainUI = "MainUI";

        #region 配置字典

        public static Dictionary<string, UIConfig> UIInfo = new Dictionary<string, UIConfig>()
        {
            {LoadingUI,new UIConfig(){Path ="Prefabs/UI/LoadingUI",Layer = UILayer.Normal, ResTypeByScene = ResTypeByScene.Global}},
            {MainUI,new UIConfig(){Path ="Prefabs/UI/MainUI",Layer = UILayer.Main, ResTypeByScene = ResTypeByScene.Global}}
        };

        #endregion

        public static UIConfig GetUIConfig(string key)
        {
            if (UIInfo.TryGetValue(key,out UIConfig uiConfig))
            {
                return uiConfig;
            }
            return null;
        }

    }
}
