# Revit MCP CommandSet 项目架构文档

## 项目概述

本项目是 Revit MCP 生态系统的核心组件，负责在 Revit 端实现与 AI 助手的通信桥梁。通过统一的命令模式，为 LLM 提供访问和操作 Revit 模型的能力。

### 核心特性
- 🔗 **AI-BIM 连接**: 连接大语言模型与 Revit 软件的桥梁
- 🏗️ **统一架构**: 基于 RevitMCPSDK 的标准化开发模式
- ⚡ **异步处理**: 支持复杂操作的异步执行和超时控制
- 🔧 **CRUD 完整**: 提供元素创建、查询、更新、删除的完整功能

## 技术架构

### 核心依赖
- **RevitMCPSDK**: 版本 `$(RevitVersion).*` - 提供统一的开发规范
- **Revit API**: 支持 Revit 2020-2025 多版本
- **Newtonsoft.Json**: JSON 序列化和数据交换
- **.NET Framework 4.8** (R20-R24) / **.NET 8** (R25+)

### 双层架构设计

项目采用 **Command + EventHandler** 双层架构：

```
MCP Client (AI/LLM)
    ↓ JSON Parameters
[ExternalEventCommandBase] ← 命令入口层
    ↓ 参数解析 & 事件触发
[IExternalEventHandler] ← Revit功能实现层
    ↓ Revit API 调用
Revit Application
```

## 目录结构

```
revit-mcp-commandset/
├── Features/                  # 功能模块目录（按功能组织）
│   ├── ElementFilter/         # 元素过滤功能模块
│   │   ├── AIElementFilterCommand.cs
│   │   ├── AIElementFilterEventHandler.cs
│   │   └── Models/           # 元素过滤模型
│   │       └── FilterSetting.cs
│   ├── ElementOperation/      # 元素操作功能模块
│   │   ├── OperateElementCommand.cs
│   │   ├── OperateElementEventHandler.cs
│   │   └── Models/           # 元素操作模型
│   │       └── OperationSetting.cs
│   ├── UnifiedCommands/      # 统一命令功能模块（取代旧的族和系统族模块）
│   │   ├── CreateElementCommand.cs
│   │   ├── CreateElementEventHandler.cs
│   │   ├── GetElementCreationSuggestionCommand.cs
│   │   ├── GetElementCreationSuggestionEventHandler.cs
│   │   ├── Models/           # 统一创建模型
│   │   │   ├── ElementCreationParameters.cs
│   │   │   ├── ElementSuggestionParameters.cs
│   │   │   ├── FamilyCreationOptions.cs
│   │   │   ├── SystemCreationOptions.cs
│   │   │   ├── SystemElementParameters.cs
│   │   │   ├── WallSpecificParameters.cs
│   │   │   └── FloorSpecificParameters.cs
│   │   └── Utils/           # 统一工具类
│   │       └── ElementUtilityService.cs
│   └── RevitStatus/          # Revit状态功能模块
│       ├── GetRevitStatusCommand.cs
│       ├── GetRevitStatusEventHandler.cs
│       └── Models/           # 状态模型
│           └── RevitStatusInfo.cs
├── Models/                    # 数据模型层
│   ├── Common/               # 通用模型
│   │   ├── AIResult.cs
│   │   ├── CreationRequirements.cs
│   │   └── ParameterInfo.cs
│   └── Geometry/             # 几何模型
│       ├── JZPoint.cs
│       ├── JZLine.cs
│       └── JZFace.cs
├── Utils/                     # 工具类层
│   ├── FamilyCreation/       # 族创建工具类
│   │   └── FamilyInstanceCreator.cs
│   └── SystemCreation/       # 系统族创建工具类
│       ├── SystemElementCreator.cs
│       └── SystemElementValidator.cs
└── RevitMCPCommandSet.csproj  # 项目配置
```

## 开发规范

### 1. 命令实现标准

每个 MCP 命令需要实现两个核心类：

#### 数据格式规范
**强制要求**: 所有命令入口层接受的参数必须被 `"data"` 包裹，以保持接口的规整性和一致性。

**标准格式**：
```json
{
  "data": {
    // 实际的业务参数
    "param1": "value1",
    "param2": "value2"
  }
}
```

#### Command 类（继承 ExternalEventCommandBase）
```csharp
public class YourCommand : ExternalEventCommandBase
{
    public override string CommandName => "your_command_name";

    public YourCommand(UIApplication uiApp)
        : base(new YourEventHandler(), uiApp) { }

    public override object Execute(JObject parameters, string requestId)
    {
        // 1. 强制解析 data 包裹层
        var dataToken = parameters["data"];
        if (dataToken == null)
        {
            return new AIResult<object>
            {
                Success = false,
                Message = "参数格式错误：缺少 'data' 包裹层"
            };
        }

        // 2. 解析实际业务参数
        var actualData = dataToken.ToObject<YourDataModel>();

        // 3. 设置 Handler 参数
        // 4. 触发异步事件
        // 5. 返回结果
    }
}
```

