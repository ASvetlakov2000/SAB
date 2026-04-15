using Autodesk.Revit.DB;
using System;
using System.Globalization;

namespace RevitLibraryBuilder.Services
{
    /// <summary>
    /// Service that reads type thickness from system parameters
    /// for walls, floors, ceilings and roofs.
    /// </summary>
    public class ElementThicknessService
    {
        /// <summary>
        /// Returns thickness text in millimeters, for example: "140 mm".
        /// </summary>
        public string GetTotalThicknessMm(ElementType elementType)
        {
            if (elementType == null)
            {
                return string.Empty;
            }

            string[] parameterNames = GetThicknessParameterNames(elementType);

            if (parameterNames == null || parameterNames.Length == 0)
            {
                return string.Empty;
            }

            for (int i = 0; i < parameterNames.Length; i++)
            {
                string parameterName = parameterNames[i];

                if (string.IsNullOrWhiteSpace(parameterName))
                {
                    continue;
                }

                BuiltInParameter builtInParameter;

                if (!Enum.TryParse(parameterName, out builtInParameter))
                {
                    continue;
                }

                if (!Enum.IsDefined(typeof(BuiltInParameter), builtInParameter))
                {
                    continue;
                }

                Parameter parameter = elementType.get_Parameter(builtInParameter);
                double thicknessMm;

                if (TryReadThicknessMm(parameter, out thicknessMm))
                {
                    return FormatMillimeters(thicknessMm) + " mm";
                }
            }

            return string.Empty;
        }

        // Block responsible for category-specific system thickness parameters.
        private static string[] GetThicknessParameterNames(ElementType elementType)
        {
            if (elementType is FloorType)
            {
                return new[] { "FLOOR_ATTR_DEFAULT_THICKNESS_PARAM" };
            }

            if (elementType is WallType)
            {
                return new[] { "WALL_ATTR_WIDTH_PARAM" };
            }

            if (elementType is CeilingType)
            {
                return new[] { "CEILING_THICKNESS" };
            }

            if (elementType is RoofType)
            {
                return new[] { "ROOF_ATTR_DEFAULT_THICKNESS_PARAM" };
            }

            return null;
        }

        private static bool TryReadThicknessMm(Parameter parameter, out double thicknessMm)
        {
            thicknessMm = 0.0;

            if (parameter == null)
            {
                return false;
            }

            if (parameter.StorageType == StorageType.Double)
            {
                double internalValue = parameter.AsDouble();

                if (internalValue <= 0)
                {
                    return false;
                }

                thicknessMm = UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.Millimeters);
                return true;
            }

            // Safety fallback for localized string values.
            string valueText = parameter.AsValueString();

            if (string.IsNullOrWhiteSpace(valueText))
            {
                valueText = parameter.AsString();
            }

            if (string.IsNullOrWhiteSpace(valueText))
            {
                return false;
            }

            string normalized = valueText
                .Replace("мм", string.Empty)
                .Replace("mm", string.Empty)
                .Trim()
                .Replace(',', '.');

            double parsedValue;

            if (!double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedValue))
            {
                return false;
            }

            if (parsedValue <= 0)
            {
                return false;
            }

            thicknessMm = parsedValue;
            return true;
        }

        private static string FormatMillimeters(double valueMillimeters)
        {
            double rounded = Math.Round(valueMillimeters, 2);

            if (Math.Abs(rounded - Math.Round(rounded)) < 0.0001)
            {
                return Math.Round(rounded).ToString(CultureInfo.InvariantCulture);
            }

            return rounded.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
