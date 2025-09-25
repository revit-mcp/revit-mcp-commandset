using Newtonsoft.Json;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Features.ElementFilter.Models
{
    /// <summary>
    /// 参数请求配置
    /// 替代原有的 ParameterOptions，提供更精确的参数控制
    /// </summary>
    public class ParameterRequest
    {
        /// <summary>
        /// 是否包含实例参数
        /// 默认值: true
        /// </summary>
        [JsonProperty("includeInstance")]
        public bool IncludeInstance { get; set; } = true;

        /// <summary>
        /// 是否包含类型参数
        /// 默认值: false
        /// </summary>
        [JsonProperty("includeType")]
        public bool IncludeType { get; set; } = false;

        /// <summary>
        /// 是否包含内置参数
        /// 默认值: false (仅返回用户定义的参数)
        /// </summary>
        [JsonProperty("includeBuiltIn")]
        public bool IncludeBuiltIn { get; set; } = false;

        /// <summary>
        /// 指定参数名列表（按显示名匹配，区分大小写）
        /// 如果指定此列表，将仅返回这些参数
        /// 例如: ["高度", "宽度", "材质"]
        /// </summary>
        [JsonProperty("names")]
        public List<string> Names { get; set; }

        /// <summary>
        /// 内置参数枚举名列表（不区分大小写）
        /// 例如: ["WINDOW_HEIGHT", "FURNITURE_WIDTH"]
        /// </summary>
        [JsonProperty("builtInNames")]
        public List<string> BuiltInNames { get; set; }

        /// <summary>
        /// 单参数查询（优先级最高）
        /// 如果指定，将仅返回该参数，忽略其他过滤条件
        /// </summary>
        [JsonProperty("singleName")]
        public string SingleName { get; set; }

        /// <summary>
        /// 扁平化输出格式
        /// true: 返回 Dictionary&lt;string,string&gt; (默认)
        /// false: 返回包含类型和元数据的对象
        /// </summary>
        [JsonProperty("flatten")]
        public bool Flatten { get; set; } = true;

        /// <summary>
        /// 是否包含原始值和元数据
        /// 仅在 Flatten = false 时生效
        /// </summary>
        [JsonProperty("includeRaw")]
        public bool IncludeRaw { get; set; } = false;

        /// <summary>
        /// 验证参数请求的有效性
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        /// <returns>如果有效返回 true</returns>
        public bool IsValid(out string errorMessage)
        {
            errorMessage = null;

            // 检查是否至少选择了一种参数范围
            if (!IncludeInstance && !IncludeType)
            {
                errorMessage = "参数请求无效: 必须至少包含实例参数或类型参数";
                return false;
            }

            // 单参数查询时不能同时指定其他过滤条件
            if (!string.IsNullOrEmpty(SingleName) &&
                ((Names != null && Names.Count > 0) || (BuiltInNames != null && BuiltInNames.Count > 0)))
            {
                errorMessage = "参数请求无效: 使用 SingleName 时不能同时指定 Names 或 BuiltInNames";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 获取参数匹配规则说明
        /// </summary>
        /// <returns>匹配规则说明</returns>
        public string GetMatchingRules()
        {
            if (!string.IsNullOrEmpty(SingleName))
            {
                return $"单参数查询: {SingleName}";
            }

            var rules = new List<string>();

            if (Names != null && Names.Count > 0)
            {
                rules.Add($"显示名匹配: {string.Join(", ", Names)}");
            }

            if (BuiltInNames != null && BuiltInNames.Count > 0)
            {
                rules.Add($"内置枚举匹配: {string.Join(", ", BuiltInNames)}");
            }

            if (rules.Count == 0)
            {
                rules.Add("所有参数");
            }

            var scope = new List<string>();
            if (IncludeInstance) scope.Add("实例");
            if (IncludeType) scope.Add("类型");

            return $"范围: {string.Join("+", scope)}, 匹配: {string.Join(" 或 ", rules)}, 内置参数: {(IncludeBuiltIn ? "包含" : "排除")}";
        }
    }
}