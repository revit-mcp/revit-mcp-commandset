# Revit MCP CommandSet 开发待办清单

## 高优先级命令 (High Priority) ⭐⭐⭐⭐⭐

| 命令名称                        | 功能描述 | 预估实现模块 | 核心需求 |
|-----------------------------|---------|-------------|----------|
| `create_system_families`    | 系统族元素智能创建 | `Features/SystemElementCreation/` | 几何路径创建墙体、轮廓创建楼板屋顶、管道系统路径创建、连接点集成 |
| `get_revit_status`          | Revit应用程序状态获取 | `Features/RevitStatus/` | 文档视图信息、项目状态、界面状态检测 |
| `get_element_geometry_info` | 项目指定几何信息获取 | `Features/ElementGeometry/` | 可配置元素类型、指定几何信息提取、批量几何数据收集 |

## 中优先级命令 (Medium Priority) ⭐⭐⭐⭐

| 命令名称 | 功能描述 | 预估实现模块 | 核心需求 |
|---------|---------|-------------|----------|
| `manage_element_parameters` | 元素参数CRUD操作 | `Features/ParameterManagement/` | 参数批量读写、分类管理、类型实例参数处理 |
| `manage_family_library` | 族库资源管理 | `Features/FamilyLibrary/` | 族库搜索、类型管理、加载状态跟踪 |

## 中优先级命令 (Medium Priority) ⭐⭐⭐

| 命令名称 | 功能描述 | 预估实现模块 | 核心需求 |
|---------|---------|-------------|----------|
| `capture_view_screenshot` | 视图截图和可视化 | `Features/ViewCapture/` | 多格式支持、可配置分辨率、路径管理 |
| `manage_views` | 视图创建和管理 | `Features/ViewManagement/` | 视图切换创建、属性配置、过滤器管理 |

## 低优先级命令 (Low Priority) ⭐

| 命令名称 | 功能描述 | 预估实现模块 | 核心需求 |
|---------|---------|-------------|----------|
| `manage_project_info` | 项目基本信息管理 | `Features/ProjectInfo/` | 项目属性读写、信息更新 |
| `manage_materials` | 材质属性管理 | `Features/MaterialManagement/` | 材质创建修改、属性配置 |

## 命令命名规范
- 命令名使用下划线分隔: `get_revit_status`
- 类名使用帕斯卡命名: `GetRevitStatusCommand`
- 文件名与类名保持一致

## 更新记录

- **2025-09-19**: 初始创建，定义高优先级核心命令
- 后续将根据开发进度和用户反馈调整优先级

---

**注意**: 本文档将根据开发进度实时更新，已完成的功能将移至完成清单。