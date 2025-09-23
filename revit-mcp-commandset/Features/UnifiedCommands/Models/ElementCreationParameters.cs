using Newtonsoft.Json;
using RevitMCPCommandSet.Models.Geometry;

namespace RevitMCPCommandSet.Features.UnifiedCommands.Models
{
    /// <summary>
    /// 统一的元素创建参数
    /// </summary>
    public class ElementCreationParameters
    {
        // === 公共字段（顶层） ===

        /// <summary>
        /// 元素类别 ("Family" | "System" | null(自动检测))
        /// </summary>
        [JsonProperty("elementClass")]
        public string ElementClass { get; set; }

        /// <summary>
        /// 族类型或系统族类型ID
        /// </summary>
        [JsonProperty("typeId")]
        public int TypeId { get; set; }

        /// <summary>
        /// 标高ID（可选）
        /// </summary>
        [JsonProperty("levelId")]
        public int? LevelId { get; set; }

        /// <summary>
        /// 自动查找最近标高
        /// </summary>
        [JsonProperty("autoFindLevel")]
        public bool AutoFindLevel { get; set; } = true;

        // === 分类特有参数（嵌套） ===

        /// <summary>
        /// 族创建特有参数（仅在elementClass为"Family"或自动检测为族时使用）
        /// </summary>
        [JsonProperty("familyOptions")]
        public FamilyCreationOptions FamilyOptions { get; set; }

        /// <summary>
        /// 系统族创建特有参数（仅在elementClass为"System"或自动检测为系统族时使用）
        /// </summary>
        [JsonProperty("systemOptions")]
        public SystemCreationOptions SystemOptions { get; set; }
    }
}