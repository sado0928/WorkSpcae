using System;
using UnityEngine;

namespace Game.Runtime.Hotfix
{
    // 实体类型定义
    public enum EntityType
    {
        Hero = 1,
        Monster = 2,
    }
    
    /// <summary>
    /// 实体 异步加载句柄
    /// </summary>
    /// <typeparam name="T">EntityBase 的具体类型</typeparam>
    public class EntityHandle<T> where T : EntityBase
    {
        private Action<T> m_OnComplete;
        private T m_Result;
        private bool m_IsDone;
        
        public Vector3 Position { get;private set; }
        public Vector3 Rotation { get;private set; }

        public Vector3 Scale { get;private set; }
        
        public EntityHandle()
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
            if (Position !=default) SetPosition(Position);
            if (Rotation !=default) SetRotation(Rotation);
            if (Scale !=default) SetScale(Scale);
            m_OnComplete?.Invoke(m_Result);
            m_OnComplete = null;
        }
        
        public void SetPosition(Vector3 pos)
        {
            Position = pos;
            if (m_IsDone) m_Result.OnSetPosition(pos); 
        }

        public void SetRotation(Vector3 rot)
        {
            Rotation = rot;
            if (m_IsDone) m_Result.OnSetRotation(rot);
        }
        
        public void SetScale(Vector3 scale)
        {
            Scale = scale;
            if (m_IsDone) m_Result.OnSetScale(scale);
        }
    }
    
    /// <summary>
    /// 实体系谱定义
    /// </summary>
    public static class EntityDefine
    {
      
    }
}
