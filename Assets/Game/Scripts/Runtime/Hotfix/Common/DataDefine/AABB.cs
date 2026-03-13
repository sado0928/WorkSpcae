using UnityEngine;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// 轴对齐包围盒 (Axis-Aligned Bounding Box)
    /// 2D 割草游戏高性能碰撞检测的核心数据结构。
    /// </summary>
    public struct AABB
    {
        public Vector2 Center;
        public Vector2 HalfSize;

        public AABB(Vector2 center, Vector2 halfSize)
        {
            Center = center;
            HalfSize = halfSize;
        }

        public void Update(Vector2 center, Vector2 halfSize)
        {
            Center = center;
            HalfSize = halfSize;
        }

        /// <summary>
        /// 检测两个 AABB 是否重叠
        /// </summary>
        public bool Overlaps(AABB other)
        {
            if (Mathf.Abs(Center.x - other.Center.x) > (HalfSize.x + other.HalfSize.x)) return false;
            if (Mathf.Abs(Center.y - other.Center.y) > (HalfSize.y + other.HalfSize.y)) return false;
            return true;
        }

        /// <summary>
        /// 检测点是否在 AABB 内
        /// </summary>
        public bool Contains(Vector2 point)
        {
            return point.x >= Center.x - HalfSize.x &&
                   point.x <= Center.x + HalfSize.x &&
                   point.y >= Center.y - HalfSize.y &&
                   point.y <= Center.y + HalfSize.y;
        }
    }
}
