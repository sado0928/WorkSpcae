# GameLogic - 核心架构逻辑

## 1. 游戏架构全景 (Overview)
框架采用 **“单驱动入口 + 订阅式管理”** 的微服务化架构。

### 1.1 启动流水线 (Bootstrap Flow)
1.  **AOT 启动**：`HotUpdateMgr` 完成热更新，反射调用 `GameEntry.StartGame()`。
2.  **常驻节点实例化**：`GameEntry` 加载 `KeepNode.prefab`。
3.  **Hotfix 驱动建立**：`KeepNode` 挂载 `Global.cs`，其 `Awake` 初始化 `App` 实例。
4.  **生命周期移交**：`Global.cs` 的 `Update`、`OnDestroy` 等 Unity 钩子全面驱动 `App` 及其下属管理器的生命周期。

## 2. 系统间协作逻辑 (Collaboration)

### 2.1 资源与内存流转
- **ResMgr** 负责“拿东西” (Addressables Load)。
- **PoolMgr** 负责“管东西” (Spawn/Despawn)。
- **UIMgr/EffectMgr** 负责“用东西” (Logic Integration)。
- 当 `Global` 收到 `OnLowMemory` 警告时，会自上而下通知 `UIMgr` (LRU 清理) -> `ResMgr` (UnloadUnusedAssets)，形成内存闭环。

### 2.2 流程与状态控制
- **ProcedureMgr** 负责宏观状态（登录、大厅、战斗）。
- 当状态切换时，`ResMgr` 监听 `EventDefine.LoadingScene` 事件，自动清理上一个场景的私有资源。

## 3. 开发范式 (Patterns)
*   **数据驱动**：业务逻辑严禁硬编码，必须通过 `CfgMgr` (Luban) 获取策划配置。
*   **UI 分离**：强推 `UIGen` + `UILogic` 的 Partial Class 方案，保证自动生成的代码不被覆盖。
*   **事件通信**：系统间解耦必须通过 `DispatcherMgr` (事件中心)，严禁跨管理器强引用业务逻辑。
*   **异步优先**：所有可能导致卡顿的 IO (加载、下载) 必须使用异步接口，配合框架提供的 `Handle` 句柄进行状态追踪。

---
*本架构文档由 Gemini 分析项目源码后自动生成，旨在为孵化期开发者提供清晰的技术路径。*
