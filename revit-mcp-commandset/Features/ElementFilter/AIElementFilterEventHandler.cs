using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Features.ElementFilter.Models;
using RevitMCPCommandSet.Features.ElementFilter.FieldBuilders;
using RevitMCPCommandSet.Models.Geometry;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Features.ElementFilter
{
    public class AIElementFilterEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;
        private Autodesk.Revit.ApplicationServices.Application app => uiApp.Application;

        /// <summary>
        /// 事件等待对象
        /// </summary>
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        /// <summary>
        /// 创建数据（传入数据）
        /// </summary>
        public FilterSetting FilterSetting { get; private set; }

        /// <summary>
        /// 执行结果（传出数据）
        /// </summary>
        public AIResult<List<object>> Result { get; private set; }

        /// <summary>
        /// 缺失的元素ID列表（用于elementIds查询时）
        /// </summary>
        private static List<int> MissingElementIds { get; set; }

        /// <summary>
        /// 设置创建的参数
        /// </summary>
        public void SetParameters(FilterSetting data)
        {
            FilterSetting = data;
            MissingElementIds = null; // 重置缺失元素列表
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                var elementInfoList = new List<object>();
                // 检查过滤器设置是否有效
                if (!FilterSetting.Validate(out string errorMessage))
                    throw new Exception(errorMessage);
                // 获取指定条件元素的Id
                var elementList = GetFilteredElements(doc, FilterSetting);
                if (elementList == null || !elementList.Any())
                    throw new Exception("未在项目中找到指定元素，请检查过滤器设置是否正确");
                // 过滤器最大个数限制
                string message = "";
                if (FilterSetting.MaxElements > 0)
                {
                    if (elementList.Count > FilterSetting.MaxElements)
                    {
                        elementList = elementList.Take(FilterSetting.MaxElements).ToList();
                        message = $"。此外，符合过滤条件的共有 {elementList.Count} 个元素，仅显示前 {FilterSetting.MaxElements} 个";
                    }
                }

                // 根据ReturnLevel构建元素信息
                var allWarnings = new List<string>();
                foreach (var element in elementList)
                {
                    var elementInfo = ElementFieldRegistry.BuildElementInfoWithWarnings(doc, element, FilterSetting, out var warnings);
                    if (elementInfo != null)
                    {
                        elementInfoList.Add(elementInfo);
                    }

                    // 收集警告信息
                    if (warnings != null && warnings.Count > 0)
                    {
                        allWarnings.AddRange(warnings);
                    }
                }

                // 构建结果消息
                string resultMessage = $"成功获取{elementInfoList.Count}个元素信息，具体信息储存在Response属性中" + message;

                // 添加警告信息
                if (allWarnings.Count > 0)
                {
                    var uniqueWarnings = allWarnings.Distinct().ToList();
                    if (uniqueWarnings.Count > 0)
                    {
                        resultMessage += $"。警告: {string.Join("; ", uniqueWarnings)}";
                    }
                }

                // 添加缺失元素的信息
                if (MissingElementIds != null && MissingElementIds.Count > 0)
                {
                    resultMessage += $"。注意：有{MissingElementIds.Count}个元素不存在 (ID: {string.Join(", ", MissingElementIds)})";
                }

                Result = new AIResult<List<object>>
                {
                    Success = true,
                    Message = resultMessage,
                    Response = elementInfoList,
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<List<object>>
                {
                    Success = false,
                    Message = $"获取元素信息时出错: {ex.Message}",
                };
            }
            finally
            {
                _resetEvent.Set(); // 通知等待线程操作已完成
            }
        }

        /// <summary>
        /// 等待创建完成
        /// </summary>
        /// <param name="timeoutMilliseconds">超时时间（毫秒）</param>
        /// <returns>操作是否在超时前完成</returns>
        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        /// <summary>
        /// IExternalEventHandler.GetName 实现
        /// </summary>
        public string GetName()
        {
            return "获取元素信息";
        }

        /// <summary>
        /// 根据过滤器设置获取Revit文档中符合条件的元素，支持多条件组合过滤
        /// </summary>
        /// <param name="doc">Revit 文档</param>
        /// <param name="settings">过滤器设置</param>
        /// <returns>符合条件的元素列表</returns>
        private static List<Element> GetFilteredElements(Document doc, FilterSetting settings)
        {
            try
            {
                List<Element> resultElements = new List<Element>();

                // 情况1：直接查询指定的元素ID
                if (settings.ElementIds != null && settings.ElementIds.Count > 0)
                {
                    MissingElementIds = new List<int>(); // 初始化缺失元素列表

                    foreach (int elementIdValue in settings.ElementIds)
                    {
                        ElementId elementId = new ElementId(elementIdValue);
                        Element element = doc.GetElement(elementId);

                        if (element != null)
                        {
                            // 检查元素是否满足其他过滤条件
                            if (IsElementMatchesFilters(element, settings))
                            {
                                resultElements.Add(element);
                            }
                        }
                        else
                        {
                            MissingElementIds.Add(elementIdValue); // 记录缺失的元素ID
                        }
                    }

                    return resultElements;
                }

                // 情况2：根据过滤条件查询
                FilteredElementCollector collector = new FilteredElementCollector(doc);

                // 应用元素种类过滤
                if (settings.IncludeInstances && !settings.IncludeTypes)
                {
                    collector = collector.WhereElementIsNotElementType();
                }
                else if (!settings.IncludeInstances && settings.IncludeTypes)
                {
                    collector = collector.WhereElementIsElementType();
                }
                // 如果两者都包含，不应用此过滤器

                // 应用类别过滤
                if (!string.IsNullOrWhiteSpace(settings.FilterCategory))
                {
                    if (Enum.TryParse(settings.FilterCategory, out BuiltInCategory builtInCategory))
                    {
                        collector = collector.OfCategory(builtInCategory);
                    }
                }

                // 应用元素类型过滤
                if (!string.IsNullOrWhiteSpace(settings.FilterElementType))
                {
                    try
                    {
                        Type elementType = GetElementTypeFromName(settings.FilterElementType);
                        if (elementType != null)
                        {
                            collector = collector.OfClass(elementType);
                        }
                    }
                    catch
                    {
                        // 如果类型名称无效，忽略此过滤器
                    }
                }

                // 获取所有符合基本条件的元素
                var elements = collector.ToElements().ToList();

                // 应用其他过滤器
                foreach (var element in elements)
                {
                    if (IsElementMatchesFilters(element, settings))
                    {
                        resultElements.Add(element);
                    }
                }

                return resultElements;
            }
            catch (Exception ex)
            {
                throw new Exception($"获取过滤元素时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查元素是否匹配过滤条件
        /// </summary>
        /// <param name="element">要检查的元素</param>
        /// <param name="settings">过滤器设置</param>
        /// <returns>如果元素匹配条件返回true</returns>
        private static bool IsElementMatchesFilters(Element element, FilterSetting settings)
        {
            if (element == null) return false;

            // 类型ID过滤（统一处理族实例和系统族）
            if (settings.FilterTypeId > 0)
            {
                int? elementTypeId = GetElementTypeId(element);
                if (elementTypeId == null || elementTypeId.Value != settings.FilterTypeId)
                {
                    return false;
                }
            }

            // 视图可见性过滤
            if (settings.FilterVisibleInCurrentView)
            {
                var doc = element.Document;
                if (doc.ActiveView != null)
                {
                    // 只对实例元素应用可见性过滤
                    if (!settings.IncludeTypes || settings.IncludeInstances)
                    {
                        if (element.IsHidden(doc.ActiveView))
                        {
                            return false;
                        }
                    }
                }
            }

            // 空间范围过滤
            if (settings.BoundingBoxMin != null && settings.BoundingBoxMax != null)
            {
                if (!IsElementInBoundingBox(element, settings.BoundingBoxMin, settings.BoundingBoxMax))
                {
                    return false;
                }
            }

            // 名称关键字过滤
            if (!string.IsNullOrWhiteSpace(settings.FilterNameKeyword))
            {
                if (!IsElementNameMatchesKeyword(element, settings.FilterNameKeyword))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 检查元素是否在指定的边界框内
        /// </summary>
        /// <param name="element">要检查的元素</param>
        /// <param name="minPoint">边界框最小点</param>
        /// <param name="maxPoint">边界框最大点</param>
        /// <returns>如果元素在边界框内返回true</returns>
        private static bool IsElementInBoundingBox(Element element, JZPoint minPoint, JZPoint maxPoint)
        {
            try
            {
                BoundingBoxXYZ bbox = element.get_BoundingBox(null);
                if (bbox == null) return false;

                // 转换单位：毫米转Revit内部单位（英尺）
                XYZ min = new XYZ(minPoint.X / 304.8, minPoint.Y / 304.8, minPoint.Z / 304.8);
                XYZ max = new XYZ(maxPoint.X / 304.8, maxPoint.Y / 304.8, maxPoint.Z / 304.8);

                // 检查边界框是否相交
                return !(bbox.Max.X < min.X || bbox.Min.X > max.X ||
                         bbox.Max.Y < min.Y || bbox.Min.Y > max.Y ||
                         bbox.Max.Z < min.Z || bbox.Min.Z > max.Z);
            }
            catch
            {
                return false; // 如果无法获取边界框，视为不匹配
            }
        }

        /// <summary>
        /// 检查元素名称是否匹配关键字
        /// 检查范围：元素名称、类型名称、族名称
        /// </summary>
        /// <param name="element">要检查的元素</param>
        /// <param name="keyword">关键字</param>
        /// <returns>如果任一名称包含关键字返回true</returns>
        private static bool IsElementNameMatchesKeyword(Element element, string keyword)
        {
            if (element == null || string.IsNullOrWhiteSpace(keyword))
                return false;

            var namesToCheck = new List<string>();

            // 1. 元素自身名称
            if (!string.IsNullOrWhiteSpace(element.Name))
                namesToCheck.Add(element.Name);

            // 2. 获取类型/族相关名称
            ElementId typeId = element.GetTypeId();
            if (typeId != null && typeId != ElementId.InvalidElementId)
            {
                ElementType elementType = element.Document.GetElement(typeId) as ElementType;
                if (elementType != null)
                {
                    // 2.1 类型名称
                    if (!string.IsNullOrWhiteSpace(elementType.Name))
                        namesToCheck.Add(elementType.Name);

                    // 2.2 如果是族实例，获取族名称
                    if (elementType is FamilySymbol familySymbol)
                    {
                        Family family = familySymbol.Family;
                        if (family != null && !string.IsNullOrWhiteSpace(family.Name))
                            namesToCheck.Add(family.Name);
                    }
                }
            }

            // 3. 检查是否有任何名称包含关键字（不区分大小写）
            return namesToCheck.Any(name =>
                name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// 根据元素类型名称获取对应的Type
        /// </summary>
        /// <param name="typeName">类型名称</param>
        /// <returns>对应的Type，如果找不到返回null</returns>
        private static Type GetElementTypeFromName(string typeName)
        {
            // 移除命名空间前缀，只保留类名
            string className = typeName.Contains(".") ? typeName.Substring(typeName.LastIndexOf('.') + 1) : typeName;

            // 常见的元素类型映射
            var typeMap = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                {"Wall", typeof(Wall)},
                {"Floor", typeof(Floor)},
                {"Ceiling", typeof(Ceiling)},
                {"Roof", typeof(RoofBase)},
                {"FamilyInstance", typeof(FamilyInstance)},
                {"Level", typeof(Level)},
                {"Grid", typeof(Grid)},
                {"Room", typeof(Room)},
                {"Area", typeof(Area)},
                {"TextNote", typeof(TextNote)},
                {"Dimension", typeof(Dimension)},
            };

            if (typeMap.TryGetValue(className, out Type type))
            {
                return type;
            }

            // 尝试从程序集中查找类型
            try
            {
                var revitDbAssembly = typeof(Element).Assembly;
                var foundType = revitDbAssembly.GetTypes()
                    .FirstOrDefault(t => t.Name.Equals(className, StringComparison.OrdinalIgnoreCase) &&
                                         typeof(Element).IsAssignableFrom(t));
                return foundType;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取元素的类型ID
        /// 对于类型元素本身，返回其自身ID；
        /// 对于实例元素（族实例或系统族实例），返回其类型的ID
        /// </summary>
        /// <param name="element">要检查的元素</param>
        /// <returns>类型ID，如果无法获取则返回null</returns>
        private static int? GetElementTypeId(Element element)
        {
            if (element == null)
                return null;

            // 如果元素本身是类型元素（ElementType），返回自身ID
            if (element is ElementType)
            {
                return element.Id.IntegerValue;
            }

            // 对于实例元素，获取其类型ID
            ElementId typeId = element.GetTypeId();
            if (typeId == null || typeId == ElementId.InvalidElementId)
            {
                return null; // 无法获取类型ID
            }

            return typeId.IntegerValue;
        }
    }
}