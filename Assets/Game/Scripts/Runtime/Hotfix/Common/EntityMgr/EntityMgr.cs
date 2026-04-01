using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// 实体管理器 (Common)
    /// 负责所有实体 (Hero, Monster) 的生命周期管理与逻辑同步。
    /// </summary>
    public class EntityMgr : IUpdate
    {
        private List<EffectHandle<EffectBase>> m_ActiveHandles = new List<EffectHandle<EffectBase>>();
        public Transform m_EntityRoot { get; private set; }
        public Dictionary<long, EntityBase> m_EntityDic { get; private set; } = new Dictionary<long, EntityBase>();
        public List<EntityBase> m_EntityList { get; private set; } = new List<EntityBase>();
        
        public EntityMgr()
        {
            m_EntityRoot = new GameObject("EntityRoot").transform;
            Object.DontDestroyOnLoad(m_EntityRoot.gameObject);
        }
        
        /// <summary>
        /// 创建实体方法
        /// </summary>
        /// <param name="assetPath">地址</param>
        /// <param name="type">类型</param>
        /// <param name="parent">挂点</param>
        /// <returns></returns>
        public EntityHandle<T> CreateEntity<T>(string assetPath,EntityType type,Transform parent = null) where T : EntityBase
        {
            
            EntityHandle<T> handle = new EntityHandle<T>();
            Global.gApp.gPoolMgr.Spawn<T>(assetPath).SetCallback((entityBase) =>
            {
                long sInstanceID = entityBase.GetInstanceID();
                // 实体节点
                entityBase.transform.SetParent(parent ?? m_EntityRoot, false);
                entityBase.SetEntityId(sInstanceID);
                entityBase.SetEntityType(type);
                m_EntityDic.Add(sInstanceID,entityBase);
                m_EntityList.Add(entityBase);
                handle.Complete(entityBase);
            });

            return handle;
        }
        
        public void Dispose(EntityBase entity)
        {
            if (entity == null) return;
            if (m_EntityList.Contains(entity))
            {
                Global.gApp.gPoolMgr.Despawn(entity.gameObject);
            }
        }

        public void OnDespawn(EntityBase entity)
        {
            if (entity == null) return;
            if (m_EntityList.Contains(entity))
            {
                m_EntityList.Remove(entity);
                m_EntityDic.Remove(entity.m_EntityId);
            }
        }
        
        public void OnDestroy()
        {
            var list = new List<EntityBase>(m_EntityList);
            foreach (var h in list) Dispose(h);
            m_EntityList.Clear();
            m_EntityDic.Clear();
            if (m_EntityRoot != null) Global.gApp.gResMgr.Destroy(m_EntityRoot.gameObject);
        }

        public void OnIUpdate(float dt)
        {
            foreach (EntityBase val in m_EntityList)
            {
                val.OnIUpdate(dt);
            }
        }
    }
}
