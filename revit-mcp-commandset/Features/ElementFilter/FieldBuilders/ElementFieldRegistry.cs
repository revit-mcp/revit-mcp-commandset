using Autodesk.Revit.DB;
using RevitMCPCommandSet.Features.ElementFilter.FieldBuilders.Core;
using RevitMCPCommandSet.Features.ElementFilter.FieldBuilders.Geometry;
using RevitMCPCommandSet.Features.ElementFilter.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitMCPCommandSet.Features.ElementFilter.FieldBuilders
{
    /// <summary>
    /// 字段注册表
    /// 管理所有字段构建器和预设组合
    /// </summary>
    public static class ElementFieldRegistry
    {
        private static readonly Dictionary<string, IFieldBuilder> _fieldBuilders;
        private static readonly Dictionary<string, List<string>> _fieldPresets;

        static ElementFieldRegistry()
        {
            // 注册字段构建器
            _fieldBuilders = new Dictionary<string, IFieldBuilder>(StringComparer.OrdinalIgnoreCase);
            RegisterFieldBuilders();

            // 注册预设组合
            _fieldPresets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            RegisterFieldPresets();
        }

        /// <summary>
        /// 注册所有字段构建器
        /// </summary>
        private static void RegisterFieldBuilders()
        {
            // Core 字段构建器
            RegisterBuilder(new IdentityFieldBuilder());    // 新增：身份信息构建器
            RegisterBuilder(new TypeInfoFieldBuilder());
            RegisterBuilder(new FamilyInfoFieldBuilder());
            RegisterBuilder(new LevelInfoFieldBuilder());

            // Geometry 字段构建器（新的统一架构）
            RegisterBuilder(new LocationFieldBuilder());    // 统一的位置字段
            RegisterBuilder(new ProfileFieldBuilder());     // 轮廓字段
            RegisterBuilder(new BoundingBoxFieldBuilder()); // 更新的包围盒字段
            RegisterBuilder(new ThicknessFieldBuilder());
            RegisterBuilder(new HeightFieldBuilder());
            RegisterBuilder(new WidthFieldBuilder());
            RegisterBuilder(new AreaFieldBuilder());
        }

        /// <summary>
        /// 注册预设组合
        /// </summary>
        private static void RegisterFieldPresets()
        {
            // 基础预设（使用新的节点结构）
            _fieldPresets["core.identityLite"] = new List<string> { "identity" };
            _fieldPresets["listDisplay"] = new List<string> { "identity" };
            _fieldPresets["typeAnalysis"] = new List<string> { "identity", "type" };
            _fieldPresets["spatialAnalysis"] = new List<string> { "identity", "geometry.location", "geometry.boundingBox" };
            _fieldPresets["detailView"] = new List<string> { "identity", "type", "level" };
            _fieldPresets["familyAnalysis"] = new List<string> { "identity", "type", "family" };
            _fieldPresets["floorAnalysis"] = new List<string> { "identity", "geometry.profile", "geometry.boundingBox" };
        }

        /// <summary>
        /// 注册单个字段构建器
        /// </summary>
        /// <param name="builder">字段构建器</param>
        private static void RegisterBuilder(IFieldBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            _fieldBuilders[builder.FieldName] = builder;
        }

        /// <summary>
        /// 构建元素信息（主入口）
        /// </summary>
        /// <param name="doc">Revit 文档</param>
        /// <param name="element">元素</param>
        /// <param name="settings">过滤器设置</param>
        /// <returns>包含字段数据的字典</returns>
        public static Dictionary<string, object> BuildElementInfo(Document doc, Element element, FilterSetting settings)
        {
            var result = BuildElementInfoWithWarnings(doc, element, settings, out var warnings);
            return result;
        }

        /// <summary>
        /// 构建元素信息并返回警告信息
        /// </summary>
        /// <param name="doc">Revit 文档</param>
        /// <param name="element">元素</param>
        /// <param name="settings">过滤器设置</param>
        /// <param name="warnings">输出的警告信息列表</param>
        /// <returns>包含字段数据的字典</returns>
        public static Dictionary<string, object> BuildElementInfoWithWarnings(Document doc, Element element, FilterSetting settings, out List<string> warnings)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (element == null) throw new ArgumentNullException(nameof(element));

            var result = new Dictionary<string, object>();
            var context = new FieldContext(doc, element, result, settings);

            // 始终包含 elementId
            result["elementId"] = element.Id.IntegerValue;

            // 解析字段请求
            var requestedFields = ResolveFields(settings?.Fields, settings?.FieldPresets);

            // 构建字段
            foreach (var field in requestedFields)
            {
                BuildField(field, context);
            }

            // 处理参数（如果指定）
            if (settings?.Parameters != null)
            {
                BuildParameters(context);
            }

            // 返回警告信息
            warnings = context.Warnings;

            return result;
        }

        /// <summary>
        /// 解析字段请求（合并 fields 和 fieldPresets）
        /// </summary>
        /// <param name="fields">原子字段列表</param>
        /// <param name="fieldPresets">预设组合列表</param>
        /// <returns>去重后的字段列表</returns>
        private static HashSet<string> ResolveFields(List<string> fields, List<string> fieldPresets)
        {
            var resolvedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 添加直接指定的字段
            if (fields != null)
            {
                foreach (var field in fields)
                {
                    resolvedFields.Add(field);
                }
            }

            // 展开预设组合
            if (fieldPresets != null)
            {
                foreach (var preset in fieldPresets)
                {
                    if (_fieldPresets.TryGetValue(preset, out var presetFields))
                    {
                        foreach (var field in presetFields)
                        {
                            resolvedFields.Add(field);
                        }
                    }
                    else
                    {
                        // 检查是否为合法字段（容错处理）
                        if (IsFieldRegistered(preset))
                        {
                            resolvedFields.Add(preset);
                            // 可以选择性地记录一个提示，但不作为错误处理
                        }
                        // 如果既不是预设也不是字段，则静默忽略（避免过多噪音）
                    }
                }
            }

            return resolvedFields;
        }

        /// <summary>
        /// 构建单个字段
        /// </summary>
        /// <param name="field">字段名</param>
        /// <param name="context">字段上下文</param>
        private static void BuildField(string field, FieldContext context)
        {
            try
            {
                // 所有字段统一使用 Builder 处理
                if (_fieldBuilders.TryGetValue(field, out var builder))
                {
                    if (builder.CanBuild(context.Element))
                    {
                        builder.Build(context);
                    }
                }
                else
                {
                    // 向后兼容：处理旧的简单字段名
                    HandleLegacyField(field, context);
                }
            }
            catch (Exception ex)
            {
                // 静默失败 - 记录日志但不影响其他字段
                System.Diagnostics.Trace.WriteLine($"字段构建失败 [{field}]: {ex.Message}");
            }
        }

        /// <summary>
        /// 构建参数信息
        /// </summary>
        /// <param name="context">字段上下文</param>
        private static void BuildParameters(FieldContext context)
        {
            try
            {
                var parameterProcessor = new ParameterProcessor();
                parameterProcessor.ProcessParameters(context, context.Settings?.Parameters);
            }
            catch (Exception ex)
            {
                // 静默失败
                System.Diagnostics.Trace.WriteLine($"参数构建失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取所有注册的字段名称
        /// </summary>
        /// <returns>字段名称列表</returns>
        public static IEnumerable<string> GetRegisteredFields()
        {
            var simpleFields = new[] { "name", "category", "builtInCategory" };
            var builderFields = _fieldBuilders.Keys;
            return simpleFields.Concat(builderFields);
        }

        /// <summary>
        /// 获取所有注册的预设名称
        /// </summary>
        /// <returns>预设名称列表</returns>
        public static IEnumerable<string> GetRegisteredPresets()
        {
            return _fieldPresets.Keys;
        }

        /// <summary>
        /// 检查字段是否已注册
        /// </summary>
        /// <param name="fieldName">字段名</param>
        /// <returns>如果已注册返回 true</returns>
        public static bool IsFieldRegistered(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName)) return false;

            var lowerField = fieldName.ToLower();
            return lowerField == "name" ||
                   lowerField == "category" ||
                   lowerField == "builtincategory" ||
                   _fieldBuilders.ContainsKey(fieldName);
        }

        /// <summary>
        /// 检查预设是否已注册
        /// </summary>
        /// <param name="presetName">预设名</param>
        /// <returns>如果已注册返回 true</returns>
        public static bool IsPresetRegistered(string presetName)
        {
            return !string.IsNullOrEmpty(presetName) && _fieldPresets.ContainsKey(presetName);
        }

        /// <summary>
        /// 处理旧版字段名（向后兼容）
        /// </summary>
        /// <param name="field">字段名</param>
        /// <param name="context">字段上下文</param>
        private static void HandleLegacyField(string field, FieldContext context)
        {
            try
            {
                switch (field?.ToLower())
                {
                    case "name":
                    case "category":
                    case "builtincategory":
                        // 旧的单独字段请求，映射到 identity 节点
                        var identityBuilder = new IdentityFieldBuilder();
                        if (identityBuilder.CanBuild(context.Element))
                        {
                            identityBuilder.Build(context);
                        }
                        break;
                    case "core.typeinfo":
                        // 旧的 core.typeInfo 映射到新的 type
                        var typeBuilder = new TypeInfoFieldBuilder();
                        if (typeBuilder.CanBuild(context.Element))
                        {
                            typeBuilder.Build(context);
                        }
                        break;
                    case "core.familyinfo":
                        // 旧的 core.familyInfo 映射到新的 family
                        var familyBuilder = new FamilyInfoFieldBuilder();
                        if (familyBuilder.CanBuild(context.Element))
                        {
                            familyBuilder.Build(context);
                        }
                        break;
                    case "core.levelinfo":
                        // 旧的 core.levelInfo 映射到新的 level
                        var levelBuilder = new LevelInfoFieldBuilder();
                        if (levelBuilder.CanBuild(context.Element))
                        {
                            levelBuilder.Build(context);
                        }
                        break;
                    default:
                        // 未知字段，忽略
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"旧版字段处理失败 [{field}]: {ex.Message}");
            }
        }
    }
}