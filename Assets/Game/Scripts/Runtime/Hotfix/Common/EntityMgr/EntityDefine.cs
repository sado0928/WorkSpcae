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
    
    public class EntityHandle
    {
        public string Path { get; private set; }
        public EntityBase m_Base { get; private set; }
        public GameObject m_GameObject
        {
            get
            {
                return  m_Base != null ? m_Base.gameObject : null;;
            }
            private set { }
        }

        public bool IsLoaded => m_Base != null;

        private Action<EntityHandle> m_Callback;

        public EntityHandle(string path)
        {
            Path = path;
        }

        public EntityHandle SetCallback(Action<EntityHandle> callback)
        {
            if (IsLoaded)
            {
                callback?.Invoke(this);
            }
            else
            {
                m_Callback = callback;
            }
            return this;
        }

        public void Complete(EntityBase baseComp)
        {
            m_Base = baseComp;
            m_Callback?.Invoke(this);
            m_Callback = null;
        }
        
        public void SetPosition(Vector3 pos)
        {
            if (IsLoaded) m_GameObject.transform.position = pos;
        }

        public void SetRotation(Quaternion rot)
        {
            if (IsLoaded) m_GameObject.transform.rotation = rot;
        }
        
        public void SetParent(Transform parent, bool worldPositionStays = false)
        {
            if (IsLoaded) m_GameObject.transform.SetParent(parent, worldPositionStays);
        }
    }
    
    /// <summary>
    /// 实体系谱定义
    /// </summary>
    public static class EntityDefine
    {
      
    }
}
