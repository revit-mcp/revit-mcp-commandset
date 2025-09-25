using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Features.ElementFilter.FieldBuilders.Core
{
    /// <summary>
    /// 族信息字段构建器
    /// 构建 core.familyInfo 字段：familyId, familyName
    /// 仅对族实例有效
    /// </summary>
    public class FamilyInfoFieldBuilder : IFieldBuilder
    {
        public string FieldName => "core.familyInfo";

        public bool CanBuild(Element element)
        {
            // 仅对族实例有效
            return element is FamilyInstance;
        }

        public void Build(FieldContext context)
        {
            var familyInstance = context.Element as FamilyInstance;
            if (familyInstance == null)
            {
                return; // 不应该发生，但安全起见
            }

            // 使用缓存的 Family
            var family = context.Family;

            if (family != null)
            {
                context.Result["familyId"] = family.Id.IntegerValue;
                context.Result["familyName"] = family.Name;
            }
            else
            {
                // 族信息不可用的情况
                context.Result["familyId"] = -1;
                context.Result["familyName"] = null;
            }
        }
    }
}