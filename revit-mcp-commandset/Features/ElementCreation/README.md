# ElementCreation 模块

## 模块概述

ElementCreation 模块负责在 Revit 模型中创建基于点定位的族实例元素。该模块专注于门、窗、设备、家具等需要精确定位的点状构件的批量创建功能，为 AI 系统提供了强大的模型构建能力。

## 主要功能

- **点状元素创建**: 支持门、窗、家具、设备等基于点定位的族实例创建
- **批量创建**: 一次调用可创建多个不同类型的元素
- **精确定位**: 基于坐标点的精确空间定位
- **参数控制**: 支持尺寸、标高、旋转等详细参数设置
- **族类型管理**: 基于族类型ID的类型选择机制

## 核心组件

### 1. CreatePointElementCommand
- **类型**: ExternalEventCommandBase
- **命令名**: `create_point_based_element`
- **功能**: 接收创建参数，触发异步创建操作

### 2. CreatePointElementEventHandler
- **类型**: IExternalEventHandler + IWaitableExternalEventHandler
- **功能**: 在 Revit 主线程中执行元素创建逻辑

## API 接口

### 命令调用

```json
{
  "data": [
    {
      "name": "入户门",
      "typeId": 123456,
      "locationPoint": { "x": 1000, "y": 2000, "z": 0 },
      "width": 900,
      "height": 2100,
      "baseLevel": 0,
      "baseOffset": 0,
      "rotation": 0
    }
  ]
}
```

### 参数说明 (PointElement Array)

| 参数名 | 类型 | 必填 | 说明 |
|--------|------|------|------|
| `name` | string | 是 | 元素描述名称 (如 "door", "window") |
| `typeId` | number | 否 | 族类型的ElementId |
| `locationPoint` | Point3D | 是 | 定位点坐标 (mm) |
| `width` | number | 是 | 宽度 (mm) |
| `height` | number | 是 | 高度 (mm) |
| `depth` | number | 否 | 深度 (mm) |
| `baseLevel` | number | 是 | 基准标高高度 |
| `baseOffset` | number | 是 | 相对基准标高的偏移量 |
| `rotation` | number | 否 | 旋转角度 (0-360度) |

#### Point3D 结构

```typescript
{
  x: number,  // X坐标 (mm)
  y: number,  // Y坐标 (mm)
  z: number   // Z坐标 (mm)
}
```

### 返回格式

```json
{
  "Success": true,
  "Message": "创建成功",
  "Response": {
    "CreatedCount": 2,
    "CreatedElements": [
      {
        "ElementId": 789012,
        "Name": "入户门",
        "Type": "单开门 900x2100",
        "Location": { "x": 1000, "y": 2000, "z": 0 },
        "Level": "Level 1"
      },
      {
        "ElementId": 789013,
        "Name": "客厅窗",
        "Type": "推拉窗 1200x1500",
        "Location": { "x": 3000, "y": 1000, "z": 800 },
        "Level": "Level 1"
      }
    ],
    "FailedElements": []
  }
}
```

## 使用示例

### 1. 创建单个门

```json
{
  "data": [
    {
      "name": "主卧门",
      "typeId": 123456,
      "locationPoint": { "x": 2000, "y": 1500, "z": 0 },
      "width": 800,
      "height": 2000,
      "baseLevel": 0,
      "baseOffset": 0,
      "rotation": 90
    }
  ]
}
```

### 2. 批量创建门窗

```json
{
  "data": [
    {
      "name": "入户门",
      "typeId": 123456,
      "locationPoint": { "x": 1000, "y": 2000, "z": 0 },
      "width": 900,
      "height": 2100,
      "baseLevel": 0,
      "baseOffset": 0
    },
    {
      "name": "客厅窗",
      "typeId": 234567,
      "locationPoint": { "x": 3000, "y": 1000, "z": 0 },
      "width": 1200,
      "height": 1500,
      "baseLevel": 0,
      "baseOffset": 800
    }
  ]
}
```

### 3. 创建家具设备

