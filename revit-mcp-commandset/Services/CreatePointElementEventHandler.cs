using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;

namespace RevitMCPCommandSet.Services
{
    public class CreatePointElementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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
        public List<PointElement> CreatedInfo { get; private set; }
        /// <summary>
        /// 执行结果（传出数据）
        /// </summary>
        public AIResult<List<int>> Result { get; private set; }

        /// <summary>
        /// 设置创建的参数
        /// </summary>
        public void SetParameters(List<PointElement> data)
        {
            CreatedInfo = data;
            _resetEvent.Reset();
        }
        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                var elementIds = new List<int>();
                foreach (var data in CreatedInfo)
                {
                    // Step0 获取构件类型
                    BuiltInCategory builtInCategory = BuiltInCategory.INVALID;
                    Enum.TryParse(data.Category.Replace(".", ""), true, out builtInCategory);

                    // Step1 获取标高和偏移
                    Level baseLevel = null;
                    Level topLevel = null;
                    double topOffset = -1;  // ft
                    double baseOffset = -1; // ft
                    baseLevel = doc.FindNearestLevel(data.BaseLevel / 304.8);
                    baseOffset = (data.BaseOffset + data.BaseLevel) / 304.8 - baseLevel.Elevation;
                    topLevel = doc.FindNearestLevel((data.BaseLevel + data.BaseOffset + data.Height) / 304.8);
                    topOffset = (data.BaseLevel + data.BaseOffset + data.Height) / 304.8 - topLevel.Elevation;
                    if (baseLevel == null)
                        continue;

                    // Step2 获取族类型
                    FamilySymbol symbol = null;
                    if (data.TypeId != -1 && data.TypeId != 0)
                    {
                        ElementId typeELeId = new ElementId(data.TypeId);
                        if (typeELeId != null)
                        {
                            Element typeEle = doc.GetElement(typeELeId);
                            if (typeEle != null && typeEle is FamilySymbol)
                            {
                                symbol = typeEle as FamilySymbol;
                                // 获取symbol的Category对象并转换为BuiltInCategory枚举
                                builtInCategory = (BuiltInCategory)ElementIdUtils.GetIdValue(symbol.Category.Id);
                            }
                        }
                    }
                    if (builtInCategory == BuiltInCategory.INVALID)
                        continue;
                    if (symbol == null)
                    {
                        symbol = new FilteredElementCollector(doc)
                            .OfClass(typeof(FamilySymbol))
                            .OfCategory(builtInCategory)
                            .Cast<FamilySymbol>()
                            .FirstOrDefault(fs => fs.IsActive); // 获取激活的类型作为默认类型
                        if (symbol == null)
                        {
                            symbol = new FilteredElementCollector(doc)
                            .OfClass(typeof(FamilySymbol))
                            .OfCategory(builtInCategory)
                            .Cast<FamilySymbol>()
                            .FirstOrDefault();
                        }
                    }
                    if (symbol == null)
                        continue;

                    // Step3 调用通用方法创建族实例
                    using (Transaction transaction = new Transaction(doc, "创建点状构件"))
                    {
                        transaction.Start();

                        if (!symbol.IsActive)
                            symbol.Activate();

                        // 调用FamilyInstance通用创建方法
                        var instance = doc.CreateInstance(symbol, JZPoint.ToXYZ(data.LocationPoint), null, baseLevel, topLevel, baseOffset, topOffset);
                        if (instance != null)
                        {
                            if (builtInCategory == BuiltInCategory.OST_Doors)
                            {
                                // 翻转门确保正常剪切
                                instance.flipFacing();
                                doc.Regenerate();
                                instance.flipFacing();
                                doc.Regenerate();
                            }

                            elementIds.Add(ElementIdUtils.GetIdValue(instance.Id));
                        }
                        //doc.Refresh();
                        transaction.Commit();
                    }
                }
                Result = new AIResult<List<int>>
                {
                    Success = true,
                    Message = $"成功创建{elementIds.Count}个族实例，其ElementId储存在Response属性中",
                    Response = elementIds,
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<List<int>>
                {
                    Success = false,
                    Message = $"创建点状构件时出错: {ex.Message}",
                };
                TaskDialog.Show("错误", $"创建点状构件时出错: {ex.Message}");
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
            return "创建点状构件";
        }

    }
}


