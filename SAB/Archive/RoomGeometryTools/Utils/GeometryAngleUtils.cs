using Autodesk.Revit.DB;
using System;

namespace SAB.RoomGeometryTools.Utils
{
    /// <summary>
    /// Утилиты расчета углов в плоскости XY.
    /// </summary>
    public static class GeometryAngleUtils
    {
        public static bool TryGetInternalAngleDegreesXY(
            XYZ previousPoint,
            XYZ vertexPoint,
            XYZ nextPoint,
            out double angleDegrees)
        {
            angleDegrees = 0.0;

            if (previousPoint == null || vertexPoint == null || nextPoint == null)
            {
                return false;
            }

            XYZ vector1 = new XYZ(previousPoint.X - vertexPoint.X, previousPoint.Y - vertexPoint.Y, 0.0);
            XYZ vector2 = new XYZ(nextPoint.X - vertexPoint.X, nextPoint.Y - vertexPoint.Y, 0.0);

            double length1 = vector1.GetLength();
            double length2 = vector2.GetLength();

            if (length1 < 1e-9 || length2 < 1e-9)
            {
                return false;
            }

            XYZ normalized1 = vector1 / length1;
            XYZ normalized2 = vector2 / length2;

            double dot = normalized1.X * normalized2.X + normalized1.Y * normalized2.Y;
            dot = Math.Max(-1.0, Math.Min(1.0, dot));

            double radians = Math.Acos(dot);
            angleDegrees = radians * 180.0 / Math.PI;
            return true;
        }

        public static bool IsRightAngle(double angleDegrees, double epsilonDegrees)
        {
            return Math.Abs(angleDegrees - 90.0) <= epsilonDegrees;
        }
    }
}

