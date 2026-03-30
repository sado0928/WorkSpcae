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

        public Vector3 Position { get;private set; }
        public Quaternion Rotation { get;private set; }
        public Vector3 Scale { get;private set; }
        public Transform Parent { get;private set; }
        
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
            if (Position !=default) SetPosition(Position);
            if (Rotation !=default) SetRotation(Rotation);
            if (Scale !=default) SetScale(Scale);
            if (Parent !=default) SetParent(Parent);
            m_Callback?.Invoke(this);
            m_Callback = null;
        }
        
        public void SetPosition(Vector3 pos)
        {
            Position = pos;
            if (IsLoaded) m_GameObject.transform.localPosition = pos; 
        }

        public void SetRotation(Quaternion rot)
        {
            Rotation = rot;
            if (IsLoaded) m_GameObject.transform.localRotation = rot;
        }
        
        public void SetScale(Vector3 scale)
        {
            Scale = scale;
            if (IsLoaded) m_GameObject.transform.localScale = scale;
        }
        public void SetParent(Transform parent)
        {
            Parent = parent;
            if (IsLoaded) m_Base.m_Parent.SetParent(parent, false);
        }
    }
    
    /// <summary>
    /// 实体系谱定义
    /// </summary>
    public static class EntityDefine
    {
      
    }
}
