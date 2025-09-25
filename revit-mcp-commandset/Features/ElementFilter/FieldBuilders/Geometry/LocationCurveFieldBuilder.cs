using Autodesk.Revit.DB;
using RevitMCPCommandSet.Models.Geometry;

namespace RevitMCPCommandSet.Features.ElementFilter.FieldBuilders.Geometry
{
    /// <summary>
    /// 位置曲线字段构建器
    /// 构建 geometry.locationCurve 字段：startX, startY, startZ, endX, endY, endZ
    /// </summary>
    public class LocationCurveFieldBuilder : IFieldBuilder
    {
        public string FieldName => "geometry.locationCurve";

        public bool CanBuild(Element element)
        {
            return element?.Location is LocationCurve;
        }

        public void Build(FieldContext context)
        {
            try
            {
                var locationCurve = context.Element.Location as LocationCurve;
                var curve = locationCurve?.Curve;

                if (curve == null)
                {
                    return; // 静默失败
                }

                var startPoint = curve.GetEndPoint(0);
                var endPoint = curve.GetEndPoint(1);

                // 转换为毫米并构建结果
                context.Result["startX"] = startPoint.X * 304.8;
                context.Result["startY"] = startPoint.Y * 304.8;
                context.Result["startZ"] = startPoint.Z * 304.8;
                context.Result["endX"] = endPoint.X * 304.8;
                context.Result["endY"] = endPoint.Y * 304.8;
                context.Result["endZ"] = endPoint.Z * 304.8;
            }
            catch
            {
                // 静默失败
            }
        }
    }
}