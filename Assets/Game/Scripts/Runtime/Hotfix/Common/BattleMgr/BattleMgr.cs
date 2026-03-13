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
        private bool m_IsStarted = false;
        private Quadtree<EntityBase> m_Quadtree;
        private List<EntityBase> m_QueryResult = new List<EntityBase>();

        // 测试用
        public float m_RoundTimer;
        public float m_RoundStep { get;private set; } = 60f;
        public int m_CurRound { get; private set; } = 1;
        public int m_MaxRound { get; private set; } = 5;
        
        // 配置参数 (可以根据手感调整)
        private const float SEPARATION_RADIUS = 2.0f;  // 排斥感知的距离
        private const float SEPARATION_WEIGHT = 5.0f;  // 排斥力的权重
        private const float ATTACK_INTERVAL = 0.5f;    // 自动攻击频率
        private const float ATTACK_RANGE = 3.0f;       // 攻击范围

        private AABB m_BattleBoundary = new AABB(Vector2.zero, new Vector2(50f, 50f));
        private float m_AttackTimer = 0f;

        public BattleMgr()
        {
            m_WorldCamera = Global.gApp.gWorldCamera;
            
        }
        
        public void OnInit()
        {
            Debug.Log("[BattleMgr] Initialized");
            m_Quadtree = new Quadtree<EntityBase>(0, m_BattleBoundary);
        }

        public void OnStartBattle()
        {
            m_IsStarted = true;
            Debug.Log("<color=cyan>[BattleMgr] Battle Started!</color>");
        }

        public void OnStopBattle()
        {
            m_IsStarted = false;
        }

        public void OnIUpdate(float dt)
        {
            if (!m_IsStarted) return;
            // 1. 重建四叉树
            m_Quadtree.Clear();
            foreach (var entity in Global.gApp.gEntityMgr.m_EntityList)
            {
                m_Quadtree.Insert(entity);
            }
            m_RoundTimer += dt;
            if (m_RoundTimer >= m_RoundStep)
            {
                m_RoundTimer = 0;
            }

            // 2. 英雄移动
            UpdateHeroMovement(dt);

            // 3. 自动攻击
            UpdateHeroAttack(dt);

            // 4. 核心：加固版集群移动
            UpdateMonsterLogic(dt);
        }

        private void UpdateHeroMovement(float dt)
        {
            // var hero = Global.gApp.gEntityMgr.gHero;
            // if (hero == null) return;
            //
            // Vector2 input = InputSystem.GetMoveInput();
            // if (input.sqrMagnitude > 0.01f)
            // {
            //     hero.Position += input * hero.MoveSpeed * dt;
            // }
        }

        private void UpdateHeroAttack(float dt)
        {
            // var hero = Global.gApp.gEntityMgr.gHero;
            // if (hero == null) return;
            //
            // m_AttackTimer += dt;
            // if (m_AttackTimer >= ATTACK_INTERVAL)
            // {
            //     m_AttackTimer = 0;
            //     m_QueryResult.Clear();
            //     m_Quadtree.Query(new AABB(hero.Position, new Vector2(ATTACK_RANGE, ATTACK_RANGE)), m_QueryResult);
            //
            //     foreach (var enemy in m_QueryResult)
            //     {
            //         if (enemy.m_Type == EntityDefine.EntityType.Monster && Vector2.Distance(hero.Position, enemy.Position) <= ATTACK_RANGE)
            //         {
            //             enemy.HP -= 20f;
            //         }
            //     }
            // }
        }

        private void UpdateMonsterLogic(float dt)
        {
            // var hero = Global.gApp.gEntityMgr.gHero;
            // if (hero == null) return;
            //
            // var entities = Global.gApp.gEntityMgr.GetActiveEntities();
            // foreach (var entity in entities)
            // {
            //     if (entity.Type != EntityDefine.EntityType.Monster) continue;
            //
            //     // A. 追踪力：朝向英雄
            //     Vector2 chaseDir = (hero.Position - entity.Position).normalized;
            //     
            //     // B. 分离力：逃离邻居
            //     Vector2 separationForce = Vector2.zero;
            //     m_QueryResult.Clear();
            //     // 查询半径设为 SEPARATION_RADIUS
            //     m_Quadtree.Query(new AABB(entity.Position, new Vector2(SEPARATION_RADIUS, SEPARATION_RADIUS)), m_QueryResult);
            //     
            //     foreach (var neighbor in m_QueryResult)
            //     {
            //         if (neighbor == entity) continue;
            //
            //         Vector2 diff = entity.Position - neighbor.Position;
            //         float dist = diff.magnitude;
            //         
            //         if (dist < SEPARATION_RADIUS && dist > 0.001f)
            //         {
            //             // 离得越近，推力越强 (1/dist)
            //             separationForce += diff.normalized * (SEPARATION_RADIUS / dist);
            //         }
            //     }
            //
            //     // C. 混合力：追踪力 + 分离力 (不再强行归一化整体，保留推开的强度)
            //     Vector2 combinedDir = (chaseDir + separationForce * SEPARATION_WEIGHT).normalized;
            //     
            //     // D. 硬性穿透修正：防止死堆叠 (类似 AABB 碰撞反馈)
            //     if (separationForce.sqrMagnitude > 0.1f)
            //     {
            //         // 如果推力很大，说明重叠严重，给一个微小的瞬间位移修正
            //         entity.Position += separationForce * 0.01f;
            //     }
            //
            //     entity.Position += combinedDir * entity.MoveSpeed * dt;
            // }
        }

        public void OnDestroy()
        {
            OnStopBattle();
            m_Quadtree = null;
        }
    }
}
