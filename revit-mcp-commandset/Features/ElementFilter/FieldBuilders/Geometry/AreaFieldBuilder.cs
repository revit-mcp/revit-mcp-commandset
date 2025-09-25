using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;

namespace RevitMCPCommandSet.Features.ElementFilter.FieldBuilders.Geometry
{
    /// <summary>
    /// 面积字段构建器
    /// 构建 geometry.area 字段：area
    /// 适用于房间、空间、楼板等有面积概念的元素
    /// </summary>
    public class AreaFieldBuilder : IFieldBuilder
    {
        public string FieldName => "geometry.area";

        public bool CanBuild(Element element)
        {
            // 检查是否为有面积概念的元素类型
            return element is Room ||
                   element is Space ||
                   element is Floor ||
                   element is RoofBase ||
                   element is Ceiling ||
                   element.get_Parameter(BuiltInParameter.ROOM_AREA) != null ||
                   element.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED) != null;
        }

        public void Build(FieldContext context)
        {
            try
            {
                var element = context.Element;
                double? area = null;

                // 根据元素类型获取面积
                switch (element)
                {
                    case Room room:
                        area = GetRoomArea(room);
                        break;
                    case Space space:
                        area = GetSpaceArea(space);
                        break;
                    case Floor floor:
                        area = GetFloorArea(floor);
                        break;
                    case RoofBase roof:
                        area = GetRoofArea(roof);
                        break;
                    case Ceiling ceiling:
                        area = GetCeilingArea(ceiling);
                        break;
                    default:
                        area = GetGenericArea(element);
                        break;
                }

                if (area.HasValue && area.Value > 0)
                {
                    // 转换为平方毫米，然后转为平方米
                    var areaInMm2 = area.Value * 304.8 * 304.8;
                    context.Result["area"] = areaInMm2 / 1000000.0; // 转为平方米
                }
            }
            catch
            {
                // 静默失败
            }
        }

        private double? GetRoomArea(Room room)
        {
            try
            {
                var areaParam = room.get_Parameter(BuiltInParameter.ROOM_AREA);
                return areaParam?.AsDouble();
            }
            catch
            {
                return null;
            }
        }

        private double? GetSpaceArea(Space space)
        {
            try
            {
                var areaParam = space.get_Parameter(BuiltInParameter.ROOM_AREA);
                return areaParam?.AsDouble();
            }
            catch
            {
                return null;
            }
        }

        private double? GetFloorArea(Floor floor)
        {
            try
            {
                var areaParam = floor.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                return areaParam?.AsDouble();
            }
            catch
            {
                return null;
            }
        }

        private double? GetRoofArea(RoofBase roof)
        {
            try
            {
                var areaParam = roof.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                return areaParam?.AsDouble();
            }
            catch
            {
                return null;
            }
        }

        private double? GetCeilingArea(Ceiling ceiling)
        {
            try
            {
                var areaParam = ceiling.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                return areaParam?.AsDouble();
            }
            catch
            {
                return null;
            }
        }

        private double? GetGenericArea(Element element)
        {
            try
            {
                // 尝试各种可能的面积参数
                var areaParams = new[]
                {
                    BuiltInParameter.HOST_AREA_COMPUTED,
                    BuiltInParameter.ROOM_AREA
                };

                foreach (var paramId in areaParams)
                {
                    var param = element.get_Parameter(paramId);
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