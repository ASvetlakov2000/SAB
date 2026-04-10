using Autodesk.Revit.DB;

namespace Helpers
{
    public static class PlacementMath
    {
        public static XYZ Grid(int index, double spacingMm)
        {
            double mmToFeet = 1.0 / 304.8;
            double spacing = spacingMm * mmToFeet;

            int row = index / 10;
            int col = index % 10;

            return new XYZ(
                col * spacing,
                -row * spacing,
                0
            );
        }
    }
}