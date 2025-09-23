# SystemElementCreation 模块重构方案

## 一、背景与问题

### 当前问题
1. **参数结构混乱**：`SystemElementParameters` 中墙体和楼板参数混在一起，使用时需要大量条件判断
2. **数据模型冗余**：`SystemElementSuggestion` 与 `FamilyCreationRequirements` 功能重复
3. **架构一致性不足**：与现有 `FamilyInstanceCreation` 模块的数据结构不统一

### 设计约束
- 需要与 MCP 服务端的 zod schema 定义兼容
- 服务端需要明确的 JSON 结构定义（不能使用继承多态）
- 保持与现有 FamilyInstanceCreation 的一致性

## 二、核心设计方案

### 2.1 参数结构设计（组合方案）

考虑到 MCP 服务端需要明确的 JSON schema 定义，采用**组合模式**而非继承：

```csharp
namespace RevitMCPCommandSet.Models.Common
{
    /// <summary>
    /// 系统族创建参数（统一结构）
    /// </summary>
    public class SystemElementParameters
    {
        /// <summary>
        /// 元素类型（必需）
        /// </summary>
        [JsonProperty("elementType")]
        public string ElementType { get; set; }  // "wall", "floor", "ceiling", "roof"

        /// <summary>
        /// 系统族类型ID（必需）
        /// </summary>
        [JsonProperty("typeId")]
        public int TypeId { get; set; }

        /// <summary>
        /// 关联标高ID（可选）
        /// </summary>
        [JsonProperty("levelId")]
        public int LevelId { get; set; } = -1;

        /// <summary>
        /// 自动查找最近标高（可选）
        /// </summary>
        [JsonProperty("autoFindLevel")]
        public bool AutoFindLevel { get; set; } = true;

        /// <summary>
        /// 是否为结构元素（可选）
        /// </summary>
        [JsonProperty("isStructural")]
        public bool IsStructural { get; set; } = false;

        // ========== 墙体专用参数 ==========

        /// <summary>
        /// 墙体路径（墙体必需）
        /// </summary>
        [JsonProperty("path")]
        public JZLine Path { get; set; }

        /// <summary>
        /// 墙体高度（墙体必需，毫米）
        /// </summary>
        [JsonProperty("height")]
        public double? Height { get; set; }

        /// <summary>
        /// 底部偏移（墙体可选，毫米）
        /// </summary>
        [JsonProperty("baseOffset")]
        public double BaseOffset { get; set; } = 0;

        /// <summary>
        /// 自动连接相邻墙体（墙体可选）
        /// </summary>
        [JsonProperty("autoJoinWalls")]
        public bool AutoJoinWalls { get; set; } = true;

        // ========== 楼板专用参数 ==========

        /// <summary>
        /// 楼板边界（楼板必需）
        /// </summary>
        [JsonProperty("boundary")]
        public List<JZPoint> Boundary { get; set; }

        /// <summary>
        /// 顶部偏移（楼板可选，毫米）
        /// </summary>
        [JsonProperty("topOffset")]
        public double TopOffset { get; set; } = 0;

        /// <summary>
        /// 楼板坡度（楼板可选，百分比）
        /// </summary>
        [JsonProperty("slope")]
        public double? Slope { get; set; }
    }
}
```

### 2.2 参数验证辅助类

为了保持代码清晰，创建验证辅助类：

```csharp
namespace RevitMCPCommandSet.Utils.SystemCreation
{
    /// <summary>
    /// 系统族参数验证器
    /// </summary>
    public static class SystemElementValidator
    {
        public static bool Validate(SystemElementParameters parameters, out string error)
        {
            error = null;

            if (parameters.TypeId <= 0)
            {
                error = "必须指定有效的类型ID";
                return false;
            }

            switch (parameters.ElementType?.ToLower())
            {
                case "wall":
                    return ValidateWallParameters(parameters, out error);
                case "floor":
                    return ValidateFloorParameters(parameters, out error);
                default:
                    error = $"不支持的元素类型: {parameters.ElementType}";
                    return false;
            }
        }

        private static bool ValidateWallParameters(SystemElementParameters p, out string error)
        {
            error = null;

            if (p.Path == null)
            {
                error = "墙体创建需要指定路径（path）";
                return false;
            }

            if (!p.Height.HasValue || p.Height.Value <= 0)
            {
                error = "墙体高度必须大于0";
                return false;
            }

            return true;
        }

        private static bool ValidateFloorParameters(SystemElementParameters p, out string error)
        {
            error = null;

            if (p.Boundary == null || p.Boundary.Count < 3)
            {
                error = "楼板边界至少需要3个点";
                return false;
            }

            return true;
        }
    }
}
```

