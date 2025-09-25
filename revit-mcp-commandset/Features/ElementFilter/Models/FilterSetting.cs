using Newtonsoft.Json;
using RevitMCPCommandSet.Models.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Features.ElementFilter.Models
{
    /// <summary>
    /// 过滤器设置 - 支持组合条件过滤
    /// </summary>
    public class FilterSetting
    {
        /// <summary>
        /// 获取或设置要过滤的 Revit 内置类别名称（如"OST_Walls"）。
        /// 如果为 null 或空，则不进行类别过滤。
        /// </summary>
        [JsonProperty("filterCategory")]
        public string FilterCategory { get; set; } = null;
        /// <summary>
        /// 获取或设置要过滤的 Revit 元素类型名称（如"Wall"或"Autodesk.Revit.DB.Wall"）。
        /// 如果为 null 或空，则不进行类型过滤。
        /// </summary>
        [JsonProperty("filterElementType")]
        public string FilterElementType { get; set; } = null;
        /// <summary>
        /// 获取或设置要过滤的族类型的ElementId值（FamilySymbol）。
        /// 如果为0或负数，则不进行族过滤。
        /// 注意：此过滤器仅适用于元素实例，不适用于类型元素。
        /// </summary>
        [JsonProperty("filterFamilySymbolId")]
        public int FilterFamilySymbolId { get; set; } = -1;
        /// <summary>
        /// 获取或设置是否包含元素类型（如墙类型、门类型等）
        /// </summary>
        [JsonProperty("includeTypes")]
        public bool IncludeTypes { get; set; } = false;
        /// <summary>
        /// 获取或设置是否包含元素实例（如已放置的墙、门等）
        /// </summary>
        [JsonProperty("includeInstances")]
        public bool IncludeInstances { get; set; } = true;
        /// <summary>
        /// 获取或设置是否仅返回在当前视图中可见的元素。
        /// 注意：此过滤器仅适用于元素实例，不适用于类型元素。
        /// </summary>
        [JsonProperty("filterVisibleInCurrentView")]
        public bool FilterVisibleInCurrentView { get; set; }
        /// <summary>
        /// 获取或设置空间范围过滤的最小点坐标 (单位：mm)
        /// 如果设置了此值和BoundingBoxMax，将筛选出与此边界框相交的元素
        /// </summary>
        [JsonProperty("boundingBoxMin")]
        public JZPoint BoundingBoxMin { get; set; } = null;
        /// <summary>
        /// 获取或设置空间范围过滤的最大点坐标 (单位：mm)
        /// 如果设置了此值和BoundingBoxMin，将筛选出与此边界框相交的元素
        /// </summary>
        [JsonProperty("boundingBoxMax")]
        public JZPoint BoundingBoxMax { get; set; } = null;
        /// <summary>
        /// 最大元素数量限制
        /// </summary>
        [JsonProperty("maxElements")]
        public int MaxElements { get; set; } = 50;

        // ===== 新增字段用于优化功能 =====

        /// <summary>
        /// 直接查询的元素ID列表
        /// 如果指定此列表，将直接获取这些元素，可配合其他过滤条件使用
        /// </summary>
        [JsonProperty("elementIds")]
        public List<int> ElementIds { get; set; }

        // ===== v3.0 新字段驱动模式 =====

        /// <summary>
        /// 原子字段列表
        /// 例如: ["geometry.boundingBox", "core.typeInfo"]
        /// 空列表时仅返回 elementId
        /// </summary>
        [JsonProperty("fields")]
        public List<string> Fields { get; set; }

        /// <summary>
        /// 预设字段组合
        /// 例如: ["listDisplay", "spatialAnalysis"]
        /// </summary>
        [JsonProperty("fieldPresets")]
        public List<string> FieldPresets { get; set; }

        /// <summary>
        /// 参数请求配置
        /// </summary>
        [JsonProperty("parameters")]
        public ParameterRequest Parameters { get; set; }

        // ===== 向后兼容（将逐步废弃）=====

        /// <summary>
        /// 返回信息的粒度级别
        /// 可选值: "Minimal", "Basic", "Geometry", "Parameters"
        /// 默认值: "Minimal" 保持轻量返回
        /// 警告: 此属性将在3个月后废弃，请使用 Fields 和 FieldPresets 替代
        /// </summary>
        [JsonProperty("returnLevel")]
        [Obsolete("returnLevel 将被废弃，请使用 fields 和 fieldPresets 替代")]
        public string ReturnLevel { get; set; } = "Minimal";

        /// <summary>
        /// 参数过滤和返回选项
        /// 警告: 此属性将在3个月后废弃，请使用 Parameters 替代
        /// </summary>
        [JsonProperty("parameterOptions")]
        [Obsolete("parameterOptions 将被废弃，请使用 parameters 替代")]
        public ParameterOptions ParameterOptions { get; set; }

        /// <summary>
        /// 自定义字段选择列表
        /// 警告: 此属性将在3个月后废弃，请使用 Fields 替代
        /// </summary>
        [JsonProperty("includeFields")]
        [Obsolete("includeFields 将被废弃，请使用 fields 替代")]
        public List<string> IncludeFields { get; set; }

        /// <summary>
        /// 几何信息选项
        /// 警告: 此属性将在3个月后废弃
        /// </summary>
        [JsonProperty("geometryOptions")]
        [Obsolete("geometryOptions 将被废弃")]
        public GeometryOptions GeometryOptions { get; set; } 
        /// <summary>
        /// 验证过滤器设置的有效性，检查潜在的冲突
        /// </summary>
        /// <returns>如果设置有效返回true，否则返回false</returns>
        public bool Validate(out string errorMessage)
        {
            errorMessage = null;

            // 检查是否至少选择了一种元素种类
            if (!IncludeTypes && !IncludeInstances)
            {
                errorMessage = "过滤设置无效: 必须至少包含元素类型或元素实例之一";
                return false;
            }

            // 检查是否至少指定了一个过滤条件（elementIds 直查模式除外）
            if (ElementIds == null || ElementIds.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(FilterCategory) &&
                    string.IsNullOrWhiteSpace(FilterElementType) &&
                    FilterFamilySymbolId <= 0)
                {
                    errorMessage = "过滤设置无效: 必须至少指定一个过滤条件(类别、元素类型或族类型)或提供元素ID列表";
                    return false;
                }
            }

            // 检查类型元素与某些过滤器的冲突
            if (IncludeTypes && !IncludeInstances)
            {
                List<string> invalidFilters = new List<string>();
                if (FilterFamilySymbolId > 0)
                    invalidFilters.Add("族实例过滤");
                if (FilterVisibleInCurrentView)
                    invalidFilters.Add("视图可见性过滤");
                if (invalidFilters.Count > 0)
                {
                    errorMessage = $"当仅过滤类型元素时，以下过滤器不适用: {string.Join(", ", invalidFilters)}";
                    return false;
                }
            }
            // 检查空间范围过滤器的有效性
            if (BoundingBoxMin != null && BoundingBoxMax != null)
            {
                // 确保最小点小于或等于最大点
                if (BoundingBoxMin.X > BoundingBoxMax.X ||
                    BoundingBoxMin.Y > BoundingBoxMax.Y ||
                    BoundingBoxMin.Z > BoundingBoxMax.Z)
                {
                    errorMessage = "空间范围过滤器设置无效: 最小点坐标必须小于或等于最大点坐标";
                    return false;
                }
            }
            else if (BoundingBoxMin != null || BoundingBoxMax != null)
            {
                errorMessage = "空间范围过滤器设置无效: 必须同时设置最小点和最大点坐标";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 标准化设置，处理向后兼容性
        /// 将旧的 returnLevel 映射为新的 fields/fieldPresets 格式
        /// </summary>
        public void NormalizeSettings()
        {
            // 如果使用了新格式，直接返回
            if ((Fields != null && Fields.Count > 0) || (FieldPresets != null && FieldPresets.Count > 0))
            {
                return;
            }

            // 如果使用了废弃的 returnLevel，进行映射
            if (!string.IsNullOrEmpty(ReturnLevel))
            {
                MapReturnLevelToNewFormat();
            }

            // 处理废弃的参数选项
            if (ParameterOptions != null && Parameters == null)
            {
                MapParameterOptionsToNewFormat();
            }
        }

        /// <summary>
        /// 映射旧的 returnLevel 到新的字段格式
        /// </summary>
        private void MapReturnLevelToNewFormat()
        {
            switch (ReturnLevel?.ToLower())
            {
                case "minimal":
                    // 默认行为：仅 elementId，无需设置字段
                    break;

                case "basic":
                    FieldPresets = new List<string> { "core.identityLite", "core.typeInfo", "core.levelInfo" };
                    break;

                case "geometry":
                    FieldPresets = new List<string> { "core.identityLite" };
                    Fields = new List<string>
                    {
                        "geometry.boundingBox",
                        "geometry.locationPoint",
                        "geometry.locationCurve",
                        "geometry.thickness",
                        "geometry.height",
                        "geometry.area"
                    };
                    break;

                case "parameters":
                    FieldPresets = new List<string> { "core.identityLite" };
                    if (Parameters == null && ParameterOptions != null)
                    {
                        MapParameterOptionsToNewFormat();
                    }
                    else if (Parameters == null)
                    {
                        // 默认参数配置
                        Parameters = new ParameterRequest
                        {
                            IncludeInstance = true,
                            IncludeType = false,
                            Flatten = true
                        };
                    }
                    break;

                case "full":
                case "custom":
                    // Full 和 Custom 模式不再支持，使用默认的基础信息
                    FieldPresets = new List<string> { "core.identityLite" };
                    break;
            }
        }

        /// <summary>
        /// 映射旧的 ParameterOptions 到新的 ParameterRequest 格式
        /// </summary>
        private void MapParameterOptionsToNewFormat()
        {
            if (ParameterOptions == null) return;

            Parameters = new ParameterRequest
            {
                IncludeInstance = ParameterOptions.Scope == "Instance" || ParameterOptions.Scope == "Both",
                IncludeType = ParameterOptions.Scope == "Type" || ParameterOptions.Scope == "Both",
                IncludeBuiltIn = true, // 旧版本默认包含内置参数
                Names = ParameterOptions.ParameterNames,
                BuiltInNames = ParameterOptions.BuiltInParameters,
                Flatten = ParameterOptions.ReturnFormat == "Merged"
            };
        }

        /// <summary>
        /// 获取废弃警告消息
        /// </summary>
        /// <returns>废弃警告消息，如果没有使用废弃功能则返回 null</returns>
        public string GetDeprecationWarning()
        {
            var warnings = new List<string>();

            if (!string.IsNullOrEmpty(ReturnLevel))
            {
                warnings.Add("returnLevel 已废弃，请使用 fields 和 fieldPresets");
            }

            if (ParameterOptions != null)
            {
                warnings.Add("parameterOptions 已废弃，请使用 parameters");
            }

            if (IncludeFields != null && IncludeFields.Count > 0)
            {
                warnings.Add("includeFields 已废弃，请使用 fields");
            }

            if (GeometryOptions != null)
            {
                warnings.Add("geometryOptions 已废弃");
            }

            return warnings.Count > 0
                ? $"废弃功能警告: {string.Join(", ", warnings)}。这些功能将在3个月后移除。"
                : null;
        }
    }
}
