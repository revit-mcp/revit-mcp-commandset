using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Models.Geometry;
using RevitMCPCommandSet.Utils.FamilyCreation;

namespace RevitMCPCommandSet.Features.FamilyInstanceCreation
{
    /// <summary>
    /// 创建族实例事件处理器
    /// </summary>
    public class CreateFamilyInstanceEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;

        /// <summary>
        /// 事件等待对象
        /// </summary>
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        /// <summary>
        /// 创建参数（传入数据）
        /// </summary>
        private FamilyCreationParameters parameters;

        /// <summary>
        /// 执行结果（传出数据）
        /// </summary>
        public AIResult<object> Result { get; private set; }

        /// <summary>
        /// 设置创建参数
        /// </summary>
        public void SetParameters(FamilyCreationParameters param)
        {
            parameters = param;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                // 使用事务包装创建过程
                using (Transaction trans = new Transaction(doc, "Create Family Instance"))
                {
                    trans.Start();

                    try
                    {
                        // 1. 获取族类型
                        var symbol = doc.GetElement(new ElementId(parameters.TypeId)) as FamilySymbol;
                        if (symbol == null)
                        {
                            trans.RollBack();
                            Result = new AIResult<object>
                            {
                                Success = false,
                                Message = $"找不到族类型 ID: {parameters.TypeId}",
                                Response = null
                            };
                            return;
                        }

                        // 2. 验证参数
                        var validation = ValidateParameters(symbol, parameters);
                        if (!validation.IsValid)
                        {
                            trans.RollBack();
                            var suggestion = AnalyzeRequirements(symbol);
                            Result = new AIResult<object>
                            {
                                Success = false,
                                Message = validation.ErrorMessage,
                                Response = suggestion
                            };
                            return;
                        }

                        // 3. 使用修复后的参数创建
                        var workingParams = validation.AdjustedParameters ?? parameters;

                        // 4. 创建族实例
                        var creator = new FamilyInstanceCreator(doc);
                        SetupCreatorByPlacementType(creator, symbol, workingParams);

                        int elementId = creator.Create();

                        trans.Commit();
                        Result = new AIResult<object>
                        {
                            Success = true,
                            Message = "族实例创建成功",
                            Response = elementId
                        };
                    }
                    catch (Exception ex)
                    {
                        trans.RollBack();
                        var suggestion = AnalyzeRequirements(doc.GetElement(new ElementId(parameters.TypeId)) as FamilySymbol);
                        Result = new AIResult<object>
                        {
                            Success = false,
                            Message = $"创建失败: {ex.Message}",
                            Response = suggestion
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = $"事务异常: {ex.Message}",
                    Response = null
                };
                System.Diagnostics.Trace.WriteLine($"[CreateFamilyInstance] 异常详情: {ex}");
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName()
        {
            return "CreateFamilyInstanceEventHandler";
        }

        #region 参数验证和分析

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

        private ValidationResult ValidateOneLevelBased(FamilyCreationParameters parameters)
        {
            var result = new ValidationResult { IsValid = true };

            if (parameters.LocationPoint == null)
            {
                result.IsValid = false;
                result.ErrorMessage = "OneLevelBased族类型需要LocationPoint参数";
                return result;
            }

            // 自动查找标高
            if (parameters.BaseLevelId == -1 && parameters.AutoFindLevel)
            {
                var level = FamilyInstanceCreator.GetNearestLevel(doc, parameters.LocationPoint.Z / 304.8);
                if (level != null)
                {
                    var adjustedParams = CloneParameters(parameters);
                    adjustedParams.BaseLevelId = level.Id.IntegerValue;
                    adjustedParams.BaseOffset = parameters.LocationPoint.Z - level.Elevation * 304.8;
                    result.AdjustedParameters = adjustedParams;
                }
            }

            return result;
        }

        private ValidationResult ValidateOneLevelBasedHosted(FamilyCreationParameters parameters)
        {
            var result = new ValidationResult { IsValid = true };

            if (parameters.LocationPoint == null)
            {
                result.IsValid = false;
                result.ErrorMessage = "OneLevelBasedHosted族类型需要LocationPoint参数";
            }

            return result;
        }

        private ValidationResult ValidateTwoLevelsBased(FamilyCreationParameters parameters)
        {
            var result = new ValidationResult { IsValid = true };

            if (parameters.LocationPoint == null)
            {
                result.IsValid = false;
                result.ErrorMessage = "TwoLevelsBased族类型需要LocationPoint参数";
                return result;
            }

            if (parameters.BaseLevelId == -1)
            {
                result.IsValid = false;
                result.ErrorMessage = "TwoLevelsBased族类型需要BaseLevelId参数";
                return result;
            }

            if (parameters.TopLevelId == -1)
            {
                result.IsValid = false;
                result.ErrorMessage = "TwoLevelsBased族类型需要TopLevelId参数";
            }

            return result;
        }

        private ValidationResult ValidateWorkPlaneBased(FamilyCreationParameters parameters)
        {
            var result = new ValidationResult { IsValid = true };

            if (parameters.LocationPoint == null)
            {
                result.IsValid = false;
                result.ErrorMessage = "WorkPlaneBased族类型需要LocationPoint参数";
            }

            return result;
        }

        private ValidationResult ValidateCurveBased(FamilyCreationParameters parameters)
        {
            var result = new ValidationResult { IsValid = true };

            if (parameters.LocationLine == null)
            {
                result.IsValid = false;
                result.ErrorMessage = "CurveBased族类型需要LocationLine参数";
            }

            return result;
        }

        private ValidationResult ValidateViewBased(FamilyCreationParameters parameters)
        {
            var result = new ValidationResult { IsValid = true };

            if (parameters.LocationPoint == null)
            {
                result.IsValid = false;
                result.ErrorMessage = "ViewBased族类型需要LocationPoint参数";
                return result;
            }

            if (parameters.ViewId == -1)
            {
                result.IsValid = false;
                result.ErrorMessage = "ViewBased族类型需要ViewId参数";
            }

            return result;
        }

        private FamilyCreationParameters CloneParameters(FamilyCreationParameters original)
        {
            return new FamilyCreationParameters
            {
                TypeId = original.TypeId,
                LocationPoint = original.LocationPoint,
                LocationLine = original.LocationLine,
                BaseLevelId = original.BaseLevelId,
                TopLevelId = original.TopLevelId,
                BaseOffset = original.BaseOffset,
                TopOffset = original.TopOffset,
                ViewId = original.ViewId,
                HostElementId = original.HostElementId,
                HostCategories = original.HostCategories,
                FaceDirection = original.FaceDirection,
                HandDirection = original.HandDirection,
                AutoFindHost = original.AutoFindHost,
                AutoFindLevel = original.AutoFindLevel,
                SearchRadius = original.SearchRadius
            };
        }

        /// <summary>
        /// 分析族类型的参数要求
        /// </summary>
        private FamilyCreationRequirements AnalyzeRequirements(FamilySymbol symbol)
        {
            if (symbol == null)
                return new FamilyCreationRequirements { Message = "族类型无效" };

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

        private void AddOneLevelBasedParameters(FamilyCreationRequirements requirements)
        {
            requirements.Parameters["typeId"] = new ParameterInfo { Required = true, Description = "族类型ID" };
            requirements.Parameters["locationPoint"] = new ParameterInfo { Required = true, Description = "位置点坐标(毫米)" };
            requirements.Parameters["baseLevelId"] = new ParameterInfo { Required = false, Description = "底部标高ID(可选，自动查找)" };
            requirements.Parameters["baseOffset"] = new ParameterInfo { Required = false, Description = "底部偏移(毫米)" };
        }

        private void AddOneLevelBasedHostedParameters(FamilyCreationRequirements requirements)
        {
            requirements.Parameters["typeId"] = new ParameterInfo { Required = true, Description = "族类型ID" };
            requirements.Parameters["locationPoint"] = new ParameterInfo { Required = true, Description = "位置点坐标(毫米)" };
            requirements.Parameters["baseLevelId"] = new ParameterInfo { Required = false, Description = "底部标高ID(可选)" };
            requirements.Parameters["hostElementId"] = new ParameterInfo { Required = false, Description = "宿主元素ID(可选，自动查找)" };
            requirements.Parameters["autoFindHost"] = new ParameterInfo { Required = false, Description = "是否自动查找宿主(默认true)" };
            requirements.Parameters["searchRadius"] = new ParameterInfo { Required = false, Description = "搜索半径(毫米，默认1000)" };
        }

        private void AddTwoLevelsBasedParameters(FamilyCreationRequirements requirements)
        {
            requirements.Parameters["typeId"] = new ParameterInfo { Required = true, Description = "族类型ID" };
            requirements.Parameters["locationPoint"] = new ParameterInfo { Required = true, Description = "位置点坐标(毫米)" };
            requirements.Parameters["baseLevelId"] = new ParameterInfo { Required = true, Description = "底部标高ID" };
            requirements.Parameters["topLevelId"] = new ParameterInfo { Required = true, Description = "顶部标高ID" };
            requirements.Parameters["baseOffset"] = new ParameterInfo { Required = false, Description = "底部偏移(毫米)" };
            requirements.Parameters["topOffset"] = new ParameterInfo { Required = false, Description = "顶部偏移(毫米)" };
        }

        private void AddWorkPlaneBasedParameters(FamilyCreationRequirements requirements)
        {
            requirements.Parameters["typeId"] = new ParameterInfo { Required = true, Description = "族类型ID" };
            requirements.Parameters["locationPoint"] = new ParameterInfo { Required = true, Description = "位置点坐标(毫米)" };
            requirements.Parameters["faceDirection"] = new ParameterInfo { Required = false, Description = "面方向向量(可选，自动生成)" };
            requirements.Parameters["handDirection"] = new ParameterInfo { Required = false, Description = "手方向向量(可选，自动生成)" };
            requirements.Parameters["autoFindHost"] = new ParameterInfo { Required = false, Description = "是否自动查找宿主(默认true)" };
            requirements.Parameters["hostCategories"] = new ParameterInfo { Required = false, Description = "宿主类别数组(可选)" };
            requirements.Parameters["searchRadius"] = new ParameterInfo { Required = false, Description = "搜索半径(毫米，默认1000)" };
        }

        private void AddCurveBasedParameters(FamilyCreationRequirements requirements)
        {
            requirements.Parameters["typeId"] = new ParameterInfo { Required = true, Description = "族类型ID" };
            requirements.Parameters["locationLine"] = new ParameterInfo { Required = true, Description = "基准线坐标(毫米)" };
            requirements.Parameters["baseLevelId"] = new ParameterInfo { Required = false, Description = "底部标高ID(可选)" };
            requirements.Parameters["viewId"] = new ParameterInfo { Required = false, Description = "视图ID(CurveBasedDetail必需)" };
            requirements.Parameters["hostCategories"] = new ParameterInfo { Required = false, Description = "宿主类别数组(无标高时需要)" };
        }

        private void AddViewBasedParameters(FamilyCreationRequirements requirements)
        {
            requirements.Parameters["typeId"] = new ParameterInfo { Required = true, Description = "族类型ID" };
            requirements.Parameters["locationPoint"] = new ParameterInfo { Required = true, Description = "位置点坐标(毫米)" };
            requirements.Parameters["viewId"] = new ParameterInfo { Required = true, Description = "视图ID" };
        }

        #endregion

        #region 创建器配置

        /// <summary>
        /// 根据族放置类型配置创建器
        /// </summary>
        private void SetupCreatorByPlacementType(FamilyInstanceCreator creator, FamilySymbol symbol, FamilyCreationParameters parameters)
        {
            var placementType = symbol.Family.FamilyPlacementType;

            switch (placementType)
            {
                case FamilyPlacementType.OneLevelBased:
                    SetupOneLevelBased(creator, symbol, parameters);
                    break;
                case FamilyPlacementType.OneLevelBasedHosted:
                    SetupOneLevelBasedHosted(creator, symbol, parameters);
                    break;
                case FamilyPlacementType.TwoLevelsBased:
                    SetupTwoLevelsBased(creator, symbol, parameters);
                    break;
                case FamilyPlacementType.WorkPlaneBased:
                    SetupWorkPlaneBased(creator, symbol, parameters);
                    break;
                case FamilyPlacementType.CurveBased:
                    SetupCurveBased(creator, symbol, parameters);
                    break;
                case FamilyPlacementType.CurveBasedDetail:
                    SetupCurveBasedDetail(creator, symbol, parameters);
                    break;
                case FamilyPlacementType.CurveDrivenStructural:
                    SetupCurveDrivenStructural(creator, symbol, parameters);
                    break;
                case FamilyPlacementType.ViewBased:
                    SetupViewBased(creator, symbol, parameters);
                    break;
                default:
                    throw new NotImplementedException($"不支持的族放置类型: {placementType}");
            }
        }

        private void SetupOneLevelBased(FamilyInstanceCreator creator, FamilySymbol symbol, FamilyCreationParameters parameters)
        {
            var location = ConvertPoint(parameters.LocationPoint);
            Level level = null;

            if (parameters.BaseLevelId != -1)
            {
                level = doc.GetElement(new ElementId(parameters.BaseLevelId)) as Level;
            }

            creator.SetupOneLevelBased(symbol, location, level);

            if (parameters.BaseOffset != 0)
            {
                creator.BaseOffset = parameters.BaseOffset / 304.8; // 毫米转英尺
            }
        }

        private void SetupOneLevelBasedHosted(FamilyInstanceCreator creator, FamilySymbol symbol, FamilyCreationParameters parameters)
        {
            var location = ConvertPoint(parameters.LocationPoint);
            Level level = null;

            if (parameters.BaseLevelId != -1)
            {
                level = doc.GetElement(new ElementId(parameters.BaseLevelId)) as Level;
            }

            creator.SetupOneLevelBasedHosted(symbol, location, level);
        }

        private void SetupTwoLevelsBased(FamilyInstanceCreator creator, FamilySymbol symbol, FamilyCreationParameters parameters)
        {
            var location = ConvertPoint(parameters.LocationPoint);
            var baseLevel = doc.GetElement(new ElementId(parameters.BaseLevelId)) as Level;
            var topLevel = doc.GetElement(new ElementId(parameters.TopLevelId)) as Level;

            var baseOffset = parameters.BaseOffset != 0 ? parameters.BaseOffset / 304.8 : -1;
            var topOffset = parameters.TopOffset != 0 ? parameters.TopOffset / 304.8 : -1;

            creator.SetupTwoLevelsBased(symbol, location, baseLevel, topLevel, baseOffset, topOffset);
        }

        private void SetupWorkPlaneBased(FamilyInstanceCreator creator, FamilySymbol symbol, FamilyCreationParameters parameters)
        {
            var location = ConvertPoint(parameters.LocationPoint);
            XYZ faceDirection = null;

            if (parameters.FaceDirection != null)
            {
                faceDirection = ConvertPoint(parameters.FaceDirection);
            }

            BuiltInCategory[] hostCategories = null;
            if (parameters.HostCategories != null && parameters.HostCategories.Length > 0)
            {
                hostCategories = parameters.HostCategories.Select(name => (BuiltInCategory)Enum.Parse(typeof(BuiltInCategory), name)).ToArray();
            }

            creator.SetupWorkPlaneBased(symbol, location, faceDirection, hostCategories);
        }

        private void SetupCurveBased(FamilyInstanceCreator creator, FamilySymbol symbol, FamilyCreationParameters parameters)
        {
            var line = ConvertLine(parameters.LocationLine);
            Level level = null;

            if (parameters.BaseLevelId != -1)
            {
                level = doc.GetElement(new ElementId(parameters.BaseLevelId)) as Level;
            }

            BuiltInCategory[] hostCategories = null;
            if (parameters.HostCategories != null && parameters.HostCategories.Length > 0)
            {
                hostCategories = parameters.HostCategories.Select(name => (BuiltInCategory)Enum.Parse(typeof(BuiltInCategory), name)).ToArray();
            }

            creator.SetupCurveBased(symbol, line, level, hostCategories);
        }

        private void SetupCurveBasedDetail(FamilyInstanceCreator creator, FamilySymbol symbol, FamilyCreationParameters parameters)
        {
            var line = ConvertLine(parameters.LocationLine);
            var view = doc.GetElement(new ElementId(parameters.ViewId)) as View;

            creator.SetupCurveBasedDetail(symbol, line, view);
        }

        private void SetupCurveDrivenStructural(FamilyInstanceCreator creator, FamilySymbol symbol, FamilyCreationParameters parameters)
        {
            var line = ConvertLine(parameters.LocationLine);
            var level = doc.GetElement(new ElementId(parameters.BaseLevelId)) as Level;

            creator.SetupCurveDrivenStructural(symbol, line, level);
        }

        private void SetupViewBased(FamilyInstanceCreator creator, FamilySymbol symbol, FamilyCreationParameters parameters)
        {
            var location = ConvertPoint(parameters.LocationPoint);
            var view = doc.GetElement(new ElementId(parameters.ViewId)) as View;

            creator.SetupViewBased(symbol, location, view);
        }

        #endregion

        #region 坐标转换

        private XYZ ConvertPoint(JZPoint jzPoint)
        {
            if (jzPoint == null) return null;
            return new XYZ(jzPoint.X / 304.8, jzPoint.Y / 304.8, jzPoint.Z / 304.8);
        }

        private Line ConvertLine(JZLine jzLine)
        {
            if (jzLine == null) return null;
            var p0 = ConvertPoint(jzLine.P0);
            var p1 = ConvertPoint(jzLine.P1);
            return Line.CreateBound(p0, p1);
        }

        #endregion
    }

    /// <summary>
    /// 验证结果
    /// </summary>
    internal class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public FamilyCreationParameters AdjustedParameters { get; set; }
    }
}