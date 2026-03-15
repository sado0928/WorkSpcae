using System.Collections.Generic;
using UnityEngine;
using Game.Runtime.Hotfix;
using UnityEngine.Serialization;


// 2D 角色控制
public class Player_Ctrl : MonoBehaviour,IUpdate
{
    
    [Header("转向设置")]
    [Tooltip("转向模式：0=朝向鼠标，1=朝移动方向")]
    public int rotateMode = 0;
    [Tooltip("转向平滑系数（0-1，值越小越丝滑）")]
    [Range(0.01f, 1f)] public float rotateSmooth = 1f;

    [Header("边界限制")]
    [Tooltip("是否限制角色在摄像机视口内")]
    public bool limitInViewport = true;
    [Tooltip("视口内偏移（避免角色贴边）")]
    public float viewportOffset = 0.5f;
    
    private Vector2 m_MoveInput;       // 输入的移动方向
    
    public Camera_Ctrl m_CameraCtrl; // 摄像机
    public SpriteRenderer m_SpriteRender; // Render
    public EntityHero m_Player { get;private set; } // 角色

    public AABB m_PlayerAABB { get; set; }
    
    #region 初始化
    private void Awake()
    {
        m_PlayerAABB = m_Player.Bounds;
        
        // 自动获取摄像机AABB组件（如果未手动赋值）
        if (limitInViewport && m_CameraCtrl == null)
        {
            m_CameraCtrl = Camera.main.GetComponent<Camera_Ctrl>();
        }
    }
    #endregion

    #region 帧更新逻辑
    public void OnIUpdate(float dt)
    {
        // 1. 检测输入（键盘/手柄）
        GetMoveInput();
        // 2. 转向逻辑（Update中执行，保证响应及时）
        UpdateRotation();
        // 3. 移动逻辑
        UpdatePosition(dt);
        // 4. 边界限制（限制角色在摄像机视口内）
        if (limitInViewport)
        {
            LimitInViewport();
        }
    }
    

    private void UpdatePosition(float dt)
    {
        if (m_MoveInput.sqrMagnitude > 0.01f)
        {
            m_Player.Position += m_MoveInput * m_Player.Speed * dt;
        }
    }
    #endregion

    #region 核心功能：输入检测
    /// <summary>
    /// 获取移动输入（适配键盘WASD/方向键，手柄左摇杆）
    /// </summary>
    private void GetMoveInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal"); // 水平输入（-1,0,1）
        float vertical = Input.GetAxisRaw("Vertical");     // 垂直输入（-1,0,1）

        // 归一化方向（避免斜向移动速度更快）
        m_MoveInput = new Vector2(horizontal, vertical).normalized;
    }
    #endregion
    
    #region 核心功能：转向逻辑
    /// <summary>
    /// 更新角色转向（朝向鼠标/移动方向）
    /// </summary>
    private void UpdateRotation()
    {
        if (rotateMode == 0)
        {
            // 模式1：朝向鼠标位置（吸血鬼幸存者原版）
            LookAtMouse();
        }
        else
        {
            // 模式2：朝向移动方向（简化版）
            LookAtMoveDirection();
        }
    }

    /// <summary>
    /// 朝向鼠标位置
    /// </summary>
    private void LookAtMouse()
    {
        // 1. 获取鼠标世界坐标（将屏幕坐标转为世界坐标）
        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        // 2. 计算角色到鼠标的方向
        Vector2 direction = mouseWorldPos - (Vector2)transform.position;
        if (direction.sqrMagnitude < 0.01f) return; // 鼠标在角色中心时不转向

        // 3. 计算目标角度（弧度转角度）
        // float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        // Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);
        //
        // // 4. 平滑转向
        // transform.rotation = Quaternion.Lerp(
        //     transform.rotation, 
        //     targetRotation, 
        //     rotateSmooth / Time.deltaTime
        // );

        // 翻转Sprite（如果角色图只有一个朝向）
        FlipSprite(direction.x);
    }

    /// <summary>
    /// 朝向移动方向
    /// </summary>
    private void LookAtMoveDirection()
    {
        if (m_MoveInput.sqrMagnitude < 0.01f) return; // 无移动时不转向

        // 1. 计算移动方向的目标角度
        float targetAngle = Mathf.Atan2(m_MoveInput.y, m_MoveInput.x) * Mathf.Rad2Deg - 90f;
        Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);

        // 2. 平滑转向
        transform.rotation = Quaternion.Lerp(
            transform.rotation, 
            targetRotation, 
            rotateSmooth / Time.deltaTime
        );
        
    }

    /// <summary>
    /// 翻转Sprite（可选：如果角色图只有右向，需翻转左向）
    /// </summary>
    /// <param name="directionX">水平方向（-1左，1右）</param>
    private void FlipSprite(float directionX)
    {
        if (m_SpriteRender == null) return;

        // 翻转X轴（保留Y轴缩放，避免角色倒过来）
        if (directionX < 0 && !m_SpriteRender.flipX)
        {
            m_SpriteRender.flipX = true;
        }
        else if (directionX > 0 && m_SpriteRender.flipX)
        {
            m_SpriteRender.flipX = false;
        }
    }
    #endregion

    #region 核心功能：边界限制（基于摄像机AABB）
    /// <summary>
    /// 限制角色在摄像机视口内
    /// </summary>
    private void LimitInViewport()
    {
        AABB viewportAABB = m_CameraCtrl.m_ViewportAABB;
        Vector2 currentPos = transform.position;

        // 计算视口内的有效范围（加偏移，避免角色贴边）
        float minX = viewportAABB.Center.x - viewportAABB.HalfSize.x + viewportOffset + m_PlayerAABB.HalfSize.x;
        float maxX = viewportAABB.Center.x + viewportAABB.HalfSize.x - viewportOffset - m_PlayerAABB.HalfSize.x;
        float minY = viewportAABB.Center.y - viewportAABB.HalfSize.y + viewportOffset + m_PlayerAABB.HalfSize.y;
        float maxY = viewportAABB.Center.y + viewportAABB.HalfSize.y - viewportOffset - m_PlayerAABB.HalfSize.y;

        // 限制角色位置在有效范围内
        currentPos.x = Mathf.Clamp(currentPos.x, minX, maxX);
        currentPos.y = Mathf.Clamp(currentPos.y, minY, maxY);
        
    }
    #endregion


}