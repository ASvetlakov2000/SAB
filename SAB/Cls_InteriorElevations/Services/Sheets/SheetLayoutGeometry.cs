using System;
using System.Collections.Generic;
using System.Linq;

namespace SAB.InteriorElevations.Services.Sheets
{
    public static class SheetPlacementAttempt
    {
        // The caller owns the Revit sub-transaction; rollback always precedes forced placement.
        public static T Run<T>(Func<T> automatic, Func<T, string> incompleteReason,
            Action commit, Action rollback, Func<string, T> forced)
        {
            string reason;
            try
            {
                var result = automatic();
                reason = incompleteReason(result);
                if (reason == null) { commit(); return result; }
            }
            catch (Exception exception) { reason = exception.Message; }
            rollback();
            return forced(reason);
        }
    }

    // Paper coordinates; independent of Revit so the layout rules can be tested.
    public sealed class SheetRectangle
    {
        public double Left { get; private set; }
        public double Bottom { get; private set; }
        public double Right { get; private set; }
        public double Top { get; private set; }
        public double Width { get { return Right - Left; } }
        public double Height { get { return Top - Bottom; } }

        public SheetRectangle(double left, double bottom, double right, double top)
        {
            if (!Finite(left) || !Finite(bottom) || !Finite(right) || !Finite(top) || right <= left || top <= bottom)
                throw new ArgumentException("Некорректные границы области листа.");
            Left = left; Bottom = bottom; Right = right; Top = top;
        }

        public bool Contains(SheetRectangle other, double tolerance = 1e-8)
        {
            return other.Left >= Left - tolerance && other.Right <= Right + tolerance &&
                   other.Bottom >= Bottom - tolerance && other.Top <= Top + tolerance;
        }

        public bool Intersects(SheetRectangle other, double gapX = 0, double gapY = 0)
        {
            return Left < other.Right + gapX - 1e-8 && Right > other.Left - gapX + 1e-8 &&
                   Bottom < other.Top + gapY - 1e-8 && Top > other.Bottom - gapY + 1e-8;
        }

        public SheetRectangle Union(SheetRectangle other)
        {
            return new SheetRectangle(Math.Min(Left, other.Left), Math.Min(Bottom, other.Bottom),
                Math.Max(Right, other.Right), Math.Max(Top, other.Top));
        }

        public SheetRectangle Translate(double x, double y)
        {
            return new SheetRectangle(Left + x, Bottom + y, Right + x, Top + y);
        }

        public static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }

    public sealed class SheetPackingItem
    {
        public long Key { get; set; }
        public int Group { get; set; }
        public long? CompanionKey { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double? AnchorFromTop { get; set; }
    }

    public static class SheetPlanPosition
    {
        public static SheetRectangle Bounds(SheetRectangle area, double width, double height, double rightOffset, double bottomOffset)
        {
            if (!SheetRectangle.Finite(rightOffset) || !SheetRectangle.Finite(bottomOffset) || rightOffset < 0 || bottomOffset < 0)
                throw new ArgumentException("Отступы план-схемы должны быть неотрицательными.");
            double right = area.Right - rightOffset, bottom = area.Bottom + bottomOffset;
            return new SheetRectangle(right - width, bottom, right, bottom + height);
        }

        public static void Offsets(SheetRectangle area, double x, double y, out double right, out double bottom)
        {
            if (!SheetRectangle.Finite(x) || !SheetRectangle.Finite(y) || x < area.Left || x > area.Right || y < area.Bottom || y > area.Top)
                throw new ArgumentException("Точка план-схемы должна находиться внутри рабочей области листа.");
            right = area.Right - x; bottom = y - area.Bottom;
        }
    }

    public sealed class SheetPackingCompanion
    {
        public long Key { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool AlreadyOnFirstSheet { get; set; }
        public bool FixedPosition { get; set; }
        public double OffsetRight { get; set; }
        public double OffsetBottom { get; set; }
    }

    public sealed class SheetPackedItem
    {
        public long Key { get; set; }
        public SheetRectangle Bounds { get; set; }
    }

    public sealed class SheetPackingPage
    {
        public int Index { get; set; }
        public List<SheetPackedItem> Views { get; private set; } = new List<SheetPackedItem>();
        public List<SheetPackedItem> Plans { get; private set; } = new List<SheetPackedItem>();
    }

    public sealed class SheetPackingResult
    {
        public List<SheetPackingPage> Pages { get; private set; } = new List<SheetPackingPage>();
        public List<long> RejectedKeys { get; private set; } = new List<long>();
    }