#### EventHandler 类（实现双接口）
```csharp
public class YourEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

    public void Execute(UIApplication uiapp)
    {
        try
        {
            // Revit API 操作
        }
        finally
        {
            _resetEvent.Set(); // 必须：通知完成
        }
    }

    public bool WaitForCompletion(int timeoutMilliseconds = 10000)
    {
        return _resetEvent.WaitOne(timeoutMilliseconds);
    }
}
```

### 2. 数据模型设计

#### 统一返回格式
```csharp
public class AIResult<T>
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public T Response { get; set; }
}
```

#### 坐标系统约定
- **单位**: 毫米 (mm) - 所有坐标和距离
- **转换**: Revit 内部单位 × 304.8 = 毫米
- **几何类**: 使用 JZPoint、JZLine、JZFace 等自定义类型

## 核心功能模块

### 1. AI 元素过滤器 (ai_element_filter)
- **功能**: 智能查询和筛选 Revit 元素
- **支持**: 类别、类型、空间范围、可见性等多维度过滤
- **返回**: 详细的元素信息（几何、参数、属性等）

### 2. 统一元素创建 (create_element)
- **功能**: 统一的元素创建命令，支持族实例和系统族元素
- **族实例支持**: 8种族放置类型（OneLevelBased、WorkPlaneBased、TwoLevelsBased、CurveBased、ViewBased等）
- **系统族支持**: Wall（墙体）、Floor（楼板），预留 Ceiling、Roof
- **智能化**: 自动类型检测、自动查找标高、自动搜索宿主、智能参数验证
- **适用范围**: 门、窗、设备、结构构件、墙体、楼板等所有Revit元素类型
- **架构特色**: 单一入口、统一参数模型、智能路由到具体创建器

### 3. 统一创建参数建议 (get_element_creation_suggestion)
- **功能**: 为AI提供统一的元素创建参数要求和指导
- **族实例分析**: 族放置类型、必需参数、可选参数、参数格式示例
- **系统族分析**: 必需参数、可选参数、参数格式示例、可用类型列表
- **智能检测**: 根据ElementId自动检测元素类型并提供相应建议
- **作用**: 统一AI对所有Revit元素创建需求的理解，提高创建成功率

### 4. 元素操作器 (operate_element)
- **功能**: 对元素进行各种操作
- **操作类型**: 选择、着色、透明度、隐藏、删除、隔离等
- **可视化**: 支持颜色标记和3D剖切框

## 快速开发指南

### 编译配置
- **标准编译配置**: Debug R20, x64
- **MSBuild路径**: `"D:\JetBrains\JetBrains Rider 2025.1.4\tools\MSBuild\Current\Bin\MSBuild.exe"`

### 编译命令
```bash
# 标准编译命令（推荐）
"D:\JetBrains\JetBrains Rider 2025.1.4\tools\MSBuild\Current\Bin\MSBuild.exe" "E:\工作文档\开发类\MyCode\Revit-MCP\revit-mcp-commandset\revit-mcp-commandset\RevitMCPCommandSet.csproj" -p:Configuration="Debug R20" -nologo -clp:ErrorsOnly
```

### 添加新功能模块

1. **创建功能模块目录**
   ```bash
   Features/YourNewFeature/
   ```

2. **创建 Command 和 EventHandler 类**
   ```bash
   Features/YourNewFeature/YourNewCommand.cs
   Features/YourNewFeature/YourNewEventHandler.cs
   ```

3. **更新命名空间**
   ```csharp
   namespace RevitMCPCommandSet.Features.YourNewFeature
   ```

4. **创建数据模型（如需要）**
   ```bash
   Features/YourNewFeature/Models/YourDataModel.cs
   ```

5. **更新 command.json**
   ```json
   {
     "commandName": "your_new_command",
     "description": "Your command description",
     "assemblyPath": "RevitMCPCommandSet.dll"
   }
   ```

### 功能模块组织原则

每个 Features 子目录代表一个完整的功能模块：
- **ElementFilter**: 元素查询和过滤相关功能
- **UnifiedCommands**: 统一元素创建和参数建议功能（整合原FamilyInstanceCreation和SystemElementCreation）
- **ElementOperation**: 元素操作相关功能
- **RevitStatus**: Revit状态查询功能

### 命名空间规范

- 功能模块命名空间：`RevitMCPCommandSet.Features.{ModuleName}`
- 模块模型命名空间：`RevitMCPCommandSet.Features.{ModuleName}.Models`
- 公共模型命名空间：`RevitMCPCommandSet.Models.Common`
- 几何模型命名空间：`RevitMCPCommandSet.Models.Geometry`
- 工具类命名空间：`RevitMCPCommandSet.Utils`

## 注意事项

1. **数据格式规范**: 所有命令必须强制要求参数被 `"data"` 包裹，确保接口一致性
2. **线程安全**: 所有 Revit API 调用必须在主线程执行
3. **事务管理**: 修改操作需要包装在 Transaction 中
4. **资源释放**: 适当释放 ManualResetEvent 等资源
5. **命名一致性**: 确保与 revit-mcp 服务端命令名称一致
6. **单位转换**: 注意 Revit 内部单位与毫米的转换，使用比例304.8进行换算
7. **模块独立性**: 各功能模块应保持相对独立，减少耦合

