using System;
using RevitMCPCommandSet.Features.UnifiedCommands.Models;
using RevitMCPCommandSet.Features.UnifiedCommands.Utils;

namespace RevitMCPCommandSet.Features.UnifiedCommands
{
    /// <summary>
    /// 元素验证服务（简化版，调用统一工具服务）
    /// </summary>
    [Obsolete("建议直接使用 ElementUtilityService.ValidateRequiredParameters")]
    public static class ElementValidationService
    {
        /// <summary>
        /// 验证元素创建参数
        /// </summary>
        /// <param name="parameters">创建参数</param>
        /// <returns>错误信息，null表示验证通过</returns>
        public static string Validate(ElementCreationParameters parameters)
        {
            // 直接调用统一工具服务
            return ElementUtilityService.ValidateRequiredParameters(parameters);
        }
    }
}