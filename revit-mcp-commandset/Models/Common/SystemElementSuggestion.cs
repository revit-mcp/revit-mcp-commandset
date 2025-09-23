using System.Collections.Generic;

namespace RevitMCPCommandSet.Models.Common
{
    /// <summary>
    /// 系统族创建参数建议
    /// </summary>
    public class SystemElementSuggestion
    {
        /// <summary>
        /// 系统族类型名称
        /// </summary>
        public string ElementType { get; set; }

        /// <summary>
        /// 系统族类型描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 必需参数
        /// </summary>
        public Dictionary<string, SystemParameterInfo> RequiredParameters { get; set; }

        /// <summary>
        /// 可选参数
        /// </summary>
        public Dictionary<string, SystemParameterInfo> OptionalParameters { get; set; }

        /// <summary>
        /// 参数示例
        /// </summary>
        public object Example { get; set; }

        /// <summary>
        /// 版本说明
        /// </summary>
        public string VersionNotes { get; set; }

        /// <summary>
        /// 常见错误和解决方案
        /// </summary>
        public List<string> CommonIssues { get; set; }
    }

    /// <summary>
    /// 系统族参数信息
    /// </summary>
    public class SystemParameterInfo
    {
        /// <summary>
        /// 参数类型
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 参数描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 示例值
        /// </summary>
        public object Example { get; set; }

        /// <summary>
        /// 默认值
        /// </summary>
        public object DefaultValue { get; set; }
    }
}