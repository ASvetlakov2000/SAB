using System;
using Autodesk.Revit.DB;

namespace SAB.GklFrame.Services
{
    public interface IMullionGeometryService
    {
        double GetLength(Mullion mullion);
    }

    public sealed class MullionGeometryService : IMullionGeometryService
    {
        public double GetLength(Mullion mullion)
        {
            if (mullion == null)
            {
                throw new ArgumentNullException("mullion");
            }

            // LocationCurve — осевая геометрия импоста и предпочтительный источник
            // фактической длины. Геометрический fallback нужен для нестандартных семейств.
            Curve locationCurve = mullion.LocationCurve;
            if (locationCurve != null && locationCurve.Length > 1e-9)
            {
                return locationCurve.Length;
            }

            Options options = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            };
            GeometryElement geometry = mullion.get_Geometry(options);
            double longestEdge = FindLongestEdge(geometry);
            if (longestEdge > 1e-9)
            {
                return longestEdge;
            }

            BoundingBoxXYZ boundingBox = mullion.get_BoundingBox(null);
            if (boundingBox != null)
            {
                XYZ size = boundingBox.Max - boundingBox.Min;
                return Math.Max(size.X, Math.Max(size.Y, size.Z));
            }

            throw new InvalidOperationException("Не удалось определить длину импоста " + mullion.Id.IntegerValue + ".");
        }

        private static double FindLongestEdge(GeometryElement geometry)
        {
            double longest = 0.0;
            if (geometry == null)
            {
                return longest;
            }

            foreach (GeometryObject geometryObject in geometry)
            {
                Solid solid = geometryObject as Solid;
                if (solid != null)
                {
                    foreach (Edge edge in solid.Edges)
                    {
                        Curve curve = edge.AsCurve();
                        if (curve != null)
                        {
                            longest = Math.Max(longest, curve.Length);
                        }
                    }

                    continue;
                }

                GeometryInstance instance = geometryObject as GeometryInstance;
                if (instance != null)
                {
                    longest = Math.Max(longest, FindLongestEdge(instance.GetInstanceGeometry()));
                }
            }

            return longest;
        }
    }
}