### 2.3 更新 SystemElementCreator

```csharp
public class SystemElementCreator
{
    private readonly Document _document;

    public SystemElementCreator(Document document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 创建系统族元素（统一入口）
    /// </summary>
    public Element Create(SystemElementParameters parameters)
    {
        // 参数验证
        if (!SystemElementValidator.Validate(parameters, out string error))
        {
            throw new ArgumentException(error);
        }

        // 根据类型分发
        return parameters.ElementType?.ToLower() switch
        {
            "wall" => CreateWall(parameters),
            "floor" => CreateFloor(parameters),
            _ => throw new NotSupportedException($"不支持的元素类型: {parameters.ElementType}")
        };
    }

    private Wall CreateWall(SystemElementParameters parameters)
    {
        // 获取墙体类型
        var wallType = _document.GetElement(new ElementId(parameters.TypeId)) as WallType;
        if (wallType == null)
            throw new ArgumentException($"无效的墙体类型ID: {parameters.TypeId}");

        // 获取或查找标高
        Level level = GetOrFindLevel(parameters);

        // 转换坐标（毫米转英尺）
        var startPoint = JZPoint.ToXYZ(parameters.Path.P0);
        var endPoint = JZPoint.ToXYZ(parameters.Path.P1);
        var line = Line.CreateBound(startPoint, endPoint);

        // 转换高度和偏移
        double heightInFeet = parameters.Height.Value / 304.8;
        double offsetInFeet = parameters.BaseOffset / 304.8;

        // 创建墙体
        Wall wall = Wall.Create(
            _document,
            line,
            wallType.Id,
            level.Id,
            heightInFeet,
            offsetInFeet,
            false,
            parameters.IsStructural
        );

        // 自动连接
        if (parameters.AutoJoinWalls && wall != null)
        {
            JoinNearbyWalls(wall);
        }

        return wall;
    }

    private Floor CreateFloor(SystemElementParameters parameters)
    {
        // 类似的实现...
    }
}
```

## 三、数据模型统一

### 3.1 删除冗余类
- 删除 `SystemElementSuggestion.cs`
- 删除 `SystemParameterInfo` 类

### 3.2 复用现有模型

使用现有的 `FamilyCreationRequirements` 处理参数建议：

```csharp
// GetSystemElementSuggestionEventHandler.cs
private FamilyCreationRequirements GenerateSuggestion(string elementType)
{
    var requirements = new FamilyCreationRequirements
    {
        TypeId = 0,  // 由客户端填充
        FamilyName = elementType == "wall" ? "墙体系统族" : "楼板系统族",
        Parameters = new Dictionary<string, ParameterInfo>(),
        Message = null
    };

    // 添加公共参数
    requirements.Parameters["elementType"] = new ParameterInfo
    {
        Required = true,
        Description = "元素类型（wall/floor）"
    };

    requirements.Parameters["typeId"] = new ParameterInfo
    {
        Required = true,
        Description = "系统族类型ID"
    };

    // 根据类型添加特定参数
    if (elementType == "wall")
    {
        requirements.Parameters["path"] = new ParameterInfo
        {
            Required = true,
            Description = "墙体路径线段（毫米）"
        };

        requirements.Parameters["height"] = new ParameterInfo
        {
            Required = true,
            Description = "墙体高度（毫米）"
        };

        requirements.Parameters["baseOffset"] = new ParameterInfo
        {
            Required = false,
            Description = "底部偏移（毫米，默认0）"
        };
    }
    else if (elementType == "floor")
    {
        requirements.Parameters["boundary"] = new ParameterInfo
        {
            Required = true,
            Description = "楼板边界点列表（毫米）"
        };

        requirements.Parameters["topOffset"] = new ParameterInfo
        {
            Required = false,
            Description = "顶部偏移（毫米，默认0）"
        };
    }

    return requirements;
}
```

## 四、MCP 服务端 Schema 定义

在 `revit-mcp/src/tools/create_system_element.ts` 中定义：

