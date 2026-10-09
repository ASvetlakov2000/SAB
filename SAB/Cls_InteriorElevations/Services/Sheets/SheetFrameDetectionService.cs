using System;
using System.Collections.Generic;
using System.Linq;

namespace SAB.InteriorElevations.Services.Sheets
{
    public sealed class SheetFrameLine
    {
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
    }

    public sealed class SheetFrames
    {
        public SheetRectangle Outer { get; set; }
        public SheetRectangle Inner { get; set; }
        public List<SheetRectangle> ReservedAreas { get; private set; } = new List<SheetRectangle>();

        public SheetRectangle GetWorkspace(double startX, double startY)
        {
            double horizontalMargin = startX - Inner.Left;
            double verticalMargin = Inner.Top - startY;
            if (!SheetRectangle.Finite(horizontalMargin) || !SheetRectangle.Finite(verticalMargin) ||
                horizontalMargin < -1e-8 || verticalMargin < -1e-8 ||
                horizontalMargin * 2 >= Inner.Width || verticalMargin * 2 >= Inner.Height)
                throw new InvalidOperationException("Начальная точка должна находиться внутри внутренней рамки в верхней левой части листа.");
            horizontalMargin = Math.Max(0, horizontalMargin); verticalMargin = Math.Max(0, verticalMargin);
            return new SheetRectangle(Inner.Left + horizontalMargin, Inner.Bottom + verticalMargin,
                Inner.Right - horizontalMargin, Inner.Top - verticalMargin);
        }
    }

    public sealed class SheetFrameDetectionService
    {
        private sealed class Interval
        {
            public double Position, Start, End;
        }

        public SheetFrames Detect(IList<SheetFrameLine> lines, double tolerance)
        {
            var horizontal = new List<Interval>(); var vertical = new List<Interval>();
            foreach (var line in lines)
            {
                if (!SheetRectangle.Finite(line.X1) || !SheetRectangle.Finite(line.X2) ||
                    !SheetRectangle.Finite(line.Y1) || !SheetRectangle.Finite(line.Y2)) continue;
                if (Math.Abs(line.Y1 - line.Y2) <= tolerance && Math.Abs(line.X1 - line.X2) > tolerance)
                    horizontal.Add(new Interval { Position = (line.Y1 + line.Y2) / 2,
                        Start = Math.Min(line.X1, line.X2), End = Math.Max(line.X1, line.X2) });
                else if (Math.Abs(line.X1 - line.X2) <= tolerance && Math.Abs(line.Y1 - line.Y2) > tolerance)
                    vertical.Add(new Interval { Position = (line.X1 + line.X2) / 2,
                        Start = Math.Min(line.Y1, line.Y2), End = Math.Max(line.Y1, line.Y2) });
            }
            var xs = Coordinates(vertical, tolerance); var ys = Coordinates(horizontal, tolerance);
            var rectangles = new List<SheetRectangle>();
            for (int left = 0; left < xs.Count; left++)
                for (int right = left + 1; right < xs.Count; right++)
                {
                    var sides = ys.Where(y => Covers(horizontal, y, xs[left], xs[right], tolerance)).ToList();
                    for (int bottom = 0; bottom < sides.Count; bottom++)
                        for (int top = bottom + 1; top < sides.Count; top++)
                            if (Covers(vertical, xs[left], sides[bottom], sides[top], tolerance) &&
                                Covers(vertical, xs[right], sides[bottom], sides[top], tolerance))
                                rectangles.Add(new SheetRectangle(xs[left], sides[bottom], xs[right], sides[top]));
                }
            var ordered = rectangles.OrderByDescending(r => r.Width * r.Height).ToList();
            // Reject stamp cells and mixed combinations of outer and inner frame edges.
            foreach (var outer in ordered)
            {
                var inner = ordered.FirstOrDefault(r =>
                    r.Left > outer.Left + tolerance && r.Right < outer.Right - tolerance &&
                    r.Bottom > outer.Bottom + tolerance && r.Top < outer.Top - tolerance &&
                    r.Width >= outer.Width * 0.65 && r.Height >= outer.Height * 0.65);
                if (inner != null)
                {
                    var frames = new SheetFrames { Outer = outer, Inner = inner };
                    foreach (var stamp in ordered.Where(r => inner.Contains(r, tolerance) &&
                        Math.Abs(r.Bottom - inner.Bottom) <= tolerance && Math.Abs(r.Right - inner.Right) <= tolerance &&
                        r.Width >= inner.Width * 0.1 && r.Height >= inner.Height * 0.03 &&
                        r.Width * r.Height <= inner.Width * inner.Height * 0.3))
                        if (!frames.ReservedAreas.Any(r => r.Contains(stamp, tolerance))) frames.ReservedAreas.Add(stamp);
                    return frames;
                }
            }
            throw new InvalidOperationException("Не удалось распознать две замкнутые прямоугольные рамки листа. Размещение отменено; произвольный отступ 5 мм не применяется.");
        }

        private List<double> Coordinates(IList<Interval> intervals, double tolerance)
        {
            var result = new List<double>();
            foreach (var value in intervals.Select(i => i.Position).OrderBy(p => p))
                if (result.Count == 0 || value - result[result.Count - 1] > tolerance) result.Add(value);
            return result;
        }

        private bool Covers(IList<Interval> intervals, double position, double start, double end, double tolerance)
        {
            double covered = start;
            foreach (var interval in intervals.Where(i => Math.Abs(i.Position - position) <= tolerance)
                .OrderBy(i => i.Start))
            {
                if (interval.End < covered - tolerance) continue;
                if (interval.Start > covered + tolerance) return false;
                covered = Math.Max(covered, interval.End);
                if (covered >= end - tolerance) return true;
            }
            return false;
        }
    }
}
