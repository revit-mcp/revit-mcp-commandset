using RevitMCPCommandSet.Models.Geometry;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Features.SystemElementCreation.Models
{
    /// <summary>
    /// 楼板专用参数
    /// </summary>
    public class FloorSpecificParameters
    {
        /// <summary>
        /// 楼板边界点列表（按顺序连接形成闭合轮廓，单位：毫米）- 必需
        /// </summary>
        [JsonProperty("boundary")]
        public List<JZPoint> Boundary { get; set; }

        /// <summary>
        /// 楼板顶部偏移（毫米）- 可选
        /// </summary>
        [JsonProperty("topOffset")]
        public double TopOffset { get; set; } = 0;

        /// <summary>
        /// 楼板坡度（可选，默认为null表示水平楼板）- 可选
        /// </summary>
        [JsonProperty("slope")]
        public double? Slope { get; set; }
    }
}