    public sealed class SheetPackingService
    {
        public SheetPackingResult Pack(SheetRectangle area, IList<SheetPackingItem> items,
            IDictionary<long, SheetPackingCompanion> companions, double gapX, double gapY,
            IList<SheetRectangle> firstSheetObstacles, IList<SheetRectangle> allSheetObstacles = null)
        {
            if (!SheetRectangle.Finite(gapX) || !SheetRectangle.Finite(gapY) || gapX < 0 || gapY < 0)
                throw new ArgumentException("Зазоры между развертками должны быть неотрицательными.");
            var result = new SheetPackingResult();
            var pending = new List<SheetPackingItem>();
            SheetPackingPage current = null;
            int pageIndex = 0;
            foreach (var item in items)
            {
                if (!SheetRectangle.Finite(item.Width) || !SheetRectangle.Finite(item.Height) ||
                    item.Width <= 0 || item.Height <= 0 ||
                    (item.AnchorFromTop.HasValue && !SheetRectangle.Finite(item.AnchorFromTop.Value)))
                    throw new ArgumentException("Не удалось измерить оформленную развертку.");
                var candidate = new List<SheetPackingItem>(pending) { item };
                SheetPackingPage trial;
                if (TryPage(area, candidate, companions, gapX, gapY, pageIndex, firstSheetObstacles, allSheetObstacles, out trial))
                {
                    pending = candidate; current = trial; continue;
                }
                SheetPackingPage emptyPage;
                if (!TryPage(area, new List<SheetPackingItem> { item }, companions, gapX, gapY,
                    1, null, allSheetObstacles, out emptyPage))
                {
                    result.RejectedKeys.Add(item.Key); continue;
                }
                if (current != null) { result.Pages.Add(current); pageIndex++; }
                else if (pageIndex == 0 && firstSheetObstacles != null && firstSheetObstacles.Count > 0) { pageIndex++; }
                pending.Clear(); current = null;
                if (TryPage(area, new List<SheetPackingItem> { item }, companions, gapX, gapY,
                    pageIndex, firstSheetObstacles, allSheetObstacles, out trial))
                {
                    pending.Add(item); current = trial;
                }
                else { result.RejectedKeys.Add(item.Key); }
            }
            if (current != null) result.Pages.Add(current);
            return result;
        }

        private bool TryPage(SheetRectangle area, IList<SheetPackingItem> items,
            IDictionary<long, SheetPackingCompanion> companions, double gapX, double gapY, int pageIndex,
            IList<SheetRectangle> firstSheetObstacles, IList<SheetRectangle> allSheetObstacles, out SheetPackingPage page)
        {
            return TryFlexiblePage(area, items, companions, gapX, gapY, pageIndex, firstSheetObstacles, allSheetObstacles, out page);
        }

        // Elevations wrap in the free space beside actual plan and stamp rectangles.
        private bool TryFlexiblePage(SheetRectangle area, IList<SheetPackingItem> items,
            IDictionary<long, SheetPackingCompanion> companions, double gapX, double gapY, int pageIndex,
            IList<SheetRectangle> firstSheetObstacles, IList<SheetRectangle> allSheetObstacles, out SheetPackingPage page)
        {
            var fixedObstacles = pageIndex == 0 && firstSheetObstacles != null
                ? new List<SheetRectangle>(firstSheetObstacles) : new List<SheetRectangle>();
            if (allSheetObstacles != null) fixedObstacles.AddRange(allSheetObstacles);
            var plans = new List<SheetPackingCompanion>();
            var keys = new HashSet<long>();
            foreach (var item in items)
            {
                if (!item.CompanionKey.HasValue || !keys.Add(item.CompanionKey.Value)) continue;
                SheetPackingCompanion plan;
                if (!companions.TryGetValue(item.CompanionKey.Value, out plan))
                    throw new ArgumentException("Для развертки не найдена план-схема.");
                if (pageIndex == 0 && plan.AlreadyOnFirstSheet) continue;
                if (!SheetRectangle.Finite(plan.Width) || !SheetRectangle.Finite(plan.Height) || plan.Width <= 0 || plan.Height <= 0)
                    throw new ArgumentException("Не удалось измерить план-схему.");
                plans.Add(plan);
            }
            // Keep plans at the lower right, preserving the top-left start for elevations.
            var trial = new SheetPackingPage { Index = pageIndex };
            var obstacles = new List<SheetRectangle>(fixedObstacles);
            foreach (var plan in plans)
            {
                SheetRectangle bounds;
                if (!TryPlacePlan(area, plan, obstacles, gapX, gapY, out bounds)) { page = null; return false; }
                trial.Plans.Add(new SheetPackedItem { Key = plan.Key, Bounds = bounds });
                obstacles.Add(bounds);
            }
            if (TryElevationRows(area, items, obstacles, gapX, gapY, trial.Views)) { page = trial; return true; }
            page = null; return false;
        }

