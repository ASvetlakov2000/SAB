using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using SAB.GklFrame.Models;

namespace SAB.GklFrame.Services
{
    // Контур берётся из геометрии стены после всех вырезов, а не из параметров
    // дверного/оконного семейства. Поэтому сквозной вырез обобщённой модели виден
    // здесь точно так же, как обычное окно.
    public sealed class WallContourOpeningCollector : IWallOpeningCollector
    {
        private struct Point2
        {
            public double X;
            public double Z;

            public Point2(double x, double z)
            {
                X = x;
                Z = z;
            }
        }

        private struct Rectangle2
        {
            public double Left;
            public double Right;
            public double Bottom;
            public double Top;
        }

        public bool TryCollect(
            Wall sourceWall,
            Line wallLine,
            double baseElevationInternal,
            double wallHeightInternal,
            out IList<WallOpeningData> openings,
            out string error)
        {
            openings = new List<WallOpeningData>();
            error = string.Empty;

            XYZ start = wallLine.GetEndPoint(0);
            XYZ delta = wallLine.GetEndPoint(1) - start;
            XYZ direction = new XYZ(delta.X, delta.Y, 0.0).Normalize();
            double wallLength = new XYZ(delta.X, delta.Y, 0.0).GetLength();
            double tolerance = RevitUnitService.MillimetersToInternal(FrameModuleConstants.GeometryToleranceMm);

            PlanarFace sideFace;
            try
            {
                sideFace = HostObjectUtils.GetSideFaces(sourceWall, ShellLayerType.Exterior)
                    .Select(reference => sourceWall.GetGeometryObjectFromReference(reference) as PlanarFace)
                    .Where(face => face != null && Math.Abs(face.FaceNormal.Z) < 0.01)
                    .OrderByDescending(face => face.Area)
                    .FirstOrDefault();
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException exception)
            {
                error = "Не удалось получить боковой контур стены: " + exception.Message;
                return false;
            }

            if (sideFace == null)
            {
                error = "Боковая грань стены не является плоской. Нельзя надёжно построить каркас по её контуру.";
                return false;
            }

            List<List<Point2>> loops = new List<List<Point2>>();
            foreach (EdgeArray edgeLoop in sideFace.EdgeLoops)
            {
                List<Point2> points = new List<Point2>();
                Point2? previousEnd = null;
                foreach (Edge edge in edgeLoop)
                {
                    Line segment = edge.AsCurveFollowingFace(sideFace) as Line;
                    if (segment == null)
                    {
                        error = "Контур стены содержит криволинейный проём; для него нельзя безопасно создать прямоугольный каркас.";
                        return false;
                    }

                    XYZ first = segment.GetEndPoint(0);
                    XYZ second = segment.GetEndPoint(1);
                    Point2 projectedStart = new Point2(
                        (first - start).DotProduct(direction), first.Z - baseElevationInternal);
                    Point2 projectedEnd = new Point2(
                        (second - start).DotProduct(direction), second.Z - baseElevationInternal);
                    if (previousEnd.HasValue && Distance(previousEnd.Value, projectedStart) > tolerance)
                    {
                        error = "Рёбра боковой грани стены не образуют непрерывный контур.";
                        return false;
                    }

                    points.Add(projectedStart);
                    previousEnd = projectedEnd;
                }

                if (points.Count >= 4)
                {
                    if (previousEnd.HasValue && Distance(previousEnd.Value, points[0]) > tolerance)
                    {
                        error = "Боковая грань стены содержит незамкнутый контур.";
                        return false;
                    }

                    loops.Add(points);
                }
            }

            if (loops.Count == 0)
            {
                error = "Не удалось прочитать замкнутый контур боковой грани стены.";
                return false;
            }

            List<Point2> outer = loops.OrderByDescending(loop => Math.Abs(Area(loop))).First();
            Rectangle2 bounds = Bounds(outer);
            // Примыкание другой стены может подрезать видимую боковую грань у
            // торца, хотя линия расположения исходной стены не меняется.
            // Допускаем такое смещение только в пределах толщины стены.
            double endJoinTolerance = sourceWall.Width + tolerance;
            if (Math.Abs(bounds.Left) > endJoinTolerance ||
                Math.Abs(bounds.Right - wallLength) > endJoinTolerance ||
                Math.Abs(bounds.Bottom) > tolerance || Math.Abs(bounds.Top - wallHeightInternal) > tolerance)
            {
                error = "Фактический наружный контур стены не совпадает с прямой стеной постоянной высоты. " +
                    "Отклонения границ от линии стены, мм: слева " +
                    Math.Round(RevitUnitService.InternalToMillimeters(bounds.Left), 1) +
                    ", справа " +
                    Math.Round(RevitUnitService.InternalToMillimeters(bounds.Right - wallLength), 1) +
                    ", снизу " +
                    Math.Round(RevitUnitService.InternalToMillimeters(bounds.Bottom), 1) +
                    ", сверху " +
                    Math.Round(RevitUnitService.InternalToMillimeters(bounds.Top - wallHeightInternal), 1) + ".";
                return false;
            }

            List<Rectangle2> rectangles = new List<Rectangle2>();
            double notchArea = 0.0;
            for (int index = 0; index < outer.Count; index++)
            {
                Point2 first = outer[index];
                Point2 second = outer[(index + 1) % outer.Count];
                Point2 previous = outer[(index + outer.Count - 1) % outer.Count];
                Point2 next = outer[(index + 2) % outer.Count];
                if (!IsAxisAligned(first, second, tolerance))
                {
                    error = "Наружный контур стены имеет наклонные участки; прямоугольный каркас не создаётся.";
                    return false;
                }

                if (Math.Abs(first.Z - second.Z) <= tolerance &&
                    first.Z > tolerance && first.Z < wallHeightInternal - tolerance &&
                    Math.Abs(previous.X - first.X) <= tolerance &&
                    Math.Abs(next.X - second.X) <= tolerance &&
                    Math.Abs(previous.Z) <= tolerance && Math.Abs(next.Z) <= tolerance)
                {
                    Rectangle2 notch = new Rectangle2
                    {
                        Left = Math.Min(first.X, second.X),
                        Right = Math.Max(first.X, second.X),
                        Bottom = 0.0,
                        Top = (first.Z + second.Z) / 2.0
                    };
                    rectangles.Add(notch);
                    notchArea += (notch.Right - notch.Left) * notch.Top;
                }
            }

            // Площадь сравниваем с фактическим прямоугольником боковой грани:
            // номинальная длина LocationCurve отличается у соединённых стен.
            double expectedArea = (bounds.Right - bounds.Left) *
                                  (bounds.Top - bounds.Bottom) - notchArea;
            if (Math.Abs(Math.Abs(Area(outer)) - expectedArea) >
                tolerance * (wallLength + wallHeightInternal) * 4.0)
            {
                error = "Наружный контур стены содержит форму, отличную от прямоугольника с дверными выемками.";
                return false;
            }

            foreach (List<Point2> loop in loops.Where(loop => !ReferenceEquals(loop, outer)))
            {
                Rectangle2 hole = Bounds(loop);
                double rectangleArea = (hole.Right - hole.Left) * (hole.Top - hole.Bottom);
                if (hole.Left <= tolerance || hole.Right >= wallLength - tolerance ||
                    hole.Bottom <= tolerance || hole.Top >= wallHeightInternal - tolerance ||
                    rectangleArea <= tolerance * tolerance ||
                    Math.Abs(Math.Abs(Area(loop)) - rectangleArea) >
                    tolerance * (hole.Right - hole.Left + hole.Top - hole.Bottom) * 4.0 ||
                    loop.Any(point => !OnRectangleBoundary(point, hole, tolerance)))
                {
                    error = "Внутренний проём стены не является отдельным прямоугольным сквозным отверстием.";
                    return false;
                }

                rectangles.Add(hole);
            }

            rectangles = rectangles.OrderBy(rectangle => rectangle.Left)
                .ThenBy(rectangle => rectangle.Bottom).ToList();
            for (int first = 0; first < rectangles.Count; first++)
            {
                for (int second = first + 1; second < rectangles.Count; second++)
                {
                    if (rectangles[first].Right > rectangles[second].Left + tolerance &&
                        rectangles[second].Right > rectangles[first].Left + tolerance &&
                        rectangles[first].Top > rectangles[second].Bottom + tolerance &&
                        rectangles[second].Top > rectangles[first].Bottom + tolerance)
                    {
                        error = "Контуры проёмов пересекаются; такую стену нельзя посчитать без объединения проёмов.";
                        return false;
                    }
                }
            }

            List<FamilyInstance> inserts = sourceWall.FindInserts(true, true, true, true)
                .Select(id => sourceWall.Document.GetElement(id) as FamilyInstance)
                .Where(instance => instance != null)
                .ToList();
            for (int index = 0; index < rectangles.Count; index++)
            {
                Rectangle2 rectangle = rectangles[index];
                FamilyInstance insert = inserts.FirstOrDefault(instance =>
                {
                    LocationPoint location = instance.Location as LocationPoint;
                    if (location == null)
                    {
                        return false;
                    }

                    double offset = (location.Point - start).DotProduct(direction);
                    double elevation = location.Point.Z - baseElevationInternal;
                    return offset >= rectangle.Left - tolerance && offset <= rectangle.Right + tolerance &&
                           elevation >= rectangle.Bottom - tolerance && elevation <= rectangle.Top + tolerance;
                });

                OpeningType type = OpeningType.Custom;
                if (insert != null && insert.Category != null)
                {
                    if (insert.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Doors)
                    {
                        type = OpeningType.Door;
                    }
                    else if (insert.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Windows)
                    {
                        type = OpeningType.Window;
                    }
                }

                openings.Add(new WallOpeningData
                {
                    SourceElementId = insert != null ? insert.Id : ElementId.InvalidElementId,
                    SourceUniqueId = insert != null ? insert.UniqueId :
                        sourceWall.UniqueId + ":contour:" + index.ToString(CultureInfo.InvariantCulture),
                    StartOffsetInternal = rectangle.Left,
                    EndOffsetInternal = rectangle.Right,
                    BottomOffsetInternal = rectangle.Bottom,
                    TopOffsetInternal = rectangle.Top,
                    OpeningType = type
                });
            }

            return true;
        }

