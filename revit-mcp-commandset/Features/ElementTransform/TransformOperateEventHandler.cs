using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Models.Geometry;
using RevitMCPCommandSet.Features.ElementTransform.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Features.ElementTransform
{
    public class TransformOperateEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc.Document;

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public TransformOperationSetting OperationData { get; private set; }
        public AIResult<ElementOperationResponse> Result { get; private set; }

        public void SetParameters(TransformOperationSetting data)
        {
            OperationData = data;
            _resetEvent.Reset();
        }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                OperationData.Validate();
                var result = ExecuteTransformOperation(uiDoc, OperationData);
                Result = result;
            }
            catch (Exception ex)
            {
                Result = new AIResult<ElementOperationResponse>
                {
                    Success = false,
                    Message = $"变换操作失败: {ex.Message}",
                    Response = new ElementOperationResponse
                    {
                        ProcessedCount = OperationData?.ElementIds?.Count ?? 0,
                        SuccessfulElements = new List<int>(),
                        FailedElements = OperationData?.ElementIds?.Select(id => new FailureInfo
                        {
                            ElementId = id,
                            Reason = ex.Message
                        }).ToList() ?? new List<FailureInfo>()
                    }
                };
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
            return "Transform操作元素";
        }

        private AIResult<ElementOperationResponse> ExecuteTransformOperation(
            UIDocument uidoc,
            TransformOperationSetting setting)
        {
            var successfulElements = new List<int>();
            var failedElements = new List<FailureInfo>();
            var details = new Dictionary<string, object>();

            ICollection<ElementId> elementIds = setting.ElementIds.Select(id => new ElementId(id)).ToList();

            try
            {
                using (Transaction trans = new Transaction(doc, $"{setting.TransformAction}操作"))
                {
                    trans.Start();

                    switch (setting.TransformAction)
                    {
                        case "Rotate":
                            ExecuteRotate(elementIds, setting, successfulElements, failedElements, details);
                            break;

                        case "Mirror":
                            ExecuteMirror(elementIds, setting, successfulElements, failedElements, details);
                            break;

                        case "Flip":
                            ExecuteFlip(elementIds, setting, successfulElements, failedElements, details);
                            break;

                        case "Move":
                            ExecuteMove(elementIds, setting, successfulElements, failedElements, details);
                            break;

                        case "Copy":
                            ExecuteCopy(elementIds, setting, successfulElements, failedElements, details);
                            break;

                        default:
                            throw new Exception($"未支持的操作类型：{setting.TransformAction}");
                    }

                    trans.Commit();
                }

                return new AIResult<ElementOperationResponse>
                {
                    Success = failedElements.Count == 0,
                    Message = failedElements.Count == 0
                        ? $"成功对 {successfulElements.Count} 个元素执行 {setting.TransformAction} 操作"
                        : $"{setting.TransformAction} 操作部分失败：成功 {successfulElements.Count} 个，失败 {failedElements.Count} 个",
                    Response = new ElementOperationResponse
                    {
                        ProcessedCount = setting.ElementIds.Count,
                        SuccessfulElements = successfulElements,
                        FailedElements = failedElements,
                        Details = details
                    }
                };
            }
            catch (Exception ex)
            {
                return new AIResult<ElementOperationResponse>
                {
                    Success = false,
                    Message = $"变换操作失败: {ex.Message}",
                    Response = new ElementOperationResponse
                    {
                        ProcessedCount = setting.ElementIds.Count,
                        SuccessfulElements = successfulElements,
                        FailedElements = setting.ElementIds.Select(id => new FailureInfo
                        {
                            ElementId = id,
                            Reason = ex.Message
                        }).ToList(),
                        Details = details
                    }
                };
            }
        }

        /// <summary>
        /// 获取元素的位置坐标（复用自验证命令）
        /// </summary>
        private XYZ GetElementLocation(Element element)
        {
            Location location = element.Location;

            if (location is LocationPoint)
            {
                LocationPoint locPoint = location as LocationPoint;
                return locPoint.Point;
            }
            else if (location is LocationCurve)
            {
                LocationCurve locCurve = location as LocationCurve;
                Curve curve = locCurve.Curve;
                // 使用线的中点作为中心
                return curve.Evaluate(0.5, true);
            }
            else
            {
                // 尝试使用包围盒中心
                BoundingBoxXYZ bbox = element.get_BoundingBox(null);
                if (bbox != null)
                {
                    return (bbox.Min + bbox.Max) * 0.5;
                }
            }

            return XYZ.Zero;
        }

        /// <summary>
        /// 执行旋转操作
        /// </summary>
        private void ExecuteRotate(
            ICollection<ElementId> elementIds,
            TransformOperationSetting setting,
            List<int> successfulElements,
            List<FailureInfo> failedElements,
            Dictionary<string, object> details)
        {
            // 角度转弧度
            double angleRad = setting.RotateAngle * Math.PI / 180.0;

            foreach (var elemId in elementIds)
            {
                try
                {
                    Element elem = doc.GetElement(elemId);

                    // 检查元素是否被锁定
                    if (elem.Pinned)
                    {
                        failedElements.Add(new FailureInfo
                        {
                            ElementId = elemId.IntegerValue,
                            Reason = "元素已被锁定"
                        });
                        continue;
                    }

                    // 构建旋转轴（复用ValidateRotateCommand逻辑）
                    Line axis;
                    if (setting.RotateAxis != null)
                    {
                        // 使用提供的旋转轴
                        axis = JZLine.ToLine(setting.RotateAxis);
                    }
                    else
                    {
                        // 智能获取元素位置作为旋转轴起点，Z轴向上
                        XYZ rotationOrigin = GetElementLocation(elem);
                        axis = Line.CreateBound(
                            rotationOrigin,
                            new XYZ(rotationOrigin.X, rotationOrigin.Y, rotationOrigin.Z + 10));
                    }

                    ElementTransformUtils.RotateElement(doc, elemId, axis, angleRad);
                    successfulElements.Add(elemId.IntegerValue);
                }
                catch (Exception ex)
                {
                    failedElements.Add(new FailureInfo
                    {
                        ElementId = elemId.IntegerValue,
                        Reason = $"旋转失败: {ex.Message}"
                    });
                }
            }

            details["rotateAngle"] = setting.RotateAngle;
            if (setting.RotateAxis != null)
            {
                details["rotateAxis"] = new {
                    p0 = setting.RotateAxis.P0,
                    p1 = setting.RotateAxis.P1
                };
            }
            else
            {
                details["rotateAxis"] = "智能获取元素位置作为Z轴旋转";
            }
        }

        /// <summary>
        /// 执行镜像操作
        /// </summary>
        private void ExecuteMirror(
            ICollection<ElementId> elementIds,
            TransformOperationSetting setting,
            List<int> successfulElements,
            List<FailureInfo> failedElements,
            Dictionary<string, object> details)
        {
            foreach (var elemId in elementIds)
            {
                try
                {
                    Element elem = doc.GetElement(elemId);

                    // 检查元素是否被锁定
                    if (elem.Pinned)
                    {
                        failedElements.Add(new FailureInfo
                        {
                            ElementId = elemId.IntegerValue,
                            Reason = "元素已被锁定"
                        });
                        continue;
                    }

                    // 构建镜像平面（复用ValidateMirrorCommand逻辑）
                    Plane plane;
                    if (setting.MirrorPlane != null && setting.MirrorPlane.Origin != null)
                    {
                        // 使用提供的镜像平面
                        plane = setting.MirrorPlane.ToRevitPlane();
                    }
                    else
                    {
                        // 智能获取元素位置作为镜像平面原点
                        XYZ mirrorOrigin = GetElementLocation(elem);
                        XYZ normal = setting.MirrorPlane?.Normal != null ?
                            new XYZ(setting.MirrorPlane.Normal.X,
                                    setting.MirrorPlane.Normal.Y,
                                    setting.MirrorPlane.Normal.Z) :
                            XYZ.BasisX; // 默认YZ平面（X轴法向量）

                        plane = Plane.CreateByNormalAndOrigin(normal, mirrorOrigin);
                    }

                    ElementTransformUtils.MirrorElement(doc, elemId, plane);
                    successfulElements.Add(elemId.IntegerValue);
                }
                catch (Exception ex)
                {
                    failedElements.Add(new FailureInfo
                    {
                        ElementId = elemId.IntegerValue,
                        Reason = $"镜像失败: {ex.Message}"
                    });
                }
            }

            if (setting.MirrorPlane != null && setting.MirrorPlane.Origin != null)
            {
                details["mirrorPlane"] = new
                {
                    origin = setting.MirrorPlane.Origin,
                    normal = setting.MirrorPlane.Normal
                };
            }
            else
            {
                details["mirrorPlane"] = "智能获取元素位置作为镜像平面原点";
            }
        }

        /// <summary>
        /// 执行翻转操作
        /// </summary>
        private void ExecuteFlip(
            ICollection<ElementId> elementIds,
            TransformOperationSetting setting,
            List<int> successfulElements,
            List<FailureInfo> failedElements,
            Dictionary<string, object> details)
        {
            foreach (var elemId in elementIds)
            {
                try
                {
                    Element elem = doc.GetElement(elemId);
                    if (elem is FamilyInstance familyInstance)
                    {
                        if (setting.FlipDirection == "Hand" && familyInstance.CanFlipHand)
                        {
                            // 使用事务中的Flip操作，等效于flipHand()
                            if (doc.GetElement(familyInstance.Id) is FamilyInstance fi)
                            {
                                fi.flipHand();
                            }
                            successfulElements.Add(elemId.IntegerValue);
                        }
                        else if (setting.FlipDirection == "Facing" && familyInstance.CanFlipFacing)
                        {
                            // 使用事务中的Flip操作，等效于flipFacing()
                            if (doc.GetElement(familyInstance.Id) is FamilyInstance fi)
                            {
                                fi.flipFacing();
                            }
                            successfulElements.Add(elemId.IntegerValue);
                        }
                        else
                        {
                            failedElements.Add(new FailureInfo
                            {
                                ElementId = elemId.IntegerValue,
                                Reason = $"元素不支持 {setting.FlipDirection} 翻转"
                            });
                        }
                    }
                    else
                    {
                        failedElements.Add(new FailureInfo
                        {
                            ElementId = elemId.IntegerValue,
                            Reason = "仅族实例支持翻转操作"
                        });
                    }
                }
                catch (Exception ex)
                {
                    failedElements.Add(new FailureInfo
                    {
                        ElementId = elemId.IntegerValue,
                        Reason = $"翻转失败: {ex.Message}"
                    });
                }
            }

            details["flipDirection"] = setting.FlipDirection;
        }

        /// <summary>
        /// 执行移动操作
        /// </summary>
        private void ExecuteMove(
            ICollection<ElementId> elementIds,
            TransformOperationSetting setting,
            List<int> successfulElements,
            List<FailureInfo> failedElements,
            Dictionary<string, object> details)
        {
            // 构建移动向量（毫米转英尺）
            XYZ translation = new XYZ(
                setting.MoveVector.X / 304.8,
                setting.MoveVector.Y / 304.8,
                setting.MoveVector.Z / 304.8
            );

            foreach (var elemId in elementIds)
            {
                try
                {
                    Element elem = doc.GetElement(elemId);

                    // 必要判断：锁定检查
                    if (elem.Pinned)
                    {
                        failedElements.Add(new FailureInfo
                        {
                            ElementId = elemId.IntegerValue,
                            Reason = "元素已被锁定"
                        });
                        continue;
                    }

                    // 直接执行移动，让 Revit API 自行判断
                    ElementTransformUtils.MoveElement(doc, elemId, translation);
                    successfulElements.Add(elemId.IntegerValue);
                }
                catch (Exception ex)
                {
                    failedElements.Add(new FailureInfo
                    {
                        ElementId = elemId.IntegerValue,
                        Reason = $"移动失败: {ex.Message}"
                    });
                }
            }

            // 核心数据结构
            details["moveVector"] = setting.MoveVector;  // 保持原始mm单位对象
            details["moveStrategy"] = "directTransform";
            details["statistics"] = new Dictionary<string, int>
            {
                ["totalElements"] = elementIds.Count,
                ["successCount"] = successfulElements.Count,
                ["failureCount"] = failedElements.Count
            };
        }

        /// <summary>
        /// 执行复制操作（复用自ValidateCopyCommand）
        /// </summary>
        private void ExecuteCopy(
            ICollection<ElementId> elementIds,
            TransformOperationSetting setting,
            List<int> successfulElements,
            List<FailureInfo> failedElements,
            Dictionary<string, object> details)
        {
            // 构建移动向量（毫米转英尺）
            XYZ translation = new XYZ(
                setting.MoveVector.X / 304.8,
                setting.MoveVector.Y / 304.8,
                setting.MoveVector.Z / 304.8
            );

            var copiedElements = new List<int>();

            foreach (var elemId in elementIds)
            {
                try
                {
                    // 使用ElementTransformUtils.CopyElement进行复制
                    ICollection<ElementId> copiedIds = ElementTransformUtils.CopyElement(
                        doc, elemId, translation);

                    successfulElements.Add(elemId.IntegerValue);

                    // 记录所有复制出的元素ID
                    foreach (var copiedId in copiedIds)
                    {
                        copiedElements.Add(copiedId.IntegerValue);
                    }
                }
                catch (Exception ex)
                {
                    failedElements.Add(new FailureInfo
                    {
                        ElementId = elemId.IntegerValue,
                        Reason = $"复制失败: {ex.Message}"
                    });
                }
            }

            // 记录复制详情
            details["copyVector"] = setting.MoveVector;
            details["copiedElements"] = copiedElements;
            details["copiedCount"] = copiedElements.Count;
        }

    }
}