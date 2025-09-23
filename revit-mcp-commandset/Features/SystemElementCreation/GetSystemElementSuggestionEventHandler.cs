using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
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
        private SystemElementType? _elementType;
        private int _typeId;
        private bool _returnAll;
        private AIResult<object> _result;

        public string GetName() => "GetSystemElementSuggestionEventHandler";

        public void SetElementType(SystemElementType elementType)
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
                    var allSuggestions = new List<SystemElementSuggestion>();
                    foreach (SystemElementType type in Enum.GetValues(typeof(SystemElementType)))
                    {
                        if (type == SystemElementType.Wall || type == SystemElementType.Floor)
                        {
                            allSuggestions.Add(GenerateSuggestion(type, doc));
                        }
                    }

                    _result = new AIResult<object>
                    {
                        Success = true,
                        Message = "获取所有系统族类型建议成功",
                        Response = allSuggestions
                    };
                }
                else if (_elementType.HasValue)
                {
                    // 根据元素类型返回建议
                    var suggestion = GenerateSuggestion(_elementType.Value, doc);
                    _result = new AIResult<object>
                    {
                        Success = true,
                        Message = $"获取{_elementType.Value}创建参数建议成功",
                        Response = suggestion
                    };
                }
                else if (_typeId > 0)
                {
                    // 根据TypeId分析类型并返回建议
                    var element = doc.GetElement(new ElementId(_typeId));
                    if (element is WallType)
                    {
                        var suggestion = GenerateSuggestion(SystemElementType.Wall, doc);
                        suggestion.Description += $"\n当前墙体类型: {element.Name}";
                        _result = new AIResult<object>
                        {
                            Success = true,
                            Message = "获取墙体创建参数建议成功",
                            Response = suggestion
                        };
                    }
                    else if (element is FloorType)
                    {
                        var suggestion = GenerateSuggestion(SystemElementType.Floor, doc);
                        suggestion.Description += $"\n当前楼板类型: {element.Name}";
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

        private SystemElementSuggestion GenerateSuggestion(SystemElementType elementType, Document doc)
        {
            var suggestion = new SystemElementSuggestion
            {
                ElementType = elementType.ToString(),
                Description = GetDescription(elementType),
                RequiredParameters = GetRequiredParameters(elementType),
                OptionalParameters = GetOptionalParameters(elementType),
                Example = GetExample(elementType),
                VersionNotes = "支持 Revit 2019-2025，其他系统族类型正在开发中",
                CommonIssues = GetCommonIssues(elementType)
            };

            // 添加可用类型列表
            if (elementType == SystemElementType.Wall)
            {
                var wallTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(WallType))
                    .Cast<WallType>()
                    .Where(wt => wt.Kind == WallKind.Basic)
                    .Take(5)
                    .Select(wt => new { id = wt.Id.IntegerValue, name = wt.Name })
                    .ToList();

                if (wallTypes.Any())
                {
                    suggestion.Description += $"\n\n可用墙体类型示例：\n" +
                        string.Join("\n", wallTypes.Select(t => $"- ID: {t.id}, 名称: {t.name}"));
                }
            }
            else if (elementType == SystemElementType.Floor)
            {
                var floorTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(FloorType))
                    .Cast<FloorType>()
                    .Take(5)
                    .Select(ft => new { id = ft.Id.IntegerValue, name = ft.Name })
                    .ToList();

                if (floorTypes.Any())
                {
                    suggestion.Description += $"\n\n可用楼板类型示例：\n" +
                        string.Join("\n", floorTypes.Select(t => $"- ID: {t.id}, 名称: {t.name}"));
                }
            }

            return suggestion;
        }

        private string GetDescription(SystemElementType elementType)
        {
            switch (elementType)
            {
                case SystemElementType.Wall:
                    return "墙体系统族：用于创建建筑墙体，支持直线墙和弧形墙";
                case SystemElementType.Floor:
                    return "楼板系统族：用于创建楼板，支持任意多边形轮廓";
                default:
                    return "系统族元素";
            }
        }

        private Dictionary<string, SystemParameterInfo> GetRequiredParameters(SystemElementType elementType)
        {
            // 复用CreateSystemElementEventHandler中的逻辑
            var handler = new CreateSystemElementEventHandler();
            return handler.GetRequiredParameters(elementType);
        }

        private Dictionary<string, SystemParameterInfo> GetOptionalParameters(SystemElementType elementType)
        {
            // 复用CreateSystemElementEventHandler中的逻辑
            var handler = new CreateSystemElementEventHandler();
            return handler.GetOptionalParameters(elementType);
        }

        private object GetExample(SystemElementType elementType)
        {
            // 复用CreateSystemElementEventHandler中的逻辑
            var handler = new CreateSystemElementEventHandler();
            return handler.GetExampleParameters(elementType);
        }

        private List<string> GetCommonIssues(SystemElementType elementType)
        {
            var issues = new List<string>
            {
                "确保TypeId对应正确的系统族类型",
                "所有坐标单位为毫米，系统会自动转换为Revit内部单位"
            };

            switch (elementType)
            {
                case SystemElementType.Wall:
                    issues.Add("墙体路径不能为零长度");
                    issues.Add("墙体高度必须大于0");
                    issues.Add("自动连接功能可能影响相邻墙体");
                    break;
                case SystemElementType.Floor:
                    issues.Add("楼板轮廓必须闭合且不能自交");
                    issues.Add("至少需要3个点定义楼板边界");
                    issues.Add("点的顺序决定楼板的法线方向");
                    break;
            }

            return issues;
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