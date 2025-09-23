using RevitMCPCommandSet.Features.UnifiedCommands.Models;

namespace RevitMCPCommandSet.Features.UnifiedCommands
{
    /// <summary>
    /// 元素验证服务
    /// </summary>
    public static class ElementValidationService
    {
        /// <summary>
        /// 验证元素创建参数
        /// </summary>
        /// <param name="parameters">创建参数</param>
        /// <returns>错误信息，null表示验证通过</returns>
        public static string Validate(ElementCreationParameters parameters)
        {
            if (parameters == null)
                return "参数不能为空";

            if (parameters.TypeId <= 0)
                return "必须提供有效的TypeId";

            // 根据类型进行特定验证
            if (parameters.SystemOptions != null)
            {
                return ValidateSystemOptions(parameters.SystemOptions);
            }

            if (parameters.FamilyOptions != null)
            {
                return ValidateFamilyOptions(parameters.FamilyOptions);
            }

            return null; // 验证通过
        }

        /// <summary>
        /// 验证系统族创建参数
        /// </summary>
        /// <param name="options">系统族参数</param>
        /// <returns>错误信息，null表示验证通过</returns>
        private static string ValidateSystemOptions(SystemCreationOptions options)
        {
            if (string.IsNullOrEmpty(options.ElementType))
                return "系统族必须指定ElementType";

            switch (options.ElementType.ToLower())
            {
                case "wall":
                    if (options.WallParameters?.Line == null)
                        return "墙体创建需要指定路径线";
                    if (options.WallParameters.Height <= 0)
                        return "墙体高度必须大于0";
                    break;
                case "floor":
                    if (options.FloorParameters?.Boundary == null ||
                        options.FloorParameters.Boundary.Count < 3)
                        return "楼板创建需要至少3个点的轮廓";
                    break;
                default:
                    return $"不支持的系统族类型: {options.ElementType}";
            }

            return null;
        }

        /// <summary>
        /// 验证族创建参数
        /// </summary>
        /// <param name="options">族创建参数</param>
        /// <returns>错误信息，null表示验证通过</returns>
        private static string ValidateFamilyOptions(FamilyCreationOptions options)
        {
            // 至少需要一个定位参数
            if (options.LocationPoint == null &&
                options.LocationLine == null)
                return "族创建需要提供定位点或定位线";

            return null;
        }
    }
}