using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Features.RevitStatus.Models;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Features.RevitStatus
{
    /// <summary>
    /// 获取Revit状态事件处理器
    /// </summary>
    public class GetRevitStatusEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private UIApplication uiApp;
        private UIDocument uiDoc => uiApp.ActiveUIDocument;
        private Document doc => uiDoc?.Document;
        private Autodesk.Revit.ApplicationServices.Application app => uiApp.Application;

        /// <summary>
        /// 事件等待对象
        /// </summary>
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        /// <summary>
        /// 执行结果（传出数据）
        /// </summary>
        public AIResult<RevitStatusInfo> Result { get; private set; }

        public void Execute(UIApplication uiapp)
        {
            uiApp = uiapp;

            try
            {
                var statusInfo = new RevitStatusInfo();

                // 获取Revit版本信息
                statusInfo.RevitVersion = app.VersionName + " " + app.VersionNumber;

                // 检查是否有活动文档
                statusInfo.HasActiveDocument = uiDoc != null;

                if (statusInfo.HasActiveDocument)
                {
                    // 获取当前活动文档信息
                    statusInfo.ActiveDocument = new ActiveDocumentInfo
                    {
                        ProjectName = doc.Title,
                        ProjectPath = doc.PathName,
                        IsModified = doc.IsModified,
                        ActiveViewName = uiDoc.ActiveView.Name,
                        ActiveViewType = uiDoc.ActiveView.ViewType.ToString(),
                        ActiveViewId = uiDoc.ActiveView.Id.IntegerValue
                    };
                }
                else
                {
                    statusInfo.ActiveDocument = null;
                }

                // 获取所有打开的文档名称列表
                statusInfo.OpenDocumentNames = new List<string>();
                foreach (Document openDoc in app.Documents)
                {
                    statusInfo.OpenDocumentNames.Add(openDoc.Title);
                }

                Result = new AIResult<RevitStatusInfo>
                {
                    Success = true,
                    Message = $"成功获取Revit状态信息。Revit版本: {statusInfo.RevitVersion}, " +
                             $"活动文档: {(statusInfo.HasActiveDocument ? statusInfo.ActiveDocument.ProjectName : "无")}, " +
                             $"打开文档数量: {statusInfo.OpenDocumentNames.Count}",
                    Response = statusInfo
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<RevitStatusInfo>
                {
                    Success = false,
                    Message = $"获取Revit状态失败: {ex.Message}",
                    Response = null
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName()
        {
            return "GetRevitStatusEventHandler";
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }
    }
}