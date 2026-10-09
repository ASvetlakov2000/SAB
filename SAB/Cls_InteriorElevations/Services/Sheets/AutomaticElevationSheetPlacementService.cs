using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Elevations;
using SAB.InteriorElevations.Services.Marks;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Sheets
{
    public sealed class AutomaticElevationSheetPlacementService
    {
        private sealed class PreparedBlock
        {
            public PlacedViewportData Data;
            public SheetRectangle Bounds;
            public int Group;
            public ElevationViewData Elevation;
            public bool ExistingPlan;
        }

        private readonly ViewportPlacementService _placement = new ViewportPlacementService();
        private readonly SheetFootprintService _footprints = new SheetFootprintService();
        private readonly SheetWorkspaceService _workspace = new SheetWorkspaceService();

        public void PrepareFamilyGeometry(Document document, ViewSheet sheet)
        {
            SheetLayoutDiagnostics.Write("Start: Revit " + document.Application.VersionNumber +
                ", assembly=" + typeof(AutomaticElevationSheetPlacementService).Assembly.Location);
            _workspace.PrepareFamilyGeometry(document, sheet);
        }

        public ViewportPlacementResult Place(Document document, ViewSheet firstSheet,
            IList<RoomData> rooms, IList<IList<ElevationViewData>> groups, IList<View> plans,
            ElevationSettings settings, ElevationNamingService naming, bool firstSheetWasCreated,
            IList<string> warnings, Action<int, int, ViewportPlacementResult> progress,
            Exception framePreparationError = null)
        {
            if (!settings.SheetLayoutSettings.UseAutomaticPlacement)
                return PlaceColumns(document, firstSheet, rooms, groups, plans, settings, naming, warnings, progress);

            int warningCount = warnings.Count;
            using (var attempt = new SubTransaction(document))
            {
                attempt.Start();
                return SheetPlacementAttempt.Run(
                    () =>
                    {
                        if (framePreparationError != null) throw framePreparationError;
                        return PlaceAutomatically(document, firstSheet, rooms, groups, plans, settings, naming,
                            firstSheetWasCreated, warnings, progress);
                    },
                    result => result.UnplacedViewIds.Count == 0 ? null :
                        "Не удалось скомпоновать " + result.UnplacedViewIds.Count + " разверток в рабочей области.",
                    () =>
                    {
                        if (attempt.Commit() != TransactionStatus.Committed)
                            throw new InvalidOperationException("Revit не подтвердил автоматическое размещение.");
                    },
                    () =>
                    {
                        if (attempt.GetStatus() == TransactionStatus.Started) attempt.RollBack();
                        while (warnings.Count > warningCount) warnings.RemoveAt(warnings.Count - 1);
                    },
                    failure =>
                    {
                        SheetLayoutDiagnostics.Write("Automatic attempt rolled back; forced placement: " + failure);
                        var forced = PlaceColumns(document, firstSheet, rooms, groups, plans, settings, naming, warnings, progress);
                        warnings.Insert(warningCount, "Автоматическая компоновка не выполнена: " + failure +
                            " Выполнено принудительное размещение по " + settings.SheetLayoutSettings.ColumnsCount +
                            " видов в строке. Проверьте положение видов относительно рамки листа.");
                        forced.AutomaticFallbackUsed = true;
                        return forced;
                    });
            }
        }

        private ViewportPlacementResult PlaceColumns(Document document, ViewSheet sheet,
            IList<RoomData> rooms, IList<IList<ElevationViewData>> groups, IList<View> plans,
            ElevationSettings settings, ElevationNamingService naming, IList<string> warnings,
            Action<int, int, ViewportPlacementResult> progress)
        {
            using (var transaction = new SubTransaction(document))
            {
                transaction.Start();
                try
                {
                    var layout = settings.SheetLayoutSettings;
                    SheetLayoutDiagnostics.Write("Forced placement: columns=" + layout.ColumnsCount);
                    var result = _placement.PlaceRoomViewGroupsOnSheet(document, sheet, groups, null, layout,
                        settings.ViewportTypeId, warnings, progress);
                    result.ForcedPlacementUsed = true;
                    var marks = new SheetCornerMarkPlacementService();
                    for (int group = 0; group < groups.Count; group++)
                    {
                        var groupResult = new ViewportPlacementResult();
                        foreach (var data in result.PlacedViewports)
                            if (groups[group].Any(v => RevitElementIdUtils.AreEqual(v.ViewId, data.ViewId))) groupResult.PlacedViewports.Add(data);
                        marks.PlaceSheetCornerMarks(document, sheet, rooms[group], settings.SheetCornerMarkTypeId,
                            settings.CornerMarksOnlyCornerNumber, settings.SheetCornerMarksBelowView,
                            groups[group], groupResult, warnings);
                        foreach (var elevation in groups[group])
                            if (!groupResult.PlacedViewports.Any(v => RevitElementIdUtils.AreEqual(v.ViewId, elevation.ViewId)))
                                result.UnplacedViewIds.Add(elevation.ViewId);
                    }
                    result.PlacedSheetMarkCount = result.PlacedViewports.Sum(v => v.SheetAnnotationIds.Count);
                    if (settings.CreateRoomPlanScheme && settings.PlaceRoomPlanSchemeOnSheet && plans != null)
                    {
                        var seen = new HashSet<long>();
                        var previousPlans = new List<SheetRectangle>();
                        foreach (var plan in plans)
                        {
                            if (plan == null || !seen.Add(RevitElementIdUtils.GetElementIdValue(plan.Id))) continue;
                            using (var planAttempt = new SubTransaction(document))
                            {
                                planAttempt.Start();
                                try
                                {
                                    var block = PreparePlan(document, sheet, plan, layout, naming);
                                    if (!block.ExistingPlan || layout.UseManualRoomPlanPosition)
                                    {
                                        PositionForcedPlan(document, sheet, block, layout, previousPlans);
                                        Refresh(document, block, sheet);
                                    }
                                    planAttempt.Commit();
                                    previousPlans.Add(block.Bounds);
                                    if (!block.ExistingPlan || layout.UseManualRoomPlanPosition) result.PlacedViewports.Add(block.Data);
                                }
                                catch (Exception exception)
                                {
                                    if (planAttempt.GetStatus() == TransactionStatus.Started) planAttempt.RollBack();
                                    warnings.Add("Не удалось разместить план-схему: " + exception.Message);
                                }
                            }
                        }
                    }
                    result.PlacedCount = result.PlacedViewports.Count;
                    result.Sheets.Add(sheet);
                    transaction.Commit();
                    return result;
                }
                catch
                {
                    if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                    throw;
                }
            }
        }

        private void PositionForcedPlan(Document document, ViewSheet sheet, PreparedBlock block, SheetLayoutSettings layout,
            IList<SheetRectangle> previousPlans)
        {
            IList<SheetRectangle> reserved;
            var area = _workspace.ReadPlanWorkspace(document, sheet,
                UnitConversionUtils.MillimetersToFeet(layout.StartXmm), UnitConversionUtils.MillimetersToFeet(layout.StartYmm), out reserved);
            if (area == null)
            {
                if (layout.UseManualRoomPlanPosition) throw new InvalidOperationException("Не удалось измерить границы листа для положения план-схемы.");
                double bottom = GetLowestSheetViewBottom(document, sheet);
                Position(document, block, sheet, new SheetRectangle(UnitConversionUtils.MillimetersToFeet(layout.StartXmm),
                    bottom - layout.StepYmm / 304.8 - block.Bounds.Height,
                    UnitConversionUtils.MillimetersToFeet(layout.StartXmm) + block.Bounds.Width, bottom - layout.StepYmm / 304.8));
                return;
            }
            var exclusions = reserved != null ? new List<SheetRectangle>(reserved) : new List<SheetRectangle>();
            exclusions.AddRange(previousPlans);
            if (layout.UseManualRoomPlanPosition)
            {
                var target = SheetPlanPosition.Bounds(area, block.Bounds.Width, block.Bounds.Height,
                    UnitConversionUtils.MillimetersToFeet(layout.RoomPlanOffsetRightMm), UnitConversionUtils.MillimetersToFeet(layout.RoomPlanOffsetBottomMm));
                if (!area.Contains(target) || exclusions.Any(o => target.Intersects(o, layout.StepXmm / 304.8, layout.StepYmm / 304.8)))
                    throw new InvalidOperationException("Заданное положение план-схемы выходит за рабочую область или пересекает штамп/другую схему. Измените отступы справа и снизу.");
                Position(document, block, sheet, target);
                return;
            }
            double left = area.Right - block.Bounds.Width, planBottom = area.Bottom;
            if (exclusions.Count > 0)
                while (true)
                {
                    var candidate = new SheetRectangle(left, planBottom, area.Right, planBottom + block.Bounds.Height);
                    double next = planBottom;
                    foreach (var stamp in exclusions)
                        if (candidate.Intersects(stamp, layout.StepXmm / 304.8, layout.StepYmm / 304.8)) next = Math.Max(next, stamp.Top + layout.StepYmm / 304.8);
                    if (Math.Abs(next - planBottom) < 1e-8) break;
                    planBottom = next;
                }
            Position(document, block, sheet, new SheetRectangle(left, planBottom, area.Right, planBottom + block.Bounds.Height));
        }

        private double GetLowestSheetViewBottom(Document document, ViewSheet sheet)
        {
            double bottom = 0;
            foreach (var id in sheet.GetAllViewports())
            {
                var viewport = document.GetElement(id) as Viewport;
                if (viewport != null) bottom = Math.Min(bottom, viewport.GetBoxOutline().MinimumPoint.Y);
            }
            return bottom;
        }

        private ViewportPlacementResult PlaceAutomatically(Document document, ViewSheet firstSheet,
            IList<RoomData> rooms, IList<IList<ElevationViewData>> groups, IList<View> plans,
            ElevationSettings settings, ElevationNamingService naming, bool firstSheetWasCreated,
            IList<string> warnings, Action<int, int, ViewportPlacementResult> progress)
        {
            using (var transaction = new SubTransaction(document))
            {
                transaction.Start();
                try
                {
                    var frames = _workspace.ReadFrames(document, firstSheet);
                    var area = frames.GetWorkspace(UnitConversionUtils.MillimetersToFeet(settings.SheetLayoutSettings.StartXmm),
                        UnitConversionUtils.MillimetersToFeet(settings.SheetLayoutSettings.StartYmm));
                    var movedPlans = settings.SheetLayoutSettings.UseManualRoomPlanPosition &&
                        settings.CreateRoomPlanScheme && settings.PlaceRoomPlanSchemeOnSheet && plans != null
                        ? new HashSet<long>(plans.Where(p => p != null).Select(p => RevitElementIdUtils.GetElementIdValue(p.Id))) : null;
                    var obstacles = _footprints.ReadObstacles(document, firstSheet, frames, movedPlans);
                    SheetLayoutDiagnostics.Write("Workspace " + Describe(area) + "; obstacles=" + obstacles.Count);
                    foreach (var reserved in frames.ReservedAreas) SheetLayoutDiagnostics.Write("Reserved stamp: " + Describe(reserved));
                    foreach (var obstacle in obstacles) SheetLayoutDiagnostics.Write("Existing obstacle: " + Describe(obstacle));
                    var staged = new ViewportPlacementResult();
                    var blocks = new Dictionary<long, PreparedBlock>();
                    var planBlocks = new Dictionary<long, PreparedBlock>();
                    var items = new List<SheetPackingItem>();
                    var companions = new Dictionary<long, SheetPackingCompanion>();
                    var layout = SingleColumnLayout(settings.SheetLayoutSettings);
                    var markService = new SheetCornerMarkPlacementService();

                    for (int group = 0; group < groups.Count; group++)
                    {
                        var placed = _placement.PlaceViewsOnSheet(document, firstSheet, groups[group], layout,
                            settings.ViewportTypeId, warnings);
                        if (placed.PlacedViewports.Count != groups[group].Count)
                            throw new InvalidOperationException("Не все развертки удалось подготовить для измерения. Временное размещение отменено.");
                        markService.PlaceSheetCornerMarks(document, firstSheet, rooms[group], settings.SheetCornerMarkTypeId,
                            settings.CornerMarksOnlyCornerNumber, settings.SheetCornerMarksBelowView,
                            groups[group], placed, warnings);
                        long? companionKey = null;
                        var planView = settings.CreateRoomPlanScheme && settings.PlaceRoomPlanSchemeOnSheet &&
                            plans != null && group < plans.Count ? plans[group] : null;
                        if (settings.CreateRoomPlanScheme && settings.PlaceRoomPlanSchemeOnSheet && planView == null)
                            warnings.Add("Для помещения " + rooms[group].RoomNumber + " план-схема не создана. Развертки размещаются без неё.");
                        if (planView != null)
                        {
                            companionKey = RevitElementIdUtils.GetElementIdValue(planView.Id);
                            if (!planBlocks.ContainsKey(companionKey.Value))
                            {
                                var planBlock = PreparePlan(document, firstSheet, planView, layout, naming);
                                SheetLayoutDiagnostics.Write("Plan '" + planView.Name + "': " + Describe(planBlock.Bounds));
                                planBlocks.Add(companionKey.Value, planBlock);
                                companions.Add(companionKey.Value, new SheetPackingCompanion { Key = companionKey.Value,
                                    Width = planBlock.Bounds.Width, Height = planBlock.Bounds.Height,
                                    AlreadyOnFirstSheet = planBlock.ExistingPlan && !settings.SheetLayoutSettings.UseManualRoomPlanPosition,
                                    FixedPosition = settings.SheetLayoutSettings.UseManualRoomPlanPosition,
                                    OffsetRight = UnitConversionUtils.MillimetersToFeet(settings.SheetLayoutSettings.RoomPlanOffsetRightMm),
                                    OffsetBottom = UnitConversionUtils.MillimetersToFeet(settings.SheetLayoutSettings.RoomPlanOffsetBottomMm) });
                            }
                        }
                        foreach (var data in placed.PlacedViewports)
                        {
                            var elevation = groups[group].First(v => RevitElementIdUtils.AreEqual(v.ViewId, data.ViewId));
                            var viewport = (Viewport)document.GetElement(data.ViewportId);
                            var bounds = _footprints.Measure(document, firstSheet, viewport, data.SheetAnnotationIds);
                            long key = RevitElementIdUtils.GetElementIdValue(data.ViewId);
                            var item = new SheetPackingItem { Key = key, Group = group, CompanionKey = companionKey,
                                Width = bounds.Width, Height = bounds.Height };
                            XYZ modelPointOnSheet;
                            if (elevation.AlignmentModelPoint != null && _placement.TryGetModelPointOnSheet(document, viewport,
                                elevation.AlignmentModelPoint, out modelPointOnSheet)) item.AnchorFromTop = bounds.Top - modelPointOnSheet.Y;
                            items.Add(item);
                            SheetLayoutDiagnostics.Write("Elevation '" + elevation.ViewName + "': " + Describe(bounds) +
                                "; anchor mm=" + (item.AnchorFromTop.HasValue ? (item.AnchorFromTop.Value * 304.8).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "none"));
                            blocks.Add(key, new PreparedBlock { Data = data, Bounds = bounds, Group = group, Elevation = elevation });
                            staged.PlacedViewports.Add(data);
                        }
                        staged.PlacedCount = staged.PlacedViewports.Count;
                        if (progress != null) progress(group + 1, groups.Count, staged);
                    }

                    double gapX = UnitConversionUtils.MillimetersToFeet(layout.StepXmm);
                    double gapY = UnitConversionUtils.MillimetersToFeet(layout.StepYmm);
                    var packing = new SheetPackingService().Pack(area, items, companions, gapX, gapY, obstacles, frames.ReservedAreas);
                    SheetLayoutDiagnostics.Write("Packed sheets=" + packing.Pages.Count + ", rejected=" + packing.RejectedKeys.Count);
                    var result = new ViewportPlacementResult();
                    foreach (long key in packing.RejectedKeys)
                    {
                        var block = blocks[key];
                        RemoveStagedBlock(document, block);
                        result.UnplacedViewIds.Add(block.Data.ViewId);
                        var item = items.First(i => i.Key == key);
                        string planSize = item.CompanionKey.HasValue ? "; план-схема " + Size(planBlocks[item.CompanionKey.Value].Bounds) : "";
                        warnings.Add("Развертка '" + block.Elevation.ViewName + "' с оформлением " + Size(block.Bounds) +
                            planSize + " не помещается в рабочую область " + Size(area) + ". Вид оставлен без размещения; масштаб не изменён.");
                    }
                    string baseName = firstSheet.Name, baseNumber = firstSheet.SheetNumber;
                    int firstPart = 1;
                    if (!firstSheetWasCreated)
                    {
                        var suffix = Regex.Match(baseName, @"(?:\s*[—-]\s*|\s+)Часть\s+(\d+)(?:_\d+)?$", RegexOptions.IgnoreCase);
                        int existingPart;
                        if (suffix.Success && int.TryParse(suffix.Groups[1].Value, out existingPart) && existingPart > 0)
                        {
                            firstPart = existingPart; baseName = baseName.Substring(0, suffix.Index).TrimEnd();
                            string numberSuffix = "-" + existingPart.ToString("00");
                            if (baseNumber.EndsWith(numberSuffix, StringComparison.Ordinal))
                                baseNumber = baseNumber.Substring(0, baseNumber.Length - numberSuffix.Length);
                        }
                    }
                    var continuation = new SheetContinuationService(_workspace);
                    var usedOriginalPlans = new HashSet<long>();
                    int totalSheets = packing.Pages.Count;
                    if (firstSheetWasCreated && totalSheets > 1)
                    {
                        firstSheet.Name = naming.GenerateUniqueSheetPartName(baseName, 1);
                        firstSheet.SheetNumber = naming.GenerateUniqueSheetPartNumber(baseNumber, 1);
                    }
                    foreach (var page in packing.Pages)
                    {
                        int part = firstPart + page.Index;
                        var sheet = page.Index == 0 ? firstSheet : continuation.Create(document, firstSheet, baseName, baseNumber, part, naming);
                        var actualFrames = _workspace.ReadFrames(document, sheet);
                        // A continuation is measured again after instance parameters have been copied.
                        var actualArea = actualFrames.GetWorkspace(area.Left, area.Top);
                        var pageObstacles = new List<SheetRectangle>(actualFrames.ReservedAreas);
                        if (page.Index == 0) pageObstacles.AddRange(obstacles);
                        var measuredOnPage = new List<SheetRectangle>();
                        foreach (var placedView in page.Views)
                        {
                            var block = blocks[placedView.Key];
                            MoveBlock(document, block, sheet);
                            Position(document, block, sheet, placedView.Bounds);
                            Refresh(document, block, sheet);
                            Validate(block.Bounds, actualArea, measuredOnPage, pageObstacles, gapX, gapY);
                            measuredOnPage.Add(block.Bounds);
                            result.PlacedViewports.Add(block.Data);
                            result.PlacedSheetMarkCount += block.Data.SheetAnnotationIds.Count;
                        }
                        foreach (var placedPlan in page.Plans)
                        {
                            var original = planBlocks[placedPlan.Key];
                            PreparedBlock block;
                            if ((!original.ExistingPlan || (page.Index == 0 && settings.SheetLayoutSettings.UseManualRoomPlanPosition)) && usedOriginalPlans.Add(placedPlan.Key))
                            { block = original; MoveBlock(document, block, sheet); }
                            else block = CopyPlan(document, original, sheet, part, naming);
                            var planTarget = settings.SheetLayoutSettings.UseManualRoomPlanPosition
                                ? SheetPlanPosition.Bounds(actualArea, block.Bounds.Width, block.Bounds.Height,
                                    UnitConversionUtils.MillimetersToFeet(settings.SheetLayoutSettings.RoomPlanOffsetRightMm),
                                    UnitConversionUtils.MillimetersToFeet(settings.SheetLayoutSettings.RoomPlanOffsetBottomMm)) : placedPlan.Bounds;
                            Position(document, block, sheet, planTarget);
                            Refresh(document, block, sheet);
                            Validate(block.Bounds, actualArea, measuredOnPage, pageObstacles, gapX, gapY);
                            measuredOnPage.Add(block.Bounds);
                            result.PlacedViewports.Add(block.Data);
                        }
                        result.Sheets.Add(sheet);
                    }
                    foreach (var entry in planBlocks)
                        if (!entry.Value.ExistingPlan && !usedOriginalPlans.Contains(entry.Key)) RemoveStagedBlock(document, entry.Value);
                    result.PlacedCount = result.PlacedViewports.Count;
                    transaction.Commit();
                    return result;
                }
                catch (Exception exception)
                {
                    SheetLayoutDiagnostics.Write("FAILED: " + exception);
                    if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                    throw;
                }
            }
        }

        private static string Describe(SheetRectangle bounds)
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "[{0:F2},{1:F2}..{2:F2},{3:F2}] {4:F2}x{5:F2} mm", bounds.Left * 304.8,
                bounds.Bottom * 304.8, bounds.Right * 304.8, bounds.Top * 304.8, bounds.Width * 304.8, bounds.Height * 304.8);
        }

        private static string Size(SheetRectangle bounds)
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F1} × {1:F1} мм", bounds.Width * 304.8, bounds.Height * 304.8);
        }

        private PreparedBlock PreparePlan(Document document, ViewSheet sheet, View plan,
            SheetLayoutSettings layout, ElevationNamingService naming)
        {
            Viewport existing = null;
            foreach (ElementId id in sheet.GetAllViewports())
            {
                var candidate = document.GetElement(id) as Viewport;
                if (candidate != null && RevitElementIdUtils.AreEqual(candidate.ViewId, plan.Id)) { existing = candidate; break; }
            }
            bool alreadyPlaced = existing != null;
            if (existing == null)
            {
                if (!Viewport.CanAddViewToSheet(document, sheet.Id, plan.Id)) plan = DuplicatePlan(document, plan, 1, naming);
                existing = Viewport.Create(document, sheet.Id, plan.Id,
                    new XYZ(UnitConversionUtils.MillimetersToFeet(layout.StartXmm), UnitConversionUtils.MillimetersToFeet(layout.StartYmm), 0));
            }
            var block = new PreparedBlock { Data = new PlacedViewportData { ViewportId = existing.Id,
                ViewId = plan.Id, SheetId = sheet.Id }, ExistingPlan = alreadyPlaced };
            block.Bounds = _footprints.Measure(document, sheet, existing, block.Data.SheetAnnotationIds);
            return block;
        }

        private View DuplicatePlan(Document document, View source, int part, ElevationNamingService naming)
        {
            if (!source.CanViewBeDuplicated(ViewDuplicateOption.WithDetailing))
                throw new InvalidOperationException("План-схему '" + source.Name + "' нельзя скопировать с детализацией.");
            var copy = (View)document.GetElement(source.Duplicate(ViewDuplicateOption.WithDetailing));
            copy.Name = naming.GenerateUniquePlanCopyName(source.Name, part, document);
            var sourceTitle = source.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION);
            var copyTitle = copy.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION);
            if (copyTitle != null && !copyTitle.IsReadOnly)
                copyTitle.Set(sourceTitle != null && !string.IsNullOrWhiteSpace(sourceTitle.AsString()) ? sourceTitle.AsString() : source.Name);
            return copy;
        }

        private PreparedBlock CopyPlan(Document document, PreparedBlock original, ViewSheet target, int part, ElevationNamingService naming)
        {
            var oldViewport = (Viewport)document.GetElement(original.Data.ViewportId);
            var copy = DuplicatePlan(document, (View)document.GetElement(original.Data.ViewId), part, naming);
            var viewport = Viewport.Create(document, target.Id, copy.Id, oldViewport.GetBoxCenter());
            CopyViewportAppearance(document, oldViewport, viewport);
            return new PreparedBlock { Data = new PlacedViewportData { ViewportId = viewport.Id, ViewId = copy.Id,
                SheetId = target.Id }, Bounds = _footprints.Measure(document, target, viewport, null) };
        }

        private void MoveBlock(Document document, PreparedBlock block, ViewSheet target)
        {
            var viewport = (Viewport)document.GetElement(block.Data.ViewportId);
            if (RevitElementIdUtils.AreEqual(viewport.SheetId, target.Id)) return;
            var sourceSheet = (ViewSheet)document.GetElement(viewport.SheetId);
            var center = viewport.GetBoxCenter(); var type = viewport.GetTypeId();
            var rotation = viewport.Rotation; var offset = viewport.LabelOffset; double lineLength = viewport.LabelLineLength;
            string detailNumber = GetDetailNumber(viewport);
            document.Delete(viewport.Id);
            var moved = Viewport.Create(document, target.Id, block.Data.ViewId, center);
            moved.ChangeTypeId(type); moved.Rotation = rotation;
            document.Regenerate(); moved.LabelOffset = offset; moved.LabelLineLength = lineLength;
            SetDetailNumber(moved, detailNumber);
            if (block.Data.SheetAnnotationIds.Count > 0)
            {
                var copied = ElementTransformUtils.CopyElements(sourceSheet, block.Data.SheetAnnotationIds, target, Transform.Identity, new CopyPasteOptions());
                document.Delete(block.Data.SheetAnnotationIds);
                block.Data.SheetAnnotationIds.Clear();
                foreach (var id in copied)
                {
                    var element = document.GetElement(id);
                    if (element != null && RevitElementIdUtils.AreEqual(element.OwnerViewId, target.Id)) block.Data.SheetAnnotationIds.Add(id);
                }
            }
            block.Data.ViewportId = moved.Id; block.Data.SheetId = target.Id;
            block.Bounds = _footprints.Measure(document, target, moved, block.Data.SheetAnnotationIds);
        }

        private void CopyViewportAppearance(Document document, Viewport source, Viewport target)
        {
            target.ChangeTypeId(source.GetTypeId()); target.Rotation = source.Rotation;
            document.Regenerate(); target.LabelOffset = source.LabelOffset; target.LabelLineLength = source.LabelLineLength;
            SetDetailNumber(target, GetDetailNumber(source));
        }

        private string GetDetailNumber(Viewport viewport)
        {
            var parameter = viewport.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
            return parameter != null ? parameter.AsString() : null;
        }

        private void SetDetailNumber(Viewport viewport, string value)
        {
            var parameter = viewport.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
            if (parameter == null || parameter.IsReadOnly || string.IsNullOrEmpty(value)) return;
            foreach (ElementId id in ((ViewSheet)viewport.Document.GetElement(viewport.SheetId)).GetAllViewports())
            {
                var other = viewport.Document.GetElement(id) as Viewport;
                if (other != null && !RevitElementIdUtils.AreEqual(id, viewport.Id) && GetDetailNumber(other) == value) return;
            }
            parameter.Set(value);
        }

        private void Position(Document document, PreparedBlock block, ViewSheet sheet, SheetRectangle target)
        {
            block.Bounds = _footprints.Measure(document, sheet, (Viewport)document.GetElement(block.Data.ViewportId), block.Data.SheetAnnotationIds);
            XYZ translation = new XYZ(target.Left - block.Bounds.Left, target.Top - block.Bounds.Top, 0);
            var ids = new List<ElementId>(block.Data.SheetAnnotationIds) { block.Data.ViewportId };
            if (translation.GetLength() > 1e-9) ElementTransformUtils.MoveElements(document, ids, translation);
            document.Regenerate();
            block.Bounds = _footprints.Measure(document, sheet, (Viewport)document.GetElement(block.Data.ViewportId), block.Data.SheetAnnotationIds);
        }

        private void Refresh(Document document, PreparedBlock block, ViewSheet sheet)
        {
            var viewport = (Viewport)document.GetElement(block.Data.ViewportId);
            block.Data.SheetId = sheet.Id; block.Data.Center = viewport.GetBoxCenter();
            XYZ topLeft, topRight, bottomLeft, bottomRight;
            if (_placement.TryGetViewportCropCorners(document, viewport, out topLeft, out topRight, out bottomLeft, out bottomRight))
            {
                block.Data.TopLeft = topLeft; block.Data.TopRight = topRight;
                block.Data.BottomLeft = bottomLeft; block.Data.BottomRight = bottomRight;
            }
        }

        private void Validate(SheetRectangle bounds, SheetRectangle area, IList<SheetRectangle> placed,
            IList<SheetRectangle> obstacles, double gapX, double gapY)
        {
            double tolerance = UnitConversionUtils.MillimetersToFeet(0.1);
            if (!area.Contains(bounds, tolerance)) throw new InvalidOperationException("Итоговый блок вышел за рабочую область после переноса. Раскладка отменена.");
            foreach (var other in placed)
                if (bounds.Intersects(other, Math.Max(0, gapX - tolerance), Math.Max(0, gapY - tolerance)))
                    throw new InvalidOperationException("Итоговые габариты оформления изменились после переноса. Раскладка отменена, чтобы сохранить заданные зазоры.");
            if (obstacles != null)
                foreach (var other in obstacles)
                    if (bounds.Intersects(other, Math.Max(0, gapX - tolerance), Math.Max(0, gapY - tolerance)))
                        throw new InvalidOperationException("Блок пересекает существующее содержимое листа. Раскладка отменена.");
        }

        private void RemoveStagedBlock(Document document, PreparedBlock block)
        {
            var ids = new List<ElementId>(block.Data.SheetAnnotationIds) { block.Data.ViewportId };
            document.Delete(ids);
        }

        private SheetLayoutSettings SingleColumnLayout(SheetLayoutSettings source)
        {
            return new SheetLayoutSettings { ColumnsCount = 1, StartXmm = source.StartXmm, StartYmm = source.StartYmm,
                StepXmm = source.StepXmm, StepYmm = source.StepYmm, ViewTitleAnchor = source.ViewTitleAnchor,
                ViewTitleOffsetXmm = source.ViewTitleOffsetXmm, ViewTitleOffsetYmm = source.ViewTitleOffsetYmm };
        }
    }
}
