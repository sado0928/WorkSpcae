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
    
    
    /// <summary>
    /// 轴对齐包围盒 (Axis-Aligned Bounding Box)
    /// 3D 游戏高性能碰撞检测的核心数据结构（兼容2D逻辑，新增Z轴支持）。
    /// </summary>
    public struct AABB3D
    {
        // 3D 中心（替代原2D Center）
        public Vector3 Center;
        // 3D 半尺寸（X/Y/Z 三轴，替代原2D HalfSize）
        public Vector3 HalfSize;

        /// <summary>
        /// 3D AABB 构造函数（核心）
        /// </summary>
        /// <param name="center">3D 包围盒中心（X/Y/Z）</param>
        /// <param name="halfSize">3D 半尺寸（X/Y/Z，即长宽高的一半）</param>
        public AABB3D(Vector3 center, Vector3 halfSize)
        {
            Center = center;
            HalfSize = halfSize;
        }

        /// <summary>
        /// 兼容2D的构造函数（自动填充Z轴为0，无缝迁移原有2D代码）
        /// </summary>
        public AABB3D(Vector2 center, Vector2 halfSize)
        {
            Center = new Vector3(center.x, center.y, 0);
            HalfSize = new Vector3(halfSize.x, halfSize.y, 0);
        }

        /// <summary>
        /// 更新3D AABB的中心和半尺寸
        /// </summary>
        public void Update(Vector3 center, Vector3 halfSize)
        {
            Center = center;
            HalfSize = halfSize;
        }

        /// <summary>
        /// 兼容2D的Update（自动填充Z轴为0）
        /// </summary>
        public void Update(Vector2 center, Vector2 halfSize)
        {
            Center = new Vector3(center.x, center.y, 0);
            HalfSize = new Vector3(halfSize.x, halfSize.y, 0);
        }

        /// <summary>
        /// 检测两个 3D AABB 是否重叠（核心：新增Z轴判断）
        /// 逻辑：三轴都满足“中心间距 ≤ 半尺寸之和”才判定重叠
        /// </summary>
        public bool Overlaps(AABB3D other)
        {
            // X轴检测（原有逻辑不变）
            if (Mathf.Abs(Center.x - other.Center.x) > (HalfSize.x + other.HalfSize.x)) return false;
            // Y轴检测（原有逻辑不变）
            if (Mathf.Abs(Center.y - other.Center.y) > (HalfSize.y + other.HalfSize.y)) return false;
            // Z轴检测（新增核心逻辑，和X/Y轴对称）
            if (Mathf.Abs(Center.z - other.Center.z) > (HalfSize.z + other.HalfSize.z)) return false;
            return true;
        }

        /// <summary>
        /// 检测3D点是否在 AABB 内（新增Z轴判断）
        /// </summary>
        public bool Contains(Vector3 point)
        {
            return point.x >= Center.x - HalfSize.x &&
                   point.x <= Center.x + HalfSize.x &&
                   point.y >= Center.y - HalfSize.y &&
                   point.y <= Center.y + HalfSize.y &&
                   // 新增Z轴范围判断
                   point.z >= Center.z - HalfSize.z &&
                   point.z <= Center.z + HalfSize.z;
        }

        /// <summary>
        /// 兼容2D的点检测（自动填充Z轴为0，原有2D代码无需修改）
        /// </summary>
        public bool Contains(Vector2 point)
        {
            return Contains(new Vector3(point.x, point.y, 0));
        }

        // 【可选扩展】快速从Unity的Bounds转换为3D AABB（便捷对接Unity内置组件）
        public static AABB3D FromBounds(Bounds bounds)
        {
            return new AABB3D(bounds.center, bounds.extents);
        }

        // 【可选扩展】转换为Unity的Bounds（方便对接Unity物理系统）
        public Bounds ToBounds()
        {
            return new Bounds(Center, HalfSize * 2);
        }
    }
}