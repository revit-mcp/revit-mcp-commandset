using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Features.UnifiedCommands.Models;
using RevitMCPCommandSet.Features.UnifiedCommands.Utils;
using System.Linq;
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

            // 系统族建议
            var systemSuggestions = new List<CreationRequirements>();
            var supportedTypes = new[] { "wall", "floor" };

            foreach (var elementType in supportedTypes)
            {
                systemSuggestions.Add(GenerateSystemSuggestion(elementType, doc));
            }

            allSuggestions.Add(new
            {
                Category = "System",
                Description = "系统族创建建议",
                Data = systemSuggestions
            });

            // 族类型建议说明
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
            try
            {
                if (!string.IsNullOrEmpty(parameters.ElementType))
                {
                    if (!IsElementTypeSupported(parameters.ElementType))
                    {
                        return new AIResult<object>
                        {
                            Success = false,
                            Message = $"不支持的系统族类型: {parameters.ElementType}。支持的类型：wall, floor"
                        };
                    }

                    var suggestion = GenerateSystemSuggestion(parameters.ElementType, doc);
                    return new AIResult<object>
                    {
                        Success = true,
                        Message = $"获取{GetFriendlyName(parameters.ElementType)}创建参数建议成功",
                        Response = suggestion
                    };
                }
                else if (parameters.ElementId.HasValue)
                {
                    var element = doc.GetElement(new ElementId(parameters.ElementId.Value));
                    if (element == null)
                    {
                        return new AIResult<object>
                        {
                            Success = false,
                            Message = $"ElementId {parameters.ElementId.Value} 无效，未找到对应的元素"
                        };
                    }

                    // 判断是否为族类型
                    if (element is FamilySymbol)
                    {
                        return new AIResult<object>
                        {
                            Success = false,
                            Message = $"ElementId {parameters.ElementId.Value} 是族类型，请使用 elementClass='Family' 获取族创建建议"
                        };
                    }
                    else if (element is WallType)
                    {
                        var suggestion = GenerateSystemSuggestion("wall", doc);
                        suggestion.FamilyName = element.Name;
                        suggestion.Message = $"当前墙体类型: {element.Name}";
                        return new AIResult<object>
                        {
                            Success = true,
                            Message = "获取墙体创建参数建议成功",
                            Response = suggestion
                        };
                    }
                    else if (element is FloorType)
                    {
                        var suggestion = GenerateSystemSuggestion("floor", doc);
                        suggestion.FamilyName = element.Name;
                        suggestion.Message = $"当前楼板类型: {element.Name}";
                        return new AIResult<object>
                        {
                            Success = true,
                            Message = "获取楼板创建参数建议成功",
                            Response = suggestion
                        };
                    }
                    else
                    {
                        return new AIResult<object>
                        {
                            Success = false,
                            Message = $"ElementId {parameters.ElementId.Value} 不是有效的系统族类型。支持的系统族类型：WallType、FloorType"
                        };
                    }
                }
                else
                {
                    return GetAllSuggestions(doc);
                }
            }
            catch (Exception ex)
            {
                return new AIResult<object>
                {
                    Success = false,
                    Message = $"获取系统族建议失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 获取族创建建议
        /// </summary>
        private AIResult<object> GetFamilySuggestions(Document doc, int elementId)
        {
            try
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

                if (!(element is FamilySymbol familySymbol))
                {
                    return new AIResult<object>
                    {
                        Success = false,
                        Message = $"ElementId {elementId} 不是族类型（FamilySymbol）"
                    };
                }

                var suggestion = GenerateFamilySuggestion(familySymbol, doc);
                return new AIResult<object>
                {
                    Success = true,
                    Message = $"获取族 '{familySymbol.FamilyName}' 创建参数建议成功",
                    Response = suggestion
                };
            }
            catch (Exception ex)
            {
                return new AIResult<object>
                {
                    Success = false,
                    Message = $"获取族创建建议失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 自动检测类型并获取建议
        /// </summary>
        private AIResult<object> AutoDetectAndGetSuggestions(Document doc, int elementId)
        {
            // 使用统一类型检测
            var elementClass = ElementUtilityService.DetectElementClassById(doc, elementId);

            if (elementClass == null)
            {
                return new AIResult<object>
                {
                    Success = false,
                    Message = $"ElementId {elementId} 无效或不是有效的创建类型"
                };
            }

            switch (elementClass)
            {
                case "Family":
                    // 是族类型，获取族建议
                    return GetFamilySuggestions(doc, elementId);

                case "System":
                    // 是系统族类型，获取系统族建议
                    var systemParams = new ElementSuggestionParameters
                    {
                        ElementClass = "System",
                        ElementId = elementId
                    };
                    return GetSystemSuggestions(doc, systemParams);

                default:
                    return new AIResult<object>
                    {
                        Success = false,
                        Message = $"不支持的元素类型: {elementClass}"
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

        #region 私有辅助方法

        /// <summary>
        /// 生成系统族建议
        /// </summary>
        private CreationRequirements GenerateSystemSuggestion(string elementType, Document doc)
        {
            var suggestion = new CreationRequirements
            {
                TypeId = 0,
                FamilyName = GetFriendlyName(elementType),
                Parameters = new Dictionary<string, ParameterInfo>()
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
                Description = $"{GetFriendlyName(elementType)}类型ID",
                Example = GetTypeIdExample(elementType, doc),
                IsRequired = true
            };

            // 添加通用可选参数
            suggestion.Parameters["levelId"] = new ParameterInfo
            {
                Type = "int",
                Description = "关联标高ID（可选，会自动查找）",
                Example = GetLevelIdExample(doc),
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

            // 添加可用类型列表到消息中
            suggestion.Message = GetAvailableTypesInfo(elementType, doc);

            return suggestion;
        }

        /// <summary>
        /// 生成族建议
        /// </summary>
        private CreationRequirements GenerateFamilySuggestion(FamilySymbol familySymbol, Document doc)
        {
            var suggestion = new CreationRequirements
            {
                TypeId = familySymbol.Id.IntegerValue,
                FamilyName = familySymbol.FamilyName,
                Parameters = new Dictionary<string, ParameterInfo>()
            };

            // 族放置基本参数
            suggestion.Parameters["typeId"] = new ParameterInfo
            {
                Type = "int",
                Description = "族类型ID",
                Example = familySymbol.Id.IntegerValue,
                IsRequired = true
            };

            // 一般族实例都需要位置信息
            suggestion.Parameters["locationPoint"] = new ParameterInfo
            {
                Type = "JZPoint",
                Description = "放置位置坐标（毫米）",
                Example = new { x = 0, y = 0, z = 0 },
                IsRequired = true
            };

            // 可选参数
            suggestion.Parameters["autoFindLevel"] = new ParameterInfo
            {
                Type = "bool",
                Description = "自动查找最近标高",
                Example = true,
                IsRequired = false
            };

            suggestion.Parameters["autoFindHost"] = new ParameterInfo
            {
                Type = "bool",
                Description = "自动查找宿主元素",
                Example = true,
                IsRequired = false
            };

            suggestion.Message = $"族名称: {familySymbol.FamilyName}, 类型: {familySymbol.Name}";

            return suggestion;
        }

        /// <summary>
        /// 添加墙体参数
        /// </summary>
        private void AddWallParameters(Dictionary<string, ParameterInfo> parameters)
        {
            parameters["wallParameters"] = new ParameterInfo
            {
                Type = "WallSpecificParameters",
                Description = "墙体特有参数（包含路径、高度、偏移等）",
                Example = new
                {
                    line = new { p0 = new { x = 0, y = 0, z = 0 }, p1 = new { x = 5000, y = 0, z = 0 } },
                    height = 3000.0,
                    baseOffset = 0.0,
                    autoJoinWalls = true
                },
                IsRequired = true
            };
        }

        /// <summary>
        /// 添加楼板参数
        /// </summary>
        private void AddFloorParameters(Dictionary<string, ParameterInfo> parameters)
        {
            parameters["floorParameters"] = new ParameterInfo
            {
                Type = "FloorSpecificParameters",
                Description = "楼板特有参数（包含边界、偏移、坡度等）",
                Example = new
                {
                    boundary = new[]
                    {
                        new { x = 0, y = 0, z = 0 },
                        new { x = 5000, y = 0, z = 0 },
                        new { x = 5000, y = 5000, z = 0 },
                        new { x = 0, y = 5000, z = 0 }
                    },
                    topOffset = 0.0,
                    slope = 2.0
                },
                IsRequired = true
            };
        }

        private bool IsElementTypeSupported(string elementType)
        {
            var supportedTypes = new[] { "wall", "floor" };
            return supportedTypes.Contains(elementType.ToLower());
        }

        private string GetFriendlyName(string elementType)
        {
            return elementType.ToLower() switch
            {
                "wall" => "墙体",
                "floor" => "楼板",
                _ => elementType
            };
        }

        private int GetTypeIdExample(string elementType, Document doc)
        {
            try
            {
                switch (elementType.ToLower())
                {
                    case "wall":
                        var wallType = new FilteredElementCollector(doc)
                            .OfClass(typeof(WallType))
                            .Cast<WallType>()
                            .FirstOrDefault(wt => wt.Kind == WallKind.Basic);
                        return wallType?.Id.IntegerValue ?? 123456;

                    case "floor":
                        var floorType = new FilteredElementCollector(doc)
                            .OfClass(typeof(FloorType))
                            .Cast<FloorType>()
                            .FirstOrDefault();
                        return floorType?.Id.IntegerValue ?? 234567;

                    default:
                        return 123456;
                }
            }
            catch
            {
                return 123456;
            }
        }

        private int GetLevelIdExample(Document doc)
        {
            try
            {
                var level = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .FirstOrDefault();
                return level?.Id.IntegerValue ?? 789;
            }
            catch
            {
                return 789;
            }
        }

        private string GetAvailableTypesInfo(string elementType, Document doc)
        {
            try
            {
                switch (elementType.ToLower())
                {
                    case "wall":
                        var wallTypes = new FilteredElementCollector(doc)
                            .OfClass(typeof(WallType))
                            .Cast<WallType>()
                            .Where(wt => wt.Kind == WallKind.Basic)
                            .Take(5)
                            .Select(wt => $"ID: {wt.Id.IntegerValue}, 名称: {wt.Name}")
                            .ToList();

                        return wallTypes.Any()
                            ? $"可用墙体类型示例：\n{string.Join("\n", wallTypes)}"
                            : "支持 Revit 2019-2025 版本";

                    case "floor":
                        var floorTypes = new FilteredElementCollector(doc)
                            .OfClass(typeof(FloorType))
                            .Cast<FloorType>()
                            .Take(5)
                            .Select(ft => $"ID: {ft.Id.IntegerValue}, 名称: {ft.Name}")
                            .ToList();

                        return floorTypes.Any()
                            ? $"可用楼板类型示例：\n{string.Join("\n", floorTypes)}"
                            : "支持 Revit 2019-2025 版本";

                    default:
                        return "支持 Revit 2019-2025 版本";
                }
            }
            catch
            {
                return "支持 Revit 2019-2025 版本";
            }
        }

        #endregion
    }
}