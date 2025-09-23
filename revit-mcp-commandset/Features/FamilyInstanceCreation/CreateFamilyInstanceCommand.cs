using System;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Features.FamilyInstanceCreation.Models;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Features.FamilyInstanceCreation.Models;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Features.FamilyInstanceCreation
{
    /// <summary>
    /// 创建族实例命令
    /// </summary>
    public class CreateFamilyInstanceCommand : ExternalEventCommandBase
    {
        private CreateFamilyInstanceEventHandler _handler => (CreateFamilyInstanceEventHandler)Handler;

        /// <summary>
        /// 命令名称
        /// </summary>
        public override string CommandName => "create_family_instance";

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="uiApp">Revit UIApplication</param>
        public CreateFamilyInstanceCommand(UIApplication uiApp)
            : base(new CreateFamilyInstanceEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // 解析参数
                var creationParams = parameters["data"].ToObject<FamilyCreationParameters>();
                if (creationParams == null)
                    throw new ArgumentNullException("data", "创建参数不能为空");

                // 设置事件处理器参数
                _handler.SetParameters(creationParams);

                // 触发外部事件并等待完成
                if (RaiseAndWaitForCompletion(10000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("创建族实例超时");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"创建族实例失败: {ex.Message}");
            }
        }
    }
}