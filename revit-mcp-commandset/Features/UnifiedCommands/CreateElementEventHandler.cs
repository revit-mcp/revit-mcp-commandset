using System;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Models.Geometry;
using RevitMCPCommandSet.Features.UnifiedCommands.Models;
using RevitMCPCommandSet.Features.SystemElementCreation.Models;
using RevitMCPCommandSet.Utils.FamilyCreation;
using RevitMCPCommandSet.Utils.SystemCreation;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Features.UnifiedCommands
{
    /// <summary>
    /// 统一元素创建事件处理器
    /// </summary>
    public class CreateElementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private ElementCreationParameters _parameters;
        private AIResult<object> _result;
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public string GetName() => "CreateElementEventHandler";

        /// <summary>
        /// 设置创建参数
        /// </summary>
        /// <param name="parameters">统一创建参数</param>
        public void SetParameters(ElementCreationParameters parameters)
        {
            _parameters = parameters;
            _result = null;  // 重置避免陈旧数据
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                var doc = uiapp.ActiveUIDocument.Document;

                // 1. 统一参数验证
                var validationError = ElementValidationService.Validate(_parameters);
                if (!string.IsNullOrEmpty(validationError))
                {
                    _result = new AIResult<object>
                    {
                        Success = false,
                        Message = validationError
                    };
                    return;
                }

                // 2. 开始事务
                using (Transaction trans = new Transaction(doc, "Create Element"))
                {
                    trans.Start();

                    try
                    {
                        Element createdElement = null;

                        // 3. 判断元素类别
                        var elementClass = DetermineElementClass(doc, _parameters);

                        // 4. 根据类别调用对应Creator
                        if (elementClass == "System")
                        {
                            // 直接使用SystemElementCreator
                            var creator = new SystemElementCreator(doc);
                            var systemParams = ConvertToSystemParameters(_parameters);
                            createdElement = creator.Create(systemParams);
                        }
                        else if (elementClass == "Family")
                        {
                            // 直接使用FamilyInstanceCreator
                            var creator = new FamilyInstanceCreator(doc);
                            ConfigureFamilyCreator(creator, _parameters);
                            var elementId = creator.Create();
                            createdElement = doc.GetElement(new ElementId(elementId));
                        }
                        else
                        {
                            throw new InvalidOperationException("无法确定元素类型");
                        }

                        trans.Commit();

                        _result = new AIResult<object>
                        {
                            Success = true,
                            Message = "元素创建成功",
                            Response = new { ElementId = createdElement.Id.IntegerValue }
                        };
                    }
                    catch (Exception ex)
                    {
                        trans.RollBack();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                _result = new AIResult<object>
                {
                    Success = false,
                    Message = $"创建失败: {ex.Message}"
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        /// <summary>
        /// 确定元素类别（含自动检测）
        /// </summary>
        private string DetermineElementClass(Document doc, ElementCreationParameters param)
        {
            // 1. 显式指定优先
            if (!string.IsNullOrEmpty(param.ElementClass))
                return param.ElementClass;

            // 2. 根据SystemOptions判断
            if (param.SystemOptions?.ElementType != null)
                return "System";

            // 3. 根据FamilyOptions判断
            if (param.FamilyOptions != null &&
                (param.FamilyOptions.LocationPoint != null ||
                 param.FamilyOptions.LocationLine != null))
                return "Family";

            // 4. 自动检测TypeId
            if (param.TypeId > 0)
            {
                var element = doc.GetElement(new ElementId(param.TypeId));
                if (element is FamilySymbol)
                    return "Family";
                if (element is WallType || element is FloorType)
                    return "System";
            }

            return null;
        }

        /// <summary>
        /// 转换为SystemElementParameters
        /// </summary>
        private SystemElementParameters ConvertToSystemParameters(ElementCreationParameters param)
        {
            return new SystemElementParameters
            {
                ElementType = param.SystemOptions?.ElementType,
                TypeId = param.TypeId,
                LevelId = param.LevelId,
                AutoFindLevel = param.AutoFindLevel,
                IsStructural = param.SystemOptions?.IsStructural ?? false,
                WallParameters = param.SystemOptions?.WallParameters,
                FloorParameters = param.SystemOptions?.FloorParameters
            };
        }

        /// <summary>
        /// 配置FamilyInstanceCreator
        /// </summary>
        private void ConfigureFamilyCreator(FamilyInstanceCreator creator, ElementCreationParameters param)
        {
            var opt = param.FamilyOptions;
            if (opt == null) return;

            var symbol = creator.doc.GetElement(new ElementId(param.TypeId)) as FamilySymbol;
            creator.FamilySymbol = symbol;

            if (opt.LocationPoint != null)
                creator.LocationPoint = JZPoint.ToXYZ(opt.LocationPoint);

            if (opt.LocationLine != null)
            {
                var start = JZPoint.ToXYZ(opt.LocationLine.P0);
                var end = JZPoint.ToXYZ(opt.LocationLine.P1);
                creator.LocationLine = Line.CreateBound(start, end);
            }

            creator.BaseOffset = opt.BaseOffset / 304.8;  // mm to ft
            creator.TopOffset = opt.TopOffset / 304.8;

            // 配置其他属性
            if (opt.TopLevelId > 0)
            {
                var topLevel = creator.doc.GetElement(new ElementId(opt.TopLevelId)) as Level;
                creator.TopLevel = topLevel;
            }

            if (opt.ViewId > 0)
            {
                var view = creator.doc.GetElement(new ElementId(opt.ViewId)) as View;
                creator.View = view;
            }

            if (opt.HostCategories != null && opt.HostCategories.Length > 0)
            {
                // 转换字符串数组为BuiltInCategory数组
                var categories = new BuiltInCategory[opt.HostCategories.Length];
                for (int i = 0; i < opt.HostCategories.Length; i++)
                {
                    if (Enum.TryParse<BuiltInCategory>(opt.HostCategories[i], out var category))
                    {
                        categories[i] = category;
                    }
                }
                creator.HostCategories = categories;
            }

            if (opt.FaceDirection != null)
                creator.FaceDirection = JZPoint.ToXYZ(opt.FaceDirection);

            if (opt.HandDirection != null)
                creator.HandDirection = JZPoint.ToXYZ(opt.HandDirection);

            // 自动查找标高
            if (param.AutoFindLevel && param.LevelId.HasValue && param.LevelId > 0)
            {
                var level = creator.doc.GetElement(new ElementId(param.LevelId.Value)) as Level;
                creator.BaseLevel = level;
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
    }
}