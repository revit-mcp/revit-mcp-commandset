using Autodesk.Revit.DB;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Features.ElementCreation.Models;
using RevitMCPCommandSet.Features.ElementCreation.Utils;
using RevitMCPCommandSet.Models.Geometry;
using RevitMCPCommandSet.Utils.SystemCreation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitMCPCommandSet.Utils.SystemCreation
{
    /// <summary>
    /// 系统族创建器 - 统一处理各种系统族的创建
    /// </summary>
    public class SystemElementCreator
    {
        private readonly Document _document;

        public SystemElementCreator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>
        /// 创建系统族元素
        /// </summary>
        public Element Create(SystemElementParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            // 参数验证（使用详细验证保持原有严格性）
            var validationError = ElementUtilityService.ValidateSystemParametersDetailed(parameters);
            if (!string.IsNullOrEmpty(validationError))
                throw new ArgumentException(validationError);

            // 验证版本兼容性
            CheckVersionCompatibility(parameters.ElementType);

            switch (parameters.ElementType.ToLower())
            {
                case "wall":
                    return CreateWall(parameters);
                case "floor":
                    return CreateFloor(parameters);
                default:
                    throw new NotImplementedException($"系统族类型 {parameters.ElementType} 尚未实现");
            }
        }

        #region 墙体创建

        /// <summary>
        /// 创建墙体
        /// </summary>
        private Wall CreateWall(SystemElementParameters parameters)
        {
            // 参数已经在Create方法中验证过，这里只做必要的检查
            if (parameters.WallParameters?.Line == null)
                throw new ArgumentException("墙体创建需要指定路径线段");

            if (parameters.WallParameters.Height <= 0)
                throw new ArgumentException("墙体高度必须大于0");

            // 获取墙体类型
            WallType wallType;
            if (parameters.WallParameters.Thickness.HasValue && parameters.WallParameters.Thickness.Value > 0)
            {
                // 使用指定厚度获取或创建墙体类型
                wallType = GetOrCreateWallType(parameters.TypeId, parameters.WallParameters.Thickness.Value);
            }
            else
            {
                // 使用原始类型
                wallType = _document.GetElement(new ElementId(parameters.TypeId)) as WallType;
                if (wallType == null)
                    throw new ArgumentException($"无效的墙体类型ID: {parameters.TypeId}");
            }

            // 获取或查找标高
            Level level = GetOrFindLevel(parameters);

            // 转换坐标（毫米转英尺）
            var startPoint = JZPoint.ToXYZ(parameters.WallParameters.Line.P0);
            var endPoint = JZPoint.ToXYZ(parameters.WallParameters.Line.P1);
            var line = Line.CreateBound(startPoint, endPoint);

            // 转换高度和偏移（毫米转英尺）
            double heightInFeet = parameters.WallParameters.Height / 304.8;
            double offsetInFeet = parameters.WallParameters.BaseOffset / 304.8;

            Wall wall = null;

#if REVIT2019 || REVIT2020 || REVIT2021 || REVIT2022 || REVIT2023 || REVIT2024 || REVIT2025
            // Revit 2019+ API
            wall = Wall.Create(
                _document,
                line,                        // 墙中心线
                wallType.Id,                // 墙类型
                level.Id,                    // 标高
                heightInFeet,               // 高度
                offsetInFeet,               // 底部偏移
                false,                      // flip
                parameters.IsStructural     // 是否结构墙
            );
#else
            throw new NotSupportedException("当前Revit版本不支持");
#endif

            return wall;
        }

        #endregion

        #region 楼板创建

        /// <summary>
        /// 创建楼板
        /// </summary>
        private Floor CreateFloor(SystemElementParameters parameters)
        {
            // 验证参数
            if (parameters.FloorParameters?.Boundary == null || parameters.FloorParameters.Boundary.Count < 3)
                throw new ArgumentException("楼板边界至少需要3个点");

            // 获取楼板类型
            FloorType floorType;
            if (parameters.FloorParameters.Thickness.HasValue && parameters.FloorParameters.Thickness.Value > 0)
            {
                // 使用指定厚度获取或创建楼板类型
                floorType = GetOrCreateFloorType(parameters.TypeId, parameters.FloorParameters.Thickness.Value);
            }
            else
            {
                // 使用原始类型
                floorType = _document.GetElement(new ElementId(parameters.TypeId)) as FloorType;
                if (floorType == null)
                    throw new ArgumentException($"无效的楼板类型ID: {parameters.TypeId}");
            }

            // 获取或查找标高
            Level level = GetOrFindLevel(parameters);

            // 创建轮廓线
            var curveArray = new CurveArray();
            for (int i = 0; i < parameters.FloorParameters.Boundary.Count; i++)
            {
                var startPoint = JZPoint.ToXYZ(parameters.FloorParameters.Boundary[i]);
                var endPoint = JZPoint.ToXYZ(parameters.FloorParameters.Boundary[(i + 1) % parameters.FloorParameters.Boundary.Count]);

                var line = Line.CreateBound(startPoint, endPoint);
                curveArray.Append(line);
            }

            // 验证轮廓是否闭合
            if (!IsClosedCurveArray(curveArray))
                throw new ArgumentException("楼板轮廓必须形成闭合区域");

            Floor floor = null;

#if REVIT2022 || REVIT2023 || REVIT2024 || REVIT2025
            // Revit 2022+ 使用新API
            var curveLoop = new CurveLoop();
            foreach (Curve curve in curveArray)
            {
                curveLoop.Append(curve);
            }

            var curveLoops = new List<CurveLoop> { curveLoop };
            floor = Floor.Create(_document, curveLoops, floorType.Id, level.Id);

            // 设置偏移
            if (parameters.FloorParameters.TopOffset != 0)
            {
                var param = floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
                if (param != null && !param.IsReadOnly)
                {
                    param.Set(parameters.FloorParameters.TopOffset / 304.8);
                }
            }
#else
            // Revit 2019-2021 使用旧API
            floor = _document.Create.NewFloor(curveArray, floorType, level, parameters.IsStructural);

            // 设置顶部偏移
            if (parameters.FloorParameters.TopOffset != 0)
            {
                var param = floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
                if (param != null && !param.IsReadOnly)
                {
                    param.Set(parameters.FloorParameters.TopOffset / 304.8); // 毫米转英尺
                }
            }
#endif
            return floor;
        }

        /// <summary>
        /// 检查曲线数组是否闭合
        /// </summary>
        private bool IsClosedCurveArray(CurveArray curveArray)
        {
            if (curveArray.Size < 3) return false;

            var curves = curveArray.Cast<Curve>().ToList();
            var firstStart = curves[0].GetEndPoint(0);
            var lastEnd = curves[curves.Count - 1].GetEndPoint(1);

            return firstStart.IsAlmostEqualTo(lastEnd, 0.001); // 容差约0.3mm
        }

        /// <summary>
        /// 设置楼板坡度
        /// </summary>
        private void SetFloorSlope(Floor floor, double slopePercentage)
        {
            try
            {
                // 简化实现：设置楼板参数而不是使用坡度箭头
                // 坡度箭头创建在不同版本的Revit中API可能不同，先跳过
                System.Diagnostics.Trace.WriteLine($"楼板坡度设置请求: {slopePercentage}%，当前版本暂不支持自动设置");

                // TODO: 在后续版本中实现具体的坡度设置逻辑
                // 可能需要根据不同Revit版本使用不同的API
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"设置楼板坡度失败: {ex.Message}");
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取或创建指定厚度的墙体类型
        /// </summary>
        /// <param name="baseTypeId">基础墙体类型ID</param>
        /// <param name="thicknessInMM">目标厚度（毫米）</param>
        /// <returns>符合厚度要求的墙体类型</returns>
        private WallType GetOrCreateWallType(int baseTypeId, double thicknessInMM)
        {
            // 获取基础墙体类型
            var baseType = _document.GetElement(new ElementId(baseTypeId)) as WallType;
            if (baseType == null)
                throw new ArgumentException($"无效的墙体类型ID: {baseTypeId}");

            // 转换厚度单位
            double thicknessInFeet = thicknessInMM / 304.8;

            // 检查当前类型厚度是否满足要求
            var currentStructure = baseType.GetCompoundStructure();
            if (currentStructure != null)
            {
                double currentThickness = currentStructure.GetWidth();
                // 允许1mm的误差
                if (Math.Abs(currentThickness - thicknessInFeet) < 1.0 / 304.8)
                {
                    return baseType; // 直接使用原类型
                }
            }

            // 查找是否已存在指定厚度的墙体类型
            string targetName = $"{baseType.Name}_{thicknessInMM}mm";
            var existingType = new FilteredElementCollector(_document)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(w => w.Name == targetName);

            if (existingType != null)
                return existingType;

            // 创建新的墙体类型
            var newWallType = baseType.Duplicate(targetName) as WallType;
            if (newWallType == null)
                throw new InvalidOperationException("复制墙体类型失败");

            // 修改墙体厚度
            var newStructure = newWallType.GetCompoundStructure();
            if (newStructure != null)
            {
                // 获取原始层的材料ID
                var layers = newStructure.GetLayers();
                if (layers.Count > 0)
                {
                    ElementId materialId = layers.First().MaterialId;

                    // 创建新的单层结构
                    var newLayer = new CompoundStructureLayer(
                        thicknessInFeet,
                        MaterialFunctionAssignment.Structure,
                        materialId
                    );

                    // 设置新的复合结构
                    IList<CompoundStructureLayer> newLayers = new List<CompoundStructureLayer> { newLayer };
                    newStructure.SetLayers(newLayers);

                    // 应用新的复合结构
                    newWallType.SetCompoundStructure(newStructure);
                }
            }

            return newWallType;
        }

        /// <summary>
        /// 获取或创建指定厚度的楼板类型
        /// </summary>
        /// <param name="baseTypeId">基础楼板类型ID</param>
        /// <param name="thicknessInMM">目标厚度（毫米）</param>
        /// <returns>符合厚度要求的楼板类型</returns>
        private FloorType GetOrCreateFloorType(int baseTypeId, double thicknessInMM)
        {
            // 获取基础楼板类型
            var baseType = _document.GetElement(new ElementId(baseTypeId)) as FloorType;
            if (baseType == null)
                throw new ArgumentException($"无效的楼板类型ID: {baseTypeId}");

            // 转换厚度单位
            double thicknessInFeet = thicknessInMM / 304.8;

            // 检查当前类型厚度是否满足要求
            var currentStructure = baseType.GetCompoundStructure();
            if (currentStructure != null)
            {
                double currentThickness = currentStructure.GetWidth();
                // 允许1mm的误差
                if (Math.Abs(currentThickness - thicknessInFeet) < 1.0 / 304.8)
                {
                    return baseType; // 直接使用原类型
                }
            }

            // 查找是否已存在指定厚度的楼板类型
            string targetName = $"{baseType.Name}_{thicknessInMM}mm";
            var existingType = new FilteredElementCollector(_document)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .FirstOrDefault(f => f.Name == targetName);

            if (existingType != null)
                return existingType;

            // 创建新的楼板类型
            var newFloorType = baseType.Duplicate(targetName) as FloorType;
            if (newFloorType == null)
                throw new InvalidOperationException("复制楼板类型失败");

            // 修改楼板厚度
            var newStructure = newFloorType.GetCompoundStructure();
            if (newStructure != null)
            {
                // 获取所有层
                var layers = newStructure.GetLayers();
                if (layers.Count > 0)
                {
                    // 如果只有一层，直接设置厚度
                    if (layers.Count == 1)
                    {
                        newStructure.SetLayerWidth(0, thicknessInFeet);
                    }
                    else
                    {
                        // 多层情况：保持第一层（通常是主结构层）为目标厚度，删除其他层
                        var mainLayer = layers[0];
                        var newLayer = new CompoundStructureLayer(
                            thicknessInFeet,
                            mainLayer.Function,
                            mainLayer.MaterialId
                        );

                        // 设置新的单层结构
                        IList<CompoundStructureLayer> newLayers = new List<CompoundStructureLayer> { newLayer };
                        newStructure.SetLayers(newLayers);
                    }

                    // 应用新的复合结构
                    newFloorType.SetCompoundStructure(newStructure);
                }
            }

            return newFloorType;
        }

        /// <summary>
        /// 获取或自动查找标高
        /// </summary>
        private Level GetOrFindLevel(SystemElementParameters parameters)
        {
            // 如果指定了标高ID
            if (parameters.LevelId.HasValue && parameters.LevelId.Value > 0)
            {
                var level = _document.GetElement(new ElementId(parameters.LevelId.Value)) as Level;
                if (level != null) return level;
            }

            // 自动查找标高
            if (parameters.AutoFindLevel)
            {
                // 获取参考高度（墙使用线段起点Z，楼板使用第一个点Z）
                double referenceZ = 0;
                if (parameters.WallParameters?.Line != null)
                {
                    referenceZ = parameters.WallParameters.Line.P0.Z;
                }
                else if (parameters.FloorParameters?.Boundary != null && parameters.FloorParameters.Boundary.Count > 0)
                {
                    referenceZ = parameters.FloorParameters.Boundary[0].Z;
                }

                var nearestLevel = GetNearestLevel(referenceZ / 304.8); // 毫米转英尺
                if (nearestLevel != null) return nearestLevel;
            }

            // 使用默认标高
            var defaultLevel = new FilteredElementCollector(_document)
                .OfClass(typeof(Level))
                .FirstElement() as Level;

            if (defaultLevel == null)
                throw new InvalidOperationException("文档中没有可用的标高");

            return defaultLevel;
        }

        /// <summary>
        /// 获取最近的标高
        /// </summary>
        private Level GetNearestLevel(double elevationInFeet)
        {
            var levels = new FilteredElementCollector(_document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => Math.Abs(l.Elevation - elevationInFeet))
                .FirstOrDefault();

            return levels;
        }

        /// <summary>
        /// 检查版本兼容性
        /// </summary>
        private void CheckVersionCompatibility(string elementType)
        {
#if !REVIT2019 && !REVIT2020 && !REVIT2021 && !REVIT2022 && !REVIT2023 && !REVIT2024 && !REVIT2025
            throw new NotSupportedException(
                $"系统族类型 {elementType} 的创建需要 Revit 2019 或更高版本");
#endif
        }

        #endregion
    }
}