        private static bool IsAxisAligned(Point2 first, Point2 second, double tolerance)
        {
            return Math.Abs(first.X - second.X) <= tolerance ||
                   Math.Abs(first.Z - second.Z) <= tolerance;
        }

        private static double Distance(Point2 first, Point2 second)
        {
            double dx = first.X - second.X;
            double dz = first.Z - second.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        private static bool OnRectangleBoundary(Point2 point, Rectangle2 bounds, double tolerance)
        {
            return (Math.Abs(point.X - bounds.Left) <= tolerance ||
                    Math.Abs(point.X - bounds.Right) <= tolerance ||
                    Math.Abs(point.Z - bounds.Bottom) <= tolerance ||
                    Math.Abs(point.Z - bounds.Top) <= tolerance) &&
                   point.X >= bounds.Left - tolerance && point.X <= bounds.Right + tolerance &&
                   point.Z >= bounds.Bottom - tolerance && point.Z <= bounds.Top + tolerance;
        }

        private static Rectangle2 Bounds(IList<Point2> loop)
        {
            return new Rectangle2
            {
                Left = loop.Min(point => point.X),
                Right = loop.Max(point => point.X),
                Bottom = loop.Min(point => point.Z),
                Top = loop.Max(point => point.Z)
            };
        }

        private static double Area(IList<Point2> loop)
        {
            double area = 0.0;
            for (int index = 0; index < loop.Count; index++)
            {
                Point2 first = loop[index];
                Point2 second = loop[(index + 1) % loop.Count];
                area += first.X * second.Z - second.X * first.Z;
            }

            return area / 2.0;
        }
    }
}
