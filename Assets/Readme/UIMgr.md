# UIMgr - 工程化 UI 管理框架

## 1. 概念 (Concept)
`UIMgr` 是一个基于“分层思想”和“自动化渲染控制”的 UI 框架。它解决了 UI 渲染顺序错乱（Z 轴冲突）、资源回收策略以及多层 UI 间的互斥逻辑。

## 2. 逻辑 (Logic)
*   **分层架构 (UILayer)**：
    *   定义了 `Main`, `Normal`, `PopUp`, `Top`, `System` 五大基础层级。
    *   每一层都有独立的容器 (Root)，确保 UI 始终处于正确的显示深度。
*   **自动化渲染控制**：
    *   **SortingOrder 自动化**：框架会根据层级基础值 + 栈深自动分配 `SortingOrder`，无需在 Prefab 上手动设置。
    *   **PlaneDistance 自动化**：针对 `ScreenSpaceCamera` 模式，自动计算 `PlaneDistance`，防止不同 UI 间的模型穿插。
*   **LRU 资源回收**：
    *   记录每个 UI 关闭的时间戳。
    *   当内存压力增大或定时器触发时，自动卸载长时间未使用的 UI Prefab 资源。
*   **拦截与托管加载**：
    *   提供 `LoadUIAsync` (拦截式) 和 `OpenUIHosting` (队列式) 接口，解决异步加载时产生的逻辑冲突。

## 3. 技术流 (Applied Technology)
*   **Partial Class 开发模式**：将 UI 分为 `UIGen` (组件绑定，自动生成) 和 `UILogic` (业务逻辑，手动编写)，实现表现与逻辑的物理分离。
*   **Canvas/CanvasScaler 同步**：框架在加载 UI 时会自动同步子 Canvas 的参数，确保所有 UI 的适配方案统一。
*   **UIHandle 模式**：采用类 Task 的异步句柄，让逻辑层写起来更顺滑。
