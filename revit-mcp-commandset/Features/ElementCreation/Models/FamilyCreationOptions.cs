using Newtonsoft.Json;
using RevitMCPCommandSet.Models.Geometry;

namespace RevitMCPCommandSet.Features.ElementCreation.Models
{
    /// <summary>
    /// 族创建特有参数
    /// </summary>
    public class FamilyCreationOptions
    {
        /// <summary>
        /// 点定位参数
        /// </summary>
        [JsonProperty("locationPoint")]
        public JZPoint LocationPoint { get; set; }

        /// <summary>
        /// 线定位参数
        /// </summary>
        [JsonProperty("locationLine")]
        public JZLine LocationLine { get; set; }

        /// <summary>
        /// 顶部标高ElementId
        /// </summary>
        [JsonProperty("topLevelId")]
        public int TopLevelId { get; set; } = -1;

        /// <summary>
        /// 底部偏移（毫米）
        /// </summary>
        [JsonProperty("baseOffset")]
        public double BaseOffset { get; set; } = 0;

        /// <summary>
        /// 顶部偏移（毫米）
        /// </summary>
        [JsonProperty("topOffset")]
        public double TopOffset { get; set; } = 0;

        /// <summary>
        /// 视图ElementId
        /// </summary>
        [JsonProperty("viewId")]
        public int ViewId { get; set; } = -1;

        /// <summary>
        /// 宿主元素ElementId
        /// </summary>
        [JsonProperty("hostElementId")]
        public int HostElementId { get; set; } = -1;

        /// <summary>
        /// 宿主类别数组（BuiltInCategory名称）
        /// </summary>
        [JsonProperty("hostCategories")]
        public string[] HostCategories { get; set; }

        /// <summary>
        /// 面方向向量（归一化）
        /// </summary>
        [JsonProperty("faceDirection")]
        public JZPoint FaceDirection { get; set; }

        /// <summary>
        /// 手方向向量（归一化）
        /// </summary>
        [JsonProperty("handDirection")]
        public JZPoint HandDirection { get; set; }

        /// <summary>
        /// 是否自动查找宿主
        /// </summary>
        [JsonProperty("autoFindHost")]
        public bool AutoFindHost { get; set; } = true;

        /// <summary>
        /// 搜索半径（毫米）
        /// </summary>
        [JsonProperty("searchRadius")]
        public double SearchRadius { get; set; } = 1000;
    }
}