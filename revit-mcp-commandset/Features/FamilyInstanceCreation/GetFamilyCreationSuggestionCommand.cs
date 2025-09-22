using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Features.FamilyInstanceCreation
{
    /// <summary>
    /// 获取族创建建议命令
    /// </summary>
    public class GetFamilyCreationSuggestionCommand : ExternalEventCommandBase
    {
        private GetFamilyCreationSuggestionEventHandler _handler => (GetFamilyCreationSuggestionEventHandler)Handler;

        /// <summary>
        /// 命令名称
        /// </summary>
        public override string CommandName => "get_family_creation_suggestion";

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="uiApp">Revit UIApplication</param>
        public GetFamilyCreationSuggestionCommand(UIApplication uiApp)
            : base(new GetFamilyCreationSuggestionEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // 解析参数
                int elementId = parameters["data"]["elementId"].Value<int>();
                if (elementId == -1||elementId == 0)
                    throw new ArgumentNullException(nameof(elementId), "AI传入数据为空");
                
                // 设置事件处理器参数
                _handler.SetParameters(elementId);

                // 触发外部事件并等待完成
                if (RaiseAndWaitForCompletion(5000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("获取创建建议超时");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"获取族创建建议失败: {ex.Message}");
            }
        }
    }
}