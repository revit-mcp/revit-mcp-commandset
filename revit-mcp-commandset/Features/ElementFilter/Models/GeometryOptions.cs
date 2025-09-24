using Newtonsoft.Json;

namespace RevitMCPCommandSet.Features.ElementFilter.Models
{
    /// <summary>
    /// 几何信息选项配置
    /// </summary>
    public class GeometryOptions
    {
        /// <summary>
        /// 是否包含手方向向量（高开销）
        /// 仅对 FamilyInstance 有效
        /// </summary>
        [JsonProperty("includeHandOrientation")]
        public bool IncludeHandOrientation { get; set; } = false;

        /// <summary>
        /// 是否包含面方向向量（高开销）
        /// 仅对 FamilyInstance 有效
        /// </summary>
        [JsonProperty("includeFacingOrientation")]
        public bool IncludeFacingOrientation { get; set; } = false;

        /// <summary>
        /// 是否包含手翻转状态（高开销）
        /// 仅对 FamilyInstance 有效
        /// </summary>
        [JsonProperty("includeIsHandFlipped")]
        public bool IncludeIsHandFlipped { get; set; } = false;

        /// <summary>
        /// 是否包含面翻转状态（高开销）
        /// 仅对 FamilyInstance 有效
        /// </summary>
        [JsonProperty("includeIsFacingFlipped")]
        public bool IncludeIsFacingFlipped { get; set; } = false;

        /// <summary>
        /// 是否计算详细的几何属性（如面积、周长等）
        /// </summary>
        [JsonProperty("calculateDetailedGeometry")]
        public bool CalculateDetailedGeometry { get; set; } = true;
    }
}