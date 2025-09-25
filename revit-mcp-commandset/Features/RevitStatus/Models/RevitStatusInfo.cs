using System.Collections.Generic;

namespace RevitMCPCommandSet.Features.RevitStatus.Models
{
    /// <summary>
    /// Revit状态信息
    /// </summary>
    public class RevitStatusInfo
    {
        /// <summary>
        /// Revit版本信息
        /// </summary>
        public string RevitVersion { get; set; }

        /// <summary>
        /// 是否有激活的文档
        /// </summary>
        public bool HasActiveDocument { get; set; }

        /// <summary>
        /// 当前激活文档信息
        /// </summary>
        public ActiveDocumentInfo ActiveDocument { get; set; }

        /// <summary>
        /// 所有打开的文档名称列表
        /// </summary>
        public List<string> OpenDocumentNames { get; set; }

        /// <summary>
        /// 应用程序语言设置
        /// </summary>
        public string ApplicationLanguage { get; set; }
    }

    /// <summary>
    /// 激活文档信息
    /// </summary>
    public class ActiveDocumentInfo
    {
        /// <summary>
        /// 项目名称
        /// </summary>
        public string ProjectName { get; set; }

        /// <summary>
        /// 项目路径
        /// </summary>
        public string ProjectPath { get; set; }

        /// <summary>
        /// 是否有未保存的修改
        /// </summary>
        public bool IsModified { get; set; }

        /// <summary>
        /// 当前激活视图名称
        /// </summary>
        public string ActiveViewName { get; set; }

        /// <summary>
        /// 当前激活视图类型
        /// </summary>
        public string ActiveViewType { get; set; }

        /// <summary>
        /// 当前激活视图ID
        /// </summary>
        public int ActiveViewId { get; set; }
    }
}