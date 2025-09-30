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

- **多维度过滤**: 支持按类别、类型、族、名称关键字、空间范围、可见性等条件进行组合过滤
- **名称关键字搜索**: 支持按关键字搜索元素名称、类型名称、族名称（不区分大小写）
- **智能查询**: 基于 AI 驱动的元素筛选逻辑
- **节点化架构**: 统一的数据节点组织，便于AI理解和处理
- **详细信息**: 返回完整的元素属性、几何信息和参数数据
- **性能优化**: 支持结果数量限制，避免大量数据的性能问题

## 节点化数据架构 v2.0

ElementFilter 采用**节点化数据组织**模式，将元素信息分类存储在不同节点中，提供更清晰的数据结构：

### 核心数据节点

| 节点名 | 内容 | 说明 |
|--------|------|------|
| `identity` | name、category、builtInCategory | 元素身份标识信息 |
| `type` | typeId、typeName、familyId*、familyName* | **统一的类型信息节点** |
| `geometry` | location、boundingBox、thickness、height、area等 | **统一的几何信息节点** |
| `level` | levelId、levelName | 所属标高信息 |
| `parameters` | instance、type 参数分类 | 元素参数信息 |

**注**：
- `*` 族实例专有字段，系统族元素不包含
- `family` 字段已**废弃**，族信息现在统一在 `type` 节点内

### 字段查询系统

支持两种字段请求方式：

#### 1. 原子字段 (fields)
精确控制返回的数据字段：
```json
"fields": ["identity", "type", "geometry.location", "geometry.thickness"]
```

#### 2. 预设组合 (fieldPresets)
快速获取常用字段组合：
```json
"fieldPresets": ["spatialAnalysis", "typeAnalysis"]
```

### 可用字段列表

**基础节点字段**：
- `identity` - 身份信息
- `type` - 类型信息（包含族信息）
- `level` - 标高信息

**几何节点字段**：
- `geometry.location` - 位置信息（自动识别点/线）
- `geometry.boundingBox` - 包围盒
- `geometry.profile` - 轮廓信息（楼板等）
- `geometry.thickness` - 厚度
- `geometry.height` - 高度
- `geometry.width` - 宽度
- `geometry.area` - 面积

### 预设字段组合

| 预设名 | 包含字段 | 适用场景 |
|--------|----------|----------|
| `listDisplay` | identity | 简单列表显示 |
| `typeAnalysis` | identity + type | 类型分析 |
| `spatialAnalysis` | identity + geometry.location + geometry.boundingBox | 空间分析 |
| `detailView` | identity + type + level | 详细视图 |
| `familyAnalysis` | identity + type + family(废弃) | 族分析 |
| `floorAnalysis` | identity + geometry.profile + geometry.boundingBox | 楼板分析 |

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

**基础过滤查询**：
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

**节点化字段查询**：
```json
{
  "data": {
    "elementIds": [123456, 789012],
    "fields": ["identity", "type", "geometry.location", "geometry.thickness"],
    "fieldPresets": ["spatialAnalysis"],
    "parameters": {
      "includeInstance": true,
      "includeType": true,
      "flatten": true
    }
  }
}
```

### 参数说明 (FilterSetting)

#### 基础过滤参数

| 参数名 | 类型 | 默认值 | 必填 | 说明 |
|--------|------|--------|------|------|
| `filterCategory` | string | null | 否 | Revit 内置类别名称 (如"OST_Walls", "OST_Doors", "OST_GenericModel") |
| `filterElementType` | string | null | 否 | 元素类型名称 (如"Wall", "Floor", "Autodesk.Revit.DB.Wall") |
| `filterTypeId` | number | -1 | 否 | **统一的类型ElementId过滤**。对于族实例，匹配其FamilySymbol的ElementId；对于系统族实例（如墙、楼板），匹配其WallType、FloorType等类型的ElementId；对于类型元素本身，匹配元素自身的ElementId。使用-1表示不过滤 |
| `filterNameKeyword` | string | null | 否 | 名称关键字过滤条件，检查元素名、类型名、族名是否包含关键字（不区分大小写） |
| `includeTypes` | boolean | false | 否 | 是否包含元素类型 |
| `includeInstances` | boolean | true | 否 | 是否包含元素实例 |
| `filterVisibleInCurrentView` | boolean | false | 否 | 仅返回当前视图可见元素 |
| `boundingBoxMin` | BoundingBoxPoint | null | 否 | 空间过滤最小边界点 (mm) |
| `boundingBoxMax` | BoundingBoxPoint | null | 否 | 空间过滤最大边界点 (mm) |
| `maxElements` | number | 50 | 否 | 返回元素数量限制，建议不超过50 |
| `elementIds` | number[] | null | 否 | 直接查询指定ElementId的元素 |

