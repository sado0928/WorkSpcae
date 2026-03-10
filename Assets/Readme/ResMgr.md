# ResMgr - 域驱动资源管理器

## 1. 概念 (Concept)
`ResMgr` 是对 `Addressables` 的高度封装。其设计核心不再是简单的“加载资源”，而是**“生命周期管理”**。通过引入“域 (Scope)”的概念，解决了 Addressables 引用计数手动管理易出错、易泄露的问题。

## 2. 逻辑 (Logic)
*   **域隔离 (ResTypeByScene)**：
    *   资源分为 `Global` (全局常驻) 和 `Level/Scene` (场景私有)。
    *   在切换场景时，`ResMgr` 会自动释放属于旧场景域的所有资源，而 `Global` 域资源保持不动。
*   **统一缓存结构**：
    *   使用嵌套字典 `Dictionary<ResTypeByScene, Dictionary<string, Object>>` 进行缓存。
    *   加载资源前先查缓存，确保同一资源在同一域下只有一份引用计数，有效降低内存占用。
*   **同步与异步并存**：
    *   提供 `LoadAsset<T>` (同步等待) 和 `LoadAssetAsync<T>` (异步回调)，满足不同业务场景。
    *   异步加载时具备“并发保护”，防止同一路径被多次触发加载请求。

## 3. 技术流 (Applied Technology)
*   **Addressables Async Handles**：所有资源加载均基于句柄管理，销毁时显式调用 `Addressables.Release`。
*   **强类型加载**：利用 C# 泛型约束，确保加载出的对象类型安全。
*   **引用计数自动托管**：通过域的生命周期钩子，实现资源的批量回收。
