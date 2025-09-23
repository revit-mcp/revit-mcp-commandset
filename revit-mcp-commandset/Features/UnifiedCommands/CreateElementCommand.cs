using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Features.UnifiedCommands.Models;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Features.UnifiedCommands
{
    /// <summary>
    /// 统一元素创建命令
    /// </summary>
    public class CreateElementCommand : ExternalEventCommandBase
    {
        private CreateElementEventHandler _handler => (CreateElementEventHandler)Handler;

        public override string CommandName => "create_element";

        public CreateElementCommand(UIApplication uiApp)
            : base(new CreateElementEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // 解析强类型参数
                var dataToken = parameters["data"];
                if (dataToken == null)
                {
                    return new AIResult<object>
                    {
                        Success = false,
                        Message = "参数格式错误：缺少 'data' 包裹层"
                    };
                }

                var creationParams = dataToken.ToObject<ElementCreationParameters>();
                if (creationParams == null)
                {
                    return new AIResult<object>
                    {
                        Success = false,
                        Message = "参数解析失败"
                    };
                }

                // 设置Handler参数
                _handler.SetParameters(creationParams);

                // 执行并返回结果
                if (RaiseAndWaitForCompletion(10000))
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