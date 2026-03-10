# CfgMgr - Luban 数据驱动配置

## 1. 概念 (Concept)
`CfgMgr` 是游戏的数据中枢。它基于 **Luban** 导出工具，将 Excel 策划表转换为高效的 C# 内存对象。

## 2. 逻辑 (Logic)
*   **加载流程**：
    *   `CfgMgr` 初始化时，通过 `Tables` 构造函数注册加载函数。
    *   利用 `ResMgr` 加载对应的 `.bytes` 配置文件。
    *   使用 `Luban.ByteBuf` 对二进制流进行反序列化，填充内存对象。
*   **访问模式**：
    *   支持 `GetData<T>(key)` 进行 O(1) 复杂度的快速查询。
    *   支持跨表引用（由 Luban 生成的 Tables 结构自动保证）。

## 3. 技术流 (Applied Technology)
*   **Luban 框架**：目前行业领先的多格式配置导出方案，支持 Excel, JSON, XML, YAML。
*   **二进制加载**：相比 JSON 加载，二进制加载速度快、内存占用低且具有一定的混淆保护。
*   **代码自动生成**：配置解析代码全部由工具生成，避免了繁琐的手动字段解析。
