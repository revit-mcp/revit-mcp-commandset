# Revit MCP CommandSet 项目架构文档

## 项目概览

本项目是 Revit MCP 生态系统的核心组件，作为 AI 助手与 Revit 软件的通信桥梁，基于 ExternalEvent 双层架构（Command + EventHandler）为 LLM 提供访问和操作 Revit 模型的能力。

### 核心特性
- **AI-BIM 连接**：连接大语言模型与 Revit 软件
- **统一架构**：基于 RevitMCPSDK 标准化开发模式
- **节点化数据**：AI 友好的结构化信息组织
- **异步处理**：支持复杂操作的异步执行和超时控制
- **CRUD 完整**：提供元素创建、查询、更新、删除的完整功能

### 支持版本
- Revit 2020-2024: .NET Framework 4.8
- Revit 2025+: .NET 8

## 快速导航 & README 协作流程

在任何代码查阅或修改前，优先确保项目记忆链路正确运行：

### 多层级 README 导航体系

```
AGENTS.md (根)
  ├── Features/*/README.md (功能模块)
  │   └── Features/*/FieldBuilders/*/README.md (子模块)
  ├── Models/README.md (数据模型)
  ├── Utils/README.md (工具类)
  └── Test/README.md (测试)
```

### 工作流程规范 **[强制]**

**▶ 规则1：先看 README 再看源码**
- 进入任意目录时，先阅读本目录的 README.md
- 系统会自动 Read 该 README，理解后再操作源码

**▶ 规则2：逐级深入遵循层级**
- 多级目录同样适用："根 → 模块 → 子模块"
- 例：AGENTS.md → Features/ElementFilter/README.md → 源码

**▶ 规则3：修改代码即更新 README**
- 功能改动完成后，立即同步更新所在目录的 README
- 若目录无 README，必须创建并补写

**违反后果**：忽略 README 导致上下文失真，文档过期影响后续协作

## 核心依赖

- **RevitMCPSDK**：版本 `$(RevitVersion).*` - 提供统一的开发规范和基础架构
- **Revit API**：支持 Revit 2020-2025 多版本
- **Newtonsoft.Json**：JSON 序列化和数据交换

## 代码架构

### 双层架构模式

```
MCP Client (AI/LLM) → [Command 层] → [EventHandler 层] → Revit API
                       参数解析      ExternalEvent触发    模型操作
```

### 目录结构

```
revit-mcp-commandset/
├── Features/               # 功能模块（详见子目录 README）
│   ├── ElementFilter/     # 节点化元素查询
│   ├── ElementVisual/     # 视觉操作
│   ├── ElementVisibility/ # 可见性控制
│   ├── ElementTransform/  # 几何变换
│   ├── ElementModify/     # 参数修改
│   ├── ElementCreation/   # 统一创建
│   └── RevitStatus/       # 状态查询
├── Models/                 # 数据模型层
│   ├── Common/            # 通用模型（AIResult、ElementOperationResponse）
│   └── Geometry/          # 几何模型（JZPoint、JZLine、JZPlane）
├── Utils/                  # 工具类层
│   ├── ParameterHelper.cs # 单位自动转换（mm↔ft, °↔rad）
│   ├── FamilyCreation/    # 族创建工具
│   └── SystemCreation/    # 系统族创建
└── Test/                   # 测试验证（Validate*.cs）
```

**节点化架构**：ElementFilter 采用节点化数据组织，详见 [Features/ElementFilter/README.md](./revit-mcp-commandset/Features/ElementFilter/README.md)

## 统一约束（跨模块硬规则）

### 数据格式规范 **[强制]**

**说明**：所有命令入参必须被 `"data"` 包裹
**违反后果**：缺失时会直接返回"参数格式错误：缺少 'data' 包裹层"并终止执行
**指向详情**：参考 RevitMCPSDK/API/Base 或各命令 README

### JsonProperty 同步规范 **[强制]**

**说明**：Revit 端 `[JsonProperty("属性名")]` 必须与服务端 Zod schema 属性名完全一致，使用 camelCase 命名
**违反后果**：导致参数反序列化失败，是最常见的集成问题
**指向详情**：修改前对比服务端 `src/tools/*.ts` 和本端 `Features/*/Models/*.cs` 的参数定义

### 单位换算规则 **[强制]**

**说明**：长度参数（毫米 ↔ 英尺，换算比例 304.8）、角度参数（度 ↔ 弧度，换算公式 π/180）
**违反后果**：导致元素位置、尺寸错误，影响模型准确性
**指向详情**：参考 Utils/ParameterHelper.cs 自动转换实现

### 线程安全要求 **[强制]**

