# Core 字段构建器

## 概述

Core 模块负责构建元素的核心信息字段，包括类型、族、标高等需要额外 API 调用的信息。

## 字段分类

### 静态字段（不需要 Builder）
这些字段直接从元素属性获取，在 `ElementFieldRegistry` 中使用静态方法处理：

- `name` - Element.Name
- `category` - Element.Category.Name
- `builtInCategory` - BuiltInCategory 枚举值

### Builder 字段（需要额外 API 调用）
这些字段需要额外的 Revit API 调用，使用专门的 Builder 实现：

- `core.typeInfo` - 类型信息（需要 GetElement(typeId)）
- `core.familyInfo` - 族信息（需要访问 FamilySymbol.Family）
- `core.levelInfo` - 标高信息（需要 GetElement(levelId)）

## 构建器列表

| 字段名 | 构建器类 | 适用元素 | 依赖缓存 |
|--------|----------|----------|----------|
| core.typeInfo | TypeInfoFieldBuilder | 所有元素 | TypeElement |
| core.familyInfo | FamilyInfoFieldBuilder | 族实例 | TypeElement, Family |
| core.levelInfo | LevelInfoFieldBuilder | 有标高的元素 | Level |

## 缓存策略

Core 构建器使用 `FieldContext` 提供的缓存机制：

1. **TypeElement 缓存** - 避免重复调用 `doc.GetElement(typeId)`
2. **Family 缓存** - 避免重复访问 `FamilySymbol.Family`
3. **Level 缓存** - 避免重复查找标高信息

## 错误处理

所有 Core 构建器遵循"静默失败"原则：
- 构建失败时不抛出异常
- 不向结果添加空值或错误信息
- 仅在调试日志中记录错误

## 扩展指南

### 添加新的 Core 字段：

1. 确定是否需要额外 API 调用：
   - **不需要** → 在 `ElementFieldRegistry` 中添加静态处理
   - **需要** → 创建新的 Builder

2. 创建 Builder（如需要）：
   ```csharp
   public class NewCoreFieldBuilder : IFieldBuilder
   {
       public string FieldName => "core.newField";

       public bool CanBuild(Element element)
       {
           // 检查适用性
           return true;
       }

       public void Build(FieldContext ctx)
       {
           // 使用缓存获取数据
           var someData = ctx.GetCached("someKey", () => ...);
           ctx.Result["newField"] = someData;
       }
   }
   ```

3. 在 `ElementFieldRegistry.RegisterFieldBuilders()` 中注册

## 注意事项

- 所有 Core 字段都应该是元素的基本属性
- 避免在 Core 中包含几何或参数信息
- 确保字段名使用 `core.` 前缀
- 利用缓存机制避免重复 API 调用