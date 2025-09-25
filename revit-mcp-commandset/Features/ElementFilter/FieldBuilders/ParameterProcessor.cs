using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitMCPCommandSet.Features.ElementFilter.Models;
using RevitMCPCommandSet.Models.Common;

namespace RevitMCPCommandSet.Features.ElementFilter.FieldBuilders
{
    /// <summary>
    /// 参数处理器
    /// 负责处理元素参数的提取和格式化
    /// 支持实例参数、类型参数、内置参数等
    /// </summary>
    public class ParameterProcessor
    {
        /// <summary>
        /// 处理元素参数并添加到结果中
        /// </summary>
        /// <param name="context">字段上下文</param>
        /// <param name="request">参数请求配置</param>
        public void ProcessParameters(FieldContext context, ParameterRequest request)
        {
            if (request == null) return;

            try
            {
                var parameters = new Dictionary<string, object>();

                // 处理单个参数名请求
                if (!string.IsNullOrEmpty(request.SingleName))
                {
                    var paramValue = GetParameterValue(context.Element, context.TypeElement, request.SingleName, request);
                    if (paramValue != null)
                    {
                        parameters[request.SingleName] = paramValue;
                    }
                    else
                    {
                        context.AddWarning($"未找到参数: {request.SingleName}");
                    }
                }
                else
                {
                    // 处理参数名列表请求
                    if (request.Names != null && request.Names.Count > 0)
                    {
                        foreach (var paramName in request.Names)
                        {
                            var paramValue = GetParameterValue(context.Element, context.TypeElement, paramName, request);
                            if (paramValue != null)
                            {
                                parameters[paramName] = paramValue;
                            }
                            else
                            {
                                context.AddWarning($"未找到参数: {paramName}");
                            }
                        }
                    }
                    else
                    {
                        // 获取所有参数
                        parameters = GetAllParameters(context.Element, context.TypeElement, request);
                    }
                }

                // 将结果添加到上下文
                if (parameters.Count > 0)
                {
                    if (request.Flatten)
                    {
                        // 扁平化模式：参数直接添加到根级
                        foreach (var kvp in parameters)
                        {
                            context.Result[kvp.Key] = kvp.Value;
                        }
                    }
                    else
                    {
                        // 分层模式：参数放在 parameters 节点下
                        context.Result["parameters"] = parameters;
                    }
                }
            }
            catch (Exception ex)
            {
                // 静默失败，在调试时记录错误
                System.Diagnostics.Debug.WriteLine($"参数处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取指定参数的值
        /// </summary>
        private object GetParameterValue(Element element, Element typeElement, string paramName, ParameterRequest request)
        {
            try
            {
                object value = null;

                // 实例参数
                if (request.IncludeInstance)
                {
                    value = GetParameterFromElement(element, paramName);
                    if (value != null) return value;
                }

                // 类型参数
                if (request.IncludeType && typeElement != null)
                {
                    value = GetParameterFromElement(typeElement, paramName);
                    if (value != null) return value;
                }

                // 内置参数
                if (request.IncludeBuiltIn)
                {
                    value = GetBuiltInParameter(element, paramName);
                    if (value != null) return value;

                    if (typeElement != null)
                    {
                        value = GetBuiltInParameter(typeElement, paramName);
                        if (value != null) return value;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取所有参数
        /// </summary>
        private Dictionary<string, object> GetAllParameters(Element element, Element typeElement, ParameterRequest request)
        {
            var parameters = new Dictionary<string, object>();

            try
            {
                // 实例参数
                if (request.IncludeInstance)
                {
                    foreach (Parameter param in element.Parameters)
                    {
                        var value = GetParameterValue(param);
                        if (value != null && !parameters.ContainsKey(param.Definition.Name))
                        {
                            parameters[param.Definition.Name] = value;
                        }
                    }
                }

                // 类型参数
                if (request.IncludeType && typeElement != null)
                {
                    foreach (Parameter param in typeElement.Parameters)
                    {
                        var value = GetParameterValue(param);
                        if (value != null && !parameters.ContainsKey(param.Definition.Name))
                        {
                            parameters[param.Definition.Name] = value;
                        }
                    }
                }

                return parameters;
            }
            catch
            {
                return parameters;
            }
        }

        /// <summary>
        /// 从元素获取参数值
        /// </summary>
        private object GetParameterFromElement(Element element, string paramName)
        {
            try
            {
                // 通过名称查找参数
                var param = element.Parameters.Cast<Parameter>()
                    .FirstOrDefault(p => p.Definition.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase));

                return param != null ? GetParameterValue(param) : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取内置参数值
        /// </summary>
        private object GetBuiltInParameter(Element element, string paramName)
        {
            try
            {
                // 尝试将参数名解析为内置参数枚举
                if (Enum.TryParse<BuiltInParameter>(paramName, true, out var builtInParam))
                {
                    var param = element.get_Parameter(builtInParam);
                    return param != null ? GetParameterValue(param) : null;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取参数值并转换为合适的类型
        /// </summary>
        private object GetParameterValue(Parameter param)
        {
            if (param == null || !param.HasValue)
                return null;

            try
            {
                switch (param.StorageType)
                {
                    case StorageType.Integer:
                        var intValue = param.AsInteger();
                        // 如果是ElementId，返回整数值
                        return intValue;

                    case StorageType.Double:
                        var doubleValue = param.AsDouble();
                        // 对于长度单位，转换为毫米
                        if (IsLengthParameter(param))
                        {
                            return doubleValue * 304.8;
                        }
                        // 对于面积单位，转换为平方米
                        else if (IsAreaParameter(param))
                        {
                            return doubleValue * 304.8 * 304.8 / 1000000.0;
                        }
                        return doubleValue;

                    case StorageType.String:
                        return param.AsString();

                    case StorageType.ElementId:
                        var elementId = param.AsElementId();
                        return elementId.IntegerValue;

                    default:
                        return param.AsValueString();
                }
            }
            catch
            {
                // 如果转换失败，尝试获取显示字符串
                try
                {
                    return param.AsValueString();
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// 判断是否为长度参数
        /// </summary>
        private bool IsLengthParameter(Parameter param)
        {
            try
            {
#if REVIT2021_OR_GREATER
                var unitType = param.Definition.GetDataType();
                return unitType == SpecTypeId.Length;
#else
                // 对于老版本 Revit，使用 UnitType
                var unitType = param.Definition.UnitType;
                return unitType == UnitType.UT_Length;
#endif
            }
            catch
            {
                // 回退到名称匹配
                var paramName = param.Definition.Name.ToLower();
                return paramName.Contains("length") ||
                       paramName.Contains("width") ||
                       paramName.Contains("height") ||
                       paramName.Contains("thickness") ||
                       paramName.Contains("offset");
            }
        }

        /// <summary>
        /// 判断是否为面积参数
        /// </summary>
        private bool IsAreaParameter(Parameter param)
        {
            try
            {
#if REVIT2021_OR_GREATER
                var unitType = param.Definition.GetDataType();
                return unitType == SpecTypeId.Area;
#else
                // 对于老版本 Revit，使用 UnitType
                var unitType = param.Definition.UnitType;
                return unitType == UnitType.UT_Area;
#endif
            }
            catch
            {
                // 回退到名称匹配
                var paramName = param.Definition.Name.ToLower();
                return paramName.Contains("area");
            }
        }
    }
}