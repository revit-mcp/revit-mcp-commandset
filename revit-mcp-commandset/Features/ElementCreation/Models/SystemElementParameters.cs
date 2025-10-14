using Newtonsoft.Json;

namespace RevitMCPCommandSet.Features.ElementCreation.Models
{
    /// <summary>
    /// 系统族创建参数（优化的包装模式，适配MCP架构）
    /// </summary>
    public class SystemElementParameters
    {
        /// <summary>
        /// 系统族类型（字符串形式：wall, floor, ceiling, roof）
        /// </summary>
        [JsonProperty("elementType")]
        public string ElementType { get; set; }

        /// <summary>
        /// 系统族类型ID (WallType或FloorType的ElementId)
        /// </summary>
        [JsonProperty("typeId")]
        public int TypeId { get; set; }

        /// <summary>
        /// 关联标高ID（可选）
        /// </summary>
        [JsonProperty("levelId")]
        public int? LevelId { get; set; }

        /// <summary>
        /// 自动查找最近标高
        /// </summary>
        [JsonProperty("autoFindLevel")]
        public bool AutoFindLevel { get; set; } = true;

        /// <summary>
        /// 是否结构构件（通用可选参数）
        /// </summary>
        [JsonProperty("isStructural")]
        public bool IsStructural { get; set; } = false;

        // === 特有参数包装 ===

        /// <summary>
        /// 墙体专用参数（仅在elementType为"wall"时使用）
        /// </summary>
        [JsonProperty("wallParameters")]
        public WallSpecificParameters WallParameters { get; set; }

        /// <summary>
        /// 楼板专用参数（仅在elementType为"floor"时使用）
        /// </summary>
        [JsonProperty("floorParameters")]
        public FloorSpecificParameters FloorParameters { get; set; }
    }
}