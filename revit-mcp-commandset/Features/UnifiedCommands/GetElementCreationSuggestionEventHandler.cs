using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Features.UnifiedCommands.Models;
using RevitMCPCommandSet.Features.FamilyInstanceCreation;
using RevitMCPCommandSet.Features.SystemElementCreation;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Features.UnifiedCommands
{
    /// <summary>
    /// 统一元素创建参数建议事件处理器
    /// </summary>
    public class GetElementCreationSuggestionEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private ElementSuggestionParameters _parameters;
        private AIResult<object> _result;
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public string GetName() => "GetElementCreationSuggestionEventHandler";

        /// <summary>
        /// 设置查询参数
        /// </summary>
        /// <param name="parameters">建议查询参数</param>
        public void SetParameters(ElementSuggestionParameters parameters)
        {
            _parameters = parameters;
            _result = null;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                var doc = uiapp.ActiveUIDocument.Document;

                // 返回所有选项
                if (_parameters.ReturnAll ||
                    (string.IsNullOrEmpty(_parameters.ElementClass) &&
                     _parameters.ElementId == null &&
                     string.IsNullOrEmpty(_parameters.ElementType)))
                {
                    _result = GetAllSuggestions(doc);
                    return;
                }

                // 根据参数类型生成建议
                if (_parameters.ElementClass == "System" ||
                    !string.IsNullOrEmpty(_parameters.ElementType))
                {
                    _result = GetSystemSuggestions(doc, _parameters);
                }
                else if (_parameters.ElementClass == "Family" &&
                         _parameters.ElementId.HasValue)
                {
                    _result = GetFamilySuggestions(doc, _parameters.ElementId.Value);
                }
                else if (_parameters.ElementId.HasValue)
                {
                    // 自动检测类型
                    _result = AutoDetectAndGetSuggestions(doc, _parameters.ElementId.Value);
                }
                else
                {
                    _result = new AIResult<object>
                    {
                        Success = false,
                        Message = "无法识别查询参数"
                    };
                }
            }
            catch (Exception ex)
            {
                _result = new AIResult<object>
                {
                    Success = false,
                    Message = $"获取建议失败: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        /// <summary>
        /// 获取所有建议
        /// </summary>
        private AIResult<object> GetAllSuggestions(Document doc)
        {
            var allSuggestions = new List<object>();

            // 获取系统族建议
            var systemHandler = new GetSystemElementSuggestionEventHandler();
            systemHandler.SetReturnAll(true);
            systemHandler.Execute(new UIApplication(doc.Application));
            var systemResults = systemHandler.GetResult();

            if (systemResults.Success)
            {
                allSuggestions.Add(new
                {
                    Category = "System",
                    Description = "系统族创建建议",
                    Data = systemResults.Response
                });
            }

            // 添加族类型建议说明
            allSuggestions.Add(new
            {
                Category = "Family",
                Description = "族实例创建建议（需要提供具体的typeId）",
                Message = "使用 elementClass='Family' 和 elementId=<族类型ID> 获取具体族的创建参数建议"
            });

            return new AIResult<object>
            {
                Success = true,
                Message = "获取所有元素创建建议成功",
                Response = allSuggestions
            };
        }

        /// <summary>
        /// 获取系统族建议
        /// </summary>
        private AIResult<object> GetSystemSuggestions(Document doc, ElementSuggestionParameters parameters)
        {
            var systemHandler = new GetSystemElementSuggestionEventHandler();

            if (!string.IsNullOrEmpty(parameters.ElementType))
            {
                systemHandler.SetElementType(parameters.ElementType);
            }
            else if (parameters.ElementId.HasValue)
            {
                systemHandler.SetElementId(parameters.ElementId.Value);
            }
            else
            {
                systemHandler.SetReturnAll(true);
            }

            systemHandler.Execute(new UIApplication(doc.Application));
            var result = systemHandler.GetResult();

            return new AIResult<object>
            {
                Success = result.Success,
                Message = result.Message,
                Response = result.Response
            };
        }

        /// <summary>
        /// 获取族创建建议
        /// </summary>
        private AIResult<object> GetFamilySuggestions(Document doc, int elementId)
        {
            var familyHandler = new GetFamilyCreationSuggestionEventHandler();
            familyHandler.SetParameters(elementId);
            familyHandler.Execute(new UIApplication(doc.Application));
            var result = familyHandler.Result;

            return new AIResult<object>
            {
                Success = result.Success,
                Message = result.Message,
                Response = result.Response
            };
        }

        /// <summary>
        /// 自动检测类型并获取建议
        /// </summary>
        private AIResult<object> AutoDetectAndGetSuggestions(Document doc, int elementId)
        {
            var element = doc.GetElement(new ElementId(elementId));
            if (element == null)
            {
                return new AIResult<object>
                {
                    Success = false,
                    Message = $"ElementId {elementId} 无效，未找到对应的元素"
                };
            }

            if (element is FamilySymbol)
            {
                // 是族类型，获取族建议
                return GetFamilySuggestions(doc, elementId);
            }
            else if (element is WallType || element is FloorType)
            {
                // 是系统族类型，获取系统族建议
                var systemParams = new ElementSuggestionParameters
                {
                    ElementClass = "System",
                    ElementId = elementId
                };
                return GetSystemSuggestions(doc, systemParams);
            }
            else
            {
                return new AIResult<object>
                {
                    Success = false,
                    Message = $"ElementId {elementId} 不是有效的创建类型。当前元素类型: {element.GetType().Name}。支持的类型：FamilySymbol（族类型）、WallType（墙体类型）、FloorType（楼板类型）"
                };
            }
        }

        public AIResult<object> GetResult() => _result ?? new AIResult<object>
        {
            Success = false,
            Message = "操作未执行或结果为空"
        };

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}