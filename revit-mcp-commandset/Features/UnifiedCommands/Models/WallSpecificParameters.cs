using RevitMCPCommandSet.Models.Geometry;
using Newtonsoft.Json;

namespace RevitMCPCommandSet.Features.UnifiedCommands.Models
{
    /// <summary>
    /// 墙体专用参数
    /// </summary>
    public class WallSpecificParameters
    {
        /// <summary>
        /// 墙体路径（起点和终点，单位：毫米）- 必需
        /// </summary>
        [JsonProperty("line")]
        public JZLine Line { get; set; }

        /// <summary>
        /// 墙体高度（毫米）- 必需
        /// </summary>
        [JsonProperty("height")]
        public double Height { get; set; }

        /// <summary>
        /// 底部偏移（毫米）- 可选
        /// </summary>
        [JsonProperty("baseOffset")]
        public double BaseOffset { get; set; } = 0;

        /// <summary>
        /// 自动连接相邻墙体 - 可选
        /// </summary>
        [JsonProperty("autoJoinWalls")]
        public bool AutoJoinWalls { get; set; } = true;
    }
}