using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Features.SystemElementCreation.Models;
using RevitMCPCommandSet.Utils.SystemCreation;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using RevitMCPCommandSet.Features.SystemElementCreation.Models;

namespace RevitMCPCommandSet.Features.SystemElementCreation
{
    /// <summary>
    /// 创建系统族元素事件处理器（集成智能功能）
    /// </summary>
    public class CreateSystemElementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private SystemElementParameters _parameters;
        private AIResult<object> _result;

        public string GetName() => "CreateSystemElementEventHandler";

        /// <summary>
        /// 设置参数
        /// </summary>
        public void SetParameters(SystemElementParameters parameters)
        {
            _parameters = parameters;
            _resetEvent.Reset();
        }

        /// <summary>
        /// 执行创建
        /// </summary>
        public void Execute(UIApplication uiapp)
        {
            try
            {
                var doc = uiapp.ActiveUIDocument.Document;

                // 参数验证 - 使用SystemElementValidator
                var validationError = SystemElementValidator.ValidateParameters(_parameters);
                if (!string.IsNullOrEmpty(validationError))
                {
                    _result = new AIResult<object>
                    {
                        Success = false,
                        Message = validationError,
                        Response = GenerateSuggestion(_parameters.ElementType, validationError)
                    };
                    return;
                }

                // 创建元素
                using (Transaction trans = new Transaction(doc, $"创建{SystemElementValidator.GetFriendlyName(_parameters.ElementType)}"))
                {
                    trans.Start();

                    try
                    {
                        var creator = new SystemElementCreator(doc);
                        var element = creator.Create(_parameters);

                        trans.Commit();

                        // 返回成功结果
                        _result = new AIResult<object>
                        {
                            Success = true,
                            Message = $"{SystemElementValidator.GetFriendlyName(_parameters.ElementType)} 创建成功",
                            Response = new
                            {
                                elementId = element.Id.IntegerValue,
                                elementType = element.GetType().Name,
                                elementName = element.Name,
                                category = element.Category?.Name
                            }
                        };
                    }
                    catch (Exception ex)
                    {
                        trans.RollBack();

                        // 创建失败，生成参数建议
                        _result = new AIResult<object>
                        {
                            Success = false,
                            Message = $"创建失败: {ex.Message}",
                            Response = GenerateSuggestion(_parameters.ElementType, ex.Message)
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _result = new AIResult<object>
                {
                    Success = false,
                    Message = $"执行错误: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        /// <summary>
        /// 生成参数建议
        /// </summary>
        private CreationRequirements GenerateSuggestion(string elementType, string errorMessage)
        {
            var suggestion = new CreationRequirements
            {
                TypeId = 0, // 用户需要指定具体的类型ID
                FamilyName = SystemElementValidator.GetFriendlyName(elementType),
                Parameters = new Dictionary<string, ParameterInfo>(),
                Message = $"当前错误: {errorMessage}"
            };

            // 添加通用必需参数
            suggestion.Parameters["elementType"] = new ParameterInfo
            {
                Type = "string",
                Description = "系统族类型",
                Example = elementType,
                IsRequired = true
            };

            suggestion.Parameters["typeId"] = new ParameterInfo
            {
                Type = "int",
                Description = $"{SystemElementValidator.GetFriendlyName(elementType)}类型ID",
                Example = 123456,
                IsRequired = true
            };

            // 添加通用可选参数
            suggestion.Parameters["levelId"] = new ParameterInfo
            {
                Type = "int",
                Description = "关联标高ID（可选，会自动查找）",
                Example = 789,
                IsRequired = false
            };

            suggestion.Parameters["autoFindLevel"] = new ParameterInfo
            {
                Type = "bool",
                Description = "自动查找最近标高",
                Example = true,
                IsRequired = false
            };

            suggestion.Parameters["isStructural"] = new ParameterInfo
            {
                Type = "bool",
                Description = "是否为结构构件",
                Example = false,
                IsRequired = false
            };

            // 根据类型添加特定参数
            switch (elementType.ToLower())
            {
                case "wall":
                    AddWallParameters(suggestion.Parameters);
                    break;
                case "floor":
                    AddFloorParameters(suggestion.Parameters);
                    break;
            }

            return suggestion;
        }

        /// <summary>
        /// 添加墙体参数
        /// </summary>
        private void AddWallParameters(Dictionary<string, ParameterInfo> parameters)
        {
            parameters["wallLine"] = new ParameterInfo
            {
                Type = "JZLine",
                Description = "墙体路径线段（毫米）",
                Example = new { p0 = new { x = 0, y = 0, z = 0 }, p1 = new { x = 5000, y = 0, z = 0 } },
                IsRequired = true
            };

            parameters["height"] = new ParameterInfo
            {
                Type = "double",
                Description = "墙体高度（毫米）",
                Example = 3000.0,
                IsRequired = true
            };

            parameters["baseOffset"] = new ParameterInfo
            {
                Type = "double",
                Description = "底部偏移（毫米）",
                Example = 0.0,
                IsRequired = false
            };

            parameters["autoJoinWalls"] = new ParameterInfo
            {
                Type = "bool",
                Description = "自动连接相邻墙体",
                Example = true,
                IsRequired = false
            };
        }

        /// <summary>
        /// 添加楼板参数
        /// </summary>
        private void AddFloorParameters(Dictionary<string, ParameterInfo> parameters)
        {
            parameters["floorBoundary"] = new ParameterInfo
            {
                Type = "JZPoint[]",
                Description = "楼板边界点列表（毫米，按顺序连接）",
                Example = new[]
                {
                    new { x = 0, y = 0, z = 0 },
                    new { x = 5000, y = 0, z = 0 },
                    new { x = 5000, y = 5000, z = 0 },
                    new { x = 0, y = 5000, z = 0 }
                },
                IsRequired = true
            };

            parameters["topOffset"] = new ParameterInfo
            {
                Type = "double",
                Description = "顶部偏移（毫米）",
                Example = 0.0,
                IsRequired = false
            };

            parameters["slope"] = new ParameterInfo
            {
                Type = "double",
                Description = "楼板坡度（百分比，可选）",
                Example = 2.0,
                IsRequired = false
            };
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public AIResult<object> GetResult()
        {
            return _result ?? new AIResult<object>
            {
                Success = false,
                Message = "未执行或结果未设置"
            };
        }
    }
}