```json
{
  "data": [
    {
      "name": "办公桌",
      "typeId": 345678,
      "locationPoint": { "x": 1500, "y": 800, "z": 0 },
      "width": 1200,
      "height": 750,
      "depth": 600,
      "baseLevel": 0,
      "baseOffset": 0,
      "rotation": 0
    }
  ]
}
```

## 支持的元素类型

### 建筑构件
- **门**: 单开门、双开门、推拉门、旋转门等
- **窗**: 平开窗、推拉窗、固定窗、百叶窗等

### 结构构件
- **柱**: 结构柱、建筑柱
- **设备基础**: 独立基础、设备基座

### MEP构件
- **照明设备**: 筒灯、吸顶灯、壁灯等
- **电气设备**: 配电箱、开关、插座等
- **暖通设备**: 风口、温控器等
- **给排水设备**: 洁具、水龙头、地漏等

### 家具设备
- **办公家具**: 办公桌、椅子、文件柜等
- **生活家具**: 床、沙发、餐桌等
- **专用设备**: 医疗设备、实验设备等

## 坐标系统说明

### 定位坐标
- **单位**: 毫米 (mm)
- **坐标系**: Revit 项目坐标系
- **原点**: 项目基点或测量点
- **Z轴**: 垂直向上为正方向

### 标高系统
- **baseLevel**: 基准标高，通常为楼层标高
- **baseOffset**: 相对于基准标高的偏移量
- **实际标高** = baseLevel + baseOffset

### 旋转角度
- **单位**: 度 (°)
- **参考**: 以X轴正方向为0°
- **方向**: 逆时针为正

## 族类型管理

### 获取族类型ID
在创建元素前，需要先获取目标族类型的ElementId：

```json
{
  "command": "ai_element_filter",
  "data": {
    "filterCategory": "OST_Doors",
    "includeTypes": true,
    "includeInstances": false
  }
}
```

### 常用族类型
- **门类型**: 通过门族名称和尺寸规格确定
- **窗类型**: 通过窗族名称和开启方式确定
- **家具类型**: 通过家具族名称和尺寸规格确定

## 性能优化建议

### 批量创建
- **推荐**: 一次性创建多个元素，减少API调用次数
- **限制**: 单次创建建议不超过100个元素
- **分组**: 按族类型分组创建，提高效率

### 内存管理
- **及时释放**: 创建完成后及时释放临时对象
- **异常处理**: 确保异常情况下的资源清理

## 错误处理

### 常见错误类型

1. **族类型不存在**
   - 原因: 提供的typeId无效或族未加载
   - 解决: 先查询可用的族类型ID

2. **坐标无效**
   - 原因: 定位点超出模型范围或与现有元素冲突
   - 解决: 检查坐标合理性，避免重叠冲突

3. **参数无效**
   - 原因: 尺寸参数超出族的有效范围
   - 解决: 根据族的约束调整参数值

4. **标高错误**
   - 原因: baseLevel不存在或baseOffset过大
   - 解决: 确认项目中的标高设置

### 错误返回示例

```json
{
  "Success": false,
  "Message": "创建失败",
  "Response": {
    "CreatedCount": 1,
    "CreatedElements": [/* 成功创建的元素 */],
    "FailedElements": [
      {
        "Name": "客厅窗",
        "Error": "族类型ID无效",
        "TypeId": 999999
      }
    ]
  }
}
```

## 最佳实践

1. **预先验证**: 创建前验证族类型和坐标的有效性
2. **合理分组**: 按功能和位置对元素进行分组创建
3. **参数检查**: 确保尺寸参数在族的约束范围内
4. **碰撞检测**: 避免新元素与现有元素产生不合理的重叠
5. **标准化命名**: 使用清晰的元素描述名称便于后续管理

## 相关文件

- `CreatePointElementCommand.cs` - 创建命令入口
- `CreatePointElementEventHandler.cs` - 创建逻辑实现
- `Models/Geometry/JZPoint.cs` - 几何点模型
- `Models/Common/AIResult.cs` - 统一返回格式