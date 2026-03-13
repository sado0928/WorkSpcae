using UnityEditor;
using UnityEngine;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// 心跳接口
    /// </summary>
    public interface IUpdate
    {
        void OnIUpdate(float dt);
    }

    /// <summary>
    ///  配置表接口 里氏替换用
    /// </summary>
    public interface ICfgAble
    {
    }

    
    /// <summary>
    /// 可被放入四叉树的物体接口
    /// </summary>
    public interface IQuadtreeItem
    {
        // 坐标
        Vector2 Position { get; }
        // 包围盒
        AABB Bounds { get; }
    }
}
