using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// 池对象身份标识组件
    /// 注意：此组件由 PoolMgr 内部自动补齐，外部无需关心。
    /// </summary>
    public class PoolItem : MonoBehaviour
    {
        public string AssetPath { get; set; }
    }
    
    /// <summary>
    /// 池对象异步句柄
    /// </summary>
    public class PoolHandle<T> where T : PoolBase
    {
        private Action<T> m_OnComplete;
        private T m_Result;
        private bool m_IsDone;

        public PoolHandle()
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

        public void Complete(T result)
        {
            m_Result = result;
            m_IsDone = true;
            m_OnComplete?.Invoke(m_Result);
            m_OnComplete = null;
        }
    }

    /// <summary>
    /// 对象池资源定义
    /// </summary>
    public static class PoolDefine
    {
       
    }
}