**说明**：所有 Revit API 调用必须在主线程执行，通过 ExternalEvent 机制触发
**违反后果**：跨线程调用会导致 Revit 崩溃或数据损坏
**指向详情**：参考 RevitMCPSDK/API/Base/ExternalEventCommandBase

### 事务管理要求 **[强制]**

**说明**：所有修改操作必须包装在 Transaction 中（查询操作除外）
**违反后果**：未包装的修改操作会被 Revit 拒绝执行
**指向详情**：参考各 EventHandler 实现（Features/*/EventHandler.cs）

### 统一返回格式规范 **[强制]**

**说明**：所有 MCP 命令必须使用 `AIResult<T>` 包装返回值，确保统一的成功/失败处理
**违反后果**：返回格式不一致导致 AI 助手无法正确解析响应
**指向详情**：参考 Models/Common/AIResult.cs 及各 EventHandler 实现

### 命名空间约定

**说明**：功能模块 `RevitMCPCommandSet.Features.{ModuleName}`、模块模型 `.Models`、公共模型 `.Models.Common`、工具类 `.Utils`
**违反后果**：导致命名空间混乱，影响代码组织和查找
**自检方法**：参考现有模块的命名空间声明，确保新代码遵循相同模式

### 代码演进原则

**说明**：项目处于快速迭代阶段，代码修改时直接更新到新语义，不保留旧字段兼容
**理由**：保持代码库语义一致性，避免技术债务累积
**指向详情**：本文档"代码演进原则"说明

## MCP 命令清单

| 命令名 | 功能概述 | 关键动作 | 文档入口 |
|--------|----------|----------|----------|
| `ai_element_filter` | 节点化元素查询 | 类别/类型/名称/空间过滤 | [ElementFilter/README.md](./revit-mcp-commandset/Features/ElementFilter/README.md) |
| `operate_element_visual` | 视觉操作(不改模型) | Select/Highlight/SetColor | [ElementVisual/README.md](./revit-mcp-commandset/Features/ElementVisual/README.md) |
| `operate_element_visibility` | 可见性控制 | Hide/Isolate/Unhide | [ElementVisibility/README.md](./revit-mcp-commandset/Features/ElementVisibility/README.md) |
| `operate_element_transform` | 几何变换 | Rotate/Mirror/Move/Copy | [ElementTransform/README.md](./revit-mcp-commandset/Features/ElementTransform/README.md) |
| `operate_element_modify` | 参数修改与删除 | SetParameter/Delete | [ElementModify/README.md](./revit-mcp-commandset/Features/ElementModify/README.md) |
| `create_element` | 统一元素创建 | 8种族类型+墙体/楼板 | [ElementCreation/README.md](./revit-mcp-commandset/Features/UnifiedCommands/README.md) |
| `get_element_creation_suggestion` | 创建参数建议 | 分析类型并提供建议 | [ElementCreation/README.md](./revit-mcp-commandset/Features/UnifiedCommands/README.md) |
| `get_revit_status` | Revit状态查询 | 版本/文档/视图 | [RevitStatus/README.md](./revit-mcp-commandset/Features/RevitStatus/README.md) |

## 协作流程 & 开发规范

### README 使用清单 **[强制]**

- [ ] 进入目录前必须阅读 README.md
- [ ] 修改代码后立即同步更新 README
- [ ] 导航优先 README → 源码（禁止直接 grep）
- [ ] 缺失 README 时必须在当前改动中补写

### 标准编译配置

- **配置**：Debug R20, x64
- **MSBuild 路径**：`D:\JetBrains\JetBrains Rider 2025.1.4\tools\MSBuild\Current\Bin\MSBuild.exe`
- **编译命令**：
  ```bash
  "<MSBuild路径>" "<项目路径>\RevitMCPCommandSet.csproj" -p:Configuration="Debug R20" -nologo -clp:ErrorsOnly
  ```

### 添加新功能模块（6步骤）

1. 创建功能模块目录：`Features/YourNewFeature/`
2. 创建 Command 和 EventHandler 类（继承自 RevitMCPSDK 基类）
3. 创建数据模型（如需要）：`Features/YourNewFeature/Models/*.cs`
4. 更新命名空间：`RevitMCPCommandSet.Features.YourNewFeature`
5. 更新 `command.json`：注册新命令
6. **必须**创建模块 README.md，保持文档系统完整性

## 参考资料

### 关键文档

- **功能模块文档**：各 Features/*/README.md（功能说明、参数定义、使用示例）
- **数据模型文档**：Models/README.md（模型结构说明）
- **工具类文档**：Utils/README.md、Utils/FamilyCreation/README.md
- **构建脚本**：RevitMCPCommandSet.csproj（多版本配置）

---

更多详细信息请参考项目源码和各模块 README.md 文档。