using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Features.ElementFilter.FieldBuilders
{
    /// <summary>
    /// 字段构建器接口
    /// 用于构建需要额外 Revit API 调用的复杂字段
    /// </summary>
    public interface IFieldBuilder
    {
        /// <summary>
        /// 字段名称（如 "core.typeInfo"）
        /// </summary>
        string FieldName { get; }

        /// <summary>
        /// 判断是否可以为指定元素构建此字段
        /// </summary>
        /// <param name="element">要检查的元素</param>
        /// <returns>如果可以构建返回 true</returns>
        bool CanBuild(Element element);

        /// <summary>
        /// 构建字段数据并写入结果字典
        /// </summary>
        /// <param name="context">字段上下文（包含缓存和输出字典）</param>
        void Build(FieldContext context);
    }
}