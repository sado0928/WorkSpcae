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
        public Transform m_EntityRoot { get; private set; }
        public Dictionary<long, EntityBase> m_EntityDic { get; private set; } = new Dictionary<long, EntityBase>();
        public List<EntityBase> m_EntityList { get; private set; } = new List<EntityBase>();
        
        private List<EntityHandle> m_ActiveHandles = new List<EntityHandle>();
        
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
        /// <param name="pos">位置</param>
        /// <param name="parent">挂点</param>
        /// <returns></returns>
        public EntityHandle CreateEntity(string assetPath,EntityType type,Transform parent = null)
        {
            // 套壳
            GameObject go = new GameObject();
            long sInstanceID = go.GetInstanceID();
            go.name = System.IO.Path.GetFileName(assetPath) + "_" + sInstanceID;
            
            EntityHandle handle = new EntityHandle(assetPath);
            m_ActiveHandles.Add(handle);

            Global.gApp.gPoolMgr.Spawn<EntityBase>(assetPath).SetCallback((entityBase) =>
            {
                if (!m_ActiveHandles.Contains(handle))
                {
                    Global.gApp.gPoolMgr.Despawn(entityBase.gameObject);
                    return;
                }
                
                // 壳子节点
                go.transform.SetParent(parent ?? m_EntityRoot, false);
                // 实体节点
                entityBase.transform.SetParent(go.transform);
                
                entityBase.SetParent(go.transform);
                entityBase.SetEntityId(sInstanceID);
                entityBase.SetEntityType(type);
                entityBase.SetHandle(handle);
                m_EntityDic.Add(sInstanceID,entityBase);
                m_EntityList.Add(entityBase);
                handle.Complete(entityBase);
            });

            return handle;
        }
        
        public void Dispose(EntityHandle handle)
        {
            if (handle == null) return;
            if (m_ActiveHandles.Contains(handle))
            {
                if (handle.IsLoaded)
                {
                    Global.gApp.gPoolMgr.Despawn(handle.m_GameObject);
                }
            }
        }

        public void OnDespawn(EntityHandle handle)
        {
            if (handle == null) return;
            if (m_ActiveHandles.Contains(handle))
            {
                m_ActiveHandles.Remove(handle);
                m_EntityList.Remove(handle.m_Base);
                m_EntityDic.Remove(handle.m_Base.m_EntityId);
                Global.gApp.gResMgr.Destroy(handle.m_Base.m_Parent?.gameObject);
            }
        }
        
        public void OnDestroy()
        {
            var list = new List<EntityHandle>(m_ActiveHandles);
            foreach (var h in list) Dispose(h);
            m_ActiveHandles.Clear();
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
