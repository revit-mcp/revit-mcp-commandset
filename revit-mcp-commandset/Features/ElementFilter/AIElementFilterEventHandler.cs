using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Features.ElementFilter.Models;
using RevitMCPCommandSet.Features.ElementFilter.FieldBuilders;
using RevitMCPCommandSet.Models.Geometry;
using RevitMCPSDK.API.Interfaces;

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
        /// <param name="doc">Revit文档</param>
        /// <param name="settings">过滤器设置</param>
        /// <returns>符合所有过滤条件的元素集合</returns>
        public static IList<Element> GetFilteredElements(Document doc, FilterSetting settings)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            // 验证过滤器设置
            if (!settings.Validate(out string errorMessage))
            {
                System.Diagnostics.Trace.WriteLine($"过滤器设置无效: {errorMessage}");
                return new List<Element>();
            }

            // 记录过滤条件应用情况
            List<string> appliedFilters = new List<string>();
            List<Element> result = new List<Element>();

            // === 新增：elementIds 直接查询分支 ===
            if (settings.ElementIds != null && settings.ElementIds.Count > 0)
            {
                System.Diagnostics.Trace.WriteLine($"使用elementIds直接查询模式，共{settings.ElementIds.Count}个ID");

                // 先获取ID对应的元素
                List<Element> idBasedElements = new List<Element>();
                List<int> missingIds = new List<int>();

                foreach (int id in settings.ElementIds)
                {
                    ElementId elementId = new ElementId(id);
                    Element element = doc.GetElement(elementId);
                    if (element != null)
                    {
                        idBasedElements.Add(element);
                    }
                    else
                    {
                        missingIds.Add(id);
                    }
                }

                // 记录缺失的元素
                if (missingIds.Count > 0)
                {
                    System.Diagnostics.Trace.WriteLine($"警告：{missingIds.Count}个元素不存在 (ID: {string.Join(", ", missingIds)})");
                    // 将缺失信息添加到结果消息中
                    MissingElementIds = missingIds;
                }

                // 检查是否有其他过滤条件
                bool hasOtherFilters = !string.IsNullOrWhiteSpace(settings.FilterCategory) ||
                                      !string.IsNullOrWhiteSpace(settings.FilterElementType) ||
                                      settings.FilterFamilySymbolId > 0 ||
                                      settings.FilterVisibleInCurrentView ||
                                      (settings.BoundingBoxMin != null && settings.BoundingBoxMax != null);

                if (hasOtherFilters)
                {
                    // 场景2：elementIds + 其他条件，应用额外过滤
                    System.Diagnostics.Trace.WriteLine("elementIds与其他过滤条件组合使用");

                    // 应用其他过滤条件到ID查询结果
                    result = ApplyAdditionalFilters(idBasedElements, settings, doc);
                    System.Diagnostics.Trace.WriteLine($"应用其他过滤后，剩余{result.Count}个元素");
                }
                else
                {
                    // 场景1：仅使用elementIds查询
                    result = idBasedElements;
                    System.Diagnostics.Trace.WriteLine($"仅elementIds查询，返回{result.Count}个元素");
                }

                return result;
            }

            // === 原有逻辑：传统过滤流程 ===
            // 如果同时包含类型和实例，需要分别过滤再合并结果
            if (settings.IncludeTypes && settings.IncludeInstances)
            {
                // 收集类型元素
                result.AddRange(GetElementsByKind(doc, settings, true, appliedFilters));

                // 收集实例元素
                result.AddRange(GetElementsByKind(doc, settings, false, appliedFilters));
            }
            else if (settings.IncludeInstances)
            {
                // 仅收集实例元素
                result = GetElementsByKind(doc, settings, false, appliedFilters);
            }
            else if (settings.IncludeTypes)
            {
                // 仅收集类型元素
                result = GetElementsByKind(doc, settings, true, appliedFilters);
            }

            // 输出应用的过滤器信息
            if (appliedFilters.Count > 0)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"已应用 {appliedFilters.Count} 个过滤条件: {string.Join(", ", appliedFilters)}");
                System.Diagnostics.Trace.WriteLine($"最终筛选结果: 共找到 {result.Count} 个元素");
            }

            return result;
        }

        /// <summary>
        /// 根据元素种类(类型或实例)获取满足过滤条件的元素
        /// </summary>
        private static List<Element> GetElementsByKind(Document doc, FilterSetting settings, bool isElementType,
            List<string> appliedFilters)
        {
            // 创建基础的FilteredElementCollector
            FilteredElementCollector collector;
            // 检查是否需要过滤当前视图可见的元素 (仅适用于实例元素)
            if (!isElementType && settings.FilterVisibleInCurrentView && doc.ActiveView != null)
            {
                collector = new FilteredElementCollector(doc, doc.ActiveView.Id);
                appliedFilters.Add("当前视图可见元素");
            }
            else
            {
                collector = new FilteredElementCollector(doc);
            }

            // 根据元素种类过滤
            if (isElementType)
            {
                collector = collector.WhereElementIsElementType();
                appliedFilters.Add("仅元素类型");
            }
            else
            {
                collector = collector.WhereElementIsNotElementType();
                appliedFilters.Add("仅元素实例");
            }

            // 创建过滤器列表
            List<Autodesk.Revit.DB.ElementFilter> filters = new List<Autodesk.Revit.DB.ElementFilter>();
            // 1. 类别过滤器
            if (!string.IsNullOrWhiteSpace(settings.FilterCategory))
            {
                BuiltInCategory category;
                if (!Enum.TryParse(settings.FilterCategory, true, out category))
                {
                    throw new ArgumentException($"无法将 '{settings.FilterCategory}' 转换为有效的Revit类别。");
                }

                ElementCategoryFilter categoryFilter = new ElementCategoryFilter(category);
                filters.Add(categoryFilter);
                appliedFilters.Add($"类别：{settings.FilterCategory}");
            }

            // 2. 元素类型过滤器
            if (!string.IsNullOrWhiteSpace(settings.FilterElementType))
            {
                Type elementType = null;
                // 尝试解析类型名称的各种可能形式
                string[] possibleTypeNames = new string[]
                {
                    settings.FilterElementType, // 原始输入
                    $"Autodesk.Revit.DB.{settings.FilterElementType}, RevitAPI", // Revit API命名空间
                    $"{settings.FilterElementType}, RevitAPI" // 完整限定带程序集
                };
                foreach (string typeName in possibleTypeNames)
                {
                    elementType = Type.GetType(typeName);
                    if (elementType != null)
                        break;
                }

                if (elementType != null)
                {
                    ElementClassFilter classFilter = new ElementClassFilter(elementType);
                    filters.Add(classFilter);
                    appliedFilters.Add($"元素类型：{elementType.Name}");
                }
                else
                {
                    throw new Exception($"警告：无法找到类型 '{settings.FilterElementType}'");
                }
            }

            // 3. 族符号过滤器 (仅适用于元素实例)
            if (!isElementType && settings.FilterFamilySymbolId > 0)
            {
                ElementId symbolId = new ElementId(settings.FilterFamilySymbolId);
                // 检查元素是否存在且是族类型
                Element symbolElement = doc.GetElement(symbolId);
                if (symbolElement != null && symbolElement is FamilySymbol)
                {
                    FamilyInstanceFilter familyFilter = new FamilyInstanceFilter(doc, symbolId);
                    filters.Add(familyFilter);
                    // 添加更详细的族信息日志
                    FamilySymbol symbol = symbolElement as FamilySymbol;
                    string familyName = symbol.Family?.Name ?? "未知族";
                    string symbolName = symbol.Name ?? "未知类型";
                    appliedFilters.Add($"族类型：{familyName} - {symbolName} (ID: {settings.FilterFamilySymbolId})");
                }
                else
                {
                    string elementType = symbolElement != null ? symbolElement.GetType().Name : "不存在";
                    System.Diagnostics.Trace.WriteLine(
                        $"警告：ID为 {settings.FilterFamilySymbolId} 的元素{(symbolElement == null ? "不存在" : "不是有效的FamilySymbol")} (实际类型: {elementType})");
                }
            }

            // 4. 空间范围过滤器
            if (settings.BoundingBoxMin != null && settings.BoundingBoxMax != null)
            {
                // 转换为Revit的XYZ坐标 (毫米转内部单位)
                XYZ minXYZ = JZPoint.ToXYZ(settings.BoundingBoxMin);
                XYZ maxXYZ = JZPoint.ToXYZ(settings.BoundingBoxMax);
                // 创建空间范围Outline对象
                Outline outline = new Outline(minXYZ, maxXYZ);
                // 创建相交过滤器
                BoundingBoxIntersectsFilter boundingBoxFilter = new BoundingBoxIntersectsFilter(outline);
                filters.Add(boundingBoxFilter);
                appliedFilters.Add(
                    $"空间范围过滤：Min({settings.BoundingBoxMin.X:F2}, {settings.BoundingBoxMin.Y:F2}, {settings.BoundingBoxMin.Z:F2}), " +
                    $"Max({settings.BoundingBoxMax.X:F2}, {settings.BoundingBoxMax.Y:F2}, {settings.BoundingBoxMax.Z:F2}) mm");
            }

            // 应用组合过滤器
            if (filters.Count > 0)
            {
                Autodesk.Revit.DB.ElementFilter combinedFilter = filters.Count == 1
                    ? filters[0]
                    : new LogicalAndFilter(filters);
                collector = collector.WherePasses(combinedFilter);
                if (filters.Count > 1)
                {
                    System.Diagnostics.Trace.WriteLine($"应用了{filters.Count}个过滤条件的组合过滤器 (逻辑AND关系)");
                }
            }

            return collector.ToElements().ToList();
        }

        /// <summary>
        /// 对已有元素列表应用额外的过滤条件
        /// 用于 elementIds + 其他过滤条件的组合场景
        /// </summary>
        private static List<Element> ApplyAdditionalFilters(List<Element> elements, FilterSetting settings, Document doc)
        {
            List<Element> filteredElements = new List<Element>(elements);

            // 1. 类别过滤
            if (!string.IsNullOrWhiteSpace(settings.FilterCategory))
            {
                BuiltInCategory category;
                if (Enum.TryParse(settings.FilterCategory, true, out category))
                {
                    filteredElements = filteredElements.Where(e => e.Category != null &&
                                                              (BuiltInCategory)e.Category.Id.IntegerValue == category).ToList();
                }
            }

            // 2. 元素类型过滤
            if (!string.IsNullOrWhiteSpace(settings.FilterElementType))
            {
                Type elementType = null;
                string[] possibleTypeNames = new string[]
                {
                    settings.FilterElementType,
                    $"Autodesk.Revit.DB.{settings.FilterElementType}, RevitAPI",
                    $"{settings.FilterElementType}, RevitAPI"
                };

                foreach (string typeName in possibleTypeNames)
                {
                    elementType = Type.GetType(typeName);
                    if (elementType != null)
                        break;
                }

                if (elementType != null)
                {
                    filteredElements = filteredElements.Where(e => elementType.IsAssignableFrom(e.GetType())).ToList();
                }
            }

            // 3. 族符号过滤
            if (settings.FilterFamilySymbolId > 0)
            {
                ElementId symbolId = new ElementId(settings.FilterFamilySymbolId);
                filteredElements = filteredElements.Where(e =>
                {
                    if (e is FamilyInstance fi)
                    {
                        return fi.Symbol?.Id == symbolId;
                    }
                    return false;
                }).ToList();
            }

            // 4. 视图可见性过滤
            if (settings.FilterVisibleInCurrentView && doc.ActiveView != null)
            {
                FilteredElementCollector viewCollector = new FilteredElementCollector(doc, doc.ActiveView.Id);
                HashSet<ElementId> visibleIds = new HashSet<ElementId>(viewCollector.ToElementIds());
                filteredElements = filteredElements.Where(e => visibleIds.Contains(e.Id)).ToList();
            }

            // 5. 空间范围过滤
            if (settings.BoundingBoxMin != null && settings.BoundingBoxMax != null)
            {
                XYZ minXYZ = JZPoint.ToXYZ(settings.BoundingBoxMin);
                XYZ maxXYZ = JZPoint.ToXYZ(settings.BoundingBoxMax);
                Outline outline = new Outline(minXYZ, maxXYZ);

                filteredElements = filteredElements.Where(e =>
                {
                    BoundingBoxXYZ bbox = e.get_BoundingBox(doc.ActiveView);
                    if (bbox == null)
                        return false;

                    // 检查边界框是否与过滤范围相交
                    Outline elementOutline = new Outline(bbox.Min, bbox.Max);
                    return outline.Intersects(elementOutline, 0);
                }).ToList();
            }

            // 6. 元素类型/实例过滤
            if (settings.IncludeTypes && !settings.IncludeInstances)
            {
                filteredElements = filteredElements.Where(e => e is ElementType).ToList();
            }
            else if (!settings.IncludeTypes && settings.IncludeInstances)
            {
                filteredElements = filteredElements.Where(e => !(e is ElementType)).ToList();
            }

            return filteredElements;
        }

        /// <summary>
        /// 根据字段请求构建元素信息 - 使用新的字段注册表
        /// </summary>
        public static object BuildElementInfo(Document doc, Element element, FilterSetting settings)
        {
            if (element == null) return null;

            // 应用向后兼容层：将旧格式映射为新格式
            // 不再需要标准化设置

            // 使用新的 ElementFieldRegistry 构建字段信息
            var elementInfo = ElementFieldRegistry.BuildElementInfo(doc, element, settings);

            return elementInfo;
        }

        /// <summary>
        /// 构建最小信息 - 仅包含ID
        /// </summary>
        private static object BuildMinimalInfo(Element element)
        {
            return new
            {
                elementId = element.Id.IntegerValue,
                uniqueId = element.UniqueId
            };
        }

        /// <summary>
        /// 构建基础信息 - ID + 基本属性
        /// </summary>
        private static object BuildBasicInfo(Document doc, Element element)
        {
            var minimal = BuildMinimalInfo(element);

            // 获取基本信息
            string name = element.Name;
            string categoryName = element.Category?.Name ?? "未知类别";
            BuiltInCategory? builtInCategory = element.Category != null
                ? (BuiltInCategory?)element.Category.Id.IntegerValue
                : null;

            // 获取类型信息
            ElementId typeId = element.GetTypeId();
            string typeName = null;
            if (typeId != ElementId.InvalidElementId)
            {
                Element typeElement = doc.GetElement(typeId);
                typeName = typeElement?.Name;
            }

            // 获取族信息（如果是族实例）
            string familyName = null;
            ElementId familyId = ElementId.InvalidElementId;
            if (element is FamilyInstance fi)
            {
                familyName = fi.Symbol?.Family?.Name;
                familyId = fi.Symbol?.Family?.Id ?? ElementId.InvalidElementId;
            }

            // 获取标高信息
            var levelInfo = GetElementLevel(doc, element);

            return new
            {
                elementId = element.Id.IntegerValue,
                uniqueId = element.UniqueId,
                name = name,
                category = categoryName,
                builtInCategory = builtInCategory?.ToString(),
                typeId = typeId.IntegerValue,
                typeName = typeName,
                familyId = familyId.IntegerValue,
                familyName = familyName,
                levelId = levelInfo?.Id ?? -1,
                levelName = levelInfo?.Name,
                documentGuid = element.Document.GetHashCode().ToString()
            };
        }

        /// <summary>
        /// 构建几何信息 - Basic + 几何属性
        /// </summary>
        private static object BuildGeometryInfo(Document doc, Element element, FilterSetting settings)
        {
            var basicInfo = BuildBasicInfo(doc, element);
            var geometryData = new Dictionary<string, object>();

            // 获取包围盒信息
            var boundingBox = GetBoundingBoxInfo(element);
            if (boundingBox != null)
            {
                geometryData["boundingBox"] = boundingBox;
            }

            // 获取变换矩阵（如果有）
            if (element is FamilyInstance familyInstance)
            {
                Transform transform = familyInstance.GetTransform();
                geometryData["transform"] = new
                {
                    origin = new { x = transform.Origin.X * 304.8, y = transform.Origin.Y * 304.8, z = transform.Origin.Z * 304.8 },
                    basisX = new { x = transform.BasisX.X, y = transform.BasisX.Y, z = transform.BasisX.Z },
                    basisY = new { x = transform.BasisY.X, y = transform.BasisY.Y, z = transform.BasisY.Z },
                    basisZ = new { x = transform.BasisZ.X, y = transform.BasisZ.Y, z = transform.BasisZ.Z }
                };

                // 获取位置点
                if (familyInstance.Location is LocationPoint locPoint)
                {
                    geometryData["locationPoint"] = new
                    {
                        x = locPoint.Point.X * 304.8,
                        y = locPoint.Point.Y * 304.8,
                        z = locPoint.Point.Z * 304.8
                    };
                    geometryData["rotation"] = locPoint.Rotation * 180 / Math.PI;
                }

                // 根据GeometryOptions决定是否包含高开销项
                if (settings.GeometryOptions != null)
                {
                    if (settings.GeometryOptions.IncludeHandOrientation)
                    {
                        XYZ hand = familyInstance.HandOrientation;
                        geometryData["handOrientation"] = new { x = hand.X, y = hand.Y, z = hand.Z };
                    }
                    if (settings.GeometryOptions.IncludeFacingOrientation)
                    {
                        XYZ facing = familyInstance.FacingOrientation;
                        geometryData["facingOrientation"] = new { x = facing.X, y = facing.Y, z = facing.Z };
                    }
                    if (settings.GeometryOptions.IncludeIsHandFlipped)
                    {
                        geometryData["isHandFlipped"] = familyInstance.HandFlipped;
                    }
                    if (settings.GeometryOptions.IncludeIsFacingFlipped)
                    {
                        geometryData["isFacingFlipped"] = familyInstance.FacingFlipped;
                    }
                }

                // 获取宿主元素标高
                if (familyInstance.Host != null)
                {
                    var hostLevel = GetElementLevel(doc, familyInstance.Host);
                    if (hostLevel != null)
                    {
                        geometryData["hostLevelId"] = hostLevel.Id;
                        geometryData["hostLevelName"] = hostLevel.Name;
                    }
                }
            }

            // 对于线性元素，获取线信息
            if (element.Location is LocationCurve locCurve)
            {
                Curve curve = locCurve.Curve;
                geometryData["locationCurve"] = new
                {
                    startPoint = new { x = curve.GetEndPoint(0).X * 304.8, y = curve.GetEndPoint(0).Y * 304.8, z = curve.GetEndPoint(0).Z * 304.8 },
                    endPoint = new { x = curve.GetEndPoint(1).X * 304.8, y = curve.GetEndPoint(1).Y * 304.8, z = curve.GetEndPoint(1).Z * 304.8 },
                    length = curve.Length * 304.8
                };

                // 获取曲线方向
                XYZ direction = (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();
                geometryData["curveDirection"] = new { x = direction.X, y = direction.Y, z = direction.Z };
            }

            // 添加面元素的几何信息（墙、楼板、屋顶等）
            if (settings.GeometryOptions == null || settings.GeometryOptions.CalculateDetailedGeometry)
            {
                // 获取厚度信息（转换为毫米数值）
                var thicknessInfo = GetThicknessInfo(element);
                if (thicknessInfo != null)
                {
                    // 从 FilterParameterInfo 提取数值
                    if (double.TryParse(thicknessInfo.Value, out double thicknessValue))
                    {
                        geometryData["thickness"] = thicknessValue; // 已经是毫米单位
                    }
                }

                // 对于楼板、屋顶等面元素
                if (element is Floor floor)
                {
                    // 获取坡度
                    Parameter slopeParam = floor.get_Parameter(BuiltInParameter.ROOF_SLOPE);
                    if (slopeParam != null && slopeParam.HasValue)
                    {
                        geometryData["slope"] = slopeParam.AsDouble();
                    }

                    // 尝试获取面积
                    Parameter areaParam = floor.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                    if (areaParam != null && areaParam.HasValue)
                    {
                        geometryData["area"] = areaParam.AsDouble() * 304.8 * 304.8; // 转换为平方毫米
                    }

                    // 尝试获取周长
                    Parameter perimeterParam = floor.get_Parameter(BuiltInParameter.HOST_PERIMETER_COMPUTED);
                    if (perimeterParam != null && perimeterParam.HasValue)
                    {
                        geometryData["perimeter"] = perimeterParam.AsDouble() * 304.8; // 转换为毫米
                    }
                }
                else if (element is Wall wall)
                {
                    // 获取墙体面积
                    Parameter areaParam = wall.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                    if (areaParam != null && areaParam.HasValue)
                    {
                        geometryData["area"] = areaParam.AsDouble() * 304.8 * 304.8;
                    }

                    // 获取墙体长度
                    Parameter lengthParam = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                    if (lengthParam != null && lengthParam.HasValue)
                    {
                        geometryData["length"] = lengthParam.AsDouble() * 304.8;
                    }

                    // 获取墙体高度
                    Parameter heightParam = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
                    if (heightParam != null && heightParam.HasValue)
                    {
                        geometryData["height"] = heightParam.AsDouble() * 304.8;
                    }
                }
#if REVIT2021_OR_GREATER
                else if (element is Ceiling ceiling)
                {
                    // 获取天花板面积
                    Parameter areaParam = ceiling.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                    if (areaParam != null && areaParam.HasValue)
                    {
                        geometryData["area"] = areaParam.AsDouble() * 304.8 * 304.8;
                    }

                    // 获取天花板周长
                    Parameter perimeterParam = ceiling.get_Parameter(BuiltInParameter.HOST_PERIMETER_COMPUTED);
                    if (perimeterParam != null && perimeterParam.HasValue)
                    {
                        geometryData["perimeter"] = perimeterParam.AsDouble() * 304.8;
                    }
                }
#endif
            }

            // 合并基础信息和几何信息
            var result = new Dictionary<string, object>();
            foreach (var prop in basicInfo.GetType().GetProperties())
            {
                result[prop.Name] = prop.GetValue(basicInfo);
            }
            foreach (var kvp in geometryData)
            {
                result[kvp.Key] = kvp.Value;
            }

            return result;
        }

        /// <summary>
        /// 构建参数信息 - Basic + 参数
        /// </summary>
        private static object BuildParametersInfo(Document doc, Element element, FilterSetting settings)
        {
            var basicInfo = BuildBasicInfo(doc, element);

            // 提取参数
            var parameters = ExtractParameters(element, settings.ParameterOptions);

            // 合并基础信息和参数信息
            var result = new Dictionary<string, object>();
            foreach (var prop in basicInfo.GetType().GetProperties())
            {
                result[prop.Name] = prop.GetValue(basicInfo);
            }

            // 根据返回格式决定如何添加参数
            if (settings.ParameterOptions?.ReturnFormat == "Separated")
            {
                result["instanceParameters"] = parameters["instanceParameters"];
                result["typeParameters"] = parameters["typeParameters"];
            }
            else
            {
                // Merged格式
                result["parameters"] = parameters["parameters"];
            }

            return result;
        }

        /// <summary>
        /// 判断是否为定位元素（标高、轴网等）
        /// </summary>
        private static bool IsPositioningElement(Element element)
        {
            return element is Level || element is Grid;
        }

        /// <summary>
        /// 判断是否为空间元素（房间、区域等）
        /// </summary>
        private static bool IsSpatialElement(Element element)
        {
#if REVIT2020 || REVIT2021 || REVIT2022 || REVIT2023 || REVIT2024
            return element is Room || element is Area || element is Autodesk.Revit.DB.Mechanical.Space;
#else
            return element is Autodesk.Revit.DB.Architecture.Room ||
                   element is Autodesk.Revit.DB.Area ||
                   element is Autodesk.Revit.DB.Mechanical.Space;
#endif
        }

        /// <summary>
        /// 判断是否为注释元素
        /// </summary>
        private static bool IsAnnotationElement(Element element)
        {
            return element is TextNote ||
                   element is Dimension ||
                   element is DetailLine ||
                   element is DetailCurve ||
                   element is IndependentTag ||
                   element is FillPatternElement;
        }

        /// <summary>
        /// 判断是否为组或链接元素
        /// </summary>
        private static bool IsGroupOrLinkElement(Element element)
        {
            return element is Group ||
                   element is GroupType ||
                   element is RevitLinkInstance ||
                   element is RevitLinkType;
        }

        /// <summary>
        /// 构建完整信息 - 包含所有信息
        /// </summary>
        private static object BuildFullInfo(Document doc, Element element, FilterSetting settings)
        {
            // 创建一个综合的字典来存放所有信息
            var fullInfo = new Dictionary<string, object>();

            // 1. 添加基础信息
            var basicInfo = BuildBasicInfo(doc, element);
            foreach (var prop in basicInfo.GetType().GetProperties())
            {
                fullInfo[prop.Name] = prop.GetValue(basicInfo);
            }

            // 2. 添加几何信息
            var geoSettings = settings ?? new FilterSetting();
            if (geoSettings.GeometryOptions == null)
            {
                geoSettings.GeometryOptions = new GeometryOptions
                {
                    CalculateDetailedGeometry = true,
                    IncludeHandOrientation = true,
                    IncludeFacingOrientation = true,
                    IncludeIsHandFlipped = true,
                    IncludeIsFacingFlipped = true
                };
            }
            var geometryInfo = BuildGeometryInfo(doc, element, geoSettings);
            if (geometryInfo is Dictionary<string, object> geoDict)
            {
                foreach (var kvp in geoDict)
                {
                    if (!fullInfo.ContainsKey(kvp.Key))
                    {
                        fullInfo[kvp.Key] = kvp.Value;
                    }
                }
            }

            // 3. 添加参数信息
            var paramOptions = settings?.ParameterOptions ?? new ParameterOptions
            {
                Scope = "Both",
                FilterMode = "None",
                ReturnFormat = "Separated"
            };
            var parameters = ExtractParameters(element, paramOptions);
            fullInfo["instanceParameters"] = parameters["instanceParameters"];
            fullInfo["typeParameters"] = parameters["typeParameters"];

            // 4. 添加额外的Full级别字段

            // 视图状态
            if (doc.ActiveView != null)
            {
                var viewStates = new List<object>();
                bool isHidden = element.IsHidden(doc.ActiveView);
                // IsHiddenTemporary 方法可能不存在于所有版本，使用条件编译或忽略
                bool isTemporary = false;
#if REVIT2023_OR_GREATER
                // isTemporary = element.IsHiddenTemporary(doc.ActiveView);
#endif

                viewStates.Add(new
                {
                    viewId = doc.ActiveView.Id.IntegerValue,
                    viewName = doc.ActiveView.Name,
                    isHidden = isHidden,
                    isTemporarilyHidden = isTemporary
                });
                fullInfo["viewStates"] = viewStates;
            }

            // 所有者视图（对于视图特定元素）
            Parameter ownerViewParam = element.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
            if (ownerViewParam != null && ownerViewParam.HasValue)
            {
                ElementId ownerViewId = ownerViewParam.AsElementId();
                if (ownerViewId != ElementId.InvalidElementId)
                {
                    fullInfo["ownerViewId"] = ownerViewId.IntegerValue;
                    Element ownerView = doc.GetElement(ownerViewId);
                    if (ownerView != null)
                    {
                        fullInfo["ownerViewName"] = ownerView.Name;
                    }
                }
            }

            // 工作集
            if (element.WorksetId != null && element.WorksetId != WorksetId.InvalidWorksetId)
            {
                fullInfo["worksetId"] = element.WorksetId.IntegerValue;
#if REVIT2022_OR_GREATER
                WorksetTable worksetTable = doc.GetWorksetTable();
                if (worksetTable != null)
                {
                    Workset workset = worksetTable.GetWorkset(element.WorksetId);
                    if (workset != null)
                    {
                        fullInfo["worksetName"] = workset.Name;
                    }
                }
#endif
            }

            // 设计选项
            if (element.DesignOption != null && element.DesignOption.Id != ElementId.InvalidElementId)
            {
                fullInfo["designOptionId"] = element.DesignOption.Id.IntegerValue;
                fullInfo["designOptionName"] = element.DesignOption.Name;
            }

            // 阶段信息
            Parameter phaseCreatedParam = element.get_Parameter(BuiltInParameter.PHASE_CREATED);
            if (phaseCreatedParam != null && phaseCreatedParam.HasValue)
            {
                ElementId phaseId = phaseCreatedParam.AsElementId();
                if (phaseId != ElementId.InvalidElementId)
                {
                    fullInfo["phaseCreatedId"] = phaseId.IntegerValue;
                    Element phase = doc.GetElement(phaseId);
                    if (phase != null)
                    {
                        fullInfo["phaseCreatedName"] = phase.Name;
                    }
                }
            }

            Parameter phaseDemolishedParam = element.get_Parameter(BuiltInParameter.PHASE_DEMOLISHED);
            if (phaseDemolishedParam != null && phaseDemolishedParam.HasValue)
            {
                ElementId phaseId = phaseDemolishedParam.AsElementId();
                if (phaseId != ElementId.InvalidElementId)
                {
                    fullInfo["phaseDemolishedId"] = phaseId.IntegerValue;
                    Element phase = doc.GetElement(phaseId);
                    if (phase != null)
                    {
                        fullInfo["phaseDemolishedName"] = phase.Name;
                    }
                }
            }

            // 可选：材料信息
            if (element is FamilyInstance fi)
            {
                var materials = new List<object>();
                foreach (ElementId matId in fi.GetMaterialIds(false))
                {
                    Material mat = doc.GetElement(matId) as Material;
                    if (mat != null)
                    {
                        materials.Add(new
                        {
                            id = mat.Id.IntegerValue,
                            name = mat.Name,
                            category = mat.MaterialCategory,
                            color = mat.Color != null ? new { r = mat.Color.Red, g = mat.Color.Green, b = mat.Color.Blue } : null
                        });
                    }
                }
                if (materials.Count > 0)
                {
                    fullInfo["materials"] = materials;
                }

                // 宿主元素ID
                if (fi.Host != null)
                {
                    fullInfo["hostElementId"] = fi.Host.Id.IntegerValue;
                    fullInfo["hostElementName"] = fi.Host.Name;
                }
            }

            // 可选：连接的元素（针对墙、梁等结构元素）
            if (element is Wall wallElem)
            {
                var connectedElements = new List<int>();

                // 获取连接的墙
                ICollection<ElementId> joinedWalls = JoinGeometryUtils.GetJoinedElements(doc, wallElem);
                foreach (ElementId id in joinedWalls)
                {
                    connectedElements.Add(id.IntegerValue);
                }

                if (connectedElements.Count > 0)
                {
                    fullInfo["connectedElements"] = connectedElements;
                }
            }

            return fullInfo;
        }

        /// <summary>
        /// 构建自定义字段信息
        /// </summary>
        private static object BuildCustomInfo(Document doc, Element element, FilterSetting settings)
        {
            if (settings.IncludeFields == null || settings.IncludeFields.Count == 0)
            {
                // 如果没有指定字段，返回最小信息
                return BuildMinimalInfo(element);
            }

            var result = new Dictionary<string, object>();

            // 分类字段请求
            var basicFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var geometryFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var parameterFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var specificParams = new List<string>();

            foreach (string field in settings.IncludeFields)
            {
                string fieldLower = field.ToLower();

                // 解析层级字段
                if (fieldLower.StartsWith("geometry."))
                {
                    geometryFields.Add(field.Substring(9)); // 移除"geometry."前缀
                }
                else if (fieldLower.StartsWith("parameters."))
                {
                    string paramName = field.Substring(11); // 移除"parameters."前缀
                    specificParams.Add(paramName);
                }
                else if (fieldLower.StartsWith("level."))
                {
                    basicFields.Add("level");
                }
                else
                {
                    // 基础字段映射
                    switch (fieldLower)
                    {
                        case "elementid":
                        case "uniqueid":
                        case "name":
                        case "category":
                        case "builtincategory":
                        case "typeid":
                        case "typename":
                        case "familyid":
                        case "familyname":
                        case "level":
                        case "levelid":
                        case "levelname":
                        case "documentguid":
                            basicFields.Add(fieldLower);
                            break;
                        case "boundingbox":
                        case "transform":
                        case "locationpoint":
                        case "locationcurve":
                        case "rotation":
                            geometryFields.Add(fieldLower);
                            break;
                        case "parameters":
                            parameterFields.Add("all");
                            break;
                    }
                }
            }

            // 如果需要基础信息
            if (basicFields.Count > 0)
            {
                var basicInfo = BuildBasicInfo(doc, element);
                // 选择性地添加基础字段
                foreach (var prop in basicInfo.GetType().GetProperties())
                {
                    string propName = prop.Name.ToLower();
                    if (basicFields.Contains(propName) || basicFields.Contains("*"))
                    {
                        result[prop.Name] = prop.GetValue(basicInfo);
                    }
                }
            }

            // 如果需要几何信息
            if (geometryFields.Count > 0)
            {
                var geoSettings = new FilterSetting { GeometryOptions = settings.GeometryOptions ?? new GeometryOptions() };
                var geometryInfo = BuildGeometryInfo(doc, element, geoSettings);

                // 转换为字典以便选择字段
                var geoDict = geometryInfo as Dictionary<string, object>;
                if (geoDict != null)
                {
                    foreach (string geoField in geometryFields)
                    {
                        string key = geoField.ToLower();
                        var matchingKey = geoDict.Keys.FirstOrDefault(k => k.ToLower() == key);
                        if (matchingKey != null)
                        {
                            result[matchingKey] = geoDict[matchingKey];
                        }
                    }
                }
            }

            // 如果需要参数信息
            if (parameterFields.Count > 0 || specificParams.Count > 0)
            {
                // 创建临时的参数选项
                var paramOptions = new ParameterOptions
                {
                    Scope = "Both",
                    FilterMode = specificParams.Count > 0 ? "Include" : "None",
                    ParameterNames = specificParams.Count > 0 ? specificParams : null,
                    ReturnFormat = "Merged"
                };

                var parameters = ExtractParameters(element, paramOptions);

                if (parameterFields.Contains("all"))
                {
                    // 返回所有参数
                    result["parameters"] = parameters["parameters"];
                }
                else if (specificParams.Count > 0)
                {
                    // 返回指定的参数
                    var mergedParams = parameters["parameters"] as Dictionary<string, string>;
                    if (mergedParams != null)
                    {
                        var filteredParams = new Dictionary<string, string>();
                        foreach (string paramName in specificParams)
                        {
                            // 查找匹配的参数（不区分大小写）
                            var matchingKey = mergedParams.Keys.FirstOrDefault(k =>
                                k.Equals(paramName, StringComparison.OrdinalIgnoreCase) ||
                                k.Equals($"Type.{paramName}", StringComparison.OrdinalIgnoreCase));

                            if (matchingKey != null)
                            {
                                filteredParams[paramName] = mergedParams[matchingKey];
                            }
                        }
                        if (filteredParams.Count > 0)
                        {
                            result["parameters"] = filteredParams;
                        }
                    }
                }
            }

            return result.Count > 0 ? result : BuildMinimalInfo(element);
        }

        /// <summary>
        /// 提取元素参数
        /// </summary>
        private static Dictionary<string, object> ExtractParameters(Element element, ParameterOptions options)
        {
            var result = new Dictionary<string, object>();

            if (options == null)
            {
                options = new ParameterOptions(); // 使用默认设置
            }

            var instanceParams = new List<object>();
            var typeParams = new List<object>();
            var mergedParams = new Dictionary<string, string>();

            // 提取实例参数
            if (options.Scope == "Instance" || options.Scope == "Both")
            {
                foreach (Parameter param in element.Parameters)
                {
                    if (param == null || !param.HasValue) continue;

                    string paramName = param.Definition.Name;

                    // 检查是否应包含此参数
                    if (!ShouldIncludeParameter(paramName, param.Definition as InternalDefinition, options))
                        continue;

                    // 检查是否为敏感参数
                    bool isSensitive = IsSensitiveParameter(paramName);
                    string displayValue = isSensitive ? "[REDACTED]" : (param.AsValueString() ?? param.AsString());
                    object rawValue = isSensitive ? "[REDACTED]" : GetParameterRawValue(param);

                    if (options.ReturnFormat == "Separated")
                    {
                        var internalDef = param.Definition as InternalDefinition;
                        instanceParams.Add(new
                        {
                            name = paramName,
                            displayValue = displayValue,
                            rawValue = rawValue,
                            storageType = param.StorageType.ToString(),
                            builtInName = internalDef?.BuiltInParameter.ToString() ?? null,
                            builtInEnum = internalDef != null ? (int?)internalDef.BuiltInParameter : null,
                            isReadOnly = param.IsReadOnly,
                            guid = param.IsShared ? param.GUID.ToString() : null,
                            source = "Instance"
                        });
                    }
                    else
                    {
                        mergedParams[paramName] = displayValue ?? string.Empty;
                    }
                }
            }

            // 提取类型参数
            if ((options.Scope == "Type" || options.Scope == "Both") && element.GetTypeId() != ElementId.InvalidElementId)
            {
                Element typeElement = element.Document.GetElement(element.GetTypeId());
                if (typeElement != null)
                {
                    foreach (Parameter param in typeElement.Parameters)
                    {
                        if (param == null || !param.HasValue) continue;

                        string paramName = param.Definition.Name;

                        // 检查是否应包含此参数
                        if (!ShouldIncludeParameter(paramName, param.Definition as InternalDefinition, options))
                            continue;

                        // 检查是否为敏感参数
                        bool isSensitive = IsSensitiveParameter(paramName);
                        string displayValue = isSensitive ? "[REDACTED]" : (param.AsValueString() ?? param.AsString());
                        object rawValue = isSensitive ? "[REDACTED]" : GetParameterRawValue(param);

                        if (options.ReturnFormat == "Separated")
                        {
                            var internalDef = param.Definition as InternalDefinition;
                            typeParams.Add(new
                            {
                                name = paramName,
                                displayValue = displayValue,
                                rawValue = rawValue,
                                storageType = param.StorageType.ToString(),
                                builtInName = internalDef?.BuiltInParameter.ToString() ?? null,
                                builtInEnum = internalDef != null ? (int?)internalDef.BuiltInParameter : null,
                                isReadOnly = param.IsReadOnly,
                                guid = param.IsShared ? param.GUID.ToString() : null,
                                source = "Type"
                            });
                        }
                        else
                        {
                            // 在合并格式中，类型参数加 "Type." 前缀
                            mergedParams[$"Type.{paramName}"] = displayValue ?? string.Empty;
                        }
                    }
                }
            }

            // 根据返回格式组装结果
            if (options.ReturnFormat == "Separated")
            {
                result["instanceParameters"] = instanceParams;
                result["typeParameters"] = typeParams;
            }
            else
            {
                result["parameters"] = mergedParams;
            }

            return result;
        }

        /// <summary>
        /// 判断是否应包含参数
        /// </summary>
        private static bool ShouldIncludeParameter(string paramName, InternalDefinition internalDef, ParameterOptions options)
        {
            if (options.FilterMode == "None")
                return true;

            bool isInList = false;

            // 检查参数名列表
            if (options.ParameterNames != null && options.ParameterNames.Contains(paramName))
                isInList = true;

            // 检查内置参数列表
            if (internalDef != null && options.BuiltInParameters != null)
            {
                string builtInName = internalDef.BuiltInParameter.ToString();
                if (options.BuiltInParameters.Contains(builtInName))
                    isInList = true;
            }

            // 根据过滤模式返回
            if (options.FilterMode == "Include")
                return isInList;
            else if (options.FilterMode == "Exclude")
                return !isInList;

            return true;
        }

        /// <summary>
        /// 判断是否为敏感参数
        /// </summary>
        private static bool IsSensitiveParameter(string paramName)
        {
            if (string.IsNullOrEmpty(paramName))
                return false;

            // 敏感参数关键词列表
            string[] sensitiveKeywords = new[]
            {
                "Password", "License", "SerialNumber", "AuthToken",
                "ApiKey", "PrivateKey", "Secret", "Credential",
                "Token", "Key"
            };

            string lowerName = paramName.ToLower();
            foreach (string keyword in sensitiveKeywords)
            {
                if (lowerName.Contains(keyword.ToLower()))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 获取参数原始值
        /// </summary>
        private static object GetParameterRawValue(Parameter param)
        {
            switch (param.StorageType)
            {
                case StorageType.Double:
                    return param.AsDouble() * 304.8; // 转换为毫米
                case StorageType.Integer:
                    return param.AsInteger();
                case StorageType.String:
                    return param.AsString();
                case StorageType.ElementId:
                    return param.AsElementId()?.IntegerValue ?? -1;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 获取模型元素信息
        /// </summary>
        public static List<object> GetElementFullInfo(Document doc, IList<Element> elementCollector)
        {
            List<object> infoList = new List<object>();

            // 获取并处理元素
            foreach (var element in elementCollector)
            {
                // 判断是否为实体模型元素
                // 获取元素实例信息
                if (element?.Category?.HasMaterialQuantities ?? false)
                {
                    var info = CreateElementFullInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 获取元素类型信息
                else if (element is ElementType elementType)
                {
                    var info = CreateTypeFullInfo(doc, elementType);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 3. 空间定位元素 (高频)
                else if (element is Level || element is Grid)
                {
                    var info = CreatePositioningElementInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 4. 空间元素 (中高频)
                else if (element is SpatialElement) // Room, Area等
                {
                    var info = CreateSpatialElementInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 5. 视图元素 (高频)
                else if (element is View)
                {
                    var info = CreateViewInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 6. 注释元素 (中频)
                else if (element is TextNote || element is Dimension ||
                         element is IndependentTag || element is AnnotationSymbol ||
                         element is SpotDimension)
                {
                    var info = CreateAnnotationInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 7. 处理组和链接
                else if (element is Group || element is RevitLinkInstance)
                {
                    var info = CreateGroupOrLinkInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
                // 8. 获取元素基本信息(兜底处理)
                else
                {
                    var info = CreateElementBasicInfo(doc, element);
                    if (info != null)
                    {
                        infoList.Add(info);
                    }
                }
            }

            return infoList;
        }

        /// <summary>
        /// 创建单个元素完整的ElementInfo对象
        /// </summary>
        public static ElementInstanceInfo CreateElementFullInfo(Document doc, Element element)
        {
            try
            {
                if (element?.Category == null)
                    return null;

                ElementInstanceInfo elementInfo = new ElementInstanceInfo(); //创建存储元素完整信息的自定义类
                // ID
                elementInfo.Id = element.Id.IntegerValue;
                // UniqueId
                elementInfo.UniqueId = element.UniqueId;
                // 类型名称
                elementInfo.Name = element.Name;
                // 族名称
                elementInfo.FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString();
                // 类别
                elementInfo.Category = element.Category.Name;
                // 内置类别
                elementInfo.BuiltInCategory = Enum.GetName(typeof(BuiltInCategory), element.Category.Id.IntegerValue);
                // 类型Id
                elementInfo.TypeId = element.GetTypeId().IntegerValue;
                //所属房间Id  
                if (element is FamilyInstance instance)
                    elementInfo.RoomId = instance.Room?.Id.IntegerValue ?? -1;
                // 标高
                elementInfo.Level = GetElementLevel(doc, element);
                // 最大包围盒
                BoundingBoxInfo boundingBoxInfo = new BoundingBoxInfo();
                elementInfo.BoundingBox = GetBoundingBoxInfo(element);
                // 参数
                //elementInfo.Parameters = GetDimensionParameters(element);
                FilterParameterInfo thicknessParam = GetThicknessInfo(element); //厚度参数
                if (thicknessParam != null)
                {
                    elementInfo.Parameters.Add(thicknessParam);
                }

                FilterParameterInfo heightParam = GetBoundingBoxHeight(elementInfo.BoundingBox); //高度参数
                if (heightParam != null)
                {
                    elementInfo.Parameters.Add(heightParam);
                }

                return elementInfo;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 创建单个类型完整的TypeFullInfo对象
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="elementType"></param>
        /// <returns></returns>
        public static ElementTypeInfo CreateTypeFullInfo(Document doc, ElementType elementType)
        {
            ElementTypeInfo typeInfo = new ElementTypeInfo();
            // Id
            typeInfo.Id = elementType.Id.IntegerValue;
            // UniqueId
            typeInfo.UniqueId = elementType.UniqueId;
            // 类型名称
            typeInfo.Name = elementType.Name;
            // 族名称
            typeInfo.FamilyName = elementType.FamilyName;
            // 类别
            typeInfo.Category = elementType.Category.Name;
            // 内置类别
            typeInfo.BuiltInCategory = Enum.GetName(typeof(BuiltInCategory), elementType.Category.Id.IntegerValue);
            // 参数字典
            typeInfo.Parameters = GetDimensionParameters(elementType);
            FilterParameterInfo thicknessParam = GetThicknessInfo(elementType); //厚度参数
            if (thicknessParam != null)
            {
                typeInfo.Parameters.Add(thicknessParam);
            }

            return typeInfo;
        }

        /// <summary>
        /// 创建空间定位元素的信息
        /// </summary>
        public static PositioningElementInfo CreatePositioningElementInfo(Document doc, Element element)
        {
            try
            {
                if (element == null)
                    return null;
                PositioningElementInfo info = new PositioningElementInfo
                {
                    Id = element.Id.IntegerValue,
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null
                        ? Enum.GetName(typeof(BuiltInCategory), element.Category.Id.IntegerValue)
                        : null,
                    ElementClass = element.GetType().Name,
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // 处理标高
                if (element is Level level)
                {
                    // 转换为mm
                    info.Elevation = level.Elevation * 304.8;
                }
                // 处理轴网
                else if (element is Grid grid)
                {
                    Curve curve = grid.Curve;
                    if (curve != null)
                    {
                        XYZ start = curve.GetEndPoint(0);
                        XYZ end = curve.GetEndPoint(1);
                        // 创建JZLine（转换为mm）
                        info.GridLine = new JZLine(
                            start.X * 304.8, start.Y * 304.8, start.Z * 304.8,
                            end.X * 304.8, end.Y * 304.8, end.Z * 304.8);
                    }
                }

                // 获取标高信息
                info.Level = GetElementLevel(doc, element);

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"创建空间定位元素信息时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 创建空间元素的信息
        /// </summary>
        public static SpatialElementInfo CreateSpatialElementInfo(Document doc, Element element)
        {
            try
            {
                if (element == null || !(element is SpatialElement))
                    return null;
                SpatialElement spatialElement = element as SpatialElement;
                SpatialElementInfo info = new SpatialElementInfo
                {
                    Id = element.Id.IntegerValue,
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null
                        ? Enum.GetName(typeof(BuiltInCategory), element.Category.Id.IntegerValue)
                        : null,
                    ElementClass = element.GetType().Name,
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // 获取房间或区域的编号
                if (element is Room room)
                {
                    info.Number = room.Number;
                    // 转换为mm³
                    info.Volume = room.Volume * Math.Pow(304.8, 3);
                }
                else if (element is Area area)
                {
                    info.Number = area.Number;
                }

                // 获取面积
                Parameter areaParam = element.get_Parameter(BuiltInParameter.ROOM_AREA);
                if (areaParam != null && areaParam.HasValue)
                {
                    // 转换为mm²
                    info.Area = areaParam.AsDouble() * Math.Pow(304.8, 2);
                }

                // 获取周长
                Parameter perimeterParam = element.get_Parameter(BuiltInParameter.ROOM_PERIMETER);
                if (perimeterParam != null && perimeterParam.HasValue)
                {
                    // 转换为mm
                    info.Perimeter = perimeterParam.AsDouble() * 304.8;
                }

                // 获取标高
                info.Level = GetElementLevel(doc, element);

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"创建空间元素信息时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 创建视图元素的信息
        /// </summary>
        public static ViewInfo CreateViewInfo(Document doc, Element element)
        {
            try
            {
                if (element == null || !(element is View))
                    return null;
                View view = element as View;

                ViewInfo info = new ViewInfo
                {
                    Id = element.Id.IntegerValue,
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null
                        ? Enum.GetName(typeof(BuiltInCategory), element.Category.Id.IntegerValue)
                        : null,
                    ElementClass = element.GetType().Name,
                    ViewType = view.ViewType.ToString(),
                    Scale = view.Scale,
                    IsTemplate = view.IsTemplate,
                    DetailLevel = view.DetailLevel.ToString(),
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // 获取与视图关联的标高
                if (view is ViewPlan viewPlan && viewPlan.GenLevel != null)
                {
                    Level level = viewPlan.GenLevel;
                    info.AssociatedLevel = new LevelInfo
                    {
                        Id = level.Id.IntegerValue,
                        Name = level.Name,
                        Height = level.Elevation * 304.8 // 转换为mm
                    };
                }

                // 判断视图是否打开和激活
                UIDocument uidoc = new UIDocument(doc);

                // 获取所有打开的视图
                IList<UIView> openViews = uidoc.GetOpenUIViews();

                foreach (UIView uiView in openViews)
                {
                    // 检查视图是否打开
                    if (uiView.ViewId.IntegerValue == view.Id.IntegerValue)
                    {
                        info.IsOpen = true;

                        // 检查视图是否是当前激活的视图
                        if (uidoc.ActiveView.Id.IntegerValue == view.Id.IntegerValue)
                        {
                            info.IsActive = true;
                        }

                        break;
                    }
                }

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"创建视图元素信息时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 创建注释元素的信息
        /// </summary>
        public static AnnotationInfo CreateAnnotationInfo(Document doc, Element element)
        {
            try
            {
                if (element == null)
                    return null;
                AnnotationInfo info = new AnnotationInfo
                {
                    Id = element.Id.IntegerValue,
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null
                        ? Enum.GetName(typeof(BuiltInCategory), element.Category.Id.IntegerValue)
                        : null,
                    ElementClass = element.GetType().Name,
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // 获取所在视图
                Parameter viewParam = element.get_Parameter(BuiltInParameter.VIEW_NAME);
                if (viewParam != null && viewParam.HasValue)
                {
                    info.OwnerView = viewParam.AsString();
                }
                else if (element.OwnerViewId != ElementId.InvalidElementId)
                {
                    View ownerView = doc.GetElement(element.OwnerViewId) as View;
                    info.OwnerView = ownerView?.Name;
                }

                // 处理文字标注
                if (element is TextNote textNote)
                {
                    info.TextContent = textNote.Text;
                    XYZ position = textNote.Coord;
                    // 转换为mm
                    info.Position = new JZPoint(
                        position.X * 304.8,
                        position.Y * 304.8,
                        position.Z * 304.8);
                }
                // 处理尺寸标注
                else if (element is Dimension dimension)
                {
                    info.DimensionValue = dimension.Value.ToString();
                    XYZ origin = dimension.Origin;
                    // 转换为mm
                    info.Position = new JZPoint(
                        origin.X * 304.8,
                        origin.Y * 304.8,
                        origin.Z * 304.8);
                }
                // 处理其他注释元素
                else if (element is AnnotationSymbol annotationSymbol)
                {
                    if (annotationSymbol.Location is LocationPoint locationPoint)
                    {
                        XYZ position = locationPoint.Point;
                        // 转换为mm
                        info.Position = new JZPoint(
                            position.X * 304.8,
                            position.Y * 304.8,
                            position.Z * 304.8);
                    }
                }

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"创建注释元素信息时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 创建组或链接的信息
        /// </summary>
        public static GroupOrLinkInfo CreateGroupOrLinkInfo(Document doc, Element element)
        {
            try
            {
                if (element == null)
                    return null;
                GroupOrLinkInfo info = new GroupOrLinkInfo
                {
                    Id = element.Id.IntegerValue,
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null
                        ? Enum.GetName(typeof(BuiltInCategory), element.Category.Id.IntegerValue)
                        : null,
                    ElementClass = element.GetType().Name,
                    BoundingBox = GetBoundingBoxInfo(element)
                };

                // 处理组
                if (element is Group group)
                {
                    ICollection<ElementId> memberIds = group.GetMemberIds();
                    info.MemberCount = memberIds?.Count;
                    info.GroupType = group.GroupType?.Name;
                }
                // 处理链接
                else if (element is RevitLinkInstance linkInstance)
                {
                    RevitLinkType linkType = doc.GetElement(linkInstance.GetTypeId()) as RevitLinkType;
                    if (linkType != null)
                    {
                        ExternalFileReference extFileRef = linkType.GetExternalFileReference();
                        // 获取绝对路径
                        string absPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extFileRef.GetAbsolutePath());
                        info.LinkPath = absPath;

                        // 使用GetLinkedFileStatus获取链接状态
                        LinkedFileStatus linkStatus = linkType.GetLinkedFileStatus();
                        info.LinkStatus = linkStatus.ToString();
                    }
                    else
                    {
                        info.LinkStatus = LinkedFileStatus.Invalid.ToString();
                    }

                    // 获取位置
                    LocationPoint location = linkInstance.Location as LocationPoint;
                    if (location != null)
                    {
                        XYZ point = location.Point;
                        // 转换为mm
                        info.Position = new JZPoint(
                            point.X * 304.8,
                            point.Y * 304.8,
                            point.Z * 304.8);
                    }
                }

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"创建组和链接信息时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 创建元素的增强基础信息
        /// </summary>
        public static ElementBasicInfo CreateElementBasicInfo(Document doc, Element element)
        {
            try
            {
                if (element == null)
                    return null;
                ElementBasicInfo basicInfo = new ElementBasicInfo
                {
                    Id = element.Id.IntegerValue,
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    FamilyName = element?.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString(),
                    Category = element.Category?.Name,
                    BuiltInCategory = element.Category != null
                        ? Enum.GetName(typeof(BuiltInCategory), element.Category.Id.IntegerValue)
                        : null,
                    BoundingBox = GetBoundingBoxInfo(element)
                };
                return basicInfo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"创建元素基础信息时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取系统族构件的厚度参数信息
        /// </summary>
        /// <param name="element">系统族构件（墙、楼板、门等）</param>
        /// <returns>参数信息对象，无效返回null</returns>
        public static FilterParameterInfo GetThicknessInfo(Element element)
        {
            if (element == null)
            {
                return null;
            }

            // 获取构件类型
            ElementType elementType = element.Document.GetElement(element.GetTypeId()) as ElementType;
            if (elementType == null)
            {
                return null;
            }

            // 根据不同构件类型获取对应的内置厚度参数
            Parameter thicknessParam = null;

            if (elementType is WallType)
            {
                thicknessParam = elementType.get_Parameter(BuiltInParameter.WALL_ATTR_WIDTH_PARAM);
            }
            else if (elementType is FloorType)
            {
                thicknessParam = elementType.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
            }
            else if (elementType is FamilySymbol familySymbol)
            {
                switch (familySymbol.Category?.Id.IntegerValue)
                {
                    case (int)BuiltInCategory.OST_Doors:
                    case (int)BuiltInCategory.OST_Windows:
                        thicknessParam = elementType.get_Parameter(BuiltInParameter.FAMILY_THICKNESS_PARAM);
                        break;
                }
            }
            else if (elementType is CeilingType)
            {
                thicknessParam = elementType.get_Parameter(BuiltInParameter.CEILING_THICKNESS);
            }

            if (thicknessParam != null && thicknessParam.HasValue)
            {
                return new FilterParameterInfo
                {
                    Name = "厚度",
                    Value = $"{thicknessParam.AsDouble() * 304.8}"
                };
            }

            return null;
        }

        /// <summary>
        /// 获取元素所属的标高信息
        /// </summary>
        public static LevelInfo GetElementLevel(Document doc, Element element)
        {
            try
            {
                Level level = null;

                // 处理不同类型元素的标高获取
                if (element is Wall wall) // 墙体
                {
                    level = doc.GetElement(wall.LevelId) as Level;
                }
                else if (element is Floor floor) // 楼板
                {
                    Parameter levelParam = floor.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                    if (levelParam != null && levelParam.HasValue)
                    {
                        level = doc.GetElement(levelParam.AsElementId()) as Level;
                    }
                }
                else if (element is FamilyInstance familyInstance) // 族实例（包括常规模型等）
                {
                    // 尝试获取族实例的标高参数
                    Parameter levelParam = familyInstance.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                    if (levelParam != null && levelParam.HasValue)
                    {
                        level = doc.GetElement(levelParam.AsElementId()) as Level;
                    }

                    // 如果上面的方法获取不到，尝试使用SCHEDULE_LEVEL_PARAM
                    if (level == null)
                    {
                        levelParam = familyInstance.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);
                        if (levelParam != null && levelParam.HasValue)
                        {
                            level = doc.GetElement(levelParam.AsElementId()) as Level;
                        }
                    }
                }
                else // 其他元素
                {
                    // 尝试获取通用的标高参数
                    Parameter levelParam = element.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
                    if (levelParam != null && levelParam.HasValue)
                    {
                        level = doc.GetElement(levelParam.AsElementId()) as Level;
                    }
                }

                if (level != null)
                {
                    LevelInfo levelInfo = new LevelInfo
                    {
                        Id = level.Id.IntegerValue,
                        Name = level.Name,
                        Height = level.Elevation * 304.8
                    };
                    return levelInfo;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取元素的包围盒信息
        /// </summary>
        public static BoundingBoxInfo GetBoundingBoxInfo(Element element)
        {
            try
            {
                BoundingBoxXYZ bbox = element.get_BoundingBox(null);
                if (bbox == null)
                    return null;
                return new BoundingBoxInfo
                {
                    Min = new JZPoint(
                        bbox.Min.X * 304.8,
                        bbox.Min.Y * 304.8,
                        bbox.Min.Z * 304.8),
                    Max = new JZPoint(
                        bbox.Max.X * 304.8,
                        bbox.Max.Y * 304.8,
                        bbox.Max.Z * 304.8)
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取包围盒的高度参数信息
        /// </summary>
        /// <param name="boundingBoxInfo">包围盒信息</param>
        /// <returns>参数信息对象，无效返回null</returns>
        public static FilterParameterInfo GetBoundingBoxHeight(BoundingBoxInfo boundingBoxInfo)
        {
            try
            {
                // 参数检查
                if (boundingBoxInfo?.Min == null || boundingBoxInfo?.Max == null)
                {
                    return null;
                }

                // Z轴方向的差值即为高度
                double height = Math.Abs(boundingBoxInfo.Max.Z - boundingBoxInfo.Min.Z);

                return new FilterParameterInfo
                {
                    Name = "高度",
                    Value = $"{height}"
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取元素中所有非空参数的名称和值
        /// </summary>
        /// <param name="element">Revit元素</param>
        /// <returns>参数信息列表</returns>
        public static List<FilterParameterInfo> GetDimensionParameters(Element element)
        {
            // 检查元素是否为空
            if (element == null)
            {
                return new List<FilterParameterInfo>();
            }

            var parameters = new List<FilterParameterInfo>();

            // 获取元素的所有参数
            foreach (Parameter param in element.Parameters)
            {
                try
                {
                    // 跳过无效参数
                    if (!param.HasValue || param.IsReadOnly)
                    {
                        continue;
                    }

                    // 如果当前参数是尺寸相关参数
                    if (IsDimensionParameter(param))
                    {
                        // 获取参数值的字符串表示
                        string value = param.AsValueString();

                        // 如果值非空，则添加到列表中
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            parameters.Add(new FilterParameterInfo
                            {
                                Name = param.Definition.Name,
                                Value = value
                            });
                        }
                    }
                }
                catch
                {
                    // 如果获取某个参数值出错，继续处理下一个
                    continue;
                }
            }

            // 按参数名称排序后返回
            return parameters.OrderBy(p => p.Name).ToList();
        }

        /// <summary>
        /// 判断参数是否为可写入的尺寸参数
        /// </summary>
        public static bool IsDimensionParameter(Parameter param)
        {
#if REVIT2023_OR_GREATER
            // 在Revit 2023中使用Definition的GetDataType()方法获取参数类型
            ForgeTypeId paramTypeId = param.Definition.GetDataType();

            // 判断参数是否为尺寸相关的类型
            bool isDimensionType = paramTypeId.Equals(SpecTypeId.Length) ||
                                   paramTypeId.Equals(SpecTypeId.Angle) ||
                                   paramTypeId.Equals(SpecTypeId.Area) ||
                                   paramTypeId.Equals(SpecTypeId.Volume);
            // 只存储尺寸类型参数
            return isDimensionType;
#else
            // 判断参数是否为尺寸相关的类型
            bool isDimensionType = param.Definition.ParameterType == ParameterType.Length ||
                                   param.Definition.ParameterType == ParameterType.Angle ||
                                   param.Definition.ParameterType == ParameterType.Area ||
                                   param.Definition.ParameterType == ParameterType.Volume;

            // 只存储尺寸类型参数
            return isDimensionType;
#endif
        }
    }

    /// <summary>
    /// 存储元素完整信息的自定义类
    /// </summary>
    public class ElementInstanceInfo
    {
        /// <summary>
        /// Id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Id
        /// </summary>
        public string UniqueId { get; set; }

        /// <summary>
        /// 类型Id
        /// </summary>
        public int TypeId { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 族名称
        /// </summary>
        public string FamilyName { get; set; }

        /// <summary>
        /// 类别
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// 内置类别
        /// </summary>
        public string BuiltInCategory { get; set; }

        /// <summary>
        /// 所属房间Id
        /// </summary>
        public int RoomId { get; set; }

        /// <summary>
        /// 所属标高名称
        /// </summary>
        public LevelInfo Level { get; set; }

        /// <summary>
        /// 位置信息
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }

        /// <summary>
        /// 实例参数
        /// </summary>
        public List<FilterParameterInfo> Parameters { get; set; } = new List<FilterParameterInfo>();
    }

    /// <summary>
    /// 存储元素类型完整信息的自定义类
    /// </summary>
    public class ElementTypeInfo
    {
        /// <summary>
        /// ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Id
        /// </summary>
        public string UniqueId { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 族名称
        /// </summary>
        public string FamilyName { get; set; }

        /// <summary>
        /// 类别名称
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// 内置类别ID
        /// </summary>
        public string BuiltInCategory { get; set; }

        /// <summary>
        /// 类型参数
        /// </summary>
        public List<FilterParameterInfo> Parameters { get; set; } = new List<FilterParameterInfo>();
    }

    /// <summary>
    /// 空间定位元素(标高、轴网等)基础信息的类
    /// </summary>
    public class PositioningElementInfo
    {
        /// <summary>
        /// 元素ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 元素唯一ID
        /// </summary>
        public string UniqueId { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 族名称
        /// </summary>
        public string FamilyName { get; set; }

        /// <summary>
        /// 类别名称
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// 内置类别(可选)
        /// </summary>
        public string BuiltInCategory { get; set; }

        /// <summary>
        /// 元素的.NET类名称
        /// </summary>
        public string ElementClass { get; set; }

        /// <summary>
        /// 高程值 (适用于标高，单位mm)
        /// </summary>
        public double? Elevation { get; set; }

        /// <summary>
        /// 所属标高
        /// </summary>
        public LevelInfo Level { get; set; }

        /// <summary>
        /// 位置信息
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }

        /// <summary>
        /// 轴网线(适用于轴网)
        /// </summary>
        public JZLine GridLine { get; set; }
    }

    /// <summary>
    /// 存储空间元素(房间、区域等)基础信息的类
    /// </summary>
    public class SpatialElementInfo
    {
        /// <summary>
        /// 元素ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 元素唯一ID
        /// </summary>
        public string UniqueId { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 族名称
        /// </summary>
        public string FamilyName { get; set; }

        /// <summary>
        /// 编号
        /// </summary>
        public string Number { get; set; }

        /// <summary>
        /// 类别名称
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// 内置类别(可选)
        /// </summary>
        public string BuiltInCategory { get; set; }

        /// <summary>
        /// 元素的.NET类名称
        /// </summary>
        public string ElementClass { get; set; }

        /// <summary>
        /// 面积(单位mm²)
        /// </summary>
        public double? Area { get; set; }

        /// <summary>
        /// 体积(单位mm³)
        /// </summary>
        public double? Volume { get; set; }

        /// <summary>
        /// 周长(单位mm)
        /// </summary>
        public double? Perimeter { get; set; }

        /// <summary>
        /// 所在标高
        /// </summary>
        public LevelInfo Level { get; set; }

        /// <summary>
        /// 位置信息
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
    }

    /// <summary>
    /// 存储视图元素基础信息的类
    /// </summary>
    public class ViewInfo
    {
        /// <summary>
        /// 元素ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 元素唯一ID
        /// </summary>
        public string UniqueId { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 族名称
        /// </summary>
        public string FamilyName { get; set; }

        /// <summary>
        /// 类别名称
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// 内置类别(可选)
        /// </summary>
        public string BuiltInCategory { get; set; }

        /// <summary>
        /// 元素的.NET类名称
        /// </summary>
        public string ElementClass { get; set; }

        /// <summary>
        /// 视图类型
        /// </summary>
        public string ViewType { get; set; }

        /// <summary>
        /// 视图比例
        /// </summary>
        public int? Scale { get; set; }

        /// <summary>
        /// 是否为模板视图
        /// </summary>
        public bool IsTemplate { get; set; }

        /// <summary>
        /// 详图级别
        /// </summary>
        public string DetailLevel { get; set; }

        /// <summary>
        /// 关联的标高
        /// </summary>
        public LevelInfo AssociatedLevel { get; set; }

        /// <summary>
        /// 位置信息
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }

        /// <summary>
        /// 视图是否已打开
        /// </summary>
        public bool IsOpen { get; set; }

        /// <summary>
        /// 是否是当前激活的视图
        /// </summary>
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// 存储注释元素基础信息的类
    /// </summary>
    public class AnnotationInfo
    {
        /// <summary>
        /// 元素ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 元素唯一ID
        /// </summary>
        public string UniqueId { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 族名称
        /// </summary>
        public string FamilyName { get; set; }

        /// <summary>
        /// 类别名称
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// 内置类别(可选)
        /// </summary>
        public string BuiltInCategory { get; set; }

        /// <summary>
        /// 元素的.NET类名称
        /// </summary>
        public string ElementClass { get; set; }

        /// <summary>
        /// 所在视图
        /// </summary>
        public string OwnerView { get; set; }

        /// <summary>
        /// 文本内容 (适用于文字标注)
        /// </summary>
        public string TextContent { get; set; }

        /// <summary>
        /// 位置信息(单位mm)
        /// </summary>
        public JZPoint Position { get; set; }

        /// <summary>
        /// 位置信息
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }

        /// <summary>
        /// 尺寸值 (适用于尺寸标注)
        /// </summary>
        public string DimensionValue { get; set; }
    }

    /// <summary>
    /// 存储组和链接基础信息的类
    /// </summary>
    public class GroupOrLinkInfo
    {
        /// <summary>
        /// 元素ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 元素唯一ID
        /// </summary>
        public string UniqueId { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 族名称
        /// </summary>
        public string FamilyName { get; set; }

        /// <summary>
        /// 类别名称
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// 内置类别(可选)
        /// </summary>
        public string BuiltInCategory { get; set; }

        /// <summary>
        /// 元素的.NET类名称
        /// </summary>
        public string ElementClass { get; set; }

        /// <summary>
        /// 组成员数量
        /// </summary>
        public int? MemberCount { get; set; }

        /// <summary>
        /// 组类型
        /// </summary>
        public string GroupType { get; set; }

        /// <summary>
        /// 链接状态
        /// </summary>
        public string LinkStatus { get; set; }

        /// <summary>
        /// 链接路径
        /// </summary>
        public string LinkPath { get; set; }

        /// <summary>
        /// 位置信息(单位mm)
        /// </summary>
        public JZPoint Position { get; set; }

        /// <summary>
        /// 位置信息
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
    }

    /// <summary>
    /// 存储元素基础信息的增强类
    /// </summary>
    public class ElementBasicInfo
    {
        /// <summary>
        /// 元素ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 元素唯一ID
        /// </summary>
        public string UniqueId { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 族名称
        /// </summary>
        public string FamilyName { get; set; }

        /// <summary>
        /// 类别名称
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// 内置类别(可选)
        /// </summary>
        public string BuiltInCategory { get; set; }

        /// <summary>
        /// 位置信息
        /// </summary>
        public BoundingBoxInfo BoundingBox { get; set; }
    }


    /// <summary>
    /// 存储过滤器参数信息的自定义类（用于ElementFilter功能）
    /// </summary>
    public class FilterParameterInfo
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    /// <summary>
    /// 存储包围盒信息的自定义类
    /// </summary>
    public class BoundingBoxInfo
    {
        public JZPoint Min { get; set; }
        public JZPoint Max { get; set; }
    }

    /// <summary>
    /// 存储标高信息的自定义类
    /// </summary>
    public class LevelInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Height { get; set; }
    }
}