        private bool TryPlacePlan(SheetRectangle area, SheetPackingCompanion plan,
            IList<SheetRectangle> obstacles, double gapX, double gapY, out SheetRectangle bounds)
        {
            bounds = null;
            if (plan.FixedPosition)
            {
                var fixedBounds = SheetPlanPosition.Bounds(area, plan.Width, plan.Height, plan.OffsetRight, plan.OffsetBottom);
                if (!area.Contains(fixedBounds) || Collides(fixedBounds, obstacles, gapX, gapY)) return false;
                bounds = fixedBounds; return true;
            }
            if (plan.Width > area.Width + 1e-8 || plan.Height > area.Height + 1e-8) return false;
            var xs = new List<double> { area.Left, area.Right - plan.Width };
            var ys = new List<double> { area.Bottom, area.Top - plan.Height };
            foreach (var obstacle in obstacles)
            {
                xs.Add(obstacle.Left - gapX - plan.Width); xs.Add(obstacle.Right + gapX);
                ys.Add(obstacle.Bottom - gapY - plan.Height); ys.Add(obstacle.Top + gapY);
            }
            xs = xs.OrderByDescending(x => x).Distinct().ToList();
            ys = ys.OrderBy(y => y).Distinct().ToList();
            foreach (double x in xs)
                foreach (double y in ys)
                {
                    var candidate = new SheetRectangle(x, y, x + plan.Width, y + plan.Height);
                    if (area.Contains(candidate) && !Collides(candidate, obstacles, gapX, gapY)) { bounds = candidate; return true; }
                }
            return false;
        }

        private bool TryElevationRows(SheetRectangle area, IList<SheetPackingItem> items,
            IList<SheetRectangle> obstacles, double gapX, double gapY, IList<SheetPackedItem> placed)
        {
            double top = area.Top;
            int start = 0;
            while (start < items.Count)
            {
                List<SheetPackedItem> best = null;
                double bestBottom = top;
                int next = start;
                for (int end = start + 1; end <= items.Count && items[end - 1].Group == items[start].Group; end++)
                {
                    List<SheetPackedItem> row; double bottom;
                    if (!TryElevationRow(area, items, start, end, top, obstacles, gapX, gapY, out row, out bottom)) break;
                    best = row; bestBottom = bottom; next = end;
                }
                if (best != null)
                {
                    foreach (var item in best) placed.Add(item);
                    start = next; top = bestBottom - gapY;
                    continue;
                }
                // A row may fit below an obstruction even if it cannot fit at this height.
                double nextTop = double.MinValue;
                foreach (var obstacle in obstacles)
                    if (obstacle.Bottom - gapY < top - 1e-8) nextTop = Math.Max(nextTop, obstacle.Bottom - gapY);
                if (nextTop <= area.Bottom + 1e-8) return false;
                top = nextTop;
            }
            return true;
        }

        private bool TryElevationRow(SheetRectangle area, IList<SheetPackingItem> items, int start, int end,
            double top, IList<SheetRectangle> obstacles, double gapX, double gapY,
            out List<SheetPackedItem> row, out double bottom)
        {
            row = new List<SheetPackedItem>();
            bool align = false;
            for (int i = start; i < end; i++) align |= items[i].AnchorFromTop.HasValue;
            double above = double.MinValue, below = double.MinValue;
            for (int i = start; i < end; i++)
            {
                double anchor = align ? (items[i].AnchorFromTop ?? items[i].Height) : 0;
                above = Math.Max(above, anchor); below = Math.Max(below, items[i].Height - anchor);
            }
            bottom = top - above - below;
            if (bottom < area.Bottom - 1e-8) return false;
            double cursor = area.Left;
            for (int i = start; i < end; i++)
            {
                var item = items[i];
                double itemTop = top - above + (align ? (item.AnchorFromTop ?? item.Height) : 0);
                var xs = new List<double> { cursor };
                foreach (var obstacle in obstacles) if (obstacle.Right + gapX >= cursor) xs.Add(obstacle.Right + gapX);
                SheetRectangle chosen = null;
                foreach (double x in xs.Distinct().OrderBy(x => x))
                {
                    var candidate = new SheetRectangle(x, itemTop - item.Height, x + item.Width, itemTop);
                    if (area.Contains(candidate) && !Collides(candidate, obstacles, gapX, gapY)) { chosen = candidate; break; }
                }
                if (chosen == null) return false;
                row.Add(new SheetPackedItem { Key = item.Key, Bounds = chosen });
                cursor = chosen.Right + gapX;
            }
            return true;
        }

        private bool Collides(SheetRectangle bounds, IList<SheetRectangle> obstacles, double gapX, double gapY)
        {
            foreach (var obstacle in obstacles) if (bounds.Intersects(obstacle, gapX, gapY)) return true;
            return false;
        }
    }
}
