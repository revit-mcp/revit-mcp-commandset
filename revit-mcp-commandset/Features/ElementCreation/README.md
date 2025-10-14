# ElementCreation 统一命令模块

## 模块概述

本模块实现了Revit MCP CommandSet的统一元素创建系统，将原本的4个分离命令合并为2个统一命令：

**合并前的4个命令：**
- `create_family_instance` (族实例创建)
- `get_family_creation_suggestion` (族创建参数建议)
- `create_system_element` (系统族创建)
- `get_system_element_suggestion` (系统族参数建议)

**合并后的2个命令：**
- `create_element` (统一元素创建)
- `get_element_creation_suggestion` (统一参数建议)

## 技术架构

### 设计方案
采用 **单一Handler + 统一参数模型** 架构：

```
MCP Client (AI)
    ↓ ElementCreationParameters
[CreateElementCommand]
    ↓
[CreateElementEventHandler] ← 单一Handler
    ↓ 分类判断和路由
[FamilyInstanceCreator] 或 [SystemElementCreator] ← 直接调用现有Creator
```

### 核心组件

#### 1. 统一参数模型
- **ElementCreationParameters**: 顶层统一参数模型
- **FamilyCreationOptions**: 族创建特有参数（嵌套）
- **SystemCreationOptions**: 系统族创建特有参数（嵌套）
- **ElementSuggestionParameters**: 参数建议查询模型

#### 2. 事件处理器 (EventHandler)
- **CreateElementEventHandler**: 统一创建处理器
- **GetElementCreationSuggestionEventHandler**: 统一建议处理器

#### 3. 命令类 (Command)
- **CreateElementCommand**: 统一创建命令
- **GetElementCreationSuggestionCommand**: 统一建议命令

#### 4. 服务类
- **ElementValidationService**: 参数验证服务

## 功能特性

### 自动类型检测
系统支持3种元素类型识别方式：

1. **显式指定**: `elementClass: "Family"` 或 `"System"`
2. **参数推断**: 根据`familyOptions`或`systemOptions`判断
3. **TypeId检测**: 查询ElementId对应的元素类型自动判断

### 统一参数格式

#### 创建命令参数格式
```json
{
  "data": {
    "elementClass": "Family|System|null",
    "typeId": 123456,
    "levelId": 789,
    "autoFindLevel": true,
    "familyOptions": {
      "locationPoint": {"x": 0, "y": 0, "z": 0},
      "baseOffset": 0,
      "autoFindHost": true,
      "searchRadius": 1000
    },
    "systemOptions": {
      "elementType": "wall|floor",
      "isStructural": false,
      "wallParameters": {
        "line": {"p0": {"x": 0, "y": 0, "z": 0}, "p1": {"x": 5000, "y": 0, "z": 0}},
        "height": 3000
      }
    }
  }
}
```

#### 建议命令参数格式
```json
{
  "data": {
    "elementClass": "Family|System|null",
    "elementId": 123456,
    "elementType": "wall|floor",
    "returnAll": false
  }
}
```

## 使用示例

### 示例1: 创建墙体（系统族）
```json
{
  "data": {
    "elementClass": "System",
    "typeId": 123456,
    "systemOptions": {
      "elementType": "wall",
      "wallParameters": {
        "line": {
          "p0": {"x": 0, "y": 0, "z": 0},
          "p1": {"x": 5000, "y": 0, "z": 0}
        },
        "height": 3000,
        "baseOffset": 0
      }
    }
  }
}
```

### 示例2: 创建门（族实例）
```json
{
  "data": {
    "elementClass": "Family",
    "typeId": 234567,
    "familyOptions": {
      "locationPoint": {"x": 2500, "y": 0, "z": 0},
      "baseOffset": 0,
      "autoFindHost": true,
      "hostCategories": ["OST_Walls"]
    }
  }
}
```

### 示例3: 自动检测类型创建
```json
{
  "data": {
    "typeId": 345678,
    "familyOptions": {
      "locationPoint": {"x": 0, "y": 0, "z": 0}
    }
  }
}
```

## 验证机制

### 参数验证规则
1. **基本验证**: TypeId必须提供且有效
2. **系统族验证**:
   - 必须指定elementType
   - 墙体需要有效的line和height
   - 楼板需要至少3个边界点
3. **族验证**: 必须提供locationPoint或locationLine之一

### 错误处理
- 详细的错误信息反馈
- 参数格式校验
- 类型兼容性检查
- 事务回滚机制

## 向后兼容

原有的4个命令仍然保持功能完整，建议：
1. 新开发使用统一命令
2. 原有命令标记为@Deprecated
3. 逐步迁移到新命令

## 技术优势

1. **简化接口**: 从4个命令减少到2个，降低AI学习成本
2. **统一体验**: 一致的参数格式和错误处理
3. **类型安全**: 强类型参数模型，编译时检查
4. **易于扩展**: 新增类型只需扩展Options模型
5. **性能优化**: 减少Handler间调用开销

## 开发规范

### 命名空间
```csharp
RevitMCPCommandSet.Features.ElementCreation
RevitMCPCommandSet.Features.ElementCreation.Models
```

### 扩展指南
添加新的系统族类型：
1. 在SystemCreationOptions中添加对应的SpecificParameters
2. 在ElementValidationService中添加验证规则
3. 在SystemElementCreator中实现创建逻辑

## 文件结构

```
Features/ElementCreation/
├── Models/
│   ├── ElementCreationParameters.cs      # 统一创建参数
│   ├── ElementSuggestionParameters.cs    # 建议查询参数
│   ├── FamilyCreationOptions.cs          # 族创建选项
│   └── SystemCreationOptions.cs          # 系统族创建选项
├── CreateElementCommand.cs               # 统一创建命令
├── CreateElementEventHandler.cs          # 统一创建处理器
├── GetElementCreationSuggestionCommand.cs # 统一建议命令
├── GetElementCreationSuggestionEventHandler.cs # 统一建议处理器
├── ElementValidationService.cs           # 验证服务
└── README.md                            # 本说明文档
```

---

## 更新历史

- **v1.0** (2025-09-23): 初始实现，完成基础功能和编译验证