#### 节点化字段参数

| 参数名 | 类型 | 默认值 | 必填 | 说明 |
|--------|------|--------|------|------|
| `fields` | string[] | null | 否 | 原子字段列表，精确控制返回字段 |
| `fieldPresets` | string[] | null | 否 | 预设字段组合，快速获取常用字段 |
| `parameters` | ParameterOptions | null | 否 | 参数查询配置 |

#### 参数查询配置 (ParameterOptions)

| 参数名 | 类型 | 默认值 | 必填 | 说明 |
|--------|------|--------|------|------|
| `includeInstance` | boolean | true | 否 | 包含实例参数 |
| `includeType` | boolean | false | 否 | 包含类型参数 |
| `includeBuiltIn` | boolean | false | 否 | 包含内置参数 |
| `flatten` | boolean | true | 否 | 扁平化参数值格式 |
| `names` | string[] | null | 否 | 指定参数名称列表 |
| `singleName` | string | null | 否 | 单个参数名称 |

#### BoundingBoxPoint 结构

```typescript
{
  p0: { x: number, y: number, z: number },  // 起始点坐标
  p1: { x: number, y: number, z: number }   // 结束点坐标
}
```

### 返回格式

#### 节点化结构返回示例

**门（族实例）**：
```json
{
  "Success": true,
  "Message": "查询成功",
  "Response": [
    {
      "elementId": 333593,
      "identity": {
        "name": "750 x 2000mm",
        "category": "门",
        "builtInCategory": -2000023
      },
      "type": {
        "typeId": 94654,
        "typeName": "750 x 2000mm",
        "familyId": 242453,
        "familyName": "单扇 - 与墙齐"
      },
      "geometry": {
        "location": {
          "point": { "x": 1337, "y": 1745, "z": 0 }
        },
        "boundingBox": {
          "min": { "x": 886, "y": 895, "z": 0 },
          "max": { "x": 1788, "y": 1870, "z": 2075 }
        },
        "width": 750,
        "height": 2000
      },
      "level": {
        "levelId": 311,
        "levelName": "标高 1"
      }
    }
  ]
}
```

**墙（系统族）**：
```json
{
  "Success": true,
  "Message": "查询成功",
  "Response": [
    {
      "elementId": 333530,
      "identity": {
        "name": "常规 - 200mm",
        "category": "墙",
        "builtInCategory": -2000011
      },
      "type": {
        "typeId": 398,
        "typeName": "常规 - 200mm"
      },
      "geometry": {
        "location": {
          "line": {
            "p0": { "x": -3512, "y": 1745, "z": 0 },
            "p1": { "x": 6187, "y": 1745, "z": 0 }
          }
        },
        "thickness": 200,
        "height": 8000
      },
      "parameters": {
        "instance": {
          "长度": 9700,
          "高度": 8000,
          "面积": 76.1
        },
        "type": {
          "厚度": 200,
          "功能": 1
        }
      }
    }
  ]
}
```

## 使用示例

### 1. 基础查询：所有墙体实例

```json
{
  "data": {
    "filterCategory": "OST_Walls",
    "includeInstances": true,
    "includeTypes": false
  }
}
```

### 2. 节点化查询：获取门的详细信息

```json
{
  "data": {
    "filterCategory": "OST_Doors",
    "includeInstances": true,
    "fields": ["identity", "type", "geometry.location", "geometry.width", "geometry.height"],
    "maxElements": 10
  }
}
```

### 3. 预设查询：空间分析

```json
{
  "data": {
    "elementIds": [333593, 333530],
    "fieldPresets": ["spatialAnalysis", "typeAnalysis"]
  }
}
```

### 4. 参数查询：获取墙体的实例和类型参数

