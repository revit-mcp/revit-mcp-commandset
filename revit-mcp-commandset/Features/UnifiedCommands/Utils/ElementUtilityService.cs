using System;
using Autodesk.Revit.DB;
using RevitMCPCommandSet.Features.UnifiedCommands.Models;

namespace RevitMCPCommandSet.Features.UnifiedCommands.Utils
{
    /// <summary>
    /// 元素工具服务类，提供类型检测和基础验证
    /// </summary>
    public static class ElementUtilityService
    {
        #region 类型检测功能

        /// <summary>
        /// 确定元素类别（统一的类型检测逻辑）
        /// </summary>
        /// <param name="doc">Revit文档</param>
        /// <param name="param">创建参数</param>
        /// <returns>元素类别：Family、System 或 null</returns>
        public static string DetermineElementClass(Document doc, ElementCreationParameters param)
        {
            // 1. 显式指定优先
            if (!string.IsNullOrEmpty(param.ElementClass))
                return param.ElementClass;

            // 2. 根据SystemOptions判断
            if (param.SystemOptions?.ElementType != null)
                return "System";

            // 3. 根据FamilyOptions判断
            if (param.FamilyOptions != null &&
                (param.FamilyOptions.LocationPoint != null ||
                 param.FamilyOptions.LocationLine != null))
                return "Family";

            // 4. 自动检测TypeId
            if (param.TypeId > 0)
            {
                var elementClass = DetectElementClassById(doc, param.TypeId);
                if (elementClass != null)
                    return elementClass;
            }

            return null;
        }

        /// <summary>
        /// 根据ElementId检测元素分类
        /// </summary>
        /// <param name="doc">Revit文档</param>
        /// <param name="elementId">元素ID</param>
        /// <returns>元素类别：Family、System 或 null</returns>
        public static string DetectElementClassById(Document doc, int elementId)
        {
            try
            {
                var element = doc.GetElement(new ElementId(elementId));
                if (element == null)
                    return null;

                if (element is FamilySymbol)
                    return "Family";

                if (IsSystemElementType(element))
                    return "System";

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 检查是否为系统族类型
        /// </summary>
        /// <param name="element">Revit元素</param>
        /// <returns>是否为系统族类型</returns>
        public static bool IsSystemElementType(Element element)
        {
            return element is WallType ||
                   element is FloorType ||
                   element is CeilingType ||
                   element is RoofType;
        }

        #endregion

        #region 必需参数验证（仅检查缺失）

        /// <summary>
        /// 验证必需参数是否存在
        /// </summary>
        /// <param name="parameters">创建参数</param>
        /// <returns>错误信息，null表示验证通过</returns>
        public static string ValidateRequiredParameters(ElementCreationParameters parameters)
        {
            if (parameters == null)
                return "参数不能为空";

            if (parameters.TypeId <= 0)
                return "必须提供有效的TypeId";

            // 验证系统族必需参数
            if (parameters.SystemOptions != null)
            {
                return ValidateSystemRequiredParameters(parameters.SystemOptions);
            }

            // 验证族必需参数
            if (parameters.FamilyOptions != null)
            {
                return ValidateFamilyRequiredParameters(parameters.FamilyOptions);
            }

            return null; // 验证通过
        }

        /// <summary>
        /// 验证系统族必需参数
        /// </summary>
        private static string ValidateSystemRequiredParameters(SystemCreationOptions options)
        {
            if (string.IsNullOrEmpty(options.ElementType))
                return "系统族必须指定ElementType";

            switch (options.ElementType.ToLower())
            {
                case "wall":
                    if (options.WallParameters?.Line == null)
                        return "墙体创建需要指定路径线";
                    if (options.WallParameters.Line.P0 == null ||
                        options.WallParameters.Line.P1 == null)
                        return "墙体路径线的起点和终点不能为空";
                    break;

                case "floor":
                    if (options.FloorParameters?.Boundary == null ||
                        options.FloorParameters.Boundary.Count < 3)
                        return "楼板创建需要至少3个边界点";
                    break;

                default:
                    return $"不支持的系统族类型: {options.ElementType}";
            }

            return null;
        }

        /// <summary>
        /// 验证族必需参数
        /// </summary>
        private static string ValidateFamilyRequiredParameters(FamilyCreationOptions options)
        {
            // 至少需要一个定位参数
            if (options.LocationPoint == null && options.LocationLine == null)
                return "族创建需要提供定位点或定位线";

            return null;
        }

        #endregion

        #region 辅助功能

        /// <summary>
        /// 获取元素类型的友好名称
        /// </summary>
        /// <param name="elementType">元素类型字符串</param>
        /// <returns>友好名称</returns>
        public static string GetFriendlyName(string elementType)
        {
            switch (elementType?.ToLower())
            {
                case "wall": return "墙体";
                case "floor": return "楼板";
                case "ceiling": return "天花板";
                case "roof": return "屋顶";
                default: return elementType ?? "未知类型";
            }
        }

        /// <summary>
        /// 检查系统族类型是否受支持
        /// </summary>
        /// <param name="elementType">元素类型字符串</param>
        /// <returns>是否受支持</returns>
        public static bool IsElementTypeSupported(string elementType)
        {
            switch (elementType?.ToLower())
            {
                case "wall":
                case "floor":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 获取支持的系统族类型列表
        /// </summary>
        /// <returns>支持的类型数组</returns>
        public static string[] GetSupportedSystemTypes()
        {
            return new[] { "wall", "floor" }; // 未来可扩展 ceiling, roof
        }

        #endregion
    }
}