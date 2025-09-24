using Newtonsoft.Json;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Features.ElementFilter.Models
{
    /// <summary>
    /// 参数过滤和返回选项配置
    /// </summary>
    public class ParameterOptions
    {
        /// <summary>
        /// 参数范围
        /// 可选值: "Instance" | "Type" | "Both"
        /// 默认值: "Instance" 仅返回实例参数
        /// </summary>
        [JsonProperty("scope")]
        public string Scope { get; set; } = "Instance";

        /// <summary>
        /// 过滤模式
        /// 可选值: "None" | "Include" | "Exclude"
        /// 默认值: "None" 返回所有参数
        /// </summary>
        [JsonProperty("filterMode")]
        public string FilterMode { get; set; } = "None";

        /// <summary>
        /// 参数名称列表
        /// 根据 FilterMode 决定是包含还是排除这些参数
        /// </summary>
        [JsonProperty("parameterNames")]
        public List<string> ParameterNames { get; set; }

        /// <summary>
        /// 内置参数名列表
        /// 根据 FilterMode 决定是包含还是排除这些参数
        /// </summary>
        [JsonProperty("builtInParameters")]
        public List<string> BuiltInParameters { get; set; }

        /// <summary>
        /// 返回格式
        /// 可选值: "Merged" | "Separated"
        /// 默认值: "Merged" 实例和类型参数合并在一个字典中
        /// </summary>
        [JsonProperty("returnFormat")]
        public string ReturnFormat { get; set; } = "Merged";
    }
}