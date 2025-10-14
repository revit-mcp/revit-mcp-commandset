# Utils 工具类层

## 目录结构

```
Utils/
├── ParameterHelper.cs     # 参数处理辅助（自动单位转换）
├── GeometryUtils.cs       # 几何计算工具
├── ProjectUtils.cs        # 项目工具类
├── FamilyCreation/        # 族创建工具集
│   └── FamilyInstanceCreator.cs
└── SystemCreation/        # 系统族创建工具集
    ├── SystemElementCreator.cs
    └── SystemElementValidator.cs
```

## 核心工具类

### ParameterHelper（P1 完成）

**用途**：Revit 参数自动单位转换和处理

**核心功能**：
- **自动单位转换**：根据参数类型自动进行单位转换
- **双向转换**：支持 Revit 内部单位 ↔ 用户单位
- **类型识别**：自动识别长度、角度、面积、体积等参数类型

**转换规则**：
| 参数类型 | 转换公式 | 示例 |
|---------|---------|------|
| 长度 | mm ÷ 304.8 = ft | 1000mm → 3.28ft |
| 角度 | ° × π/180 = rad | 90° → 1.57rad |
| 面积 | mm² ÷ (304.8)² | 1000000mm² → 10.76ft² |
| 体积 | mm³ ÷ (304.8)³ | - |

**使用示例**：
```csharp
// 自动转换为 Revit 内部单位
double revitValue = ParameterHelper.ConvertToRevitUnits(1000, ParameterType.Length);
// 结果：3.28084 (ft)

// 获取参数并自动转换
double userValue = ParameterHelper.GetParameterValue(parameter, true);
// 自动识别类型并转换为用户单位
```

### GeometryUtils

**用途**：几何计算和转换工具

**主要方法**：
- `ToRevitXYZ(JZPoint point)`：JZPoint 转 XYZ
- `ToJZPoint(XYZ xyz)`：XYZ 转 JZPoint
- `CalculateDistance(JZPoint p1, JZPoint p2)`：计算距离
- `GetBoundingBox(Element element)`：获取元素包围盒
- `IsPointInBoundingBox(JZPoint point, BoundingBoxInfo box)`：点是否在包围盒内

### ProjectUtils

**用途**：项目级别的工具方法

**主要功能**：
- 获取所有标高
- 查找最近标高
- 获取项目信息
- 视图相关操作

## FamilyCreation 族创建工具

### FamilyInstanceCreator

**用途**：统一的族实例创建工具

**支持的放置类型**：
1. **OneLevelBased** - 基于标高的族（独立族）
2. **OneLevelBasedHosted** - 基于标高的宿主族（如门窗）
3. **TwoLevelsBased** - 两个标高之间的族（如结构柱）
4. **WorkPlaneBased** - 基于工作平面的族
5. **CurveBased** - 基于线的族
6. **ViewBased** - 基于视图的族（注释）
7. **CurveBasedDetail** - 基于线的详图族
8. **CurveDrivenStructural** - 结构线驱动族

**智能功能**：
- 自动检测族放置类型
- 自动查找合适的宿主
- 自动查找最近标高
- 参数验证和错误处理

## SystemCreation 系统族创建工具

### SystemElementCreator

**用途**：系统族元素创建（墙、楼板、屋顶等）

**支持的元素类型**：
- **Wall** - 墙体创建
- **Floor** - 楼板创建
- **Roof** - 屋顶创建
- **Ceiling** - 天花板创建

**创建方法**：
```csharp
// 创建墙体
Wall wall = SystemElementCreator.CreateWall(
    doc, wallType, line, level, height, baseOffset
);

// 创建楼板
Floor floor = SystemElementCreator.CreateFloor(
    doc, floorType, boundary, level, structural
);
```

### SystemElementValidator

**用途**：系统族创建参数验证

**验证内容**：
- 类型有效性
- 几何参数合理性
- 标高存在性
- 边界闭合性（楼板、屋顶）

## 使用规范

### 错误处理

所有工具类方法应：
- 验证输入参数
- 捕获并包装异常
- 返回有意义的错误信息
- 记录详细日志（如需要）

### 性能考虑

- 批量操作时使用事务组
- 缓存常用查询结果
- 避免重复的元素过滤
- 使用 ElementId 而非 Element 传递

### 扩展点

新增工具类时：
1. 在 Utils 下创建新类文件
2. 遵循现有命名规范
3. 提供完整的 XML 注释
4. 添加单元测试（Test/）
5. 更新本 README

## 相关文档

- **单位转换详情**：ParameterHelper.cs 源码
- **族创建详情**：FamilyCreation/README.md
- **系统族创建**：Features/ElementCreation/README.md
- **几何模型**：Models/Geometry/README.md