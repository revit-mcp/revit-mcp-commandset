using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Features.ElementFilter.FieldBuilders.Geometry
{
    /// <summary>
    /// 宽度字段构建器
    /// 构建 geometry.width 字段：width
    /// 适用于门、窗、家具等有宽度概念的元素
    /// </summary>
    public class WidthFieldBuilder : IFieldBuilder
    {
        public string FieldName => "geometry.width";

        public bool CanBuild(Element element)
        {
            // 检查是否为有宽度概念的元素类型
            return element is FamilyInstance fi && HasWidthConcept(fi);
        }

        public void Build(FieldContext context)
        {
            try
            {
                var element = context.Element;
                double? width = null;

                if (element is FamilyInstance familyInstance)
                {
                    width = GetFamilyInstanceWidth(familyInstance);
                }

                if (width.HasValue && width.Value > 0)
                {
                    // 转换为毫米
                    context.Result["width"] = width.Value * 304.8;
                }
            }
            catch
            {
                // 静默失败
            }
        }

        private bool HasWidthConcept(FamilyInstance fi)
        {
            try
            {
                // 检查是否为门、窗、家具等有宽度概念的元素
                var category = fi.Category?.Id?.IntegerValue;
                return category == (int)BuiltInCategory.OST_Doors ||
                       category == (int)BuiltInCategory.OST_Windows ||
                       category == (int)BuiltInCategory.OST_Furniture ||
                       category == (int)BuiltInCategory.OST_GenericModel ||
                       category == (int)BuiltInCategory.OST_Casework;
            }
            catch
            {
                return false;
            }
        }

        private double? GetFamilyInstanceWidth(FamilyInstance fi)
        {
            try
            {
                // 尝试各种可能的宽度参数
                var widthParams = new[]
                {
                    BuiltInParameter.DOOR_WIDTH,
                    BuiltInParameter.WINDOW_WIDTH,
                    BuiltInParameter.FURNITURE_WIDTH,
                    BuiltInParameter.CASEWORK_WIDTH
                };

                foreach (var paramId in widthParams)
                {
                    var param = fi.get_Parameter(paramId);
                    if (param != null && param.HasValue)
                    {
                        var value = param.AsDouble();
                        if (value > 0) return value;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}