using Autodesk.Revit.DB;

namespace SAB.GklFrame.Services
{
    internal static class RevitUnitService
    {
        public static double MillimetersToInternal(double millimeters)
        {
            return UnitUtils.ConvertToInternalUnits(millimeters, UnitTypeId.Millimeters);
        }

        public static double InternalToMillimeters(double internalValue)
        {
            return UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.Millimeters);
        }
    }
}
