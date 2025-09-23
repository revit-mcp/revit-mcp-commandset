using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Models.Common;

namespace RevitMCPCommandSet.Features.FamilyInstanceCreation
{
    /// <summary>
    /// 获取族创建建议事件处理器
    /// </summary>
    public class GetFamilyCreationSuggestionEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;

        /// <summary>
        /// 事件等待对象
        /// </summary>
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        /// <summary>
        /// 族类型ID（传入数据）
        /// </summary>
        private int typeId;

        /// <summary>
        /// 执行结果（传出数据）
        /// </summary>
        public AIResult<CreationRequirements> Result { get; private set; }

        /// <summary>
        /// 设置参数
        /// </summary>
        public void SetParameters(int id)
        {
            typeId = id;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                // 获取族类型
                var symbol = doc.GetElement(new ElementId(typeId)) as FamilySymbol;

                if (symbol == null)
                {
                    Result = new AIResult<CreationRequirements>
                    {
                        Success = false,
                        Message = $"无效的族类型ID: {typeId}，Response为null",
                        Response = null
                    };
                }
                else
                {
                    // 直接分析创建参数需求
                    var requirements = AnalyzeRequirements(symbol);

                    Result = new AIResult<CreationRequirements>
                    {
                        Success = true,
                        Response = requirements,
                        Message = "成功获取族创建参数需求，Response包含FamilyCreationRequirements对象，详细说明了该族类型所需的创建参数"
                    };
                }
            }
            catch (Exception ex)
            {
                Result = new AIResult<CreationRequirements>
                {
                    Success = false,
                    Message = $"分析异常: {ex.Message}",
                    Response = null
                };
                System.Diagnostics.Trace.WriteLine($"[GetFamilyCreationSuggestion] 异常详情: {ex}");
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
            return "GetFamilyCreationSuggestionEventHandler";
        }

        #region 参数分析

        /// <summary>
        /// 分析族类型的参数要求
        /// </summary>
        private CreationRequirements AnalyzeRequirements(FamilySymbol symbol)
        {
            var requirements = new CreationRequirements
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

        private void AddOneLevelBasedParameters(CreationRequirements requirements)
        {
            requirements.Parameters["typeId"] = new ParameterInfo { Required = true, Description = "族类型ID" };
            requirements.Parameters["locationPoint"] = new ParameterInfo { Required = true, Description = "位置点坐标(毫米)" };
            requirements.Parameters["baseLevelId"] = new ParameterInfo { Required = false, Description = "底部标高ID(可选，自动查找)" };
            requirements.Parameters["baseOffset"] = new ParameterInfo { Required = false, Description = "底部偏移(毫米)" };
            requirements.Parameters["autoFindLevel"] = new ParameterInfo { Required = false, Description = "是否自动查找标高(默认true)" };
        }

        private void AddOneLevelBasedHostedParameters(CreationRequirements requirements)
        {
            requirements.Parameters["typeId"] = new ParameterInfo { Required = true, Description = "族类型ID" };
            requirements.Parameters["locationPoint"] = new ParameterInfo { Required = true, Description = "位置点坐标(毫米)" };
            requirements.Parameters["baseLevelId"] = new ParameterInfo { Required = false, Description = "底部标高ID(可选)" };
            requirements.Parameters["hostElementId"] = new ParameterInfo { Required = false, Description = "宿主元素ID(可选，自动查找)" };
            requirements.Parameters["autoFindHost"] = new ParameterInfo { Required = false, Description = "是否自动查找宿主(默认true)" };
            requirements.Parameters["searchRadius"] = new ParameterInfo { Required = false, Description = "搜索半径(毫米，默认1000)" };
        }

        private void AddTwoLevelsBasedParameters(CreationRequirements requirements)
        {
            requirements.Parameters["typeId"] = new ParameterInfo { Required = true, Description = "族类型ID" };
            requirements.Parameters["locationPoint"] = new ParameterInfo { Required = true, Description = "位置点坐标(毫米)" };
            requirements.Parameters["baseLevelId"] = new ParameterInfo { Required = true, Description = "底部标高ID" };
            requirements.Parameters["topLevelId"] = new ParameterInfo { Required = true, Description = "顶部标高ID" };
            requirements.Parameters["baseOffset"] = new ParameterInfo { Required = false, Description = "底部偏移(毫米)" };
            requirements.Parameters["topOffset"] = new ParameterInfo { Required = false, Description = "顶部偏移(毫米)" };
        }

        private void AddWorkPlaneBasedParameters(CreationRequirements requirements)
        {
            requirements.Parameters["typeId"] = new ParameterInfo { Required = true, Description = "族类型ID" };
            requirements.Parameters["locationPoint"] = new ParameterInfo { Required = true, Description = "位置点坐标(毫米)" };
            requirements.Parameters["faceDirection"] = new ParameterInfo { Required = false, Description = "面方向向量(可选，自动生成)" };
            requirements.Parameters["handDirection"] = new ParameterInfo { Required = false, Description = "手方向向量(可选，自动生成)" };
            requirements.Parameters["autoFindHost"] = new ParameterInfo { Required = false, Description = "是否自动查找宿主(默认true)" };
            requirements.Parameters["hostCategories"] = new ParameterInfo { Required = false, Description = "宿主类别数组(可选)" };
            requirements.Parameters["searchRadius"] = new ParameterInfo { Required = false, Description = "搜索半径(毫米，默认1000)" };
        }

        private void AddCurveBasedParameters(CreationRequirements requirements)
        {
            requirements.Parameters["typeId"] = new ParameterInfo { Required = true, Description = "族类型ID" };
            requirements.Parameters["locationLine"] = new ParameterInfo { Required = true, Description = "基准线坐标(毫米)" };
            requirements.Parameters["baseLevelId"] = new ParameterInfo { Required = false, Description = "底部标高ID(可选)" };
            requirements.Parameters["viewId"] = new ParameterInfo { Required = false, Description = "视图ID(CurveBasedDetail必需)" };
            requirements.Parameters["hostCategories"] = new ParameterInfo { Required = false, Description = "宿主类别数组(无标高时需要)" };
        }

        private void AddViewBasedParameters(CreationRequirements requirements)
        {
            requirements.Parameters["typeId"] = new ParameterInfo { Required = true, Description = "族类型ID" };
            requirements.Parameters["locationPoint"] = new ParameterInfo { Required = true, Description = "位置点坐标(毫米)" };
            requirements.Parameters["viewId"] = new ParameterInfo { Required = true, Description = "视图ID" };
        }

        #endregion
    }
}