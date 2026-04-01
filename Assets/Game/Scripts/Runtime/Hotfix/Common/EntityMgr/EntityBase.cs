using UnityEngine;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// 实体基类 (Common) 
    /// </summary>
    public class EntityBase :PoolBase,IQuadtreeItem,IUpdate
    {
        // 实体id
        public long m_EntityId { get;private set; }
        // 实体类型
        public EntityType m_Type { get; private set; } 
        public BoxCollider2D m_Box2d { get;private set; }
        public Vector2 m_Position { get; set; }
        public AABB m_Bounds { get; private set; }
        
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
            m_Position = gameObject.transform.position;
            // AABB 使用 HalfSize (半径)
            m_Bounds = new AABB(m_Position, m_Box2d.size * 0.5f);
        }

        protected override void OnDespawn()
        {
            Global.gApp.gEntityMgr.OnDespawn(this);
        }
        
        public void SetEntityId(long id)
        {
            m_EntityId = id;
        }

        public void SetEntityType(EntityType type)
        {
            m_Type = type;
        }

        public void OnSetPosition(Vector3 pos)
        {
            m_Position = pos;
        }

        public void OnSetRotation(Vector3 eulerAngle)
        {
            transform.localRotation = Quaternion.Euler(eulerAngle);
        }

        public void OnSetScale(Vector3 scale)
        {
            transform.localScale = scale;
        }

        public void OnIUpdate(float dt)
        {
            if (gameObject.transform != null)
            {
                gameObject.transform.localPosition = m_Position;
                // 同步 AABB 中心点
                m_Bounds.Update(m_Position, m_Bounds.HalfSize);
            }
        }
    }
}
