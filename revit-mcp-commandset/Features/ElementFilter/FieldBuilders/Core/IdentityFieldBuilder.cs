using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Features.ElementFilter.FieldBuilders.Core
{
    /// <summary>
    /// 身份信息字段构建器
    /// 构建 identity 字段：name, category, builtInCategory
    /// </summary>
    public class IdentityFieldBuilder : IFieldBuilder
    {
        public string FieldName => "identity";

        public bool CanBuild(Element element)
        {
            // 所有元素都有基础身份信息
            return element != null;
        }

        public void Build(FieldContext context)
        {
            var element = context.Element;

            // 设置基础身份信息到 identity 节点
            context.SetNodeValue("identity", "name", element.Name ?? "");
            context.SetNodeValue("identity", "category", element.Category?.Name);

            // 内置类别信息
            if (element.Category != null)
            {
                var builtInCategory = element.Category.Id.IntegerValue;
                context.SetNodeValue("identity", "builtInCategory", builtInCategory);
            }
            else
            {
                context.SetNodeValue("identity", "builtInCategory", null);
            }
        }
    }
}