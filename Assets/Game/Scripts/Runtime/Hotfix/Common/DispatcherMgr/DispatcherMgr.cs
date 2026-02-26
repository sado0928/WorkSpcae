using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Hotfix
{
    // 事件管理类
    public class DispatcherMgr
    {
        // 使用 Dictionary 存储事件，Key 为 EventDefine，Value 为 MulticastDelegate
        private Dictionary<EventDefine, Delegate> m_HandlerDic = new Dictionary<EventDefine, Delegate>();
        
        // =========================================================
        // 添加事件监听 (Add)
        // =========================================================

        public void AddEventListener(EventDefine eventType, Action listener)
        {
            OnListenerAdding(eventType, listener);
            m_HandlerDic[eventType] = (Action)m_HandlerDic[eventType] + listener;
        }

        public void AddEventListener<T1>(EventDefine eventType, Action<T1> listener)
        {
            OnListenerAdding(eventType, listener);
            m_HandlerDic[eventType] = (Action<T1>)m_HandlerDic[eventType] + listener;
        }

        public void AddEventListener<T1, T2>(EventDefine eventType, Action<T1, T2> listener)
        {
            OnListenerAdding(eventType, listener);
            m_HandlerDic[eventType] = (Action<T1, T2>)m_HandlerDic[eventType] + listener;
        }

        public void AddEventListener<T1, T2, T3>(EventDefine eventType, Action<T1, T2, T3> listener)
        {
            OnListenerAdding(eventType, listener);
            m_HandlerDic[eventType] = (Action<T1, T2, T3>)m_HandlerDic[eventType] + listener;
        }

        // 统一的添加前检查逻辑
        private void OnListenerAdding(EventDefine eventType, Delegate listener)
        {
            if (!m_HandlerDic.ContainsKey(eventType))
            {
                m_HandlerDic[eventType] = null;
            }
            
            Delegate d = m_HandlerDic[eventType];
            if (d != null && d.GetType() != listener.GetType())
            {
                throw new Exception($"尝试为事件 {eventType} 添加不同类型的监听器。当前: {d.GetType().Name}, 添加: {listener.GetType().Name}");
            }
        }

        // =========================================================
        // 移除事件监听 (Remove)
        // =========================================================

        public void RemoveEventListener(EventDefine eventType, Action listener)
        {
            OnListenerRemoving(eventType, listener);
        }

        public void RemoveEventListener<T1>(EventDefine eventType, Action<T1> listener)
        {
            OnListenerRemoving(eventType, listener);
        }

        public void RemoveEventListener<T1, T2>(EventDefine eventType, Action<T1, T2> listener)
        {
            OnListenerRemoving(eventType, listener);
        }

        public void RemoveEventListener<T1, T2, T3>(EventDefine eventType, Action<T1, T2, T3> listener)
        {
            OnListenerRemoving(eventType, listener);
        }

        // 统一的移除逻辑
        private void OnListenerRemoving(EventDefine eventType, Delegate listener)
        {
            if (m_HandlerDic.TryGetValue(eventType, out Delegate d))
            {
                if (d != null)
                {
                    // 移除委托
                    d = Delegate.Remove(d, listener);
                    
                    if (d == null)
                    {
                        m_HandlerDic.Remove(eventType);
                    }
                    else
                    {
                        m_HandlerDic[eventType] = d;
                    }
                }
            }
        }

        // =========================================================
        // 事件分发 (Dispatch)
        // =========================================================

        public void Dispatch(EventDefine eventType)
        {
            if (m_HandlerDic.TryGetValue(eventType, out Delegate d))
            {
                if (d == null) return;
                
                // 获取调用列表（快照），防止回调中修改导致异常
                Delegate[] invocationList = d.GetInvocationList();
                
                // 正序遍历，符合先注册先执行的直觉
                for (int i = 0; i < invocationList.Length; i++)
                {
                    try
                    {
                        (invocationList[i] as Action)?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"事件 {eventType} 回调报错: {ex}");
                    }
                }
            }
        }

        public void Dispatch<T1>(EventDefine eventType, T1 arg1)
        {
            if (m_HandlerDic.TryGetValue(eventType, out Delegate d))
            {
                if (d == null) return;
                
                Delegate[] invocationList = d.GetInvocationList();
                for (int i = 0; i < invocationList.Length; i++)
                {
                    try
                    {
                        (invocationList[i] as Action<T1>)?.Invoke(arg1);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"事件 {eventType} 回调报错: {ex}");
                    }
                }
            }
        }

        public void Dispatch<T1, T2>(EventDefine eventType, T1 arg1, T2 arg2)
        {
            if (m_HandlerDic.TryGetValue(eventType, out Delegate d))
            {
                if (d == null) return;
                
                Delegate[] invocationList = d.GetInvocationList();
                for (int i = 0; i < invocationList.Length; i++)
                {
                    try
                    {
                        (invocationList[i] as Action<T1, T2>)?.Invoke(arg1, arg2);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"事件 {eventType} 回调报错: {ex}");
                    }
                }
            }
        }

        public void Dispatch<T1, T2, T3>(EventDefine eventType, T1 arg1, T2 arg2, T3 arg3)
        {
            if (m_HandlerDic.TryGetValue(eventType, out Delegate d))
            {
                if (d == null) return;
                
                Delegate[] invocationList = d.GetInvocationList();
                for (int i = 0; i < invocationList.Length; i++)
                {
                    try
                    {
                        (invocationList[i] as Action<T1, T2, T3>)?.Invoke(arg1, arg2, arg3);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"事件 {eventType} 回调报错: {ex}");
                    }
                }
            }
        }

        // =========================================================
        // 管理接口
        // =========================================================

        public void ClearAll()
        {
            m_HandlerDic.Clear();
        }
    }
}