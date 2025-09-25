using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace RevitMCPCommandSet.Features.ElementFilter.FieldBuilders.Core
{
    /// <summary>
    /// 标高信息字段构建器
    /// 构建 core.levelInfo 字段：levelId, levelName
    /// </summary>
    public class LevelInfoFieldBuilder : IFieldBuilder
    {
        public string FieldName => "core.levelInfo";

        public bool CanBuild(Element element)
        {
            // 大多数元素都有标高信息，但不是全部
            return element != null;
        }

        public void Build(FieldContext context)
        {
            // 使用缓存的 Level
            var level = context.Level;

            if (level != null)
            {
                context.Result["levelId"] = level.Id.IntegerValue;
                context.Result["levelName"] = level.Name;
            }
            else
            {
                // 没有关联标高的元素
                context.Result["levelId"] = -1;
                context.Result["levelName"] = null;
            }
        }
    }
}