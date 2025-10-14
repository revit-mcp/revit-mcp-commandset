using RevitMCPCommandSet.Models.Geometry;
using Newtonsoft.Json;

namespace RevitMCPCommandSet.Features.ElementCreation.Models
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
        /// 墙体厚度（毫米）- 可选，如果不指定则使用类型默认厚度
        /// </summary>
        [JsonProperty("thickness")]
        public double? Thickness { get; set; }
    }
}