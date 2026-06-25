using System;
using Autodesk.Revit.DB;
using SAB.CreateViewsAndSheets.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.CreateViewsAndSheets.Services
{
    public class SheetBoundsService
    {
        public bool TryGetSheetBounds(Document document, ViewSheet sheet, out SheetBounds bounds)
        {
            bounds = null;

            if (document == null || sheet == null)
            {
                return false;
            }

            BoundingBoxXYZ titleBlockBox = null;
            FilteredElementCollector titleBlockCollector = new FilteredElementCollector(document, sheet.Id)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType();

            foreach (Element element in titleBlockCollector)
            {
                if (element == null)
                {
                    continue;
                }

                titleBlockBox = element.get_BoundingBox(sheet);
                if (titleBlockBox == null)
                {
                    titleBlockBox = element.get_BoundingBox(null);
                }

                if (titleBlockBox != null)
                {
                    break;
                }
            }

            if (titleBlockBox == null || titleBlockBox.Min == null || titleBlockBox.Max == null)
            {
                return false;
            }

            bounds = BuildBoundsFromBox(titleBlockBox);
            return true;
        }

        public ElementId GetTitleBlockTypeId(Document document, ViewSheet sheet)
        {
            if (document == null || sheet == null)
            {
                return ElementId.InvalidElementId;
            }

            FilteredElementCollector titleBlockCollector = new FilteredElementCollector(document, sheet.Id)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType();

            foreach (Element element in titleBlockCollector)
            {
                FamilyInstance titleBlockInstance = element as FamilyInstance;
                if (titleBlockInstance == null || titleBlockInstance.Symbol == null)
                {
                    continue;
                }

                return titleBlockInstance.Symbol.Id;
            }

            return ElementId.InvalidElementId;
        }

        private SheetBounds BuildBoundsFromBox(BoundingBoxXYZ box)
        {
            SheetBounds bounds = new SheetBounds();
            bounds.MinXFeet = Math.Min(box.Min.X, box.Max.X);
            bounds.MinYFeet = Math.Min(box.Min.Y, box.Max.Y);
            bounds.MaxXFeet = Math.Max(box.Min.X, box.Max.X);
            bounds.MaxYFeet = Math.Max(box.Min.Y, box.Max.Y);
            bounds.FormatName = DetectIsoFormat(bounds.WidthMm, bounds.HeightMm);
            return bounds;
        }

        private string DetectIsoFormat(double widthMm, double heightMm)
        {
            double shortSide = Math.Min(widthMm, heightMm);
            double longSide = Math.Max(widthMm, heightMm);

            if (IsCloseToIsoSize(shortSide, longSide, 841, 1189))
            {
                return "A0";
            }

            if (IsCloseToIsoSize(shortSide, longSide, 594, 841))
            {
                return "A1";
            }

            if (IsCloseToIsoSize(shortSide, longSide, 420, 594))
            {
                return "A2";
            }

            if (IsCloseToIsoSize(shortSide, longSide, 297, 420))
            {
                return "A3";
            }

            if (IsCloseToIsoSize(shortSide, longSide, 210, 297))
            {
                return "A4";
            }

            return Math.Round(widthMm, 0) + " x " + Math.Round(heightMm, 0) + " мм";
        }

        private bool IsCloseToIsoSize(double shortSide, double longSide, double expectedShort, double expectedLong)
        {
            const double toleranceMm = 20.0;
            return Math.Abs(shortSide - expectedShort) <= toleranceMm &&
                   Math.Abs(longSide - expectedLong) <= toleranceMm;
        }
    }
}