```typescript
import { z } from "zod";

// 系统族创建参数 schema
const SystemElementParametersSchema = z.object({
  elementType: z
    .enum(["wall", "floor", "ceiling", "roof"])
    .describe("System family element type"),

  typeId: z
    .number()
    .describe("ElementId of the system family type (WallType/FloorType)"),

  levelId: z
    .number()
    .optional()
    .default(-1)
    .describe("Associated level ElementId"),

  autoFindLevel: z
    .boolean()
    .optional()
    .default(true)
    .describe("Automatically find nearest level"),

  isStructural: z
    .boolean()
    .optional()
    .default(false)
    .describe("Whether the element is structural"),

  // Wall-specific parameters
  path: z
    .object({
      p0: z.object({
        x: z.number(),
        y: z.number(),
        z: z.number()
      }),
      p1: z.object({
        x: z.number(),
        y: z.number(),
        z: z.number()
      })
    })
    .optional()
    .describe("Wall path line (required for walls, in mm)"),

  height: z
    .number()
    .optional()
    .describe("Wall height (required for walls, in mm)"),

  baseOffset: z
    .number()
    .optional()
    .default(0)
    .describe("Base offset from level (in mm)"),

  autoJoinWalls: z
    .boolean()
    .optional()
    .default(true)
    .describe("Automatically join adjacent walls"),

  // Floor-specific parameters
  boundary: z
    .array(z.object({
      x: z.number(),
      y: z.number(),
      z: z.number()
    }))
    .optional()
    .describe("Floor boundary points (required for floors, in mm)"),

  topOffset: z
    .number()
    .optional()
    .default(0)
    .describe("Top offset from level (in mm)"),

  slope: z
    .number()
    .optional()
    .describe("Floor slope percentage")
});

// 注册工具
server.tool(
  "create_system_element",
  "Creates system family elements (walls, floors, etc.) in Revit",
  {
    data: SystemElementParametersSchema
  },
  async ({ data }) => {
    return await withRevitConnection(async (connection) => {
      return await connection.executeCommand("create_system_element", data);
    });
  }
);
```

## 五、实施步骤

### 第一阶段：重构参数类（1天）
1. 修改 `SystemElementParameters.cs` 采用组合结构
2. 创建 `SystemElementValidator.cs` 验证器
3. 更新相关的 using 和引用

### 第二阶段：更新业务逻辑（1天）
1. 修改 `SystemElementCreator.Create()` 使用新参数结构
2. 更新 `CreateSystemElementEventHandler` 的验证逻辑
3. 调整参数解析和错误处理

### 第三阶段：统一数据模型（0.5天）
1. 删除 `SystemElementSuggestion.cs`
2. 删除 `SystemParameterInfo` 类
3. 修改建议生成使用 `FamilyCreationRequirements`

### 第四阶段：测试验证（0.5天）
1. 编译验证
2. 测试墙体创建
3. 测试楼板创建
4. 测试参数建议功能

## 六、使用示例

### 创建墙体
```json
{
  "data": {
    "elementType": "wall",
    "typeId": 123456,
    "path": {
      "p0": { "x": 0, "y": 0, "z": 0 },
      "p1": { "x": 5000, "y": 0, "z": 0 }
    },
    "height": 3000,
    "autoFindLevel": true,
    "autoJoinWalls": true
  }
}
```

### 创建楼板
```json
{
  "data": {
    "elementType": "floor",
    "typeId": 234567,
    "boundary": [
      { "x": 0, "y": 0, "z": 0 },
      { "x": 5000, "y": 0, "z": 0 },
      { "x": 5000, "y": 5000, "z": 0 },
      { "x": 0, "y": 5000, "z": 0 }
    ],
    "autoFindLevel": true
  }
}
```

## 七、优势总结

1. **MCP 兼容**：单一平面结构，易于在服务端定义 schema
2. **简单直接**：没有复杂的继承关系
3. **类型安全**：通过验证器保证参数正确性
4. **易于扩展**：新增元素类型只需添加字段和验证逻辑
5. **统一模型**：复用现有的数据结构，减少代码冗余

## 八、注意事项

1. **向后兼容**：本次重构不考虑向后兼容，直接替换现有实现
2. **参数验证**：所有验证逻辑集中在 `SystemElementValidator` 中
3. **服务端同步**：需要同步更新 MCP 服务端的 schema 定义
4. **文档更新**：重构完成后需要更新相关的 README 文档

---

*最后更新：2024-09-23*
*作者：Claude Assistant*