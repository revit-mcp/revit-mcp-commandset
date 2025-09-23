# SystemElementCreation 功能模块

## 概述

SystemElementCreation 是 Revit MCP CommandSet 的重要功能模块，专门用于创建 Revit 系统族元素，如墙体、楼板等。该模块填补了项目在系统族创建方面的空白，与现有的 FamilyInstanceCreation 模块形成互补，为 AI 助手提供完整的 BIM 元素创建能力。

### 核心价值

- 🏗️ **系统族创建**：支持 Revit 系统族（墙、楼板）的智能创建
- 🎯 **精准控制**：基于 Revit 2019 API，提供稳定可靠的创建功能
- 🤖 **AI 友好**：结构化的参数分析和智能建议系统
- ⚡ **版本兼容**：支持 Revit 2019-2025，优先保证 2019 版本稳定性

## MCP 命令

### 1. create_system_element

创建系统族元素（墙体、楼板等）。

#### 请求格式

```json
{
  "data": {
    "elementType": "Wall",
    "typeId": 123456,
    "wallLine": {
      "p0": { "x": 0, "y": 0, "z": 0 },
      "p1": { "x": 5000, "y": 0, "z": 0 }
    },
    "height": 3000,
    "autoFindLevel": true
  }
}
```

#### 参数说明

核心参数：

| 参数 | 类型 | 必需 | 说明 |
|------|------|------|------|
| elementType | string | ✅ | 系统族类型（Wall/Floor） |
| typeId | int | ✅ | 系统族类型的 ElementId |

几何参数：

| 参数 | 类型 | 适用类型 | 说明 |
|------|------|----------|------|
| wallLine | JZLine | Wall | 墙体路径线段（毫米） |
| height | double | Wall | 墙体高度（毫米） |
| floorBoundary | JZPoint[] | Floor | 楼板边界点列表（毫米） |

控制参数：

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| levelId | int | 0 | 关联标高 ElementId |
| autoFindLevel | bool | true | 自动查找最近标高 |
| baseOffset | double | 0 | 底部偏移（毫米） |
| topOffset | double | 0 | 顶部偏移（毫米） |
| isStructural | bool | false | 是否为结构元素 |
| autoJoinWalls | bool | true | 自动连接相邻墙体 |
| slope | double? | null | 楼板坡度（百分比） |

### 2. get_system_element_suggestion

获取系统族创建参数建议。

#### 请求格式

```json
{
  "data": {
    "elementType": "Wall"
  }
}
```

或通过 typeId：

```json
{
  "data": {
    "typeId": 123456
  }
}
```

或获取全部类型：

```json
{
  "data": {}
}
```

## 支持的系统族类型

### 1. Wall（墙体）

创建建筑墙体，支持直线墙。

**必需参数**：
- `wallLine`: 墙体路径线段
- `height`: 墙体高度

**可选参数**：
- `baseOffset`: 底部偏移
- `isStructural`: 是否结构墙
- `autoJoinWalls`: 自动连接相邻墙体

**示例**：
```json
{
  "data": {
    "elementType": "Wall",
    "typeId": 123456,
    "wallLine": {
      "p0": { "x": 0, "y": 0, "z": 0 },
      "p1": { "x": 5000, "y": 0, "z": 0 }
    },
    "height": 3000,
    "levelId": 789,
    "baseOffset": 0,
    "isStructural": false,
    "autoFindLevel": true,
    "autoJoinWalls": true
  }
}
```

### 2. Floor（楼板）

创建建筑楼板，支持任意多边形轮廓。

**必需参数**：
- `floorBoundary`: 楼板边界点列表（至少3个点）

**可选参数**：
- `topOffset`: 顶部偏移
- `slope`: 楼板坡度
- `isStructural`: 是否结构楼板

**示例**：
```json
{
  "data": {
    "elementType": "Floor",
    "typeId": 234567,
    "floorBoundary": [
      { "x": 0, "y": 0, "z": 0 },
      { "x": 5000, "y": 0, "z": 0 },
      { "x": 5000, "y": 5000, "z": 0 },
      { "x": 0, "y": 5000, "z": 0 }
    ],
    "levelId": 789,
    "topOffset": 0,
    "isStructural": false,
    "autoFindLevel": true
  }
}
```

## 技术特性

### 版本兼容性策略

本模块采用保守的版本兼容性策略：

- **主要支持**：Revit 2019 API
- **兼容范围**：Revit 2019-2025
- **实施原则**：优先保证 2019 版本稳定，其他版本逐步扩展

### 单位转换

- **输入单位**：所有长度坐标使用**毫米（mm）**
- **内部转换**：自动转换为 Revit 内部单位（英尺）
- **转换比例**：1 英尺 = 304.8 毫米

