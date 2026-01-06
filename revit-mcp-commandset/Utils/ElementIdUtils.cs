using Autodesk.Revit.DB;
using System;
using System.Reflection;

namespace RevitMCPCommandSet.Utils
{
    internal static class ElementIdUtils
    {
        private static readonly Func<ElementId, int> GetValue = CreateGetter();

        public static int GetIdValue(ElementId id)
        {
            if (id == null)
            {
                return -1;
            }

            return GetValue(id);
        }

        private static Func<ElementId, int> CreateGetter()
        {
            var type = typeof(ElementId);
            var valueProp = type.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
            if (valueProp != null)
            {
                return id => ConvertToInt(valueProp.GetValue(id));
            }

            var intProp = type.GetProperty("IntegerValue", BindingFlags.Instance | BindingFlags.Public);
            if (intProp != null)
            {
                return id => ConvertToInt(intProp.GetValue(id));
            }

            return _ => -1;
        }

        private static int ConvertToInt(object value)
        {
            if (value is int intValue)
            {
                return intValue;
            }

            if (value is long longValue)
            {
                return unchecked((int)longValue);
            }

            return value != null ? Convert.ToInt32(value) : -1;
        }
    }
}
