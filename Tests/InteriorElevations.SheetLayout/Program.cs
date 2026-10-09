using System;
using System.Collections.Generic;
using System.Linq;
using SAB.InteriorElevations.Services.Sheets;

internal static class Program
{
    private static int _checks;
    private static readonly SheetRectangle Area = new SheetRectangle(10, 10, 210, 210);
    private static readonly SheetPackingService Packer = new SheetPackingService();

    private static void Main()
    {
        FramesAndMargins();
        WrapAndCompanions();
        ObstaclesAndAlignment();
        PlansBesideElevations();
        PlacementFallback();
        ManualPlanPosition();
        RandomPacking();
        Console.WriteLine("PASS: " + _checks + " assertions (frames, margins, wrapping, pagination, plans, alignment, obstacles, random layouts)");
    }

    private static void FramesAndMargins()
    {
        var lines = new List<SheetFrameLine>();
        AddFrame(lines, new SheetRectangle(-420, -297, 0, 0), true);
        AddFrame(lines, new SheetRectangle(-400, -292, -5, -5), true);
        AddFrame(lines, new SheetRectangle(-190, -292, -5, -237), false); // Stamp.
        var frames = new SheetFrameDetectionService().Detect(lines, 0.01);
        Check(Close(frames.Outer.Width, 420) && Close(frames.Inner.Width, 395), "Actual asymmetric inner frame");
        Check(frames.ReservedAreas.Count == 1 && Close(frames.ReservedAreas[0].Height, 55), "Stamp is reserved, not mistaken for inner frame");
        var area = frames.GetWorkspace(-390, -15);
        Check(Close(area.Left, -390) && Close(area.Right, -15) && Close(area.Top, -15) && Close(area.Bottom, -282), "Mirrored inner margins");
        ExpectFailure(() => frames.GetWorkspace(-410, -10), "Point outside inner frame");
        ExpectFailure(() => frames.GetWorkspace(-100, -10), "Point is not at top left");
        var missingSide = new List<SheetFrameLine>();
        AddFrame(missingSide, new SheetRectangle(0, 0, 420, 297), false);
        ExpectFailure(() => new SheetFrameDetectionService().Detect(missingSide, 0.01), "Never guess the second frame");
        var broken = new List<SheetFrameLine>(lines);
        broken.RemoveAt(5);
        ExpectFailure(() => new SheetFrameDetectionService().Detect(broken, 0.01), "Open outer frame is not paper");
        lines.Reverse();
        var reordered = new SheetFrameDetectionService().Detect(lines, 0.01);
        Check(Close(reordered.Inner.Left, frames.Inner.Left), "Detection ignores input order");
        ExpectFailure(() => frames.GetWorkspace(double.NaN, 0), "NaN point");
    }

