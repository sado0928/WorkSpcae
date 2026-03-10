# HotUpdateMgr - AOT 引导与热更新系统

## 1. 概念 (Concept)
`HotUpdateMgr` 是框架的“守门员”。由于 Unity 原生不支持 AOT 代码的热更新，该组件位于 AOT 层，其唯一职责是：**在极其有限的原生环境下，构建出可以运行热更代码（Hotfix）的“沙盒”环境**。它实现了从资源下载、MD5 校验到 HybridCLR 注入的全流程。

## 2. 逻辑 (Logic)
热更新流程遵循 **三段式加载** 协议：
1.  **资源分析 (Scan)**：
    *   对比 `StreamingAssets` (包内) 与 `PersistentDataPath` (包外) 的 `filelist.json`。
    *   建立本地资源快照，确定当前活跃的资源版本。
2.  **网络同步 (Sync)**：
    *   请求远程 `version.txt` 进行版本比对。
    *   如果版本不一致，拉取远程差异化 `filelist.json`。
    *   **MD5 校验下载**：只下载 MD5 变化的文件，并使用 `.tmp` 临时文件确保下载原子性。
3.  **环境注入 (Inject)**：
    *   **元数据注入**：调用 `RuntimeApi.LoadMetadataForAOTAssembly`，补充 AOT 泛型元数据，解决 HybridCLR 在 IL2CPP 下的泛型实例化问题。
    *   **程序集加载**：通过 `Assembly.Load` 加载热更新 DLL。
    *   **移交控制权**：通过反射调用 `GameEntry.StartGame()`，正式进入 Hotfix 层逻辑。

## 3. 技术流 (Applied Technology)
*   **Addressables 资源重定向**：利用 `Addressables.InternalIdTransformFunc` 拦截所有资源请求。如果沙盒路径 (Persistent) 存在最新资源，则将请求重定向至本地文件流，从而绕过 Addressables 默认的下载机制，实现自定义热更。
*   **MD5 强校验**：不信任文件长度，仅信任 MD5，确保资源完整性。
*   **SemVer 语义化版本**：支持 `Major.Minor.Patch` 规则，由框架判定是热更小补丁还是需要跳转商店的大版本更新。
*   **HybridCLR 补元技术**：解决了代码热更新中最棘手的泛型实例化 (AOT Generic) 问题。
