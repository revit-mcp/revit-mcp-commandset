using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.Common
{
    /// <summary>
    /// 族创建参数需求（精简版）
    /// </summary>
    public class FamilyCreationRequirements
    {
        /// <summary>
        /// 族类型ElementId
        /// </summary>
        [JsonProperty("typeId")]
        public int TypeId { get; set; }

        /// <summary>
        /// 族名称
        /// </summary>
        [JsonProperty("familyName")]
        public string FamilyName { get; set; }


        /// <summary>
        /// 统一参数字典（包含必需和可选参数）
        /// </summary>
        [JsonProperty("parameters")]
        public Dictionary<string, ParameterInfo> Parameters { get; set; }

        /// <summary>
        /// 消息（用于不支持族类型的说明）
        /// </summary>
        [JsonProperty("message")]
        public string Message { get; set; }

        public FamilyCreationRequirements()
        {
            Parameters = new Dictionary<string, ParameterInfo>();
        }
    }

    /// <summary>
    /// 参数信息（精简版）
    /// </summary>
    public class ParameterInfo
    {
        /// <summary>
        /// 是否为必需参数
        /// </summary>
        [JsonProperty("required")]
        public bool Required { get; set; }

        /// <summary>
        /// 参数说明（包含单位信息）
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; }


        /// <summary>
        /// 默认值（仅可选参数使用）
        /// </summary>
        [JsonProperty("default")]
        public object Default { get; set; }
    }
}