    private static void WrapAndCompanions()
    {
        var items = Enumerable.Range(1, 6).Select(i => Item(i, 90, 60)).ToList();
        var result = Pack(items, 10, 10);
        Check(result.Pages.Count == 1, "Three rows fit exactly");
        Check(Close(result.Pages[0].Views[1].Bounds.Left, 110), "Horizontal gap is measured between full blocks");
        Check(Close(result.Pages[0].Views[2].Bounds.Top, 140), "Wrap uses row height and exact vertical gap");
        Validate(result, items, new Dictionary<long, SheetPackingCompanion>(), 10, 10);
        items.Add(Item(7, 90, 60));
        result = Pack(items, 10, 10);
        Check(result.Pages.Count == 2 && result.Pages[1].Views.Count == 1, "Bottom overflow creates a new page");
        var exact = Pack(new List<SheetPackingItem> { Item(1, 200, 200) }, 0, 0);
        Check(exact.Pages.Count == 1 && exact.RejectedKeys.Count == 0, "Exact boundary fit");
        items = new List<SheetPackingItem> { Item(1, 90, 60), Item(2, 201, 40), Item(3, 90, 60) };
        result = Pack(items, 10, 10);
        Check(result.RejectedKeys.SequenceEqual(new long[] { 2 }) && result.Pages.Count == 1, "Oversize item doesn't discard a usable page");
        Check(result.Pages[0].Views.Select(v => v.Key).SequenceEqual(new long[] { 1, 3 }), "Input order preserved after rejection");
        var plans = new Dictionary<long, SheetPackingCompanion> { [100] = new SheetPackingCompanion { Key = 100, Width = 80, Height = 50 } };
        items = Enumerable.Range(1, 6).Select(i => Item(i, 90, 60, 0, 100)).ToList();
        result = Pack(items, 10, 10, plans);
        Check(result.Pages.Count == 2 && result.Pages.All(p => p.Plans.Count == 1), "Plan reserved and repeated on each page");
        Check(result.Pages[0].Views.Count == 5, "Space beside bottom-right plan is used without bottom overflow");
        Check(Close(result.Pages[0].Plans[0].Bounds.Right, Area.Right) && Close(result.Pages[0].Plans[0].Bounds.Bottom, Area.Bottom), "Plan respects bottom-right margins");
        Validate(result, items, plans, 10, 10);
        plans[101] = new SheetPackingCompanion { Key = 101, Width = 80, Height = 50 };
        items = new List<SheetPackingItem> { Item(1, 90, 40, 0, 100), Item(2, 90, 40, 1, 101) };
        result = Pack(items, 10, 10, plans);
        Check(result.Pages.Count == 1 && result.Pages[0].Plans.Count == 2, "Multiple rooms keep their own plans on a shared page");
        Validate(result, items, plans, 10, 10);
        plans[101].Width = 300;
        result = Pack(items, 10, 10, plans);
        Check(result.RejectedKeys.Contains(2), "Oversized companion also prevents unsafe placement");
        plans[100].AlreadyOnFirstSheet = true;
        items = Enumerable.Range(1, 7).Select(i => Item(i, 90, 60, 0, 100)).ToList();
        result = Pack(items, 10, 10, plans);
        Check(result.Pages[0].Plans.Count == 0 && result.Pages[1].Plans.Count == 1, "Existing source plan remains untouched and repeats on continuation");
        ExpectFailure(() => Pack(new List<SheetPackingItem> { Item(1, double.NaN, 40) }, 10, 10), "NaN footprint");
        ExpectFailure(() => Pack(items, -1, 10), "Negative gap");
        Check(Pack(new List<SheetPackingItem>(), 10, 10).Pages.Count == 0, "Empty input does not create sheets");
    }

    private static void ObstaclesAndAlignment()
    {
        var items = new List<SheetPackingItem> { Item(1, 90, 60), Item(2, 90, 80) };
        items[0].AnchorFromTop = 45; items[1].AnchorFromTop = 65;
        var result = Pack(items, 10, 10);
        var first = result.Pages[0].Views[0].Bounds; var second = result.Pages[0].Views[1].Bounds;
        Check(Close(first.Top - 45, second.Top - 65), "Model alignment is retained despite different decorated heights");
        Validate(result, items, new Dictionary<long, SheetPackingCompanion>(), 10, 10);
        var obstacle = new SheetRectangle(10, 150, 100, 210);
        result = Pack(items, 10, 10, null, new List<SheetRectangle> { obstacle });
        Check(result.Pages[0].Views.All(v => !v.Bounds.Intersects(obstacle, 10, 10)), "Existing sheet contents are protected");
        Validate(result, items, new Dictionary<long, SheetPackingCompanion>(), 10, 10);
        result = Pack(items, 10, 10, null, new List<SheetRectangle> { Area });
        Check(result.Pages.Count == 1 && result.Pages[0].Index == 1, "Full existing sheet continues without creating empty intermediate pages");
        items[0].Group = 1; items[1].Group = 2;
        result = Pack(items, 10, 10);
        Check(result.Pages[0].Views[1].Bounds.Top <= result.Pages[0].Views[0].Bounds.Bottom - 10 + 1e-8, "Room groups start a separate row");
        var stamp = new SheetRectangle(100, 10, 210, 60);
        var plans = new Dictionary<long, SheetPackingCompanion> { [100] = new SheetPackingCompanion { Key = 100, Width = 150, Height = 40 } };
        items = Enumerable.Range(1, 8).Select(i => Item(i, 90, 45, 0, 100)).ToList();
        result = Packer.Pack(Area, items, plans, 10, 10, null, new List<SheetRectangle> { stamp });
        Check(result.Pages.Count > 1 && result.Pages.All(p => p.Plans[0].Bounds.Bottom >= 70), "Plans avoid the title block on every continuation");
        Check(result.Pages.SelectMany(p => p.Views.Concat(p.Plans)).All(v => !v.Bounds.Intersects(stamp, 10, 10)), "Every complete block avoids stamp");
        Validate(result, items, plans, 10, 10);
    }

