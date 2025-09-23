using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils.SystemCreation;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;

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

                // 参数验证
                var validationError = ValidateParameters(_parameters);
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
                using (Transaction trans = new Transaction(doc, $"创建{_parameters.ElementType}"))
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
                            Message = $"{_parameters.ElementType} 创建成功",
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
        /// 验证参数
        /// </summary>
        private string ValidateParameters(SystemElementParameters parameters)
        {
            if (parameters == null)
                return "参数不能为空";

            if (parameters.TypeId <= 0)
                return "必须指定有效的类型ID";

            switch (parameters.ElementType)
            {
                case SystemElementType.Wall:
                    if (parameters.WallLine == null)
                        return "墙体创建需要指定路径线段（wallLine）";
                    if (parameters.Height <= 0)
                        return "墙体高度必须大于0";
                    break;

                case SystemElementType.Floor:
                    if (parameters.FloorBoundary == null || parameters.FloorBoundary.Count < 3)
                        return "楼板边界至少需要3个点";
                    break;

                default:
                    return $"不支持的系统族类型: {parameters.ElementType}";
            }

            return null; // 验证通过
        }

        /// <summary>
        /// 生成参数建议
        /// </summary>
        private SystemElementSuggestion GenerateSuggestion(SystemElementType elementType, string errorMessage)
        {
            var suggestion = new SystemElementSuggestion
            {
                ElementType = elementType.ToString(),
                Description = GetElementTypeDescription(elementType),
                RequiredParameters = GetRequiredParameters(elementType),
                OptionalParameters = GetOptionalParameters(elementType),
                Example = GetExampleParameters(elementType),
                VersionNotes = "当前支持 Revit 2019-2025 版本",
                CommonIssues = new List<string>
                {
                    $"当前错误: {errorMessage}",
                    "确保类型ID对应正确的系统族类型",
                    "所有坐标单位为毫米",
                    "楼板轮廓必须闭合且不自交"
                }
            };

            return suggestion;
        }

        /// <summary>
        /// 获取元素类型描述
        /// </summary>
        private string GetElementTypeDescription(SystemElementType elementType)
        {
            switch (elementType)
            {
                case SystemElementType.Wall:
                    return "创建墙体元素，支持直线墙、弧形墙";
                case SystemElementType.Floor:
                    return "创建楼板元素，支持任意多边形轮廓";
                default:
                    return "系统族元素";
            }
        }

        /// <summary>
        /// 获取必需参数
        /// </summary>
        public Dictionary<string, SystemParameterInfo> GetRequiredParameters(SystemElementType elementType)
        {
            var required = new Dictionary<string, SystemParameterInfo>
            {
                ["elementType"] = new SystemParameterInfo
                {
                    Type = "string",
                    Description = "系统族类型（Wall/Floor）",
                    Example = elementType.ToString()
                },
                ["typeId"] = new SystemParameterInfo
                {
                    Type = "int",
                    Description = $"{elementType}Type的ElementId",
                    Example = 123456
                }
            };

            switch (elementType)
            {
                case SystemElementType.Wall:
                    required["wallLine"] = new SystemParameterInfo
                    {
                        Type = "JZLine",
                        Description = "墙体路径线段（毫米）",
                        Example = new { p0 = new { x = 0, y = 0, z = 0 }, p1 = new { x = 5000, y = 0, z = 0 } }
                    };
                    required["height"] = new SystemParameterInfo
                    {
                        Type = "double",
                        Description = "墙体高度（毫米）",
                        Example = 3000,
                        DefaultValue = 3000
                    };
                    break;

                case SystemElementType.Floor:
                    required["floorBoundary"] = new SystemParameterInfo
                    {
                        Type = "JZPoint[]",
                        Description = "楼板边界点列表（毫米）",
                        Example = new[]
                        {
                            new { x = 0, y = 0, z = 0 },
                            new { x = 5000, y = 0, z = 0 },
                            new { x = 5000, y = 5000, z = 0 },
                            new { x = 0, y = 5000, z = 0 }
                        }
                    };
                    break;
            }

            return required;
        }

        /// <summary>
        /// 获取可选参数
        /// </summary>
        public Dictionary<string, SystemParameterInfo> GetOptionalParameters(SystemElementType elementType)
        {
            var optional = new Dictionary<string, SystemParameterInfo>
            {
                ["levelId"] = new SystemParameterInfo
                {
                    Type = "int",
                    Description = "关联标高的ElementId",
                    Example = 789
                },
                ["autoFindLevel"] = new SystemParameterInfo
                {
                    Type = "bool",
                    Description = "自动查找最近标高",
                    DefaultValue = true
                }
            };

            switch (elementType)
            {
                case SystemElementType.Wall:
                    optional["baseOffset"] = new SystemParameterInfo
                    {
                        Type = "double",
                        Description = "底部偏移（毫米）",
                        DefaultValue = 0
                    };
                    optional["isStructural"] = new SystemParameterInfo
                    {
                        Type = "bool",
                        Description = "是否为结构墙",
                        DefaultValue = false
                    };
                    optional["autoJoinWalls"] = new SystemParameterInfo
                    {
                        Type = "bool",
                        Description = "自动连接相邻墙体",
                        DefaultValue = true
                    };
                    break;

                case SystemElementType.Floor:
                    optional["topOffset"] = new SystemParameterInfo
                    {
                        Type = "double",
                        Description = "顶部偏移（毫米）",
                        DefaultValue = 0
                    };
                    optional["slope"] = new SystemParameterInfo
                    {
                        Type = "double",
                        Description = "楼板坡度（百分比）",
                        Example = 2.0
                    };
                    optional["isStructural"] = new SystemParameterInfo
                    {
                        Type = "bool",
                        Description = "是否为结构楼板",
                        DefaultValue = false
                    };
                    break;
            }

            return optional;
        }

        /// <summary>
        /// 获取参数示例
        /// </summary>
        public object GetExampleParameters(SystemElementType elementType)
        {
            switch (elementType)
            {
                case SystemElementType.Wall:
                    return new
                    {
                        elementType = "Wall",
                        typeId = 123456,
                        wallLine = new
                        {
                            p0 = new { x = 0, y = 0, z = 0 },
                            p1 = new { x = 5000, y = 0, z = 0 }
                        },
                        height = 3000,
                        levelId = 789,
                        baseOffset = 0,
                        isStructural = false,
                        autoFindLevel = true,
                        autoJoinWalls = true
                    };

                case SystemElementType.Floor:
                    return new
                    {
                        elementType = "Floor",
                        typeId = 234567,
                        floorBoundary = new[]
                        {
                            new { x = 0, y = 0, z = 0 },
                            new { x = 5000, y = 0, z = 0 },
                            new { x = 5000, y = 5000, z = 0 },
                            new { x = 0, y = 5000, z = 0 }
                        },
                        levelId = 789,
                        topOffset = 0,
                        isStructural = false,
                        autoFindLevel = true
                    };

                default:
                    return null;
            }
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