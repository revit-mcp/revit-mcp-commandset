using Newtonsoft.Json;

namespace RevitMCPCommandSet.Features.ElementCreation.Models
{
    /// <summary>
    /// 系统族创建特有参数
    /// </summary>
    public class SystemCreationOptions
    {
        /// <summary>
        /// 系统族类型 ("wall", "floor", "ceiling", "roof")
        /// </summary>
        [JsonProperty("elementType")]
        public string ElementType { get; set; }

        /// <summary>
        /// 是否结构构件
        /// </summary>
        [JsonProperty("isStructural")]
        public bool IsStructural { get; set; } = false;

        // 特定类型参数（根据elementType使用）

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

        // 未来扩展
        // public CeilingSpecificParameters CeilingParameters { get; set; }
        // public RoofSpecificParameters RoofParameters { get; set; }
    }
}