    private static void RandomPacking()
    {
        var random = new Random(91407);
        for (int run = 0; run < 250; run++)
        {
            double gapX = random.Next(0, 16), gapY = random.Next(0, 16);
            var plans = new Dictionary<long, SheetPackingCompanion>();
            var items = new List<SheetPackingItem>();
            int group = 0;
            for (int i = 0; i < 35; i++)
            {
                if (i % 7 == 0) group++;
                long key = 1000 + group;
                if (!plans.ContainsKey(key)) plans[key] = new SheetPackingCompanion { Key = key, Width = random.Next(30, 140), Height = random.Next(20, 70) };
                var item = Item(i + 1, random.Next(20, 225), random.Next(15, 190), group, key);
                if (run % 2 == 0) item.AnchorFromTop = random.NextDouble() * item.Height;
                items.Add(item);
            }
            var result = Pack(items, gapX, gapY, plans);
            Validate(result, items, plans, gapX, gapY);
        }
    }

    private static void ManualPlanPosition()
    {
        var area = new SheetRectangle(-400, 5, -5, 292);
        var bounds = SheetPlanPosition.Bounds(area, 110, 70, 25, 65);
        Check(Close(bounds.Right, -30) && Close(bounds.Bottom, 70) && Close(bounds.Left, -140), "Offsets move complete plan left and up from lower-right workspace corner");
        double right, bottom;
        SheetPlanPosition.Offsets(area, bounds.Right, bounds.Bottom, out right, out bottom);
        Check(Close(right, 25) && Close(bottom, 65), "Click and numeric offsets round trip with negative sheet coordinates");
        ExpectFailure(() => SheetPlanPosition.Offsets(area, 1, 10, out right, out bottom), "Click outside workspace rejected");
        ExpectFailure(() => SheetPlanPosition.Bounds(area, 110, 70, -1, 0), "Negative plan offset rejected");
        var plan = new SheetPackingCompanion { Key = 100, Width = 80, Height = 50, FixedPosition = true, OffsetRight = 15, OffsetBottom = 70 };
        var plans = new Dictionary<long, SheetPackingCompanion> { [100] = plan };
        var items = Enumerable.Range(1, 7).Select(i => Item(i, 90, 60, 0, 100)).ToList();
        var stamp = new SheetRectangle(100, 10, 210, 60);
        var result = Packer.Pack(Area, items, plans, 10, 10, null, new List<SheetRectangle> { stamp });
        Check(result.RejectedKeys.Count == 0 && result.Pages.Count > 1, "Fixed plan above stamp allows automatic continuation");
        foreach (var page in result.Pages)
            Check(page.Plans.Count == 1 && Close(page.Plans[0].Bounds.Right, Area.Right - 15) &&
                Close(page.Plans[0].Bounds.Bottom, Area.Bottom + 70), "Every continuation retains exact manual plan anchor");
        Validate(result, items, plans, 10, 10);
        plan.OffsetBottom = 0;
        result = Packer.Pack(Area, items, plans, 10, 10, null, new List<SheetRectangle> { stamp });
        Check(result.RejectedKeys.Count == items.Count, "Fixed plan overlapping stamp fails without silently changing user anchor");
        plan.OffsetRight = 500;
        result = Packer.Pack(Area, items, plans, 10, 10, null);
        Check(result.RejectedKeys.Count == items.Count, "Anchor pushing complete plan outside workspace is rejected");
    }

