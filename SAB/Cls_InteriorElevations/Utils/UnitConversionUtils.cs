namespace SAB.InteriorElevations.Utils
{
    public static class UnitConversionUtils
    {
        // Revit stores distances in feet. UI values are entered in millimeters.
        private const double MillimetersPerFoot = 304.8;

        public static double MillimetersToFeet(double millimeters)
        {
            return millimeters / MillimetersPerFoot;
        }

        public static double FeetToMillimeters(double feet)
        {
            return feet * MillimetersPerFoot;
        }
    }
}
