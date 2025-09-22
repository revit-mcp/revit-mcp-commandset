using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Models.Geometry;
using RevitMCPCommandSet.Utils.FamilyCreation;

namespace RevitMCPCommandSet.Utils.FamilyCreation
{
    /// <summary>
    /// 族实例服务类 - 统一处理族创建、验证、建议功能
    /// </summary>
    public class FamilyInstanceService
    {
        private readonly Document doc;
        private readonly FamilyInstanceCreator creator;

        public FamilyInstanceService(Document document)
        {
            doc = document ?? throw new ArgumentNullException(nameof(document));
            creator = new FamilyInstanceCreator(doc);
        }

        /// <summary>
        /// 智能创建族实例
        /// </summary>
        public Models.Common.CreateResult CreateInstance(FamilyCreationParameters parameters)
        {
            try
            {
                // 1. 获取族类型
                var symbol = doc.GetElement(new ElementId(parameters.TypeId)) as FamilySymbol;
                if (symbol == null)
                {
                    return new Models.Common.CreateResult
                    {
                        Success = false,
                        Message = $"找不到族类型 ID: {parameters.TypeId}"
                    };
                }

                // 2. 验证参数
                var validation = ValidateParameters(symbol, parameters);
                if (!validation.IsValid)
                {
                    var suggestion = AnalyzeRequirements(symbol);
                    return new Models.Common.CreateResult
                    {
                        Success = false,
                        Message = validation.ErrorMessage,
                        AdditionalInfo = new Dictionary<string, object>
                        {
                            ["suggestion"] = suggestion
                        }
                    };
                }

                // 3. 使用修复后的参数创建
                var workingParams = validation.AdjustedParameters ?? parameters;

                // 4. 重置并设置创建器
                creator.Reset();
                SetupCreatorByPlacementType(symbol, workingParams);

                // 5. 执行创建
                var instance = creator.Create();
                if (instance != null)
                {
                    return new Models.Common.CreateResult
                    {
                        Success = true,
                        ElementId = instance.Id.IntegerValue,
                        Message = "族实例创建成功"
                    };
                }
                else
                {
                    var suggestion = AnalyzeRequirements(symbol);
                    return new Models.Common.CreateResult
                    {
                        Success = false,
                        Message = "创建返回空实例",
                        AdditionalInfo = new Dictionary<string, object>
                        {
                            ["suggestion"] = suggestion
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                return BuildFailureResult(ex, parameters);
            }
        }

        /// <summary>
        /// 分析族类型的参数要求
        /// </summary>
        public FamilyCreationRequirements AnalyzeRequirements(int typeId)
        {
            var symbol = doc.GetElement(new ElementId(typeId)) as FamilySymbol;
            if (symbol == null)
            {
                throw new ArgumentException($"找不到族类型 ID: {typeId}");
            }

            return AnalyzeRequirements(symbol);
        }

        /// <summary>
        /// 分析族类型的参数要求（内部方法）
        /// </summary>
        private FamilyCreationRequirements AnalyzeRequirements(FamilySymbol symbol)
        {
            var requirements = new FamilyCreationRequirements
            {
                TypeId = symbol.Id.IntegerValue,
                FamilyName = symbol.FamilyName,
                Parameters = new Dictionary<string, ParameterInfo>()
            };

            var placementType = symbol.Family.FamilyPlacementType;

            // 根据族放置类型添加参数要求
            switch (placementType)
            {
                case FamilyPlacementType.OneLevelBased:
                    AddOneLevelBasedParameters(requirements);
                    break;
                case FamilyPlacementType.OneLevelBasedHosted:
                    AddOneLevelBasedHostedParameters(requirements);
                    break;
                case FamilyPlacementType.TwoLevelsBased:
                    AddTwoLevelsBasedParameters(requirements);
                    break;
                case FamilyPlacementType.WorkPlaneBased:
                    AddWorkPlaneBasedParameters(requirements);
                    break;
                case FamilyPlacementType.CurveBased:
                case FamilyPlacementType.CurveBasedDetail:
                case FamilyPlacementType.CurveDrivenStructural:
                    AddCurveBasedParameters(requirements);
                    break;
                case FamilyPlacementType.ViewBased:
                    AddViewBasedParameters(requirements);
                    break;
                case FamilyPlacementType.Adaptive:
                    requirements.Message = "Adaptive族类型暂不支持自动创建";
                    break;
                default:
                    requirements.Message = $"未知族放置类型: {placementType}";
                    break;
            }

            return requirements;
        }

        /// <summary>
        /// 验证参数完整性和正确性
        /// </summary>
        private ValidationResult ValidateParameters(FamilySymbol symbol, FamilyCreationParameters parameters)
        {
            var result = new ValidationResult { IsValid = true };
            var placementType = symbol.Family.FamilyPlacementType;

            // 根据族类型验证必需参数
            switch (placementType)
            {
                case FamilyPlacementType.OneLevelBased:
                    result = ValidateOneLevelBased(parameters);
                    break;
                case FamilyPlacementType.OneLevelBasedHosted:
                    result = ValidateOneLevelBasedHosted(parameters);
                    break;
                case FamilyPlacementType.TwoLevelsBased:
                    result = ValidateTwoLevelsBased(parameters);
                    break;
                case FamilyPlacementType.WorkPlaneBased:
                    result = ValidateWorkPlaneBased(parameters);
                    break;
                case FamilyPlacementType.CurveBased:
                case FamilyPlacementType.CurveBasedDetail:
                case FamilyPlacementType.CurveDrivenStructural:
                    result = ValidateCurveBased(parameters);
                    break;
                case FamilyPlacementType.ViewBased:
                    result = ValidateViewBased(parameters);
                    break;
                case FamilyPlacementType.Adaptive:
                    result.IsValid = false;
                    result.ErrorMessage = "Adaptive族类型暂不支持";
                    break;
                default:
                    result.IsValid = false;
                    result.ErrorMessage = $"未知的族放置类型: {placementType}";
                    break;
            }

            return result;
        }

        /// <summary>
        /// 根据族放置类型设置创建器
        /// </summary>
        private void SetupCreatorByPlacementType(FamilySymbol symbol, FamilyCreationParameters parameters)
        {
            var placementType = symbol.Family.FamilyPlacementType;

            switch (placementType)
            {
                case FamilyPlacementType.OneLevelBased:
                    creator.SetupOneLevelBased(
                        symbol,
                        JZPoint.ToXYZ(parameters.LocationPoint),
                        parameters.BaseLevelId > 0 ? doc.GetElement(new ElementId(parameters.BaseLevelId)) as Level : null
                    );
                    break;

                case FamilyPlacementType.OneLevelBasedHosted:
                    creator.SetupOneLevelBasedHosted(
                        symbol,
                        JZPoint.ToXYZ(parameters.LocationPoint),
                        parameters.BaseLevelId > 0 ? doc.GetElement(new ElementId(parameters.BaseLevelId)) as Level : null
                    );
                    break;

                case FamilyPlacementType.TwoLevelsBased:
                    creator.SetupTwoLevelsBased(
                        symbol,
                        JZPoint.ToXYZ(parameters.LocationPoint),
                        parameters.BaseLevelId > 0 ? doc.GetElement(new ElementId(parameters.BaseLevelId)) as Level : null,
                        parameters.TopLevelId > 0 ? doc.GetElement(new ElementId(parameters.TopLevelId)) as Level : null,
                        parameters.BaseOffset / 304.8,
                        parameters.TopOffset / 304.8
                    );
                    break;

                case FamilyPlacementType.WorkPlaneBased:
                    // WorkPlaneBased 需要查看具体的签名
                    creator.SetupWorkPlaneBased(
                        symbol,
                        JZPoint.ToXYZ(parameters.LocationPoint)
                    );
                    break;

                case FamilyPlacementType.CurveBased:
                    creator.SetupCurveBased(
                        symbol,
                        Line.CreateBound(JZPoint.ToXYZ(parameters.LocationLine.P0), JZPoint.ToXYZ(parameters.LocationLine.P1))
                    );
                    break;

                case FamilyPlacementType.CurveDrivenStructural:
                    creator.SetupCurveDrivenStructural(
                        symbol,
                        Line.CreateBound(JZPoint.ToXYZ(parameters.LocationLine.P0), JZPoint.ToXYZ(parameters.LocationLine.P1)),
                        parameters.BaseLevelId > 0 ? doc.GetElement(new ElementId(parameters.BaseLevelId)) as Level : null
                    );
                    break;

                case FamilyPlacementType.CurveBasedDetail:
                    creator.SetupCurveBasedDetail(
                        symbol,
                        Line.CreateBound(JZPoint.ToXYZ(parameters.LocationLine.P0), JZPoint.ToXYZ(parameters.LocationLine.P1)),
                        parameters.ViewId > 0 ? doc.GetElement(new ElementId(parameters.ViewId)) as View : null
                    );
                    break;

                case FamilyPlacementType.ViewBased:
                    creator.SetupViewBased(
                        symbol,
                        JZPoint.ToXYZ(parameters.LocationPoint),
                        parameters.ViewId > 0 ? doc.GetElement(new ElementId(parameters.ViewId)) as View : null
                    );
                    break;
            }
        }

        /// <summary>
        /// 构建失败结果
        /// </summary>
        private Models.Common.CreateResult BuildFailureResult(Exception ex, FamilyCreationParameters parameters)
        {
            var result = new Models.Common.CreateResult
            {
                Success = false,
                Message = $"创建失败: {ex.Message}"
            };

            // 如果能获取族类型，提供参数建议
            try
            {
                var symbol = doc.GetElement(new ElementId(parameters.TypeId)) as FamilySymbol;
                if (symbol != null)
                {
                    var suggestion = AnalyzeRequirements(symbol);
                    result.AdditionalInfo = new Dictionary<string, object>
                    {
                        ["suggestion"] = suggestion
                    };
                }
            }
            catch
            {
                // 忽略获取建议时的异常
            }

            return result;
        }

        #region 参数验证方法

        private ValidationResult ValidateOneLevelBased(FamilyCreationParameters parameters)
        {
            var result = new ValidationResult { IsValid = true };

            if (parameters.LocationPoint == null)
            {
                result.IsValid = false;
                result.ErrorMessage = "缺少必需参数: locationPoint";
                return result;
            }

            // 自动修复标高
            if (parameters.AutoFindLevel && parameters.BaseLevelId <= 0)
            {
                var nearestLevel = GetNearestLevel(parameters.LocationPoint.Z / 304.8);
                if (nearestLevel != null)
                {
                    result.AdjustedParameters = new FamilyCreationParameters
                    {
                        TypeId = parameters.TypeId,
                        LocationPoint = parameters.LocationPoint,
                        BaseLevelId = nearestLevel.Id.IntegerValue,
                        BaseOffset = parameters.BaseOffset,
                        AutoFindLevel = parameters.AutoFindLevel
                    };
                }
            }

            return result;
        }

        private ValidationResult ValidateOneLevelBasedHosted(FamilyCreationParameters parameters)
        {
            var result = ValidateOneLevelBased(parameters);
            if (!result.IsValid) return result;

            // 门窗等需要宿主的族，如果没有指定宿主且未启用自动查找，给出提示
            if (parameters.HostElementId <= 0 && !parameters.AutoFindHost)
            {
                result.IsValid = false;
                result.ErrorMessage = "门窗族需要宿主元素，请设置 hostElementId 或启用 autoFindHost";
            }

            return result;
        }

        private ValidationResult ValidateTwoLevelsBased(FamilyCreationParameters parameters)
        {
            var result = ValidateOneLevelBased(parameters);
            if (!result.IsValid) return result;

            if (parameters.TopLevelId <= 0)
            {
                result.IsValid = false;
                result.ErrorMessage = "缺少必需参数: topLevelId";
            }

            return result;
        }

        private ValidationResult ValidateWorkPlaneBased(FamilyCreationParameters parameters)
        {
            var result = new ValidationResult { IsValid = true };

            if (parameters.LocationPoint == null)
            {
                result.IsValid = false;
                result.ErrorMessage = "缺少必需参数: locationPoint";
            }

            return result;
        }

        private ValidationResult ValidateCurveBased(FamilyCreationParameters parameters)
        {
            var result = new ValidationResult { IsValid = true };

            if (parameters.LocationLine == null)
            {
                result.IsValid = false;
                result.ErrorMessage = "缺少必需参数: locationLine";
            }

            return result;
        }

        private ValidationResult ValidateViewBased(FamilyCreationParameters parameters)
        {
            var result = new ValidationResult { IsValid = true };

            if (parameters.LocationPoint == null)
            {
                result.IsValid = false;
                result.ErrorMessage = "缺少必需参数: locationPoint";
                return result;
            }

            if (parameters.ViewId <= 0)
            {
                result.IsValid = false;
                result.ErrorMessage = "缺少必需参数: viewId";
            }

            return result;
        }

        #endregion

        #region 参数定义方法

        private void AddOneLevelBasedParameters(FamilyCreationRequirements requirements)
        {
            requirements.Parameters["locationPoint"] = new ParameterInfo
            {
                Required = true,
                Description = "放置点坐标(mm)",
            };

            requirements.Parameters["baseLevelId"] = new ParameterInfo
            {
                Required = false,
                Description = "基准标高的ElementId",
                Default = -1
            };

            requirements.Parameters["baseOffset"] = new ParameterInfo
            {
                Required = false,
                Description = "相对基准标高的偏移(mm)",
                Default = FamilyCreationDefaults.BaseOffset
            };

            requirements.Parameters["autoFindLevel"] = new ParameterInfo
            {
                Required = false,
                Description = "是否自动查找最近标高",
                Default = FamilyCreationDefaults.AutoFindLevel
            };
        }

        private void AddOneLevelBasedHostedParameters(FamilyCreationRequirements requirements)
        {
            AddOneLevelBasedParameters(requirements);

            requirements.Parameters["autoFindHost"] = new ParameterInfo
            {
                Required = false,
                Description = "是否自动查找宿主元素",
                Default = FamilyCreationDefaults.AutoFindHost
            };

            requirements.Parameters["searchRadius"] = new ParameterInfo
            {
                Required = false,
                Description = "自动查找宿主的搜索半径(mm)",
                Default = FamilyCreationDefaults.SearchRadius
            };

            requirements.Parameters["hostCategories"] = new ParameterInfo
            {
                Required = false,
                Description = "宿主类别过滤",
                Default = FamilyCreationDefaults.DefaultHostCategories
            };

            requirements.Parameters["hostElementId"] = new ParameterInfo
            {
                Required = false,
                Description = "指定宿主元素的ElementId",
                Default = -1
            };
        }

        private void AddTwoLevelsBasedParameters(FamilyCreationRequirements requirements)
        {
            AddOneLevelBasedParameters(requirements);

            requirements.Parameters["topLevelId"] = new ParameterInfo
            {
                Required = true,
                Description = "顶部标高的ElementId",
            };

            requirements.Parameters["topOffset"] = new ParameterInfo
            {
                Required = false,
                Description = "相对顶部标高的偏移(mm)",
                Default = FamilyCreationDefaults.TopOffset
            };
        }

        private void AddWorkPlaneBasedParameters(FamilyCreationRequirements requirements)
        {
            requirements.Parameters["locationPoint"] = new ParameterInfo
            {
                Required = true,
                Description = "放置点坐标（毫米）",
            };

            requirements.Parameters["faceDirection"] = new ParameterInfo
            {
                Required = true,
                Description = "面法向量（标准化）",
            };

            requirements.Parameters["handDirection"] = new ParameterInfo
            {
                Required = true,
                Description = "手向量（标准化）",
            };

            requirements.Parameters["autoFindHost"] = new ParameterInfo
            {
                Required = false,
                Description = "是否自动查找宿主面",
            };

            requirements.Parameters["searchRadius"] = new ParameterInfo
            {
                Required = false,
                Description = "自动查找宿主的搜索半径（毫米）",
            };

            requirements.Parameters["hostCategories"] = new ParameterInfo
            {
                Required = false,
                Description = "宿主类别过滤",
            };
        }

        private void AddCurveBasedParameters(FamilyCreationRequirements requirements)
        {
            requirements.Parameters["locationLine"] = new ParameterInfo
            {
                Required = true,
                Description = "线性路径（毫米）",
            };

            requirements.Parameters["baseLevelId"] = new ParameterInfo
            {
                Required = true,
                Description = "基准标高的ElementId",
            };

            requirements.Parameters["baseOffset"] = new ParameterInfo
            {
                Required = false,
                Description = "相对基准标高的偏移（毫米）",
            };
        }

        private void AddViewBasedParameters(FamilyCreationRequirements requirements)
        {
            requirements.Parameters["locationPoint"] = new ParameterInfo
            {
                Required = true,
                Description = "放置点坐标（毫米）",
            };

            requirements.Parameters["viewId"] = new ParameterInfo
            {
                Required = true,
                Description = "目标2D视图的ElementId",
            };
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取最近的标高
        /// </summary>
        private Level GetNearestLevel(double elevation)
        {
            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => Math.Abs(l.Elevation - elevation))
                .FirstOrDefault();

            return levels;
        }

        #endregion
    }

    #region 数据结构定义

    /// <summary>
    /// 验证结果
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public FamilyCreationParameters AdjustedParameters { get; set; }
    }

    #endregion
}