    private static void PlacementFallback()
    {
        bool committed = false, rolledBack = false, forced = false;
        int result = SheetPlacementAttempt.Run(() => 14, count => count == 14 ? null : "partial",
            () => committed = true, () => rolledBack = true, reason => { forced = true; return 0; });
        Check(result == 14 && committed && !rolledBack && !forced, "Complete automatic result commits without forced placement");
        foreach (bool throwError in new[] { false, true })
        {
            committed = false; rolledBack = false;
            var temporaryViewports = new List<int>();
            var temporarySheets = new List<int>();
            result = SheetPlacementAttempt.Run(() =>
            {
                temporaryViewports.Add(1); temporarySheets.Add(2);
                if (throwError) throw new InvalidOperationException("frames missing");
                return 1;
            }, count => "partial", () => committed = true,
            () => { temporaryViewports.Clear(); temporarySheets.Clear(); rolledBack = true; },
            reason =>
            {
                Check(rolledBack && !committed && temporaryViewports.Count == 0 && temporarySheets.Count == 0,
                    "Temporary viewports and continuation sheets removed before forced placement");
                Check(reason == (throwError ? "frames missing" : "partial"), "Fallback preserves the actual reason");
                temporaryViewports.AddRange(Enumerable.Range(1, 14)); return 14;
            });
            Check(result == 14 && temporaryViewports.Distinct().Count() == 14 && temporarySheets.Count == 0,
                "Partial result or exception forces whole set once on original sheet");
        }
        bool failed = false;
        try
        {
            SheetPlacementAttempt.Run(() => 0, count => "partial", () => { }, () => { },
                reason => throw new InvalidOperationException("manual failed"));
        }
        catch (InvalidOperationException exception) { failed = exception.Message == "manual failed"; }
        Check(failed, "Forced-placement error is propagated instead of reported as success");
    }

    private static void PlansBesideElevations()
    {
        var plans = new Dictionary<long, SheetPackingCompanion> {
            [100] = new SheetPackingCompanion { Key = 100, Width = 100, Height = 170 } };
        var items = new List<SheetPackingItem> { Item(1, 80, 60, 0, 100), Item(2, 80, 60, 0, 100) };
        var result = Pack(items, 10, 10, plans);
        Check(result.RejectedKeys.Count == 0 && result.Pages.Count == 1, "Tall plan fits beside elevations instead of reserving a full-width band");
        Validate(result, items, plans, 10, 10);
        Check(Close(result.Pages[0].Views[0].Bounds.Left, Area.Left) && Close(result.Pages[0].Views[0].Bounds.Top, Area.Top), "First elevation retains top-left start beside plan");
        var stamp = new SheetRectangle(170, 10, 210, 60);
        plans[100].Width = 60; plans[100].Height = 130;
        result = Packer.Pack(Area, items, plans, 10, 10, null, new List<SheetRectangle> { stamp });
        Check(result.RejectedKeys.Count == 0, "Tall plan lifted above stamp still leaves space beside it");
        Validate(result, items, plans, 10, 10);
        Check(result.Pages.SelectMany(p => p.Views.Concat(p.Plans)).All(v => !v.Bounds.Intersects(stamp, 10, 10)), "Flexible layout protects stamp");
        plans[100].Width = 200; plans[100].Height = 170;
        result = Pack(items, 10, 10, plans);
        Check(result.RejectedKeys.Count == items.Count, "Truly oversized plan plus elevation remains rejected");
        var outside = Item(3, 80, 60); outside.AnchorFromTop = 500;
        result = Pack(new List<SheetPackingItem> { outside }, 10, 10);
        Check(result.RejectedKeys.Count == 0 && Close(result.Pages[0].Views[0].Bounds.Top, Area.Top), "Alignment point outside crop does not enlarge a single block");
        outside.AnchorFromTop = -500;
        result = Pack(new List<SheetPackingItem> { outside }, 10, 10);
        Check(result.RejectedKeys.Count == 0 && Close(result.Pages[0].Views[0].Bounds.Height, outside.Height), "Alignment above crop does not add imaginary empty space");
        // Measured A1 workspace/elevation sizes, with a tall synthetic companion.
        var a1 = new SheetRectangle(-801.04, 25.38, -24.96, 568.62);
        var a1Plans = new Dictionary<long, SheetPackingCompanion> { [100] = new SheetPackingCompanion { Key = 100, Width = 300, Height = 450 } };
        double[] widths = { 207.95, 121.98, 81.25, 81.25, 81.25, 94.35, 124.15, 94.35, 81.69, 82.40, 82.40, 83.11, 83.52, 96.12 };
        var a1Items = widths.Select((w, i) => Item(i + 1, w, 148.59, 0, 100)).ToList();
        var a1Stamp = new SheetRectangle(-190, 5, -5, 60);
        result = Packer.Pack(a1, a1Items, a1Plans, 25, 30, null, new List<SheetRectangle> { a1Stamp });
        Check(result.RejectedKeys.Count == 0 && result.Pages.SelectMany(p => p.Views).Count() == 14, "Measured A1 elevations fit beside tall plan above stamp");
        foreach (var page in result.Pages)
        {
            Check(page.Plans.Count == 1 && Close(page.Plans[0].Bounds.Right, a1.Right), "A1 continuation repeats plan on the right");
            var pageBounds = page.Views.Concat(page.Plans).Select(v => v.Bounds).ToList();
            for (int i = 0; i < pageBounds.Count; i++)
            {
                Check(a1.Contains(pageBounds[i]) && !pageBounds[i].Intersects(a1Stamp, 25, 30), "A1 blocks stay within margins and avoid stamp");
                for (int j = i + 1; j < pageBounds.Count; j++) Check(!pageBounds[i].Intersects(pageBounds[j], 25, 30), "A1 horizontal and vertical gaps preserved");
            }
        }
    }

