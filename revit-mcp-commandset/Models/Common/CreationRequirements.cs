using Newtonsoft.Json;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Models.Common
{
    /// <summary>
    /// 族创建参数需求（精简版）
    /// </summary>
    public class CreationRequirements
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

        public CreationRequirements()
        {
            Parameters = new Dictionary<string, ParameterInfo>();
        }
    }

    /// <summary>
    /// 参数信息（扩展版）
    /// </summary>
    public class ParameterInfo
    {
        /// <summary>
        /// 参数类型
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>
        /// 参数说明（包含单位信息）
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>
        /// 示例值
        /// </summary>
        [JsonProperty("example")]
        public object Example { get; set; }

        /// <summary>
        /// 是否为必需参数
        /// </summary>
        [JsonProperty("required")]
        public bool Required { get; set; }

        /// <summary>
        /// 是否为必需参数（别名，为兼容新代码）
        /// </summary>
        [JsonIgnore]
        public bool IsRequired
        {
            get => Required;
            set => Required = value;
        }
    }
}