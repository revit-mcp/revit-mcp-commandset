using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Features.ElementFilter.FieldBuilders.Geometry
{
    /// <summary>
    /// 厚度字段构建器
    /// 构建 geometry.thickness 字段：thickness
    /// 适用于墙体、楼板等有厚度概念的元素
    /// </summary>
    public class ThicknessFieldBuilder : IFieldBuilder
    {
        public string FieldName => "geometry.thickness";

        public bool CanBuild(Element element)
        {
            // 检查是否为有厚度概念的元素类型
            return element is Wall ||
                   element is Floor ||
                   element is RoofBase ||
                   element is Ceiling;
        }

        public void Build(FieldContext context)
        {
            try
            {
                var element = context.Element;
                double? thickness = null;

                switch (element)
                {
                    case Wall wall:
                        thickness = GetWallThickness(wall);
                        break;
                    case Floor floor:
                        thickness = GetFloorThickness(floor);
                        break;
                    case RoofBase roof:
                        thickness = GetRoofThickness(roof);
                        break;
                    case Ceiling ceiling:
                        thickness = GetCeilingThickness(ceiling);
                        break;
                }

                if (thickness.HasValue)
                {
                    // 转换为毫米
                    context.Result["thickness"] = thickness.Value * 304.8;
                }
            }
            catch
            {
                // 静默失败
            }
        }

        private double? GetWallThickness(Wall wall)
        {
            try
            {
                var wallType = wall.Document.GetElement(wall.GetTypeId()) as WallType;
                return wallType?.Width;
            }
            catch
            {
                return null;
            }
        }

        private double? GetFloorThickness(Floor floor)
        {
            try
            {
                var floorType = floor.Document.GetElement(floor.GetTypeId()) as FloorType;
                return floorType?.get_Parameter(BuiltInParameter.FLOOR_ATTR_DEFAULT_THICKNESS_PARAM)?.AsDouble();
            }
            catch
            {
                return null;
            }
        }

        private double? GetRoofThickness(RoofBase roof)
        {
            try
            {
                var roofType = roof.Document.GetElement(roof.GetTypeId()) as RoofType;
                return roofType?.get_Parameter(BuiltInParameter.ROOF_ATTR_DEFAULT_THICKNESS_PARAM)?.AsDouble();
            }
            catch
            {
                return null;
            }
        }

        private double? GetCeilingThickness(Ceiling ceiling)
        {
            try
            {
                var ceilingType = ceiling.Document.GetElement(ceiling.GetTypeId()) as CeilingType;
                return ceilingType?.get_Parameter(BuiltInParameter.CEILING_THICKNESS)?.AsDouble();
            }
            catch
            {
                return null;
            }
        }
    }
}