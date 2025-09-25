using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Features.ElementFilter.FieldBuilders.Core
{
    /// <summary>
    /// 类型信息字段构建器
    /// 构建 core.typeInfo 字段：typeId, typeName
    /// </summary>
    public class TypeInfoFieldBuilder : IFieldBuilder
    {
        public string FieldName => "core.typeInfo";

        public bool CanBuild(Element element)
        {
            // 所有元素都有类型ID，但可能为无效ID
            return element != null;
        }

        public void Build(FieldContext context)
        {
            var element = context.Element;
            var typeId = element.GetTypeId();

            if (typeId == ElementId.InvalidElementId)
            {
                // 对于没有类型的元素（如Project Info等），返回空值
                context.Result["typeId"] = -1;
                context.Result["typeName"] = null;
                return;
            }

            // 使用缓存的 TypeElement
            var typeElement = context.TypeElement;

            if (typeElement != null)
            {
                context.Result["typeId"] = typeId.IntegerValue;
                context.Result["typeName"] = typeElement.Name;
            }
            else
            {
                // 类型元素不存在的情况
                context.Result["typeId"] = typeId.IntegerValue;
                context.Result["typeName"] = null;
            }
        }
    }
}