    private static SheetPackingItem Item(long key, double width, double height, int group = 0, long? plan = null)
    { return new SheetPackingItem { Key = key, Width = width, Height = height, Group = group, CompanionKey = plan }; }

    private static SheetPackingResult Pack(IList<SheetPackingItem> items, double gapX, double gapY,
        IDictionary<long, SheetPackingCompanion> plans = null, IList<SheetRectangle> obstacles = null)
    { return Packer.Pack(Area, items, plans ?? new Dictionary<long, SheetPackingCompanion>(), gapX, gapY, obstacles); }

    private static void Validate(SheetPackingResult result, IList<SheetPackingItem> source,
        IDictionary<long, SheetPackingCompanion> plans, double gapX, double gapY)
    {
        var actualKeys = result.Pages.SelectMany(p => p.Views).Select(v => v.Key).ToList();
        Check(actualKeys.Distinct().Count() == actualKeys.Count, "No view is placed twice");
        Check(actualKeys.SequenceEqual(source.Where(i => !result.RejectedKeys.Contains(i.Key)).Select(i => i.Key)), "Order and complete accounting");
        foreach (var page in result.Pages)
        {
            Check(page.Views.Count > 0, "No empty new page");
            var bounds = page.Views.Concat(page.Plans).Select(v => v.Bounds).ToList();
            for (int i = 0; i < bounds.Count; i++)
            {
                Check(Area.Contains(bounds[i]), "All complete blocks fit the workspace");
                for (int j = i + 1; j < bounds.Count; j++) Check(!bounds[i].Intersects(bounds[j], gapX, gapY), "No overlap and requested gaps preserved");
            }
            var expectedPlans = page.Views.Select(v => source.First(s => s.Key == v.Key).CompanionKey)
                .Where(k => k.HasValue).Select(k => k.Value).Distinct()
                .Where(k => !(page.Index == 0 && plans[k].AlreadyOnFirstSheet)).OrderBy(k => k);
            Check(expectedPlans.SequenceEqual(page.Plans.Select(p => p.Key).OrderBy(k => k)), "Correct companions on every page");
        }
    }

    private static void AddFrame(IList<SheetFrameLine> lines, SheetRectangle frame, bool split)
    {
        var points = new[] { new[] { frame.Left, frame.Bottom }, new[] { frame.Right, frame.Bottom },
            new[] { frame.Right, frame.Top }, new[] { frame.Left, frame.Top } };
        for (int i = 0; i < 4; i++)
        {
            var a = points[i]; var b = points[(i + 1) % 4];
            if (split)
            {
                double x = (a[0] + b[0]) / 2, y = (a[1] + b[1]) / 2;
                lines.Add(new SheetFrameLine { X1 = a[0], Y1 = a[1], X2 = x, Y2 = y });
                lines.Add(new SheetFrameLine { X1 = x, Y1 = y, X2 = b[0], Y2 = b[1] });
            }
            else lines.Add(new SheetFrameLine { X1 = a[0], Y1 = a[1], X2 = b[0], Y2 = b[1] });
        }
    }

    private static bool Close(double a, double b) { return Math.Abs(a - b) < 1e-7; }
    private static void Check(bool condition, string message)
    { _checks++; if (!condition) throw new Exception("FAILED: " + message); }
    private static void ExpectFailure(Action action, string message)
    {
        try { action(); } catch (ArgumentException) { _checks++; return; } catch (InvalidOperationException) { _checks++; return; }
        throw new Exception("FAILED (expected exception): " + message);
    }
}
