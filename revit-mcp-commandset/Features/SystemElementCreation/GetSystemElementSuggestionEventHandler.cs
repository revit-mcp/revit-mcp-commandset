using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils.SystemCreation;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Features.SystemElementCreation
{
    /// <summary>
    /// 获取系统族创建参数建议事件处理器
    /// </summary>
    public class GetSystemElementSuggestionEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private string _elementType;
        private int _typeId;
        private bool _returnAll;
        private AIResult<object> _result;

        public string GetName() => "GetSystemElementSuggestionEventHandler";

        public void SetElementType(string elementType)
        {
            _elementType = elementType;
            _typeId = 0;
            _returnAll = false;
            _resetEvent.Reset();
        }

        public void SetTypeId(int typeId)
        {
            _typeId = typeId;
            _elementType = null;
            _returnAll = false;
            _resetEvent.Reset();
        }

        public void SetReturnAll(bool returnAll)
        {
            _returnAll = returnAll;
            _elementType = null;
            _typeId = 0;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                var doc = uiapp.ActiveUIDocument.Document;

                if (_returnAll)
                {
                    // 返回所有支持的系统族类型建议
                    var allSuggestions = new List<CreationRequirements>();
                    var supportedTypes = new[] { "wall", "floor" };

                    foreach (var elementType in supportedTypes)
                    {
                        allSuggestions.Add(GenerateSuggestion(elementType, doc));
                    }

                    _result = new AIResult<object>
                    {
                        Success = true,
                        Message = "获取所有系统族类型建议成功",
                        Response = allSuggestions
                    };
                }
                else if (!string.IsNullOrEmpty(_elementType))
                {
                    // 根据元素类型返回建议
                    if (!SystemElementValidator.IsElementTypeSupported(_elementType))
                    {
                        _result = new AIResult<object>
                        {
                            Success = false,
                            Message = $"不支持的系统族类型: {_elementType}。支持的类型：wall, floor"
                        };
                        return;
                    }

                    var suggestion = GenerateSuggestion(_elementType, doc);
                    _result = new AIResult<object>
                    {
                        Success = true,
                        Message = $"获取{SystemElementValidator.GetFriendlyName(_elementType)}创建参数建议成功",
                        Response = suggestion
                    };
                }
                else if (_typeId > 0)
                {
                    // 根据TypeId分析类型并返回建议
                    var element = doc.GetElement(new ElementId(_typeId));
                    if (element is WallType)
                    {
                        var suggestion = GenerateSuggestion("wall", doc);
                        suggestion.FamilyName = element.Name;
                        suggestion.Message = $"当前墙体类型: {element.Name}";
                        _result = new AIResult<object>
                        {
                            Success = true,
                            Message = "获取墙体创建参数建议成功",
                            Response = suggestion
                        };
                    }
                    else if (element is FloorType)
                    {
                        var suggestion = GenerateSuggestion("floor", doc);
                        suggestion.FamilyName = element.Name;
                        suggestion.Message = $"当前楼板类型: {element.Name}";
                        _result = new AIResult<object>
                        {
                            Success = true,
                            Message = "获取楼板创建参数建议成功",
                            Response = suggestion
                        };
                    }
                    else
                    {
                        _result = new AIResult<object>
                        {
                            Success = false,
                            Message = $"TypeId {_typeId} 不是有效的系统族类型"
                        };
                    }
                }
                else
                {
                    _result = new AIResult<object>
                    {
                        Success = false,
                        Message = "未指定元素类型或TypeId"
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
        /// 生成参数建议
        /// </summary>
        private CreationRequirements GenerateSuggestion(string elementType, Document doc)
        {
            var suggestion = new CreationRequirements
            {
                TypeId = 0, // 用户需要指定具体的类型ID
                FamilyName = SystemElementValidator.GetFriendlyName(elementType),
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
                Description = $"{SystemElementValidator.GetFriendlyName(elementType)}类型ID",
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

        /// <summary>
        /// 获取类型ID示例
        /// </summary>
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
                return 123456; // 默认示例值
            }
        }

        /// <summary>
        /// 获取标高ID示例
        /// </summary>
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
                return 789; // 默认示例值
            }
        }

        /// <summary>
        /// 获取可用类型信息
        /// </summary>
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

        public bool WaitForCompletion(int timeoutMilliseconds = 5000)
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