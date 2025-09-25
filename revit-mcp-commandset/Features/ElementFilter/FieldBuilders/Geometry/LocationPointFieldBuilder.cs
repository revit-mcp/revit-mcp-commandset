using Autodesk.Revit.DB;
using RevitMCPCommandSet.Models.Geometry;

namespace RevitMCPCommandSet.Features.ElementFilter.FieldBuilders.Geometry
{
    /// <summary>
    /// 位置点字段构建器
    /// 构建 geometry.locationPoint 字段：x, y, z
    /// </summary>
    public class LocationPointFieldBuilder : IFieldBuilder
    {
        public string FieldName => "geometry.locationPoint";

        public bool CanBuild(Element element)
        {
            return element?.Location is LocationPoint;
        }

        public void Build(FieldContext context)
        {
            try
            {
                var locationPoint = context.Element.Location as LocationPoint;
                if (locationPoint?.Point == null)
                {
                    return; // 静默失败
                }

                var point = locationPoint.Point;

                // 转换为毫米并构建结果
                context.Result["x"] = point.X * 304.8;
                context.Result["y"] = point.Y * 304.8;
                context.Result["z"] = point.Z * 304.8;
            }
            catch
            {
                // 静默失败
            }
        }
    }
}