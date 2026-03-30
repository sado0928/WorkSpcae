using UnityEngine;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// 实体基类 (Common) 
    /// </summary>
    public class EntityBase :PoolBase,IQuadtreeItem,IUpdate
    {
        // 壳子（Transform相关的应该使用local API
        public Transform m_Parent { get;private set; }
        // 实体id
        public long m_EntityId { get;private set; }
        // 实体类型
        public EntityType m_Type { get; private set; } 
        public EntityHandle m_EntityHandle { get;private set; }
        public BoxCollider2D m_Box2d { get;private set; }
        public Vector2 Position { get; set; }
        public AABB Bounds { get; private set; }
        
        protected override void OnInit()
        {
            // BoxCollider2D 读取编辑器配置
            m_Box2d = gameObject.GetComponentInChildren<BoxCollider2D>();
            if (m_Box2d == null) m_Box2d = gameObject.AddComponent<BoxCollider2D>();
            // 强制禁用物理，仅作为数据容器
            m_Box2d.enabled = false;
        }
        
        protected override void OnSpawn()
        {
            Position = gameObject.transform.position;
            // AABB 使用 HalfSize (半径)
            Bounds = new AABB(Position, m_Box2d.size * 0.5f);
        }

        protected override void OnDespawn()
        {
            Global.gApp.gEntityMgr.OnDespawn(m_EntityHandle);
        }
        
        public void SetParent(Transform go)
        {
            m_Parent = go;
        }

        public void SetEntityId(long id)
        {
            m_EntityId = id;
        }

        public void SetEntityType(EntityType type)
        {
            m_Type = type;
        }

        public void SetHandle(EntityHandle handle)
        {
            m_EntityHandle = handle;
        }
        
        public void OnIUpdate(float dt)
        {
            if (gameObject.transform != null)
            {
                gameObject.transform.position = Position;
                // 同步 AABB 中心点
                Bounds.Update(Position, Bounds.HalfSize);
            }
        }
    }
}
