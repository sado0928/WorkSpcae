using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// 战斗管理器 (驱动层)
    /// 负责驱动所有战斗子系统，包括输入、AI 集群、碰撞。
    /// </summary>
    public class BattleMgr : IUpdate
    {
        public Camera m_WorldCamera { get; private set; }
        public Camera_Ctrl m_CameraCtrl { get;private set; }
        public Player_Ctrl m_PlayerCtrl { get;private set; }
    
        // 四叉树
        private Quadtree<EntityBase> m_Quadtree;
        // 查询的数据
        private List<EntityBase> m_QueryResult = new List<EntityBase>();
        private Dictionary<long, EntityBase> m_BattleEntityDic = new Dictionary<long, EntityBase>();
        
        private bool m_IsStarted = false;
        
        // 测试用
        public float m_RoundTimer;
        public float m_RoundStep { get;private set; } = 10f;
        
        // 配置参数 (可以根据手感调整)
        private const float SEPARATION_RADIUS = 2.0f;  // 排斥感知的距离
        private const float SEPARATION_WEIGHT = 5.0f;  // 排斥力的权重
        private const float ATTACK_INTERVAL = 0.5f;    // 自动攻击频率
        private const float ATTACK_RANGE = 3.0f;       // 攻击范围

        private float m_AttackTimer = 0f;

        public BattleMgr()
        {
            m_WorldCamera = Global.gApp.gWorldCamera;
            var cameraAABBAdapter = m_WorldCamera.GetComponent<Camera_Ctrl>();
            if (cameraAABBAdapter == null) m_CameraCtrl = m_WorldCamera.gameObject.AddComponent<Camera_Ctrl>();
        }
        
        public void OnStartBattle()
        {
           
            m_Quadtree = new Quadtree<EntityBase>(0, m_CameraCtrl.m_ViewportAABB);
            Global.gApp.gEntityMgr.CreateEntity<EntityHero>("Prefabs/Hero/1001/Hero1001",EntityType.Hero).SetCallback((entity) =>
            {
                entity.SetData(Tbhero.Data.Get(1001));
                var playerCtrl = entity.gameObject.GetComponent<Player_Ctrl>();
                if (playerCtrl == null) m_PlayerCtrl = entity.gameObject.AddComponent<Player_Ctrl>();
                m_BattleEntityDic.Add(entity.m_EntityId,entity);
                m_IsStarted = true;
            });
          
            Debug.Log("<color=cyan>[BattleMgr] Battle Started!</color>");
        }

        public void OnStopBattle()
        {
            m_IsStarted = false;
            m_Quadtree.Clear();
            foreach (KeyValuePair<long, EntityBase> pair in m_BattleEntityDic)
            {
                Global.gApp.gEntityMgr.Dispose(pair.Value);
            }
            m_BattleEntityDic.Clear();
        }

        public void OnIUpdate(float dt)
        {
            if (!m_IsStarted) return;
            // 1. 重建四叉树
            m_Quadtree.Clear();
            Global.gApp.gEntityMgr.m_EntityList.Foreach(entity =>
            {
                m_Quadtree.Insert(entity);
            });
            
            m_RoundTimer += dt;
            if (m_RoundTimer >= m_RoundStep)
            {
                m_RoundTimer = 0;
                CreateMonster();
            }

            m_PlayerCtrl.OnIUpdate(dt);
            // 自动攻击
            UpdateHeroAttack(dt);

            // 加固版集群移动
            UpdateMonsterLogic(dt);
        }

        private void CreateMonster()
        {
            //测试：生成一批怪物 (暂时使用英雄模型作为占位)
            for (int i = 0; i < 10; i++)
            {
                var aabb = m_CameraCtrl.m_ViewportAABB;
                float randX = Random.Range(
                    aabb.Center.x - aabb.HalfSize.x,
                    aabb.Center.x + aabb.HalfSize.x
                );

                float randY = Random.Range(
                    aabb.Center.y - aabb.HalfSize.y,
                    aabb.Center.y + aabb.HalfSize.y
                );
                Vector2 randomPos = new Vector2(randX, randY);
                Global.gApp.gEntityMgr.CreateEntity<EntityMonster>("Prefabs/Monster/1001/Monster1001", EntityType.Monster).SetCallback(entity =>
                {
                    entity.SetData(Tbmonster.Data.Get(1001));
                    entity.OnSetPosition(randomPos);
                    m_BattleEntityDic.Add(entity.m_EntityId,entity);
                });
            }
        }
        
        private void UpdateHeroAttack(float dt)
        {
            var hero = m_PlayerCtrl.m_Player;
            if (hero == null) return;
            
            m_AttackTimer += dt;
            if (m_AttackTimer >= ATTACK_INTERVAL)
            {
                m_AttackTimer = 0;
                m_QueryResult.Clear();
                m_Quadtree.Query(new AABB(hero.m_Position, new Vector2(ATTACK_RANGE, ATTACK_RANGE)), m_QueryResult);
            
                foreach (var enemy in m_QueryResult)
                {
                    if (enemy is EntityMonster entityMonster)
                    {
                        if (entityMonster.m_Type == EntityType.Monster && Vector2.Distance(hero.m_Position, entityMonster.m_Position) <= ATTACK_RANGE)
                        {
                            entityMonster.Hp -= 20f;
                        }
                    }
                }
            }
        }

        private void UpdateMonsterLogic(float dt)
        {
            var hero = m_PlayerCtrl.m_Player;
            if (hero == null) return;
            
            var entities = Global.gApp.gEntityMgr.m_EntityList;
            entities.Foreach(entity =>
            {
                if (entity is EntityMonster monsterEntity)
                {
                    // 1. 获取动态半径 (基于 AABB)
                    float myRadius = Mathf.Max(monsterEntity.m_Bounds.HalfSize.x, monsterEntity.m_Bounds.HalfSize.y);
                    if (myRadius <= 0) myRadius = 0.5f; 
                    
                    float perceptionRadius = myRadius * 2.5f; 

                    // A. 追踪力：朝向英雄
                    Vector2 chaseDir = (hero.m_Position - monsterEntity.m_Position).normalized;
                
                    // B. 分离力：逃离邻居
                    Vector2 separationForce = Vector2.zero;
                    m_QueryResult.Clear();
                    // 查询半径设为 SEPARATION_RADIUS
                    m_Quadtree.Query(new AABB(monsterEntity.m_Position, new Vector2(perceptionRadius, perceptionRadius)), m_QueryResult);
                
                    foreach (var neighbor in m_QueryResult)
                    {
                        if (neighbor == monsterEntity) continue;
            
                        Vector2 diff = monsterEntity.m_Position - neighbor.m_Position;
                        float dist = diff.magnitude;
                    
                        if (dist < perceptionRadius && dist > 0.001f)
                        {
                            // 离得越近，推力越强 (1/dist)
                            separationForce += diff.normalized * (perceptionRadius / dist);
                        }
                    }
            
                    // C. 混合力：追踪力 + 分离力 (不再强行归一化整体，保留推开的强度)
                    Vector2 combinedDir = (chaseDir + separationForce * SEPARATION_WEIGHT).normalized;
                
                    // D. 硬性穿透修正：防止死堆叠 (类似 AABB 碰撞反馈)
                    if (separationForce.sqrMagnitude > 0.1f)
                    {
                        // 如果推力很大，说明重叠严重，给一个微小的瞬间位移修正
                        monsterEntity.m_Position += separationForce * 0.01f;
                    }
                    monsterEntity.m_Position += combinedDir * monsterEntity.Speed * dt;
                }
            });
        }

        public void OnDestroy()
        {
            m_IsStarted = false;
            m_Quadtree = null;
            foreach (KeyValuePair<long, EntityBase> pair in m_BattleEntityDic)
            {
                Global.gApp.gEntityMgr.Dispose(pair.Value);
            }
            m_BattleEntityDic.Clear();
        }
    }
}