```json
{
  "data": {
    "filterCategory": "OST_Walls",
    "includeInstances": true,
    "fields": ["identity", "type"],
    "parameters": {
      "includeInstance": true,
      "includeType": true,
      "flatten": false
    }
  }
}
```

### 5. 混合查询：楼板轮廓分析

```json
{
  "data": {
    "filterCategory": "OST_Floors",
    "fields": ["geometry.profile", "geometry.area"],
    "fieldPresets": ["floorAnalysis"]
  }
}
```

### 6. 指定ElementId查询

```json
{
  "data": {
    "elementIds": [333593],
    "fields": ["identity", "type"],
    "parameters": {
      "names": ["高度", "宽度"]
    }
  }
}
```

### 7. 名称关键字过滤：搜索包含"300"的墙体

```json
{
  "data": {
    "filterCategory": "OST_Walls",
    "filterNameKeyword": "300",
    "includeInstances": true,
    "fields": ["identity", "type"]
  }
}
```

### 8. 组合过滤：搜索当前视图可见且名称包含"单扇"的门

```json
{
  "data": {
    "filterCategory": "OST_Doors",
    "filterNameKeyword": "单扇",
    "filterVisibleInCurrentView": true,
    "includeInstances": true,
    "fields": ["identity", "type", "geometry.location"]
  }
}
```

### 9. 类型ID过滤：查询特定类型的族实例

```json
{
  "data": {
    "filterTypeId": 94654,
    "includeInstances": true,
    "includeTypes": false,
    "fields": ["identity", "type", "geometry.location"]
  }
}
```
**说明**: 查询所有使用 ElementId 为 94654 的类型的族实例

### 10. 类型ID过滤：查询特定类型的系统族实例（如墙体）

```json
{
  "data": {
    "filterTypeId": 398,
    "filterCategory": "OST_Walls",
    "includeInstances": true,
    "fields": ["identity", "type", "geometry.location", "geometry.thickness"]
  }
}
```
**说明**: 查询所有使用 WallType ElementId 为 398 的墙体实例

### 11. 类型元素查询：直接查询类型元素本身

```json
{
  "data": {
    "filterTypeId": 398,
    "includeTypes": true,
    "includeInstances": false,
    "fields": ["identity", "type"]
  }
}
```
**说明**: 当查询类型元素时，filterTypeId 匹配类型元素自身的 ElementId

## 名称关键字过滤详解

### 功能说明

`filterNameKeyword` 参数提供了灵活的名称搜索功能，可以按关键字匹配以下名称字段：

1. **元素名称** - `Element.Name`
2. **类型名称** - `ElementType.Name`
3. **族名称** - `Family.Name`（仅族实例）

### 匹配规则

- **包含匹配**: 只要任一名称字段包含关键字即匹配
- **不区分大小写**: 中英文关键字都支持大小写不敏感搜索
- **组合过滤**: 可与其他过滤条件（类别、类型、可见性等）配合使用

### 使用场景示例

#### 场景1: 搜索特定尺寸的墙体
```json
{
  "data": {
    "filterCategory": "OST_Walls",
    "filterNameKeyword": "300",
    "includeInstances": true
  }
}
```
**匹配**: "常规-300mm"、"300x500"、"基本墙-300" 等

#### 场景2: 搜索特定类型的门
```json
{
  "data": {
    "filterCategory": "OST_Doors",
    "filterNameKeyword": "单扇",
    "includeInstances": true,
    "includeTypes": false
  }
}
```
**匹配**: 族名包含"单扇"的所有门实例

#### 场景3: 搜索特定材质的元素
```json
{
  "data": {
    "filterNameKeyword": "不锈钢",
    "includeInstances": true
  }
}
```
**匹配**: 所有名称、类型名或族名包含"不锈钢"的元素

### 技术细节

**族实例匹配逻辑**：
```
检查顺序：
1. Element.Name (如 "750 x 2000mm [123456]")
2. FamilySymbol.Name (如 "750 x 2000mm")
3. Family.Name (如 "单扇 - 与墙齐")
```

**系统族匹配逻辑**：
```
检查顺序：
1. Element.Name (如 "墙 [234567]")
2. ElementType.Name (如 "常规 - 300mm")
```

只要任一字段匹配，元素即被选中。

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