# AudioMgr - 多通道音频管理器

## 1. 概念 (Concept)
`AudioMgr` 负责游戏中所有的听觉反馈，包括 BGM (背景音乐)、SFX (音效) 和 Voice (语音)。

## 2. 逻辑 (Logic)
*   **分类管理**：
    *   **BGM**：单通道，支持跨场景淡入淡出，自动循环。
    *   **SFX**：多通道对象池管理，支持 2D (UI/全局) 和 3D (空间方位) 模式。
    *   **Voice**：高优先级独占通道。
*   **监听器自动同步**：
    *   框架内置一个常驻 `AudioListener`。每帧会自动对齐到 `Camera.main`，确保 3D 音效在场景切换或相机移动时依然准确。
*   **持久化存储**：
    *   内置音量设置与静音开关，并自动保存至本地磁盘。

## 3. 技术流 (Applied Technology)
*   **音频对象池**：为了防止高频音效导致的 `GameObject` 创建开销，SFX 采用复用池模式。
*   **空间音效 (Spatial Blend)**：利用 Unity AudioSource 的 3D 属性，结合框架的坐标同步实现。
