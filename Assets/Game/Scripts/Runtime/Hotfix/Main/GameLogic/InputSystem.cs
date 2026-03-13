using UnityEngine;

namespace Game.Runtime.Hotfix
{
    /// <summary>
    /// 简单的输入系统 (GameLogic)
    /// 提供基础的 2D 移动向量。
    /// </summary>
    public static class InputSystem
    {
        public static Vector2 GetMoveInput()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            return new Vector2(horizontal, vertical).normalized;
        }
    }
}
