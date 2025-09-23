using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Features.SystemElementCreation
{
    /// <summary>
    /// 创建系统族元素命令
    /// </summary>
    public class CreateSystemElementCommand : ExternalEventCommandBase
    {
        private CreateSystemElementEventHandler _handler => (CreateSystemElementEventHandler)Handler;

        public override string CommandName => "create_system_element";

        public CreateSystemElementCommand(UIApplication uiApp)
            : base(new CreateSystemElementEventHandler(), uiApp)
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

                // 解析实际参数
                var elementParams = dataToken.ToObject<SystemElementParameters>();
                if (elementParams == null)
                {
                    return new AIResult<object>
                    {
                        Success = false,
                        Message = "参数解析失败"
                    };
                }

                // 设置EventHandler参数
                _handler.SetParameters(elementParams);

                // 触发外部事件并等待完成
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