## 常见问题

**Q: 为什么要强制使用 "data" 包裹参数？**
A: 统一的数据格式确保接口规整性，便于后续扩展（如添加元数据、版本信息等），同时降低解析复杂度。

**Q: 命令执行超时怎么办？**
A: 检查 `_resetEvent.Set()` 是否在 finally 块中调用，增加超时时间。

**Q: 如何处理 Revit 版本兼容性？**
A: 使用条件编译指令 `#if REVIT2023_OR_GREATER` 等。

**Q: 参数解析失败怎么办？**
A: 首先检查是否有 "data" 包裹层，然后检查 JSON 结构是否与数据模型匹配，使用 try-catch 捕获解析异常。

**Q: 新增功能模块后如何组织代码？**
A: 在 Features 下创建新目录，将相关的 Command 和 EventHandler 放在一起，保持功能内聚。

---

## 📊 项目最近更新

基于 Git 历史记录的最新进展（截至 2025-09-23）：

### v2.3.0 - Models架构重组完成 (2025-09-23)
- 🗂️ **Models文件夹重组**：将模块特定Models移至各功能模块下
- 📁 **架构清晰化**：Features\[Module]\Models结构，提升模块独立性
- 🔧 **命名空间优化**：统一模块Models命名空间规范
- ✅ **编译验证通过**：R20+x64平台兼容性确认
- 📚 **文档同步更新**：CLAUDE.md反映最新目录结构

### v3.0.0 - UnifiedCommands 架构整合完成 (2025-09-23)
- 🎉 **完成架构统一**：成功整合FamilyInstanceCreation和SystemElementCreation为UnifiedCommands模块
- 🔄 **命令简化**：4个命令合并为2个统一命令（create_element, get_element_creation_suggestion）
- 🗂️ **Models迁移**：SystemElementCreation/Models文件迁移至UnifiedCommands/Models
- 🧹 **依赖清理**：GetElementCreationSuggestionEventHandler完全移除对旧模块依赖，实现自主逻辑
- 📝 **注册更新**：command.json更新为新的统一命令注册
- 🗑️ **模块删除**：清理FamilyInstanceCreation和SystemElementCreation目录
- ✅ **编译验证**：确保重构后代码编译通过，功能完整

### v2.2.0 - SystemElementCreation 重构完成 (2025-09-23)
- 🎉 **完成系统族创建模块重构**：采用MCP友好的组合模式设计
- 🔧 **重构SystemElementParameters**：改用字符串elementType和组合模式
- ⚡ **新建SystemElementValidator**：集中处理参数验证逻辑
- 🗑️ **删除冗余类**：移除SystemElementSuggestion和SystemParameterInfo
- 🔄 **统一数据模型**：全面使用CreationRequirements和ParameterInfo
- 💡 **扩展ParameterInfo**：添加Type、Example、IsRequired字段支持
- 🧹 **更新所有相关类**：Creator、EventHandler、Command全部适配新架构
- ✅ **编译通过验证**：确保重构结果代码正确性

### v2.1.0 - API 优化更新 (2025-09-23)
- 🔧 **AIResult.Message字段优化**：明确Response数据类型和含义，提升API文档清晰度
- 📚 **文档全面更新**：同步更新所有功能模块README.md，反映最新架构变更和功能特性
- 🎯 **统一规范完善**：强化"data"包裹层要求，保持接口一致性

### v2.0.x - 架构重构系列 (2025-09-22)
- 🧹 **FamilyCreationDefaults清理** (662eaae)：删除冗余默认值类，移至FamilyInstanceService静态属性
- 🏗️ **双层架构完善** (03999cc)：Creator专注核心创建逻辑，Service负责智能验证和建议
- ⚡ **错误处理标准化** (9aa1c0e)：FamilyInstanceCreator改用标准异常抛出，替代Console.WriteLine
- 🚀 **参数建议精简** (8991e6e)：彻底优化族创建参数建议格式，提升AI理解效率
- 🔄 **ElementFilter修复** (fcd879f)：完成ParameterInfo类型引用修复，确保过滤功能稳定

---

更多详细信息请参考项目源码和 RevitMCPSDK 文档。

### 📋 相关文档链接
- [统一命令功能文档](./revit-mcp-commandset/Features/UnifiedCommands/README.md)
- [元素过滤器文档](./revit-mcp-commandset/Features/ElementFilter/README.md)
- [元素操作器文档](./revit-mcp-commandset/Features/ElementOperation/README.md)
- [Revit状态功能文档](./revit-mcp-commandset/Features/RevitStatus/README.md)
- [族创建工具模块文档](./revit-mcp-commandset/Utils/FamilyCreation/README.md)
- [系统族创建工具模块文档](./revit-mcp-commandset/Utils/SystemCreation/README.md)
