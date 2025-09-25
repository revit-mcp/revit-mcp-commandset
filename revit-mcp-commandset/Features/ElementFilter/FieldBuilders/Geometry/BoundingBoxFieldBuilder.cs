using Autodesk.Revit.DB;
using RevitMCPCommandSet.Models.Geometry;

namespace RevitMCPCommandSet.Features.ElementFilter.FieldBuilders.Geometry
{
    /// <summary>
    /// 边界框字段构建器
    /// 构建 geometry.boundingBox 字段：minX, minY, minZ, maxX, maxY, maxZ
    /// </summary>
    public class BoundingBoxFieldBuilder : IFieldBuilder
    {
        public string FieldName => "geometry.boundingBox";

        public bool CanBuild(Element element)
        {
            return element != null;
        }

        public void Build(FieldContext context)
        {
            try
            {
                // 使用缓存的边界框
                var boundingBox = context.BoundingBox;
                if (boundingBox == null || !boundingBox.Enabled)
                {
                    return; // 无法获取边界框，静默失败
                }

                // 转换为毫米并构建结果
                context.Result["minX"] = boundingBox.Min.X * 304.8;
                context.Result["minY"] = boundingBox.Min.Y * 304.8;
                context.Result["minZ"] = boundingBox.Min.Z * 304.8;
                context.Result["maxX"] = boundingBox.Max.X * 304.8;
                context.Result["maxY"] = boundingBox.Max.Y * 304.8;
                context.Result["maxZ"] = boundingBox.Max.Z * 304.8;
            }
            catch
            {
                // 静默失败
            }
        }
    }
}