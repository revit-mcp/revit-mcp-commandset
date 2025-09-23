using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Features.SystemElementCreation.Models;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Features.SystemElementCreation
{
    /// <summary>
    /// 获取系统族创建参数建议命令
    /// </summary>
    public class GetSystemElementSuggestionCommand : ExternalEventCommandBase
    {
        private GetSystemElementSuggestionEventHandler _handler => (GetSystemElementSuggestionEventHandler)Handler;

        public override string CommandName => "get_system_element_suggestion";

        public GetSystemElementSuggestionCommand(UIApplication uiApp)
            : base(new GetSystemElementSuggestionEventHandler(), uiApp)
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

                // 尝试获取elementType
                var elementTypeToken = dataToken["elementType"];
                if (elementTypeToken != null)
                {
                    var elementTypeStr = elementTypeToken.ToString();
                    _handler.SetElementType(elementTypeStr);
                }
                // 尝试获取typeId
                else if (dataToken["typeId"] != null)
                {
                    var typeId = dataToken["typeId"].Value<int>();
                    _handler.SetTypeId(typeId);
                }
                else
                {
                    // 返回所有支持的类型
                    _handler.SetReturnAll(true);
                }

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