using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Geometry;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Features.ElementCreation
{
    public class CreatePointElementCommand : ExternalEventCommandBase
    {
        private CreatePointElementEventHandler _handler => (CreatePointElementEventHandler)Handler;

        /// <summary>
        /// 命令名称
        /// </summary>
        public override string CommandName => "create_point_based_element";

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="uiApp">Revit UIApplication</param>
        public CreatePointElementCommand(UIApplication uiApp)
            : base(new CreatePointElementEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                List<PointElement> data = new List<PointElement>();
                // 解析参数
                data = parameters["data"].ToObject<List<PointElement>>();
                if (data == null)
                    throw new ArgumentNullException(nameof(data), "AI传入数据为空");

                // 设置点状构件体参数
                _handler.SetParameters(data);

                // 触发外部事件并等待完成
                if (RaiseAndWaitForCompletion(10000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("创建点状构件操作超时");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"创建点状构件失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    ///     点状构件
    /// </summary>
    public class PointElement
    {
        public PointElement()
        {
            Parameters = new Dictionary<string, double>();
        }

        /// <summary>
        ///     构件类型
        /// </summary>
        [JsonProperty("category")]
        public string Category { get; set; } = "INVALID";

        /// <summary>
        ///     类型Id
        /// </summary>
        [JsonProperty("typeId")]
        public int TypeId { get; set; } = -1;

        /// <summary>
        ///     定位点坐标
        /// </summary>
        [JsonProperty("locationPoint")]
        public JZPoint LocationPoint { get; set; }

        /// <summary>
        ///     宽度
        /// </summary>
        [JsonProperty("width")]
        public double Width { get; set; } = -1;

        /// <summary>
        ///     深度
        /// </summary>
        [JsonProperty("depth")]
        public double Depth { get; set; }

        /// <summary>
        ///     高度
        /// </summary>
        [JsonProperty("height")]
        public double Height { get; set; }

        /// <summary>
        ///     底部标高
        /// </summary>
        [JsonProperty("baseLevel")]
        public double BaseLevel { get; set; }

        /// <summary>
        ///     底部偏移
        /// </summary>
        [JsonProperty("baseOffset")]
        public double BaseOffset { get; set; }

        /// <summary>
        ///     参数化属性
        /// </summary>
        [JsonProperty("parameters")]
        public Dictionary<string, double> Parameters { get; set; }
    }
}