using Autodesk.Revit.DB;
using System;
using System.Globalization;

namespace SAB.RoomGeometryTools.Utils
{
    /// <summary>
    /// Безопасное чтение параметров Revit.
    /// </summary>
    public static class RevitParameterUtils
    {
        public static string GetStringValue(Parameter parameter)
        {
            if (parameter == null)
            {
                return string.Empty;
            }

            string value = parameter.AsString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            value = parameter.AsValueString();
            return value ?? string.Empty;
        }

        public static bool TryGetDouble(Parameter parameter, out double value)
        {
            value = 0.0;

            if (parameter == null)
            {
                return false;
            }

            if (parameter.StorageType == StorageType.Double)
            {
                value = parameter.AsDouble();
                return true;
            }

            string stringValue = GetStringValue(parameter);
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return false;
            }

            string normalized = stringValue.Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        public static string GetRoomNumber(Element roomElement)
        {
            return GetBuiltInString(roomElement, BuiltInParameter.ROOM_NUMBER, "Без номера");
        }

        public static string GetRoomName(Element roomElement)
        {
            return GetBuiltInString(roomElement, BuiltInParameter.ROOM_NAME, "Без имени");
        }

        private static string GetBuiltInString(Element element, BuiltInParameter parameterId, string fallback)
        {
            if (element == null)
            {
                return fallback;
            }

            Parameter parameter = element.get_Parameter(parameterId);
            string value = GetStringValue(parameter);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}

