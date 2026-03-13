using UnityEngine;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// 实体基类 (Common)
    /// </summary>
    public abstract class EntityBase :PoolBase,IQuadtreeItem,IUpdate
    {
        // 实体id
        public int m_EntityId { get;private set; }
        // 实体类型
        public EntityType m_Type { get; private set; }
        public BoxCollider2D m_Box2d { get; set; }
        // 位置
        public Vector2 Position { get; set; }
        // 包围盒
        public AABB Bounds { get; protected set; }

        public EntityHandle m_EntityHandle { get;private set; }

        protected override void OnInit()
        {
            // BoxCollider2D 读取编辑器配置
            m_Box2d = gameObject.GetComponentInChildren<BoxCollider2D>();
            if (m_Box2d == null) m_Box2d = gameObject.AddComponent<BoxCollider2D>();

            // 强制禁用物理，仅作为数据容器
            m_Box2d.enabled = false;
        }

        public void SetEntityId(int id)
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
        
        protected override void OnSpawn()
        {
            Position = gameObject.transform.position;
            // AABB 使用 HalfSize (半径)
            Bounds = new AABB(Position, m_Box2d.size * 0.5f);
        }

        protected override void OnDespawn()
        {
            m_EntityHandle.Dispose();
        }
        
        public void OnIUpdate(float dt)
        {
            if (gameObject.transform != null)
            {
                Position = gameObject.transform.position;
                // 同步 AABB 中心点
                Bounds.Update(Position, Bounds.HalfSize);
            }
        }
    }
}
