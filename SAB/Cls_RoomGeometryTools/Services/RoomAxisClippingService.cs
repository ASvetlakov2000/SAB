using Autodesk.Revit.DB;
using SAB.RoomGeometryTools.Utils;
using System;
using System.Collections.Generic;

namespace SAB.RoomGeometryTools.Services
{
    /// <summary>
    /// Сервис отсечения оси границами помещения.
    /// </summary>
    public class RoomAxisClippingService
    {
        private const double Epsilon = 1e-9;

        public bool TryClipAxisByPolygon(
            XYZ centroid,
            XYZ direction,
            IList<XYZ> polygonVertices,
            out Line clippedLine,
            out string errorMessage)
        {
            clippedLine = null;
            errorMessage = string.Empty;

            if (centroid == null || direction == null || polygonVertices == null || polygonVertices.Count < 3)
            {
                errorMessage = "Недостаточно данных для отсечения оси.";
                return false;
            }

            XYZ normalizedDirection = new XYZ(direction.X, direction.Y, 0.0);
            if (normalizedDirection.GetLength() < Epsilon)
            {
                errorMessage = "Направление оси имеет нулевую длину.";
                return false;
            }

            normalizedDirection = normalizedDirection.Normalize();

            List<AxisIntersection> intersections = CollectIntersections(centroid, normalizedDirection, polygonVertices);
            if (intersections.Count < 2)
            {
                errorMessage = "Не удалось найти достаточное количество пересечений оси с границей.";
                return false;
            }

            intersections.Sort(delegate (AxisIntersection left, AxisIntersection right)
            {
                return left.T.CompareTo(right.T);
            });

            AxisInterval selectedInterval = SelectIntervalContainingCentroid(intersections, centroid, normalizedDirection, polygonVertices);
            if (selectedInterval == null)
            {
                errorMessage = "Не найден внутренний отрезок оси, проходящий через помещение.";
                return false;
            }

            if (selectedInterval.EndPoint.DistanceTo(selectedInterval.StartPoint) < Epsilon)
            {
                errorMessage = "Внутренний отрезок оси слишком мал.";
                return false;
            }

            clippedLine = Line.CreateBound(selectedInterval.StartPoint, selectedInterval.EndPoint);
            return true;
        }

        private static List<AxisIntersection> CollectIntersections(XYZ centroid, XYZ direction, IList<XYZ> polygon)
        {
            List<AxisIntersection> result = new List<AxisIntersection>();

            for (int i = 0; i < polygon.Count; i++)
            {
                XYZ a = polygon[i];
                XYZ b = polygon[(i + 1) % polygon.Count];

                double t;
                XYZ point;
                if (!TryIntersectInfiniteLineWithSegmentXY(centroid, direction, a, b, out t, out point))
                {
                    continue;
                }

                bool exists = false;
                for (int j = 0; j < result.Count; j++)
                {
                    if (Math.Abs(result[j].T - t) < 1e-7)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    result.Add(new AxisIntersection { T = t, Point = point });
                }
            }

            return result;
        }

        private static AxisInterval SelectIntervalContainingCentroid(
            IList<AxisIntersection> intersections,
            XYZ centroid,
            XYZ direction,
            IList<XYZ> polygon)
        {
            AxisInterval best = null;

            for (int i = 0; i < intersections.Count - 1; i++)
            {
                AxisIntersection left = intersections[i];
                AxisIntersection right = intersections[i + 1];

                if (right.T - left.T < 1e-7)
                {
                    continue;
                }

                double tMid = (left.T + right.T) * 0.5;
                XYZ midpoint = centroid + direction * tMid;

                if (!PolygonUtils.IsPointInsidePolygonXY(polygon, midpoint))
                {
                    continue;
                }

                bool containsCentroidByParameter = left.T <= 0.0 && 0.0 <= right.T;
                double length = right.Point.DistanceTo(left.Point);

                if (best == null)
                {
                    best = new AxisInterval(left.Point, right.Point, containsCentroidByParameter, length);
                    continue;
                }

                if (containsCentroidByParameter && !best.ContainsCentroid)
                {
                    best = new AxisInterval(left.Point, right.Point, true, length);
                    continue;
                }

                if (containsCentroidByParameter == best.ContainsCentroid && length > best.Length)
                {
                    best = new AxisInterval(left.Point, right.Point, containsCentroidByParameter, length);
                }
            }

            return best;
        }

        private static bool TryIntersectInfiniteLineWithSegmentXY(
            XYZ linePoint,
            XYZ lineDirection,
            XYZ segmentStart,
            XYZ segmentEnd,
            out double lineParameter,
            out XYZ intersectionPoint)
        {
            lineParameter = 0.0;
            intersectionPoint = XYZ.Zero;

            double px = linePoint.X;
            double py = linePoint.Y;
            double rx = lineDirection.X;
            double ry = lineDirection.Y;

            double qx = segmentStart.X;
            double qy = segmentStart.Y;
            double sx = segmentEnd.X - segmentStart.X;
            double sy = segmentEnd.Y - segmentStart.Y;

            double denominator = Cross2d(rx, ry, sx, sy);
            if (Math.Abs(denominator) < Epsilon)
            {
                return false;
            }

            double qpx = qx - px;
            double qpy = qy - py;

            double t = Cross2d(qpx, qpy, sx, sy) / denominator;
            double u = Cross2d(qpx, qpy, rx, ry) / denominator;

            if (u < -1e-9 || u > 1.0 + 1e-9)
            {
                return false;
            }

            lineParameter = t;
            intersectionPoint = new XYZ(px + t * rx, py + t * ry, linePoint.Z);
            return true;
        }

        private static double Cross2d(double ax, double ay, double bx, double by)
        {
            return ax * by - ay * bx;
        }

        private class AxisIntersection
        {
            public double T { get; set; }

            public XYZ Point { get; set; }
        }

        private class AxisInterval
        {
            public AxisInterval(XYZ startPoint, XYZ endPoint, bool containsCentroid, double length)
            {
                StartPoint = startPoint;
                EndPoint = endPoint;
                ContainsCentroid = containsCentroid;
                Length = length;
            }

            public XYZ StartPoint { get; private set; }

            public XYZ EndPoint { get; private set; }

            public bool ContainsCentroid { get; private set; }

            public double Length { get; private set; }
        }
    }
}

