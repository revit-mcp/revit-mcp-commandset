using RevitMCPCommandSet.Models.Geometry;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Features.ElementCreation.Models
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
        /// 楼板厚度（毫米）- 可选，如果不指定则使用类型默认厚度
        /// </summary>
        [JsonProperty("thickness")]
        public double? Thickness { get; set; }
    }
}