# ElementFilter 模块

## 模块概述

ElementFilter 模块是专为 AI 助手设计的智能 Revit 元素查询工具，用于从 Revit 项目中检索详细的元素信息。该工具允许 AI 根据特定条件（如类别、类型、可见性或空间位置）请求匹配的元素，然后对返回的数据进行进一步分析，以回答用户关于 Revit 模型元素的复杂查询。

### AI 助手使用场景示例

当用户询问"查找项目中所有高度超过5米的墙体"时，AI助手的处理流程：

1. **调用工具**：使用参数 `{"filterCategory": "OST_Walls", "includeInstances": true}`
2. **接收数据**：获得项目中所有墙体实例的详细信息
3. **数据分析**：处理返回数据，筛选高度 > 5000mm 的墙体
4. **结果展示**：向用户呈现筛选后的结果和相关详细信息

## 主要功能

- **多维度过滤**: 支持按类别、类型、族、空间范围、可见性等条件进行组合过滤
- **智能查询**: 基于 AI 驱动的元素筛选逻辑
- **详细信息**: 返回完整的元素属性、几何信息和参数数据
- **性能优化**: 支持结果数量限制，避免大量数据的性能问题

## 核心组件

### 1. AIElementFilterCommand
- **类型**: ExternalEventCommandBase
- **命令名**: `ai_element_filter`
- **功能**: 接收过滤参数，触发异步查询操作

### 2. AIElementFilterEventHandler
- **类型**: IExternalEventHandler + IWaitableExternalEventHandler
- **功能**: 在 Revit 主线程中执行元素查询和过滤逻辑

## API 接口

### 命令调用

```json
{
  "data": {
    "filterCategory": "OST_Walls",
    "includeInstances": true,
    "includeTypes": false,
    "filterVisibleInCurrentView": true,
    "maxElements": 50
  }
}
```

### 参数说明 (FilterSetting)

| 参数名 | 类型 | 默认值 | 必填 | 说明 |
|--------|------|--------|------|------|
| `filterCategory` | string | null | 否 | Revit 内置类别名称 (如"OST_Walls", "OST_Doors", "OST_GenericModel") |
| `filterElementType` | string | null | 否 | 元素类型名称 (如"Wall", "Floor", "Autodesk.Revit.DB.Wall") |
| `filterFamilySymbolId` | number | -1 | 否 | 族类型的ElementId，使用-1表示不过滤 |
| `includeTypes` | boolean | false | 否 | 是否包含元素类型 |
| `includeInstances` | boolean | true | 否 | 是否包含元素实例 |
| `filterVisibleInCurrentView` | boolean | false | 否 | 仅返回当前视图可见元素 |
| `boundingBoxMin` | BoundingBoxPoint | null | 否 | 空间过滤最小边界点 (mm) |
| `boundingBoxMax` | BoundingBoxPoint | null | 否 | 空间过滤最大边界点 (mm) |
| `maxElements` | number | 50 | 否 | 返回元素数量限制，建议不超过50 |

#### BoundingBoxPoint 结构

```typescript
{
  p0: { x: number, y: number, z: number },  // 起始点坐标
  p1: { x: number, y: number, z: number }   // 结束点坐标
}
```

### 返回格式

```json
{
  "Success": true,
  "Message": "查询成功",
  "Response": [
    {
      "Id": 123456,
      "Category": "墙",
      "Type": "基本墙",
      "Level": "Level 1",
      "Location": { "x": 1000, "y": 2000, "z": 0 },
      "Parameters": {
        "长度": 5000.0,
        "高度": 3000.0,
        "宽度": 200.0
      },
      "Geometry": { /* 几何信息 */ }
    }
  ]
}
```

## 使用示例

### 1. 查询所有墙体实例

```json
{
  "data": {
    "filterCategory": "OST_Walls",
    "includeInstances": true,
    "includeTypes": false
  }
}
```

### 2. 查询指定空间范围内的门

```json
{
  "data": {
    "filterCategory": "OST_Doors",
    "includeInstances": true,
    "boundingBoxMin": {
      "p0": { "x": 0, "y": 0, "z": 0 },
      "p1": { "x": 5000, "y": 5000, "z": 2000 }
    },
    "boundingBoxMax": {
      "p0": { "x": 5000, "y": 5000, "z": 2000 },
      "p1": { "x": 10000, "y": 10000, "z": 4000 }
    }
  }
}
```

### 3. 查询当前视图可见的所有构件类型

```json
{
  "data": {
    "includeTypes": true,
    "includeInstances": false,
    "filterVisibleInCurrentView": true,
    "maxElements": 100
  }
}
```

## 支持的元素类别

常用的 Revit 内置类别包括：

- `OST_Walls` - 墙体
- `OST_Doors` - 门
- `OST_Windows` - 窗
- `OST_Floors` - 楼板
- `OST_Roofs` - 屋顶
- `OST_Columns` - 柱
- `OST_StructuralFraming` - 结构梁
- `OST_Furniture` - 家具
- `OST_GenericModel` - 常规模型
- `OST_MEPCurves` - MEP管线
- `OST_LightingFixtures` - 灯具

## 空间坐标系统

- **单位**: 毫米 (mm)
- **坐标系**: Revit 项目坐标系
- **转换公式**: Revit内部单位 × 304.8 = 毫米

## 性能注意事项

1. **结果限制**: 使用 `maxElements` 限制返回数量，避免内存溢出
2. **精确过滤**: 优先使用类别和类型过滤，减少无关元素的处理
3. **空间过滤**: 大模型中使用边界框过滤可显著提升性能
4. **可见性过滤**: 在复杂视图中启用可见性过滤可减少处理时间

## 错误处理

常见错误及解决方案：

- **参数为空**: 检查 FilterSetting 对象是否正确序列化
- **无效类别**: 确认使用正确的 Revit 内置类别名称
- **超时错误**: 减少查询范围或增加 `maxElements` 限制
- **权限错误**: 确保在有效的 Revit 文档上下文中执行

## 相关文件

- `AIElementFilterCommand.cs` - 命令入口点
- `AIElementFilterEventHandler.cs` - 查询逻辑实现
- `Models/Common/FilterSetting.cs` - 过滤参数模型
- `Models/Common/AIResult.cs` - 统一返回格式