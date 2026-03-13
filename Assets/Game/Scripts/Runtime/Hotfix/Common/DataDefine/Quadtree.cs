using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Hotfix
{
    public class Quadtree<T> where T : IQuadtreeItem
    {
        private const int MAX_ITEMS = 8;
        private const int MAX_LEVELS = 5;

        private int m_Level;
        private AABB m_Boundary;
        private List<T> m_Items;
        private Quadtree<T>[] m_Nodes;
        private bool m_IsSplit = false;

        public Quadtree(int level, AABB boundary)
        {
            m_Level = level;
            m_Boundary = boundary;
            m_Items = new List<T>(MAX_ITEMS + 1); // 预分配容量
        }

        /// <summary>
        /// 零 GC 清空：只重置状态，不销毁对象
        /// </summary>
        public void Clear()
        {
            m_Items.Clear();
            m_IsSplit = false;
            
            if (m_Nodes != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    m_Nodes[i].Clear();
                }
            }
        }

        private void Subdivide()
        {
            if (m_Nodes == null)
            {
                // 只有第一次分裂时才创建节点对象，后续永远重用
                float subWidth = m_Boundary.HalfSize.x / 2f;
                float subHeight = m_Boundary.HalfSize.y / 2f;
                Vector2 halfSize = new Vector2(subWidth, subHeight);

                m_Nodes = new Quadtree<T>[4];
                m_Nodes[0] = new Quadtree<T>(m_Level + 1, new AABB(new Vector2(m_Boundary.Center.x + subWidth, m_Boundary.Center.y + subHeight), halfSize));
                m_Nodes[1] = new Quadtree<T>(m_Level + 1, new AABB(new Vector2(m_Boundary.Center.x - subWidth, m_Boundary.Center.y + subHeight), halfSize));
                m_Nodes[2] = new Quadtree<T>(m_Level + 1, new AABB(new Vector2(m_Boundary.Center.x - subWidth, m_Boundary.Center.y - subHeight), halfSize));
                m_Nodes[3] = new Quadtree<T>(m_Level + 1, new AABB(new Vector2(m_Boundary.Center.x + subWidth, m_Boundary.Center.y - subHeight), halfSize));
            }
            m_IsSplit = true;
        }

        public bool Insert(T item)
        {
            if (!m_Boundary.Contains(item.Position)) return false;

            // 如果还没分裂且没满，或者达到最大深度
            if (!m_IsSplit && (m_Items.Count < MAX_ITEMS || m_Level >= MAX_LEVELS))
            {
                m_Items.Add(item);
                return true;
            }

            // 否则分裂并向下传递
            if (!m_IsSplit)
            {
                Subdivide();
                // 将当前已有的物体重新分发给子节点 (这一步是必要的)
                for (int i = m_Items.Count - 1; i >= 0; i--)
                {
                    T existingItem = m_Items[i];
                    foreach (var node in m_Nodes)
                    {
                        if (node.Insert(existingItem)) break;
                    }
                }
                m_Items.Clear();
            }

            foreach (var node in m_Nodes)
            {
                if (node.Insert(item)) return true;
            }

            return false;
        }

        public void Query(AABB range, List<T> results)
        {
            if (!m_Boundary.Overlaps(range)) return;

            // 检查当前节点的物体
            for (int i = 0; i < m_Items.Count; i++)
            {
                if (range.Contains(m_Items[i].Position))
                {
                    results.Add(m_Items[i]);
                }
            }

            // 如果已分裂，检查子节点
            if (m_IsSplit && m_Nodes != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    m_Nodes[i].Query(range, results);
                }
            }
        }
    }
}