### 智能功能

#### 自动标高查找
```csharp
// 当 autoFindLevel = true 且未指定 levelId 时
// 系统会查找距离几何中心Z坐标最近的标高
Level nearestLevel = GetNearestLevel(referenceZ / 304.8);
```

#### 自动墙体连接
```csharp
// 当 autoJoinWalls = true 时
// 系统会在容差范围内自动连接相邻墙体端点
// 容差：1mm = 0.00328英尺
```

#### 参数验证
- 墙体路径不能为零长度
- 墙体高度必须大于0
- 楼板轮廓必须闭合且不自交
- 楼板边界至少需要3个点

## 架构设计

### 模块结构

```
Features/SystemElementCreation/
├── CreateSystemElementCommand.cs              # MCP 创建命令
├── CreateSystemElementEventHandler.cs         # 创建事件处理器（集成智能功能）
├── GetSystemElementSuggestionCommand.cs       # MCP 建议命令
├── GetSystemElementSuggestionEventHandler.cs  # 建议事件处理器
└── README.md                                  # 本说明文档

Utils/SystemCreation/
└── SystemElementCreator.cs                    # 系统族创建器（核心 API 封装）

Models/Common/
├── SystemElementParameters.cs                 # 系统族创建参数
└── SystemElementSuggestion.cs                 # 参数建议响应
```

### 设计理念

与 FamilyInstanceCreation 模块保持一致的设计理念：

1. **简化架构**：智能功能直接集成到 EventHandler 中
2. **统一接口**：遵循 "data" 包裹层的接口规范
3. **版本控制**：通过条件编译处理不同 Revit 版本
4. **错误友好**：失败时自动生成参数建议

### 核心组件

#### SystemElementCreator
- **职责**：Revit API 直接操作，专注创建逻辑
- **特点**：版本兼容性处理、单位转换、几何验证

#### EventHandler
- **职责**：参数验证、智能建议、事务管理
- **特点**：失败即建议、错误分析、类型推荐

## 常见问题

### 创建相关

#### 1. "墙体创建需要指定路径线段"
**原因**：缺少 wallLine 参数
**解决**：提供有效的起点和终点坐标

#### 2. "楼板边界至少需要3个点"
**原因**：floorBoundary 点数不足
**解决**：确保至少提供3个边界点

#### 3. "楼板轮廓必须形成闭合区域"
**原因**：边界点无法形成闭合轮廓
**解决**：检查点的顺序和连续性

### 版本相关

#### 4. "当前Revit版本不支持"
**原因**：使用了不支持的 Revit 版本
**解决**：使用 Revit 2019-2025 版本

### 参数相关

#### 5. "无效的墙体类型ID"
**原因**：typeId 不对应有效的 WallType
**解决**：使用正确的系统族类型 ElementId

## 使用建议

### 最佳实践

1. **类型选择**：创建前先获取可用的系统族类型列表
2. **参数验证**：使用 suggestion 命令预检查参数要求
3. **几何合理性**：确保墙体路径和楼板轮廓符合建筑逻辑
4. **标高关联**：优先使用 autoFindLevel 功能

### 测试用例

#### 简单墙体
```json
{
  "data": {
    "elementType": "Wall",
    "typeId": 123456,
    "wallLine": {
      "p0": { "x": 0, "y": 0, "z": 0 },
      "p1": { "x": 3000, "y": 0, "z": 0 }
    },
    "height": 2700
  }
}
```

#### 矩形楼板
```json
{
  "data": {
    "elementType": "Floor",
    "typeId": 234567,
    "floorBoundary": [
      { "x": 0, "y": 0, "z": 0 },
      { "x": 4000, "y": 0, "z": 0 },
      { "x": 4000, "y": 3000, "z": 0 },
      { "x": 0, "y": 3000, "z": 0 }
    ]
  }
}
```

## 扩展规划

### Phase 2（预留）
- 天花板（Ceiling）创建
- 屋顶（Roof）创建
- 楼梯（Stair）创建

### Phase 3（未来）
- MEP 系统族（管道、风管）
- 结构系统族（梁、柱）
- 场地系统族（地形、道路）

## 版本历史

### v1.0.0 (2025-09-23)
- 初始版本发布
- 支持墙体（Wall）和楼板（Floor）创建
- 基于 Revit 2019 API 的稳定实现
- 完整的参数验证和智能建议功能
- 与 FamilyInstanceCreation 模块一致的架构设计

---

本模块与 [FamilyInstanceCreation](../FamilyInstanceCreation/README.md) 模块共同构成了完整的 Revit 元素创建生态系统，为 AI 助手提供了强大的 BIM 建模能力。