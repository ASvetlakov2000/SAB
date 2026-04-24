using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Sheets
{
    public class ViewportPlacementService
    {
        public int PlaceViewsOnSheet(
            Document document,
            ViewSheet sheet,
            IList<ElevationViewData> createdViews,
            SheetLayoutSettings layoutSettings,
            IList<string> warnings)
        {
            if (document == null || sheet == null || createdViews == null || layoutSettings == null)
            {
                return 0;
            }

            int columnsCount = Math.Max(1, layoutSettings.ColumnsCount);
            int placedCount = 0;

            for (int index = 0; index < createdViews.Count; index++)
            {
                ElevationViewData elevationViewData = createdViews[index];
                if (elevationViewData == null || elevationViewData.ViewSection == null)
                {
                    continue;
                }

                int row = index / columnsCount;
                int column = index % columnsCount;

                // Block responsible for converting user sheet layout (mm) into Revit internal units (feet).
                double xFeet = UnitConversionUtils.MillimetersToFeet(layoutSettings.StartXmm + column * layoutSettings.StepXmm);
                double yFeet = UnitConversionUtils.MillimetersToFeet(layoutSettings.StartYmm - row * layoutSettings.StepYmm);

                XYZ placementPoint = new XYZ(xFeet, yFeet, 0.0);

                try
                {
                    if (!Viewport.CanAddViewToSheet(document, sheet.Id, elevationViewData.ViewSection.Id))
                    {
                        if (warnings != null)
                        {
                            warnings.Add("View " + elevationViewData.ViewName + " cannot be added to the sheet.");
                        }

                        continue;
                    }

                    Viewport.Create(document, sheet.Id, elevationViewData.ViewSection.Id, placementPoint);
                    placedCount++;
                }
                catch (Exception exception)
                {
                    if (warnings != null)
                    {
                        warnings.Add("Viewport placement failed for view " + elevationViewData.ViewName + ": " + exception.Message);
                    }
                }
            }

            return placedCount;
        }
    }
}
