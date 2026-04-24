using System;
using System.Reflection;
using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Utils
{
    public static class RevitElementIdUtils
    {
        public static long GetElementIdValue(ElementId elementId)
        {
            if (elementId == null)
            {
                return -1;
            }

            // Newer Revit APIs expose ElementId.Value (long). Older APIs expose IntegerValue (int).
            PropertyInfo valueProperty = typeof(ElementId).GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
            if (valueProperty != null)
            {
                object rawValue = valueProperty.GetValue(elementId, null);
                if (rawValue is long)
                {
                    return (long)rawValue;
                }

                if (rawValue is int)
                {
                    return (int)rawValue;
                }
            }

            return elementId.IntegerValue;
        }

        public static int Compare(ElementId left, ElementId right)
        {
            long leftValue = GetElementIdValue(left);
            long rightValue = GetElementIdValue(right);
            return leftValue.CompareTo(rightValue);
        }

        public static bool AreEqual(ElementId left, ElementId right)
        {
            return Compare(left, right) == 0;
        }
    }
}
