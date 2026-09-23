using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace SAB.RoomGeometryTools.Utils
{
    /// <summary>
    /// Утилиты работы с полигонами в плоскости XY.
    /// </summary>
    public static class PolygonUtils
    {
        public static double CalculateSignedAreaXY(IList<XYZ> vertices)
        {
            if (vertices == null || vertices.Count < 3)
            {
                return 0.0;
            }

            double area2 = 0.0;

            for (int i = 0; i < vertices.Count; i++)
            {
                XYZ current = vertices[i];
                XYZ next = vertices[(i + 1) % vertices.Count];
                area2 += current.X * next.Y - next.X * current.Y;
            }

            return area2 * 0.5;
        }

        public static bool TryCalculateCentroidXY(IList<XYZ> vertices, out XYZ centroid)
        {
            centroid = XYZ.Zero;

            if (vertices == null || vertices.Count < 3)
            {
                return false;
            }

            double signedArea = CalculateSignedAreaXY(vertices);
            if (Math.Abs(signedArea) < 1e-12)
            {
                return false;
            }

            double factor = 0.0;
            double cx = 0.0;
            double cy = 0.0;

            for (int i = 0; i < vertices.Count; i++)
            {
                XYZ current = vertices[i];
                XYZ next = vertices[(i + 1) % vertices.Count];

                factor = current.X * next.Y - next.X * current.Y;
                cx += (current.X + next.X) * factor;
                cy += (current.Y + next.Y) * factor;
            }

            double divisor = 6.0 * signedArea;
            if (Math.Abs(divisor) < 1e-12)
            {
                return false;
            }

            centroid = new XYZ(cx / divisor, cy / divisor, 0.0);
            return true;
        }

        public static bool IsPointInsidePolygonXY(IList<XYZ> polygon, XYZ point)
        {
            if (polygon == null || polygon.Count < 3 || point == null)
            {
                return false;
            }

            bool inside = false;
            double testX = point.X;
            double testY = point.Y;

            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                XYZ pi = polygon[i];
                XYZ pj = polygon[j];

                bool intersect =
                    ((pi.Y > testY) != (pj.Y > testY)) &&
                    (testX < (pj.X - pi.X) * (testY - pi.Y) / ((pj.Y - pi.Y) + 1e-15) + pi.X);

                if (intersect)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}

