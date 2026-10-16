using System;
using System.Collections.Generic;
using System.Linq;

namespace SAB.InteriorElevations.Services.Geometry
{
    public struct ContourPoint
    {
        public double X, Y;
        public ContourPoint(double x, double y) { X = x; Y = y; }
        public static ContourPoint operator +(ContourPoint a, ContourPoint b) { return new ContourPoint(a.X + b.X, a.Y + b.Y); }
        public static ContourPoint operator -(ContourPoint a, ContourPoint b) { return new ContourPoint(a.X - b.X, a.Y - b.Y); }
        public static ContourPoint operator *(ContourPoint a, double b) { return new ContourPoint(a.X * b, a.Y * b); }
        public double Length { get { return Math.Sqrt(X * X + Y * Y); } }
    }

    public sealed class ContourSegment
    {
        public ContourPoint Start, End;
        public ContourSegment(ContourPoint start, ContourPoint end) { Start = start; End = end; }
    }

    public sealed class ContourDirectionResult
    {
        public bool IsClosed;
        public bool UsesVirtualClosure;
        public ContourPoint[] Normals;
        public double[] MarkerDistances;
    }

    // No Room/Revit dependency: direction follows the topology of the selected line group.
    public static class ContourDirectionGeometry
    {
        private const double Epsilon = 1e-10;
        private static double Cross(ContourPoint a, ContourPoint b) { return a.X * b.Y - a.Y * b.X; }
        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }

        public static ContourDirectionResult Build(IList<ContourSegment> segments, double requestedDistance, double joinTolerance)
        {
            if (segments == null || segments.Count == 0) throw new ArgumentException("Группа не содержит линий.");
            if (!Finite(requestedDistance) || requestedDistance < 0 || !Finite(joinTolerance) || joinTolerance <= 0)
                throw new ArgumentException("Некорректное смещение или допуск соединения линий.");
            double minimumLength = double.MaxValue;
            foreach (var segment in segments)
            {
                if (segment == null || !Finite(segment.Start.X) || !Finite(segment.Start.Y) || !Finite(segment.End.X) || !Finite(segment.End.Y))
                    throw new ArgumentException("Некорректные координаты линии.");
                minimumLength = Math.Min(minimumLength, (segment.End - segment.Start).Length);
            }
            if (minimumLength <= Epsilon) throw new ArgumentException("Группа содержит линию нулевой длины.");
            double tolerance = Math.Min(joinTolerance, minimumLength * 0.1);
            var points = new List<ContourPoint>();
            var incident = new List<List<int>>();
            var first = new int[segments.Count]; var second = new int[segments.Count];
            for (int i = 0; i < segments.Count; i++)
            {
                first[i] = Node(points, incident, segments[i].Start, tolerance);
                second[i] = Node(points, incident, segments[i].End, tolerance);
                if (first[i] == second[i]) throw new ArgumentException("Концы линии совпадают в пределах допуска.");
                incident[first[i]].Add(i); incident[second[i]].Add(i);
            }
            if (incident.Any(edges => edges.Count > 2)) throw new ArgumentException("В группе есть разветвление. Разделите её на отдельные цепочки.");
            var ends = Enumerable.Range(0, points.Count).Where(i => incident[i].Count == 1).ToList();
            bool closed = ends.Count == 0;
            if (!closed && ends.Count != 2) throw new ArgumentException("Группа содержит несколько несоединённых цепочек.");
            if (closed && points.Count < 3) throw new ArgumentException("Замкнутый контур должен содержать минимум три угла.");
            var visited = new HashSet<int>(); var orderedPoints = new List<ContourPoint>();
            var orderedEdges = new List<int>(); var directions = new List<ContourPoint>();
            int current = closed ? first[0] : ends[0];
            orderedPoints.Add(points[current]);
            while (true)
            {
                int edge = incident[current].Where(i => !visited.Contains(i)).DefaultIfEmpty(-1).First();
                if (edge < 0) break;
                visited.Add(edge); orderedEdges.Add(edge);
                int next = first[edge] == current ? second[edge] : first[edge];
                var direction = first[edge] == current ? segments[edge].End - segments[edge].Start : segments[edge].Start - segments[edge].End;
                directions.Add(direction * (1.0 / direction.Length));
                orderedPoints.Add(points[next]); current = next;
            }
            if (visited.Count != segments.Count) throw new ArgumentException("Группа содержит отдельные контуры. Выберите каждый контур отдельной группой.");
            for (int i = 0; i < segments.Count; i++)
                for (int j = i + 1; j < segments.Count; j++)
                {
                    int shared = first[i] == first[j] || first[i] == second[j] ? first[i] :
                        second[i] == first[j] || second[i] == second[j] ? second[i] : -1;
                    if (shared >= 0)
                    {
                        var a = points[first[i] == shared ? second[i] : first[i]] - points[shared];
                        var b = points[first[j] == shared ? second[j] : first[j]] - points[shared];
                        if (Math.Abs(Cross(a, b)) <= tolerance * Math.Min(a.Length, b.Length) && a.X * b.X + a.Y * b.Y > 0)
                            throw new ArgumentException("Линии группы накладываются или возвращаются по уже выбранному участку.");
                        continue;
                    }
                    if (Intersects(segments[i], segments[j], tolerance)) throw new ArgumentException("Линии группы пересекаются. Исправьте контур перед предпросмотром.");
                }
            double area = 0;
            for (int i = 0; i + 1 < orderedPoints.Count; i++) area += Cross(orderedPoints[i], orderedPoints[i + 1]);
            if (closed && Math.Abs(area) <= tolerance * tolerance) throw new ArgumentException("Замкнутый контур имеет нулевую площадь.");
            var boundaries = new List<ContourSegment>(segments);
            bool virtualClosure = false;
            if (!closed && segments.Count >= 2)
            {
                var closure = new ContourSegment(orderedPoints[orderedPoints.Count - 1], orderedPoints[0]);
                double closedArea = area + Cross(closure.Start, closure.End);
                if (Math.Abs(closedArea) > tolerance * tolerance && CanClose(segments, orderedEdges, closure, tolerance))
                {
                    virtualClosure = true; area = closedArea; boundaries.Add(closure);
                }
            }
            bool hasInterior = closed || virtualClosure;
            double sign = hasInterior && area < 0 ? -1.0 : 1.0;
            var result = new ContourDirectionResult { IsClosed = closed, UsesVirtualClosure = virtualClosure, Normals = new ContourPoint[segments.Count], MarkerDistances = new double[segments.Count] };
            for (int i = 0; i < orderedEdges.Count; i++)
            {
                int edge = orderedEdges[i]; var d = directions[i];
                var normal = new ContourPoint(-d.Y * sign, d.X * sign);
                result.Normals[edge] = normal;
                double distance = requestedDistance;
                if (hasInterior && distance > Epsilon)
                {
                    var middle = (segments[edge].Start + segments[edge].End) * 0.5;
                    double clearance = double.MaxValue;
                    foreach (var boundary in boundaries)
                    {
                        var v = boundary.End - boundary.Start;
                        double denominator = Cross(normal, v);
                        if (Math.Abs(denominator) <= Epsilon) continue;
                        var delta = boundary.Start - middle;
                        double t = Cross(delta, v) / denominator, u = Cross(delta, normal) / denominator;
                        if (t > tolerance && u >= -Epsilon && u <= 1 + Epsilon) clearance = Math.Min(clearance, t);
                    }
                    if (distance >= clearance) distance = clearance * 0.5;
                }
                result.MarkerDistances[edge] = distance;
            }
            return result;
        }

