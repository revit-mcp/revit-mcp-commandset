using Newtonsoft.Json;

namespace RevitMCPCommandSet.Features.ElementCreation.Models
{
    /// <summary>
    /// 元素创建参数建议查询参数
    /// </summary>
    public class ElementSuggestionParameters
    {
        /// <summary>
        /// 元素类别 ("Family" | "System" | null)
        /// </summary>
        [JsonProperty("elementClass")]
        public string ElementClass { get; set; }

        /// <summary>
        /// 用于查询特定元素类型的ElementId
        /// </summary>
        [JsonProperty("elementId")]
        public int? ElementId { get; set; }

        /// <summary>
        /// 系统族类型名（wall, floor等）
        /// </summary>
        [JsonProperty("elementType")]
        public string ElementType { get; set; }

        /// <summary>
        /// 返回所有可用选项
        /// </summary>
        [JsonProperty("returnAll")]
        public bool ReturnAll { get; set; }
    }
}