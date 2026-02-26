# FreamWork - Unity HybridCLR + Addressables Framework

这是一个基于 Unity 引擎的游戏开发框架，集成了 **HybridCLR** (原 Huatuo) 实现 C# 代码热更新，以及 **Addressables** 实现资源按需加载与热更新。

## 项目概述

项目采用 AOT (Ahead-Of-Time) 与 Hotfix (热更新) 分离的架构：
- **AOT 层**: 位于 `Assets/Game/Scripts/Runtime/AOT`，负责游戏启动、版本检测、资源下载、HybridCLR 初始化以及加载热更新 DLL。
- **Hotfix 层**: 位于 `Assets/Game/Scripts/Runtime/Hotfix`，包含游戏的实际核心逻辑，编译为独立 DLL 后作为 Addressables 资源进行热更新。

## 核心架构与组件

### 1. 启动与热更新流程 (`HotUpdateMgr.cs`)
- **三段式版本号**: 使用 `Major.Minor.Patch` 方案。
- **版本校验**: 通过远程 `version.txt` 和 `filelist_{version}.json` 进行 MD5 差异对比。
- **并发下载**: 支持最大 5 个文件并发下载，提高资源更新效率。
- **地址重定向**: 通过 `Addressables.InternalIdTransformFunc` 自动将资源请求导向 `persistentDataPath` (热更资源) 或 `streamingAssetsPath` (内置资源)。

### 2. 逻辑入口
- **AOT 入口**: `HotUpdateMgr` 完成更新后，通过反射调用 `Game.Runtime.Hotfix.GameEntry.StartGame()`。
- **Hotfix 驱动**: `GameEntry` 实例化 `KeepNode` 预制体，其上挂载的 `Global.cs` 脚本负责驱动 `App` 类的生命周期（Awake, Start, Update, Destroy）。

### 3. 管理器系统 (Hotfix 层)
- `App.cs`: 热更新层的核心，管理以下系统：
    - `UIMgr`: UI 框架管理。
    - `ResMgr`: 资源加载与卸载封装。
    - `ProcedureMgr`: 流程/状态机切换。
    - `TimerMgr` & `FrameTimerMgr`: 定时器系统。
    - `DispatcherMgr`: 事件分发中心。

## 开发规范

### 1. 代码存放
- **非热更代码**: 存放在 `Assets/Game/Scripts/Runtime/AOT`，仅限框架基础逻辑和热更引导逻辑。
- **热更代码**: 存放在 `Assets/Game/Scripts/Runtime/Hotfix`。
- **注意**: AOT 逻辑不能直接引用 Hotfix 逻辑，必须通过反射进行调用。

### 2. 资源管理
- 所有需要热更的资源都应标记为 Addressable。
- 热更新 DLL 需手动或自动编译为 `.bytes` 后存放在 `Assets/ResBundle/Hotfix`。

## 构建与运行

### 关键步骤 (TODO)
1. **HybridCLR 设置**:
    - 确保 `HybridCLR -> Settings` 配置正确。
    - 执行 `HybridCLR -> Generate -> All` 生成必要的 AOT 元数据和胶水代码。
2. **Addressables 打包**:
    - 在 `Addressables Groups` 窗口中配置资源分组。
    - 执行 `Build -> New Build -> Default Build Script`。
3. **热更打包**:
    - 编译 Hotfix DLL。
    - 生成并上传 `filelist.json` 和 `version.txt` 到服务器根目录（默认为 `http://localhost:8888/`）。

## 依赖项
- **HybridCLR**: `https://gitee.com/focus-creative-games/hybridclr_unity.git`
- **Addressables**: `1.22.3`
- **TextMeshPro**: `3.0.7`

---
*此 GEMINI.md 文件由 AI Agent 自动生成，用于提供项目上下文。*