        private static bool CanClose(IList<ContourSegment> segments, IList<int> orderedEdges, ContourSegment closure, double tolerance)
        {
            for (int i = 0; i < orderedEdges.Count; i++)
            {
                var edge = segments[orderedEdges[i]];
                if (i == 0 || i == orderedEdges.Count - 1)
                {
                    // At either free endpoint, the virtual edge must not retrace its neighbour.
                    var shared = i == 0 ? closure.End : closure.Start;
                    var away = (edge.Start - shared).Length <= tolerance ? edge.End - edge.Start : edge.Start - edge.End;
                    var closing = i == 0 ? closure.Start - closure.End : closure.End - closure.Start;
                    if (Math.Abs(Cross(away, closing)) <= tolerance * Math.Min(away.Length, closing.Length) &&
                        away.X * closing.X + away.Y * closing.Y > 0) return false;
                }
                else if (Intersects(edge, closure, tolerance)) return false;
            }
            return true;
        }

        private static int Node(List<ContourPoint> points, List<List<int>> incident, ContourPoint point, double tolerance)
        {
            for (int i = 0; i < points.Count; i++) if ((points[i] - point).Length <= tolerance) return i;
            points.Add(point); incident.Add(new List<int>()); return points.Count - 1;
        }

        private static bool Intersects(ContourSegment a, ContourSegment b, double tolerance)
        {
            var u = a.End - a.Start; var v = b.End - b.Start; var delta = b.Start - a.Start;
            double denominator = Cross(u, v);
            if (Math.Abs(denominator) <= Epsilon)
            {
                if (Math.Abs(Cross(delta, u)) > tolerance * u.Length) return false;
                double lengthSquared = u.X * u.X + u.Y * u.Y;
                double t0 = (delta.X * u.X + delta.Y * u.Y) / lengthSquared;
                var endDelta = b.End - a.Start;
                double t1 = (endDelta.X * u.X + endDelta.Y * u.Y) / lengthSquared;
                return Math.Max(Math.Min(t0, t1), 0) <= Math.Min(Math.Max(t0, t1), 1);
            }
            double t = Cross(delta, v) / denominator, s = Cross(delta, u) / denominator;
            return t >= 0 && t <= 1 && s >= 0 && s <= 1;
        }
    }
}
