using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Elevations
{
    public class ElevationGridExtentService
    {
        public int AdjustExistingViewSpecificGrids(
            ViewSection view,
            ElevationCropAdjustmentSettings settings,
            IList<string> warnings)
        {
            if (view == null || settings == null)
            {
                return 0;
            }

            BoundingBoxXYZ cropBox = view.CropBox;
            if (cropBox == null || cropBox.Transform == null)
            {
                return 0;
            }

            double leftDelta = UnitConversionUtils.MillimetersToFeet(settings.LeftMm);
            double rightDelta = UnitConversionUtils.MillimetersToFeet(settings.RightMm);
            double topDelta = UnitConversionUtils.MillimetersToFeet(settings.TopMm);
            double bottomDelta = UnitConversionUtils.MillimetersToFeet(settings.BottomMm);
            Transform inverse = cropBox.Transform.Inverse;
            int adjustedCount = 0;

            IList<Grid> grids = new FilteredElementCollector(view.Document, view.Id)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .Where(grid => grid != null)
                .ToList();

            for (int index = 0; index < grids.Count; index++)
            {
                Grid grid = grids[index];
                try
                {
                    DatumExtentType end0Extent = grid.GetDatumExtentTypeInView(DatumEnds.End0, view);
                    DatumExtentType end1Extent = grid.GetDatumExtentTypeInView(DatumEnds.End1, view);
                    if (end0Extent != DatumExtentType.ViewSpecific ||
                        end1Extent != DatumExtentType.ViewSpecific)
                    {
                        continue;
                    }

                    IList<Curve> curves = grid.GetCurvesInView(DatumExtentType.ViewSpecific, view);
                    Line sourceLine = curves != null
                        ? curves.OfType<Line>().FirstOrDefault(line => line != null && line.IsBound)
                        : null;
                    if (sourceLine == null)
                    {
                        continue;
                    }

                    XYZ end0Local = inverse.OfPoint(sourceLine.GetEndPoint(0));
                    XYZ end1Local = inverse.OfPoint(sourceLine.GetEndPoint(1));
                    double spanX = Math.Abs(end1Local.X - end0Local.X);
                    double spanY = Math.Abs(end1Local.Y - end0Local.Y);

                    if (spanY >= spanX)
                    {
                        bool end0IsTop = end0Local.Y >= end1Local.Y;
                        end0Local = new XYZ(
                            end0Local.X,
                            end0Local.Y + (end0IsTop ? topDelta : -bottomDelta),
                            end0Local.Z);
                        end1Local = new XYZ(
                            end1Local.X,
                            end1Local.Y + (end0IsTop ? -bottomDelta : topDelta),
                            end1Local.Z);
                    }
                    else
                    {
                        bool end0IsRight = end0Local.X >= end1Local.X;
                        end0Local = new XYZ(
                            end0Local.X + (end0IsRight ? rightDelta : -leftDelta),
                            end0Local.Y,
                            end0Local.Z);
                        end1Local = new XYZ(
                            end1Local.X + (end0IsRight ? -leftDelta : rightDelta),
                            end1Local.Y,
                            end1Local.Z);
                    }

                    XYZ newEnd0 = cropBox.Transform.OfPoint(end0Local);
                    XYZ newEnd1 = cropBox.Transform.OfPoint(end1Local);
                    if (newEnd0.DistanceTo(newEnd1) <= 1e-9)
                    {
                        AddWarning(
                            warnings,
                            "Ось '" + grid.Name + "' не подрезана: заданные смещения сводят ее к нулевой длине.");
                        continue;
                    }

                    grid.SetCurveInView(
                        DatumExtentType.ViewSpecific,
                        view,
                        Line.CreateBound(newEnd0, newEnd1));
                    adjustedCount++;
                }
                catch (Exception exception)
                {
                    AddWarning(
                        warnings,
                        "Не удалось синхронно подрезать ось '" + grid.Name +
                        "' на виде '" + view.Name + "': " + exception.Message);
                }
            }

            return adjustedCount;
        }

        public void ApplyToView(
            Document document,
            ViewSection view,
            ElevationSettings settings,
            IList<string> warnings)
        {
            if (document == null || view == null || settings == null || !view.CropBoxActive)
            {
                return;
            }

            BoundingBoxXYZ cropBox = view.CropBox;
            if (cropBox == null || cropBox.Transform == null)
            {
                return;
            }

            double paperToModelFactor = Math.Max(1, view.Scale);
            double topExtension = UnitConversionUtils.MillimetersToFeet(
                Math.Max(0.0, settings.GridTopExtensionPaperMm) * paperToModelFactor);
            double bottomExtension = UnitConversionUtils.MillimetersToFeet(
                Math.Max(0.0, settings.GridBottomExtensionPaperMm) * paperToModelFactor);

            Transform inverse = cropBox.Transform.Inverse;
            double targetTopY = Math.Max(cropBox.Min.Y, cropBox.Max.Y) + topExtension;
            double targetBottomY = Math.Min(cropBox.Min.Y, cropBox.Max.Y) - bottomExtension;

            IList<Grid> grids = new FilteredElementCollector(document, view.Id)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .Where(grid => grid != null)
                .ToList();

            for (int index = 0; index < grids.Count; index++)
            {
                Grid grid = grids[index];
                try
                {
                    IList<Curve> curves = grid.GetCurvesInView(DatumExtentType.Model, view);
                    if (curves == null || curves.Count == 0)
                    {
                        curves = grid.GetCurvesInView(DatumExtentType.ViewSpecific, view);
                    }

                    Curve sourceCurve = curves != null
                        ? curves.FirstOrDefault(curve => curve != null && curve.IsBound)
                        : null;
                    if (sourceCurve == null)
                    {
                        continue;
                    }

                    XYZ end0Local = inverse.OfPoint(sourceCurve.GetEndPoint(0));
                    XYZ end1Local = inverse.OfPoint(sourceCurve.GetEndPoint(1));
                    double verticalSpan = Math.Abs(end1Local.Y - end0Local.Y);
                    if (verticalSpan <= 1e-9)
                    {
                        continue;
                    }

                    bool end0IsTop = end0Local.Y > end1Local.Y;
                    XYZ topLocal = end0IsTop ? end0Local : end1Local;
                    XYZ bottomLocal = end0IsTop ? end1Local : end0Local;

                    topLocal = new XYZ(topLocal.X, targetTopY, topLocal.Z);
                    bottomLocal = new XYZ(bottomLocal.X, targetBottomY, bottomLocal.Z);

                    XYZ newEnd0 = cropBox.Transform.OfPoint(end0IsTop ? topLocal : bottomLocal);
                    XYZ newEnd1 = cropBox.Transform.OfPoint(end0IsTop ? bottomLocal : topLocal);
                    Line viewCurve = Line.CreateBound(newEnd0, newEnd1);

                    grid.SetDatumExtentType(DatumEnds.End0, view, DatumExtentType.ViewSpecific);
                    grid.SetDatumExtentType(DatumEnds.End1, view, DatumExtentType.ViewSpecific);
                    grid.SetCurveInView(DatumExtentType.ViewSpecific, view, viewCurve);

                    DatumEnds topEnd = end0IsTop ? DatumEnds.End0 : DatumEnds.End1;
                    DatumEnds bottomEnd = end0IsTop ? DatumEnds.End1 : DatumEnds.End0;
                    if (grid.HasBubbleInView(topEnd, view))
                    {
                        grid.HideBubbleInView(topEnd, view);
                    }

                    if (grid.HasBubbleInView(bottomEnd, view))
                    {
                        grid.ShowBubbleInView(bottomEnd, view);
                    }
                }
                catch (Exception exception)
                {
                    if (warnings != null)
                    {
                        warnings.Add(
                            "Не удалось подрезать ось " + grid.Name + " на виде \"" +
                            view.Name + "\": " + exception.Message);
                    }
                }
            }
        }

        private static void AddWarning(IList<string> warnings, string warning)
        {
            if (warnings != null && !string.IsNullOrWhiteSpace(warning))
            {
                warnings.Add(warning);
            }
        }
    }
}
