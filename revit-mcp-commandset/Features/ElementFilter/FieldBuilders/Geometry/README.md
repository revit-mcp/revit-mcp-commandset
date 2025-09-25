# Geometry 字段构建器

## 概述

Geometry 模块负责构建元素的几何信息字段，包括边界框、位置、尺寸等空间相关数据。

## 字段分类

### 几何字段（Geometry Fields）
这些字段提供元素的空间信息，支持空间查询和分析：

- `geometry.boundingBox` - 边界框信息（最小/最大坐标点）
- `geometry.locationPoint` - 位置点坐标（适用于点定位元素）
- `geometry.locationCurve` - 位置曲线（适用于线性元素）
- `geometry.thickness` - 厚度信息（墙体、楼板等）
- `geometry.height` - 高度信息（墙体、柱子等）
- `geometry.width` - 宽度信息（门、窗、家具等）
- `geometry.area` - 面积信息（房间、楼板等）

## 构建器列表

| 字段名 | 构建器类 | 适用元素 | 单位 | 说明 |
|--------|----------|----------|------|------|
| geometry.boundingBox | BoundingBoxFieldBuilder | 所有元素 | mm | 6个坐标值：minX,minY,minZ,maxX,maxY,maxZ |
| geometry.locationPoint | LocationPointFieldBuilder | 点定位元素 | mm | 3个坐标值：x,y,z |
| geometry.locationCurve | LocationCurveFieldBuilder | 线性元素 | mm | 6个坐标值：startX,startY,startZ,endX,endY,endZ |
| geometry.thickness | ThicknessFieldBuilder | 墙体/楼板/屋顶/天花 | mm | 厚度值 |
| geometry.height | HeightFieldBuilder | 墙体/柱子/门窗 | mm | 高度值 |
| geometry.width | WidthFieldBuilder | 门/窗/家具 | mm | 宽度值 |
| geometry.area | AreaFieldBuilder | 房间/楼板/屋顶 | m² | 面积值 |

## 坐标系统和单位

### 单位规范
- **长度**: 毫米 (mm)
- **面积**: 平方米 (m²)
- **转换**: Revit 内部单位 × 304.8 = 毫米

### 坐标系统
- 使用 Revit 项目坐标系
- Z 轴向上为正
- 所有坐标值转换为毫米单位

## 适用性检查

每个构建器都实现了智能的适用性检查：

### BoundingBoxFieldBuilder
- 适用于所有元素
- 检查边界框是否有效

### LocationPointFieldBuilder
- 仅适用于 `LocationPoint` 类型的元素
- 如：族实例（门、窗、家具等）

### LocationCurveFieldBuilder
- 仅适用于 `LocationCurve` 类型的元素
- 如：墙体、管道、梁等线性元素

### ThicknessFieldBuilder
- 适用于：Wall、Floor、RoofBase、Ceiling
- 从对应的类型元素获取厚度信息

### HeightFieldBuilder
- 适用于：Wall、垂直族实例（柱子、门窗）
- 优先使用实例参数，回退到类型参数

### WidthFieldBuilder
- 适用于：门、窗、家具等有宽度概念的族实例
- 尝试多种宽度参数

### AreaFieldBuilder
- 适用于：Room、Space、Floor、RoofBase、Ceiling
- 根据元素类型选择合适的面积参数

## 错误处理

所有 Geometry 构建器遵循"静默失败"原则：
- 构建失败时不抛出异常
- 不向结果添加空值或错误信息
- 仅在调试日志中记录错误

## 性能优化

### 缓存策略
- BoundingBox 通过 `FieldContext.BoundingBox` 缓存
- 避免重复的几何计算
- 统一的单位转换

### 参数查找优化
- 使用参数ID数组进行批量查找
- 优先级排序：实例参数 → 类型参数 → 内置参数

## 扩展指南

### 添加新的 Geometry 字段：

1. 创建新的 Builder 类：
   ```csharp
   public class NewGeometryFieldBuilder : IFieldBuilder
   {
       public string FieldName => "geometry.newField";

       public bool CanBuild(Element element)
       {
           // 检查元素适用性
           return element is SomeElementType;
       }

       public void Build(FieldContext context)
       {
           try
           {
               // 获取几何数据
               var value = GetGeometryValue(context.Element);
               if (value.HasValue)
               {
                   // 转换单位并添加到结果
                   context.Result["newField"] = value.Value * 304.8;
               }
           }
           catch
           {
               // 静默失败
           }
       }
   }
   ```

2. 在 `ElementFieldRegistry.RegisterFieldBuilders()` 中注册

### 单位转换注意事项
- **长度**: `revitValue * 304.8` → 毫米
- **面积**: `revitValue * 304.8 * 304.8 / 1000000` → 平方米
- **体积**: `revitValue * 304.8³ / 10⁹` → 立方米

## 注意事项

1. **适用性检查**: 每个构建器都应该有准确的适用性判断
2. **单位一致性**: 确保所有输出使用统一的单位系统
3. **性能考虑**: 避免在构建器中进行复杂的几何计算
4. **兼容性**: 考虑不同 Revit 版本的 API 差异
5. **静默失败**: 异常情况不应该影响其他字段的构建