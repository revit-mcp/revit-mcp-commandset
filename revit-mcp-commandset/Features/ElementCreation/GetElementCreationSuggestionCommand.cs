using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Features.ElementCreation.Models;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Features.ElementCreation
{
    /// <summary>
    /// 统一元素创建参数建议命令
    /// </summary>
    public class GetElementCreationSuggestionCommand : ExternalEventCommandBase
    {
        private GetElementCreationSuggestionEventHandler _handler => (GetElementCreationSuggestionEventHandler)Handler;

        public override string CommandName => "get_element_creation_suggestion";

        public GetElementCreationSuggestionCommand(UIApplication uiApp)
            : base(new GetElementCreationSuggestionEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // 解析data包裹层
                var dataToken = parameters["data"];
                if (dataToken == null)
                {
                    return new AIResult<object>
                    {
                        Success = false,
                        Message = "参数格式错误：缺少 'data' 包裹层"
                    };
                }

                var suggestionParams = dataToken.ToObject<ElementSuggestionParameters>();
                if (suggestionParams == null)
                {
                    // 如果无法解析为强类型，则创建默认参数（返回所有建议）
                    suggestionParams = new ElementSuggestionParameters { ReturnAll = true };
                }

                // 设置Handler参数
                _handler.SetParameters(suggestionParams);

                // 触发外部事件并等待完成
                if (RaiseAndWaitForCompletion(5000))
                {
                    return _handler.GetResult();
                }
                else
                {
                    return new AIResult<object>
                    {
                        Success = false,
                        Message = "操作超时"
                    };
                }
            }
            catch (Exception ex)
            {
                return new AIResult<object>
                {
                    Success = false,
                    Message = $"命令执行失败: {ex.Message}"
                };
            }
        }
    }
}