using Autodesk.Revit.DB;

namespace SAB.RoomGeometryTools.Utils
{
    /// <summary>
    /// Утилиты конвертации единиц Revit.
    /// </summary>
    public static class RevitUnitUtils
    {
        public static double MillimetersToInternal(double millimeters)
        {
            return UnitUtils.ConvertToInternalUnits(millimeters, UnitTypeId.Millimeters);
        }

        public static double InternalToMillimeters(double internalValue)
        {
            return UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.Millimeters);
        }

        public static double InternalAreaToSquareMeters(double internalArea)
        {
            return UnitUtils.ConvertFromInternalUnits(internalArea, UnitTypeId.SquareMeters);
        }
    }
}

