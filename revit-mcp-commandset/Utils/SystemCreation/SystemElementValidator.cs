using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Features.SystemElementCreation.Models;
using RevitMCPCommandSet.Features.UnifiedCommands.Utils;
using System;

namespace RevitMCPCommandSet.Utils.SystemCreation
{
    /// <summary>
    /// 系统族元素参数验证器（已过时，请使用ElementUtilityService）
    /// </summary>
    [Obsolete("请使用 ElementUtilityService 代替")]
    public static class SystemElementValidator
    {
        /// <summary>
        /// 验证系统族创建参数
        /// </summary>
        /// <param name="parameters">参数对象</param>
        /// <returns>验证错误信息，null表示通过验证</returns>
        public static string ValidateParameters(SystemElementParameters parameters)
        {
            if (parameters == null)
                return "参数不能为空";

            if (string.IsNullOrWhiteSpace(parameters.ElementType))
                return "必须指定系统族类型（elementType）";

            if (parameters.TypeId <= 0)
                return "必须指定有效的类型ID（typeId）";

            // 根据元素类型进行具体验证
            switch (parameters.ElementType.ToLower())
            {
                case "wall":
                    return ValidateWallParameters(parameters);

                case "floor":
                    return ValidateFloorParameters(parameters);

                case "ceiling":
                    return "天花板类型尚未实现";

                case "roof":
                    return "屋顶类型尚未实现";

                default:
                    return $"不支持的系统族类型: {parameters.ElementType}。支持的类型：wall, floor";
            }
        }

        /// <summary>
        /// 验证墙体参数
        /// </summary>
        private static string ValidateWallParameters(SystemElementParameters parameters)
        {
            if (parameters.WallParameters == null)
                return "墙体创建需要指定墙体参数（wallParameters）";

            if (parameters.WallParameters.Line == null)
                return "墙体创建需要指定路径线段（wallParameters.line）";

            if (parameters.WallParameters.Line.P0 == null || parameters.WallParameters.Line.P1 == null)
                return "墙体路径线段的起点（P0）和终点（P1）不能为空";

            // 检查起点和终点是否重合
            var p0 = parameters.WallParameters.Line.P0;
            var p1 = parameters.WallParameters.Line.P1;
            if (Math.Abs(p0.X - p1.X) < 1 && Math.Abs(p0.Y - p1.Y) < 1 && Math.Abs(p0.Z - p1.Z) < 1)
                return "墙体路径的起点和终点不能重合（最小距离1毫米）";

            if (parameters.WallParameters.Height <= 0)
                return "墙体高度必须大于0（wallParameters.height）";

            if (parameters.WallParameters.Height > 100000) // 最大100米
                return "墙体高度不能超过100米（100000毫米）";

            return null; // 验证通过
        }

        /// <summary>
        /// 验证楼板参数
        /// </summary>
        private static string ValidateFloorParameters(SystemElementParameters parameters)
        {
            if (parameters.FloorParameters == null)
                return "楼板创建需要指定楼板参数（floorParameters）";

            if (parameters.FloorParameters.Boundary == null || parameters.FloorParameters.Boundary.Count < 3)
                return "楼板边界至少需要3个点（floorParameters.boundary）";

            if (parameters.FloorParameters.Boundary.Count > 1000)
                return "楼板边界点数过多，最多支持1000个点";

            // 检查边界点是否有效
            for (int i = 0; i < parameters.FloorParameters.Boundary.Count; i++)
            {
                var point = parameters.FloorParameters.Boundary[i];
                if (point == null)
                    return $"楼板边界第{i + 1}个点为空";

                // 检查是否有重复点（相邻点距离小于1毫米）
                if (i > 0)
                {
                    var prevPoint = parameters.FloorParameters.Boundary[i - 1];
                    if (Math.Abs(point.X - prevPoint.X) < 1 &&
                        Math.Abs(point.Y - prevPoint.Y) < 1 &&
                        Math.Abs(point.Z - prevPoint.Z) < 1)
                        return $"楼板边界第{i}和第{i + 1}个点距离过近（最小距离1毫米）";
                }
            }

            // 检查坡度值
            if (parameters.FloorParameters.Slope.HasValue)
            {
                if (parameters.FloorParameters.Slope.Value < -45 || parameters.FloorParameters.Slope.Value > 45)
                    return "楼板坡度应在-45%到45%之间";
            }

            return null; // 验证通过
        }

        /// <summary>
        /// 获取元素类型的友好名称（调用统一工具服务）
        /// </summary>
        /// <param name="elementType">元素类型字符串</param>
        /// <returns>友好名称</returns>
        public static string GetFriendlyName(string elementType)
        {
            return ElementUtilityService.GetFriendlyName(elementType);
        }

        /// <summary>
        /// 检查元素类型是否受支持（调用统一工具服务）
        /// </summary>
        /// <param name="elementType">元素类型字符串</param>
        /// <returns>是否受支持</returns>
        public static bool IsElementTypeSupported(string elementType)
        {
            return ElementUtilityService.IsElementTypeSupported(elementType);
        }
    }
}