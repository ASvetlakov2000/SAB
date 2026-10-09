using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Marks;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Elevations
{
    public class ElevationCropAdjustmentService
    {
        private const double MinimumCropSizeMm = 10.0;

        public ElevationCropAdjustmentResult Apply(
            Document document,
            IList<Viewport> viewports,
            IList<FamilyInstance> cornerMarks,
            ElevationCropAdjustmentSettings settings,
            IList<string> warnings)
        {
            ElevationCropAdjustmentResult result = new ElevationCropAdjustmentResult();
            result.SelectedCount = viewports != null ? viewports.Count : 0;
            result.SelectedMarkCount = cornerMarks != null ? cornerMarks.Count : 0;

            if (document == null || viewports == null || settings == null)
            {
                return result;
            }

            List<ViewportSnapshot> snapshots = CaptureViewportSnapshots(document, viewports, warnings);
            result.FailedCount = result.SelectedCount - snapshots.Count;
            List<CornerMarkBinding> markBindings = BuildMarkBindings(snapshots, cornerMarks, result, warnings);

            for (int index = 0; index < snapshots.Count; index++)
            {
                ViewportSnapshot snapshot = snapshots[index];
                if (TryApply(snapshot.View, settings, warnings))
                {
                    snapshot.CropUpdated = true;
                    result.UpdatedCount++;

                    ElevationGridExtentService gridExtentService = new ElevationGridExtentService();
                    int warningCountBeforeGridAdjustment = warnings != null ? warnings.Count : 0;
                    result.UpdatedGridCount += gridExtentService.AdjustExistingViewSpecificGrids(
                        snapshot.View,
                        settings,
                        warnings);
                    int warningCountAfterGridAdjustment = warnings != null ? warnings.Count : 0;
                    result.FailedGridCount += Math.Max(
                        0,
                        warningCountAfterGridAdjustment - warningCountBeforeGridAdjustment);
                }
                else
                {
                    result.FailedCount++;
                }
            }

            document.Regenerate();
            UpdateCornerMarks(document, markBindings, result, warnings);

            return result;
        }

        private static List<ViewportSnapshot> CaptureViewportSnapshots(
            Document document,
            IList<Viewport> viewports,
            IList<string> warnings)
        {
            List<ViewportSnapshot> snapshots = new List<ViewportSnapshot>();

            for (int index = 0; index < viewports.Count; index++)
            {
                Viewport viewport = viewports[index];
                ViewSection view = viewport != null
                    ? document.GetElement(viewport.ViewId) as ViewSection
                    : null;
                ViewportBoundary boundary = TryGetViewportCropBoundary(document, viewport);

                if (viewport == null || view == null || boundary == null)
                {
                    AddWarning(warnings, "Не удалось определить исходные границы одного из выбранных видов.");
                    continue;
                }

                ViewportSnapshot snapshot = new ViewportSnapshot();
                snapshot.Viewport = viewport;
                snapshot.View = view;
                snapshot.OldBoundary = boundary;
                snapshots.Add(snapshot);
            }

            return snapshots;
        }

        private static List<CornerMarkBinding> BuildMarkBindings(
            IList<ViewportSnapshot> snapshots,
            IList<FamilyInstance> cornerMarks,
            ElevationCropAdjustmentResult result,
            IList<string> warnings)
        {
            List<CornerMarkBinding> bindings = new List<CornerMarkBinding>();
            if (cornerMarks == null || cornerMarks.Count == 0)
            {
                AddWarning(warnings, "Марки углов не выбраны. Изменены только границы видов.");
                return bindings;
            }

            List<BindingCandidate> candidates = new List<BindingCandidate>();
            for (int markIndex = 0; markIndex < cornerMarks.Count; markIndex++)
            {
                FamilyInstance mark = cornerMarks[markIndex];
                LocationPoint locationPoint = mark != null ? mark.Location as LocationPoint : null;
                if (locationPoint == null || locationPoint.Point == null)
                {
                    AddWarning(warnings, "У одной из выбранных марок угла отсутствует точка размещения.");
                    continue;
                }

                IList<ViewportCorner> allowedCorners = GetAllowedCorners(mark);
                for (int snapshotIndex = 0; snapshotIndex < snapshots.Count; snapshotIndex++)
                {
                    ViewportSnapshot snapshot = snapshots[snapshotIndex];
                    for (int cornerIndex = 0; cornerIndex < allowedCorners.Count; cornerIndex++)
                    {
                        ViewportCorner corner = allowedCorners[cornerIndex];
                        XYZ anchorPoint = GetCornerPoint(snapshot.OldBoundary, corner);

                        BindingCandidate candidate = new BindingCandidate();
                        candidate.Mark = mark;
                        candidate.Snapshot = snapshot;
                        candidate.Corner = corner;
                        candidate.Distance = locationPoint.Point.DistanceTo(anchorPoint);
                        candidates.Add(candidate);
                    }
                }
            }

            candidates.Sort(delegate(BindingCandidate left, BindingCandidate right)
            {
                return left.Distance.CompareTo(right.Distance);
            });

            HashSet<long> boundMarkIds = new HashSet<long>();
            HashSet<string> usedAnchors = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < candidates.Count; index++)
            {
                BindingCandidate candidate = candidates[index];
                long markId = RevitElementIdUtils.GetElementIdValue(candidate.Mark.Id);
                string anchorKey = BuildAnchorKey(candidate.Snapshot.Viewport.Id, candidate.Corner);

                if (boundMarkIds.Contains(markId) || usedAnchors.Contains(anchorKey))
                {
                    continue;
                }

                CornerMarkBinding binding = new CornerMarkBinding();
                binding.Mark = candidate.Mark;
                binding.Snapshot = candidate.Snapshot;
                binding.Corner = candidate.Corner;
                bindings.Add(binding);

                boundMarkIds.Add(markId);
                usedAnchors.Add(anchorKey);
            }

            int unboundCount = cornerMarks.Count - boundMarkIds.Count;
            if (unboundCount > 0)
            {
                result.FailedMarkCount += unboundCount;
                AddWarning(
                    warnings,
                    "Не удалось однозначно связать с выбранными видами марок углов: " + unboundCount + ".");
            }

            return bindings;
        }

        private static void UpdateCornerMarks(
            Document document,
            IList<CornerMarkBinding> bindings,
            ElevationCropAdjustmentResult result,
            IList<string> warnings)
        {
            for (int index = 0; index < bindings.Count; index++)
            {
                CornerMarkBinding binding = bindings[index];
                if (binding == null || binding.Mark == null || binding.Snapshot == null)
                {
                    continue;
                }

                if (!binding.Snapshot.CropUpdated)
                {
                    result.FailedMarkCount++;
                    continue;
                }

                try
                {
                    ViewportBoundary newBoundary = TryGetViewportCropBoundary(
                        document,
                        binding.Snapshot.Viewport);
                    if (newBoundary == null)
                    {
                        result.FailedMarkCount++;
                        AddWarning(warnings, "Не удалось определить новые границы вида для переноса марки угла.");
                        continue;
                    }

                    XYZ oldAnchor = GetCornerPoint(binding.Snapshot.OldBoundary, binding.Corner);
                    XYZ newAnchor = GetCornerPoint(newBoundary, binding.Corner);
                    XYZ translation = newAnchor - oldAnchor;

                    if (translation.GetLength() > 1e-9)
                    {
                        ElementTransformUtils.MoveElement(document, binding.Mark.Id, translation);
                    }

                    result.UpdatedMarkCount++;
                }
                catch (Exception exception)
                {
                    result.FailedMarkCount++;
                    AddWarning(
                        warnings,
                        "Не удалось переместить марку угла: " + exception.Message);
                }
            }
        }

        private static ViewportBoundary TryGetViewportCropBoundary(
            Document document,
            Viewport viewport)
        {
            try
            {
                View view = document != null && viewport != null
                    ? document.GetElement(viewport.ViewId) as View
                    : null;
                BoundingBoxXYZ cropBox = view != null ? view.CropBox : null;
                if (cropBox == null || cropBox.Min == null || cropBox.Max == null || cropBox.Transform == null)
                {
                    return null;
                }

                double minimumX = double.MaxValue;
                double minimumY = double.MaxValue;
                double maximumX = double.MinValue;
                double maximumY = double.MinValue;
                double[] xValues = { cropBox.Min.X, cropBox.Max.X };
                double[] yValues = { cropBox.Min.Y, cropBox.Max.Y };
                double[] zValues = { cropBox.Min.Z, cropBox.Max.Z };

                for (int xIndex = 0; xIndex < xValues.Length; xIndex++)
                {
                    for (int yIndex = 0; yIndex < yValues.Length; yIndex++)
                    {
                        for (int zIndex = 0; zIndex < zValues.Length; zIndex++)
                        {
                            XYZ cropPoint = new XYZ(xValues[xIndex], yValues[yIndex], zValues[zIndex]);
                            XYZ modelPoint = cropBox.Transform.OfPoint(cropPoint);
                            XYZ sheetPoint;
                            if (!TryGetModelPointOnSheet(view, viewport, modelPoint, out sheetPoint))
                            {
                                continue;
                            }

                            minimumX = Math.Min(minimumX, sheetPoint.X);
                            minimumY = Math.Min(minimumY, sheetPoint.Y);
                            maximumX = Math.Max(maximumX, sheetPoint.X);
                            maximumY = Math.Max(maximumY, sheetPoint.Y);
                        }
                    }
                }

                if (minimumX >= maximumX || minimumY >= maximumY)
                {
                    return null;
                }

                ViewportBoundary boundary = new ViewportBoundary();
                boundary.Minimum = new XYZ(minimumX, minimumY, 0.0);
                boundary.Maximum = new XYZ(maximumX, maximumY, 0.0);
                return boundary;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryGetModelPointOnSheet(
            View view,
            Viewport viewport,
            XYZ modelPoint,
            out XYZ sheetPoint)
        {
            sheetPoint = XYZ.Zero;
            if (view == null || viewport == null || modelPoint == null)
            {
                return false;
            }

            try
            {
                MethodInfo modelToProjectionMethod = view.GetType().GetMethod(
                    "GetModelToProjectionTransforms",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    Type.EmptyTypes,
                    null);
                MethodInfo projectionToSheetMethod = viewport.GetType().GetMethod(
                    "GetProjectionToSheetTransform",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    Type.EmptyTypes,
                    null);

                if (modelToProjectionMethod != null && projectionToSheetMethod != null)
                {
                    IEnumerable transformations = modelToProjectionMethod.Invoke(view, null) as IEnumerable;
                    Transform projectionToSheet = projectionToSheetMethod.Invoke(viewport, null) as Transform;
                    if (transformations != null && projectionToSheet != null)
                    {
                        foreach (object transformWithBoundary in transformations)
                        {
                            if (transformWithBoundary == null)
                            {
                                continue;
                            }

                            MethodInfo getTransformMethod = transformWithBoundary.GetType().GetMethod(
                                "GetModelToProjectionTransform",
                                BindingFlags.Instance | BindingFlags.Public,
                                null,
                                Type.EmptyTypes,
                                null);
                            Transform modelToProjection = getTransformMethod != null
                                ? getTransformMethod.Invoke(transformWithBoundary, null) as Transform
                                : null;
                            if (modelToProjection == null)
                            {
                                continue;
                            }

                            sheetPoint = projectionToSheet.OfPoint(modelToProjection.OfPoint(modelPoint));
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Revit 2022 does not expose the projection transform API.
            }

            try
            {
                BoundingBoxUV outline = view.Outline;
                if (outline == null || outline.Min == null || outline.Max == null || view.Scale <= 0)
                {
                    return false;
                }

                double projectedPaperX = (modelPoint - view.Origin).DotProduct(view.RightDirection) / view.Scale;
                double projectedPaperY = (modelPoint - view.Origin).DotProduct(view.UpDirection) / view.Scale;
                double outlineCenterX = (outline.Min.U + outline.Max.U) / 2.0;
                double outlineCenterY = (outline.Min.V + outline.Max.V) / 2.0;
                double offsetX = projectedPaperX - outlineCenterX;
                double offsetY = projectedPaperY - outlineCenterY;

                if (viewport.Rotation == ViewportRotation.Clockwise)
                {
                    double rotatedX = offsetY;
                    offsetY = -offsetX;
                    offsetX = rotatedX;
                }
                else if (viewport.Rotation == ViewportRotation.Counterclockwise)
                {
                    double rotatedX = -offsetY;
                    offsetY = offsetX;
                    offsetX = rotatedX;
                }

                XYZ viewportCenter = viewport.GetBoxCenter();
                sheetPoint = new XYZ(
                    viewportCenter.X + offsetX,
                    viewportCenter.Y + offsetY,
                    0.0);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static IList<ViewportCorner> GetAllowedCorners(FamilyInstance mark)
        {
            List<ViewportCorner> corners = new List<ViewportCorner>();
            string typeName = mark != null && mark.Symbol != null
                ? mark.Symbol.Name
                : string.Empty;

            if (string.Equals(typeName, CornerMarkConstants.LeftCornerMarkTypeName, StringComparison.OrdinalIgnoreCase))
            {
                corners.Add(ViewportCorner.TopLeft);
                corners.Add(ViewportCorner.BottomLeft);
                return corners;
            }

            if (string.Equals(typeName, CornerMarkConstants.RightCornerMarkTypeName, StringComparison.OrdinalIgnoreCase))
            {
                corners.Add(ViewportCorner.TopRight);
                corners.Add(ViewportCorner.BottomRight);
                return corners;
            }

            corners.Add(ViewportCorner.TopLeft);
            corners.Add(ViewportCorner.TopRight);
            corners.Add(ViewportCorner.BottomLeft);
            corners.Add(ViewportCorner.BottomRight);
            return corners;
        }

        private static XYZ GetCornerPoint(ViewportBoundary boundary, ViewportCorner corner)
        {
            XYZ minimum = boundary.Minimum;
            XYZ maximum = boundary.Maximum;
            double x = corner == ViewportCorner.TopLeft || corner == ViewportCorner.BottomLeft
                ? minimum.X
                : maximum.X;
            double y = corner == ViewportCorner.TopLeft || corner == ViewportCorner.TopRight
                ? maximum.Y
                : minimum.Y;
            return new XYZ(x, y, 0.0);
        }

        private static string BuildAnchorKey(ElementId viewportId, ViewportCorner corner)
        {
            return RevitElementIdUtils.GetElementIdValue(viewportId) + "|" + corner;
        }

        private bool TryApply(
            ViewSection view,
            ElevationCropAdjustmentSettings settings,
            IList<string> warnings)
        {
            if (view == null)
            {
                AddWarning(warnings, "Один из выбранных видов не найден.");
                return false;
            }

            try
            {
                BoundingBoxXYZ cropBox = view.CropBox;
                if (cropBox == null)
                {
                    AddWarning(warnings, "Для вида '" + view.Name + "' недоступна рамка подрезки.");
                    return false;
                }

                double minX = cropBox.Min.X - UnitConversionUtils.MillimetersToFeet(settings.LeftMm);
                double maxX = cropBox.Max.X + UnitConversionUtils.MillimetersToFeet(settings.RightMm);
                double minY = cropBox.Min.Y - UnitConversionUtils.MillimetersToFeet(settings.BottomMm);
                double maxY = cropBox.Max.Y + UnitConversionUtils.MillimetersToFeet(settings.TopMm);
                double minimumSizeFeet = UnitConversionUtils.MillimetersToFeet(MinimumCropSizeMm);

                if (maxX - minX < minimumSizeFeet || maxY - minY < minimumSizeFeet)
                {
                    AddWarning(
                        warnings,
                        "Для вида '" + view.Name + "' заданные смещения уменьшают рамку менее чем до " +
                        MinimumCropSizeMm.ToString("F0") + " мм.");
                    return false;
                }

                BoundingBoxXYZ adjustedCropBox = new BoundingBoxXYZ();
                adjustedCropBox.Transform = cropBox.Transform;
                adjustedCropBox.Min = new XYZ(minX, minY, cropBox.Min.Z);
                adjustedCropBox.Max = new XYZ(maxX, maxY, cropBox.Max.Z);

                view.CropBoxActive = true;
                view.CropBox = adjustedCropBox;
                return true;
            }
            catch (Exception exception)
            {
                AddWarning(
                    warnings,
                    "Не удалось изменить границы вида '" + view.Name + "': " + exception.Message);
                return false;
            }
        }

        private static void AddWarning(IList<string> warnings, string warning)
        {
            if (warnings != null && !string.IsNullOrWhiteSpace(warning))
            {
                warnings.Add(warning);
            }
        }

        private enum ViewportCorner
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        private class ViewportSnapshot
        {
            public Viewport Viewport { get; set; }

            public ViewSection View { get; set; }

            public ViewportBoundary OldBoundary { get; set; }

            public bool CropUpdated { get; set; }
        }

        private class ViewportBoundary
        {
            public XYZ Minimum { get; set; }

            public XYZ Maximum { get; set; }
        }

        private class BindingCandidate
        {
            public FamilyInstance Mark { get; set; }

            public ViewportSnapshot Snapshot { get; set; }

            public ViewportCorner Corner { get; set; }

            public double Distance { get; set; }
        }

        private class CornerMarkBinding
        {
            public FamilyInstance Mark { get; set; }

            public ViewportSnapshot Snapshot { get; set; }

            public ViewportCorner Corner { get; set; }
        }
    }
}
