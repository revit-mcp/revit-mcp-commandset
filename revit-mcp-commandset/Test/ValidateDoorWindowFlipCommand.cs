using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace RevitMCPCommandSet.Test
{
    /// <summary>
    /// 门窗翻转技术验证命令
    /// 功能：提示用户选择门或窗，然后进行左右(Hand)或前后(Facing)翻转
    /// 支持通过属性控制翻转类型：Hand / Facing / Both
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ValidateDoorWindowFlipCommand : IExternalCommand
    {
        /// <summary>
        /// 翻转类型枚举
        /// </summary>
        public enum FlipType
        {
            Hand,     // 左右翻转（门把手位置）
            Facing,   // 前后翻转（门的朝向）
            Both      // 两种都翻转
        }

        /// <summary>
        /// 控制翻转类型的属性（可通过外部设置）
        /// </summary>
        public FlipType FlipDirection { get; set; } = FlipType.Hand;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // 1. 提示用户选择门或窗元素
                Reference reference = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new DoorWindowSelectionFilter(),
                    "请选择要翻转的门或窗元素");

                if (reference == null)
                {
                    return Result.Cancelled;
                }

                ElementId elementId = reference.ElementId;
                Element selectedElement = doc.GetElement(elementId);

                // 2. 验证元素类型
                if (!IsDoorOrWindow(selectedElement))
                {
                    TaskDialog.Show("错误", "选择的元素不是门或窗，无法进行翻转操作。");
                    return Result.Failed;
                }

                FamilyInstance familyInstance = selectedElement as FamilyInstance;
                if (familyInstance == null)
                {
                    TaskDialog.Show("错误", "选择的元素不是族实例。");
                    return Result.Failed;
                }

                // 3. 检查元素是否被锁定
                if (selectedElement.Pinned)
                {
                    TaskDialog.Show("错误", "选中的元素已被锁定，无法翻转。");
                    return Result.Failed;
                }

                // 4. 获取翻转前的状态
                FlipStatus beforeFlip = GetFlipStatus(familyInstance);

                // 5. 执行翻转操作
                FlipResult result = PerformFlip(doc, familyInstance, FlipDirection);

                // 6. 获取翻转后的状态
                FlipStatus afterFlip = GetFlipStatus(familyInstance);

                // 7. 显示结果
                ShowFlipResult(selectedElement, beforeFlip, afterFlip, result);

                return Result.Succeeded;
            }
            catch (OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = $"翻转操作失败: {ex.Message}";
                return Result.Failed;
            }
        }

        /// <summary>
        /// 判断元素是否为门或窗
        /// </summary>
        private bool IsDoorOrWindow(Element element)
        {
            if (element?.Category == null) return false;

            BuiltInCategory category = (BuiltInCategory)element.Category.Id.IntegerValue;

            return category == BuiltInCategory.OST_Doors ||
                   category == BuiltInCategory.OST_Windows;
        }

        /// <summary>
        /// 执行翻转操作
        /// </summary>
        private FlipResult PerformFlip(Document doc, FamilyInstance familyInstance, FlipType flipType)
        {
            FlipResult result = new FlipResult();

            using (Transaction trans = new Transaction(doc, "翻转门窗"))
            {
                trans.Start();

                try
                {
                    switch (flipType)
                    {
                        case FlipType.Hand:
                            result.HandFlipAttempted = true;
                            if (familyInstance.CanFlipHand)
                            {
                                result.HandFlipSucceeded = familyInstance.flipHand();
                            }
                            else
                            {
                                result.HandFlipMessage = "此元素不支持Hand翻转";
                            }
                            break;

                        case FlipType.Facing:
                            result.FacingFlipAttempted = true;
                            if (familyInstance.CanFlipFacing)
                            {
                                result.FacingFlipSucceeded = familyInstance.flipFacing();
                            }
                            else
                            {
                                result.FacingFlipMessage = "此元素不支持Facing翻转";
                            }
                            break;

                        case FlipType.Both:
                            // Hand翻转
                            result.HandFlipAttempted = true;
                            if (familyInstance.CanFlipHand)
                            {
                                result.HandFlipSucceeded = familyInstance.flipHand();
                            }
                            else
                            {
                                result.HandFlipMessage = "此元素不支持Hand翻转";
                            }

                            // Facing翻转
                            result.FacingFlipAttempted = true;
                            if (familyInstance.CanFlipFacing)
                            {
                                result.FacingFlipSucceeded = familyInstance.flipFacing();
                            }
                            else
                            {
                                result.FacingFlipMessage = "此元素不支持Facing翻转";
                            }
                            break;
                    }

                    trans.Commit();
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    result.ErrorMessage = ex.Message;
                }
            }

            return result;
        }

        /// <summary>
        /// 获取元素的翻转状态
        /// </summary>
        private FlipStatus GetFlipStatus(FamilyInstance familyInstance)
        {
            return new FlipStatus
            {
                CanFlipHand = familyInstance.CanFlipHand,
                CanFlipFacing = familyInstance.CanFlipFacing,
                HandFlipped = familyInstance.HandFlipped,
                FacingFlipped = familyInstance.FacingFlipped,
                HandOrientation = familyInstance.HandOrientation,
                FacingOrientation = familyInstance.FacingOrientation
            };
        }

        /// <summary>
        /// 显示翻转结果
        /// </summary>
        private void ShowFlipResult(Element element, FlipStatus before, FlipStatus after, FlipResult result)
        {
            string elementInfo = $"元素: {element.Name} (ID: {element.Id.IntegerValue})";
            string categoryInfo = $"类别: {element.Category.Name}";

            string beforeInfo = $"翻转前状态:\n" +
                               $"  Hand翻转: {(before.CanFlipHand ? (before.HandFlipped ? "已翻转" : "未翻转") : "不支持")}\n" +
                               $"  Facing翻转: {(before.CanFlipFacing ? (before.FacingFlipped ? "已翻转" : "未翻转") : "不支持")}";

            string afterInfo = $"翻转后状态:\n" +
                              $"  Hand翻转: {(after.CanFlipHand ? (after.HandFlipped ? "已翻转" : "未翻转") : "不支持")}\n" +
                              $"  Facing翻转: {(after.CanFlipFacing ? (after.FacingFlipped ? "已翻转" : "未翻转") : "不支持")}";

            string operationInfo = "执行的操作:\n";
            if (result.HandFlipAttempted)
            {
                operationInfo += $"  Hand翻转: {(result.HandFlipSucceeded ? "成功" : result.HandFlipMessage)}\n";
            }
            if (result.FacingFlipAttempted)
            {
                operationInfo += $"  Facing翻转: {(result.FacingFlipSucceeded ? "成功" : result.FacingFlipMessage)}\n";
            }

            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                operationInfo += $"  错误: {result.ErrorMessage}";
            }

            string fullMessage = $"{elementInfo}\n{categoryInfo}\n\n{beforeInfo}\n\n{afterInfo}\n\n{operationInfo}";

            TaskDialog dialog = new TaskDialog("门窗翻转结果");
            dialog.MainInstruction = "翻转操作完成";
            dialog.MainContent = fullMessage;
            dialog.CommonButtons = TaskDialogCommonButtons.Ok;
            dialog.Show();
        }

        /// <summary>
        /// 翻转状态信息
        /// </summary>
        public class FlipStatus
        {
            public bool CanFlipHand { get; set; }
            public bool CanFlipFacing { get; set; }
            public bool HandFlipped { get; set; }
            public bool FacingFlipped { get; set; }
            public XYZ HandOrientation { get; set; }
            public XYZ FacingOrientation { get; set; }
        }

        /// <summary>
        /// 翻转操作结果
        /// </summary>
        public class FlipResult
        {
            public bool HandFlipAttempted { get; set; }
            public bool HandFlipSucceeded { get; set; }
            public string HandFlipMessage { get; set; } = "";

            public bool FacingFlipAttempted { get; set; }
            public bool FacingFlipSucceeded { get; set; }
            public string FacingFlipMessage { get; set; } = "";

            public string ErrorMessage { get; set; } = "";
        }

        /// <summary>
        /// 门窗选择过滤器
        /// </summary>
        public class DoorWindowSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                if (elem?.Category == null) return false;

                BuiltInCategory category = (BuiltInCategory)elem.Category.Id.IntegerValue;

                // 允许门和窗
                return category == BuiltInCategory.OST_Doors ||
                       category == BuiltInCategory.OST_Windows;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}