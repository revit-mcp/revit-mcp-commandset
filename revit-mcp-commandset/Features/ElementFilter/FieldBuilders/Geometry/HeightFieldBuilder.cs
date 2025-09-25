using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Features.ElementFilter.FieldBuilders.Geometry
{
    /// <summary>
    /// 高度字段构建器
    /// 构建 geometry.height 字段：height
    /// 适用于墙体、柱子等有高度概念的元素
    /// </summary>
    public class HeightFieldBuilder : IFieldBuilder
    {
        public string FieldName => "geometry.height";

        public bool CanBuild(Element element)
        {
            // 检查是否为有高度概念的元素类型
            return element is Wall ||
                   element is FamilyInstance fi && IsVerticalElement(fi) ||
                   element.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM) != null;
        }

        public void Build(FieldContext context)
        {
            try
            {
                var element = context.Element;
                double? height = null;

                if (element is Wall wall)
                {
                    height = GetWallHeight(wall);
                }
                else if (element is FamilyInstance familyInstance)
                {
                    height = GetFamilyInstanceHeight(familyInstance);
                }

                if (height.HasValue && height.Value > 0)
                {
                    // 转换为毫米
                    context.Result["height"] = height.Value * 304.8;
                }
            }
            catch
            {
                // 静默失败
            }
        }

        private bool IsVerticalElement(FamilyInstance fi)
        {
            try
            {
                // 检查是否为柱子、门、窗等垂直元素
                var category = fi.Category?.Id?.IntegerValue;
                return category == (int)BuiltInCategory.OST_Columns ||
                       category == (int)BuiltInCategory.OST_StructuralColumns ||
                       category == (int)BuiltInCategory.OST_Doors ||
                       category == (int)BuiltInCategory.OST_Windows;
            }
            catch
            {
                return false;
            }
        }

        private double? GetWallHeight(Wall wall)
        {
            try
            {
                // 优先使用墙体的用户高度
                var userHeightParam = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
                if (userHeightParam != null && userHeightParam.HasValue)
                {
                    return userHeightParam.AsDouble();
                }

                // 使用墙体的实际高度
                var heightParam = wall.get_Parameter(BuiltInParameter.WALL_ATTR_HEIGHT_PARAM);
                return heightParam?.AsDouble();
            }
            catch
            {
                return null;
            }
        }

        private double? GetFamilyInstanceHeight(FamilyInstance fi)
        {
            try
            {
                // 尝试各种可能的高度参数
                var heightParams = new[]
                {
                    BuiltInParameter.DOOR_HEIGHT,
                    BuiltInParameter.WINDOW_HEIGHT,
                    BuiltInParameter.FURNITURE_HEIGHT
                };

                foreach (var paramId in heightParams)
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