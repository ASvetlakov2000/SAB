using System;
using System.Reflection;
using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Utils
{
public static class RevitElementIdUtils
{
        public static ElementId CreateElementIdFromLong(long value)
        {
            if (value < 0)
            {
                return ElementId.InvalidElementId;
            }

            // Newer Revit APIs provide constructor ElementId(long), while older APIs use ElementId(int).
            ConstructorInfo longConstructor = typeof(ElementId).GetConstructor(new[] { typeof(long) });
            if (longConstructor != null)
            {
                return (ElementId)longConstructor.Invoke(new object[] { value });
            }

            if (value > int.MaxValue)
            {
                return ElementId.InvalidElementId;
            }

            ConstructorInfo intConstructor = typeof(ElementId).GetConstructor(new[] { typeof(int) });
            if (intConstructor != null)
            {
                return (ElementId)intConstructor.Invoke(new object[] { Convert.ToInt32(value) });
            }

            return ElementId.InvalidElementId;
        }

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
