using System.Collections.Generic;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SAB.InteriorElevations.Services.Elevations;

namespace SAB.InteriorElevations.Services.Reports
{
    public class ElevationCreationReportService
    {
        public void ShowFinalReport(
            int selectedLinesCount,
            ElevationViewCreationResult creationResult,
            ViewSheet createdSheet,
            int placedViewportCount,
            IList<string> warnings)
        {
            StringBuilder reportBuilder = new StringBuilder();
            reportBuilder.AppendLine("SAB Interior Elevations report");
            reportBuilder.AppendLine(string.Empty);
            reportBuilder.AppendLine("Selected lines: " + selectedLinesCount);
            reportBuilder.AppendLine("Created elevations: " + creationResult.CreatedViews.Count);
            reportBuilder.AppendLine("Failed elevations: " + creationResult.FailedViews.Count);

            if (createdSheet != null)
            {
                reportBuilder.AppendLine("Created sheet: " + createdSheet.SheetNumber + " | " + createdSheet.Name);
                reportBuilder.AppendLine("Placed viewports: " + placedViewportCount);
            }
            else
            {
                reportBuilder.AppendLine("Sheet: not created");
                reportBuilder.AppendLine("Placed viewports: 0");
            }

            if (warnings != null && warnings.Count > 0)
            {
                reportBuilder.AppendLine(string.Empty);
                reportBuilder.AppendLine("Warnings:");

                for (int i = 0; i < warnings.Count; i++)
                {
                    reportBuilder.AppendLine((i + 1) + ". " + warnings[i]);
                }
            }

            TaskDialog.Show("SAB Interior Elevations", reportBuilder.ToString());
        }
    }
}
