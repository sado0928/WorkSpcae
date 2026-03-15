using UnityEngine;
using Game.Runtime.Hotfix;

/// <summary>
/// 世界摄像机的 AABB 适配工具（挂载到世界摄像机）
/// 自动同步摄像机位置、缩放，生成视口范围的 AABB
/// </summary>
[RequireComponent(typeof(Camera))]
public class Camera_Ctrl : MonoBehaviour
{
    private Camera m_WorldCamera;
    public AABB m_ViewportAABB { get; private set; }// 摄像机视口的 AABB 包围盒
    private float m_lastOrthographicSize; // 记录上一帧的缩放值，用于检测变化
    private Vector2 m_LastCameraPos; // 记录上一帧的摄像机位置

    // 对外提供视口 AABB（只读）
    private void Awake()
    {
        m_WorldCamera = GetComponent<Camera>();
        if (!m_WorldCamera.orthographic)
        {
            Debug.LogError("世界摄像机必须设置为正交模式（Orthographic）！");
            enabled = false;
            return;
        }

        // 初始化视口 AABB
        UpdateViewportAABB();
        m_lastOrthographicSize = m_WorldCamera.orthographicSize;
        m_LastCameraPos = transform.position;
    }

    private void LateUpdate()
    {
        // 检测摄像机位置或缩放是否变化，变化则更新 AABB
        bool isPosChanged = m_LastCameraPos != (Vector2)transform.position;
        bool isScaleChanged = m_lastOrthographicSize != m_WorldCamera.orthographicSize;

        if (isPosChanged || isScaleChanged)
        {
            UpdateViewportAABB();
            m_LastCameraPos = transform.position;
            m_lastOrthographicSize = m_WorldCamera.orthographicSize;
        }
    }

    /// <summary>
    /// 核心方法：根据摄像机当前状态（位置、缩放）更新视口 AABB
    /// </summary>
    private void UpdateViewportAABB()
    {
        // 步骤1：计算摄像机视口的实际范围（世界单位）
        // 正交摄像机的 Size = 视口高度的一半，缩放时该值会变化
        float viewportHalfHeight = m_WorldCamera.orthographicSize;
        float viewportHalfWidth = viewportHalfHeight * m_WorldCamera.aspect;

        // 步骤2：封装为你定义的 AABB 结构体
        // AABB 的 Center = 摄像机世界位置，HalfSize = 视口半宽、半高
        m_ViewportAABB.Update(
            center: (Vector2)transform.position,
            halfSize: new Vector2(viewportHalfWidth, viewportHalfHeight)
        );
    }

    /// <summary>
    /// 检测目标 AABB 是否在摄像机视口内（复用你的 Overlaps 方法）
    /// </summary>
    public bool IsAABBInViewport(AABB targetAABB)
    {
        return m_ViewportAABB.Overlaps(targetAABB);
    }

    /// <summary>
    /// 检测目标点是否在摄像机视口内（复用你的 Contains 方法）
    /// </summary>
    public bool IsPointInViewport(Vector2 targetPoint)
    {
        return m_ViewportAABB.Contains(targetPoint);
    }

    // Gizmos 调试：Scene 视图中绘制视口 AABB（绿色框）
    private void OnDrawGizmos()
    {
        if (m_WorldCamera == null) return;

        Gizmos.color = Color.green;
        Vector2 center = m_ViewportAABB.Center;
        Vector2 halfSize = m_ViewportAABB.HalfSize;
        
        // 绘制视口矩形边界
        Vector2 topLeft = new Vector2(center.x - halfSize.x, center.y + halfSize.y);
        Vector2 topRight = new Vector2(center.x + halfSize.x, center.y + halfSize.y);
        Vector2 bottomRight = new Vector2(center.x + halfSize.x, center.y - halfSize.y);
        Vector2 bottomLeft = new Vector2(center.x - halfSize.x, center.y - halfSize.y);
        
        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
        Gizmos.DrawLine(bottomLeft, topLeft);
    }
}