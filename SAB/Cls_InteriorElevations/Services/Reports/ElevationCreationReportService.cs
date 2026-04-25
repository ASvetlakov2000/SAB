using System;
using System.Collections.Generic;
using System.Text;
using Autodesk.Revit.DB;
using Helpers.Notifications.ToastNotifications;
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
            int placedPlanMarksCount,
            int placedSheetMarksCount,
            IList<string> warnings)
        {
            int createdCount = creationResult != null ? creationResult.CreatedViews.Count : 0;
            int failedCount = creationResult != null ? creationResult.FailedViews.Count : 0;

            StringBuilder reportBuilder = new StringBuilder();
            reportBuilder.AppendLine("Отчет SAB по созданию разверток");
            reportBuilder.AppendLine();
            reportBuilder.AppendLine("Выбрано линий: " + selectedLinesCount);
            reportBuilder.AppendLine("Создано разверток: " + createdCount);
            reportBuilder.AppendLine("Не удалось создать: " + failedCount);
            reportBuilder.AppendLine("Марок углов на плане: " + placedPlanMarksCount);
            reportBuilder.AppendLine("Марок углов на листе: " + placedSheetMarksCount);

            if (createdSheet != null)
            {
                reportBuilder.AppendLine("Создан лист: " + createdSheet.SheetNumber + " | " + createdSheet.Name);
                reportBuilder.AppendLine("Размещено видовых экранов: " + placedViewportCount);
            }
            else
            {
                reportBuilder.AppendLine("Лист: не создан");
                reportBuilder.AppendLine("Размещено видовых экранов: 0");
            }

            if (warnings != null && warnings.Count > 0)
            {
                reportBuilder.AppendLine();
                reportBuilder.AppendLine("Предупреждения: " + warnings.Count);

                int maxWarningsToShow = Math.Min(5, warnings.Count);
                for (int i = 0; i < maxWarningsToShow; i++)
                {
                    reportBuilder.AppendLine((i + 1) + ". " + warnings[i]);
                }

                if (warnings.Count > maxWarningsToShow)
                {
                    reportBuilder.AppendLine("... и еще " + (warnings.Count - maxWarningsToShow) + " предупреждений.");
                }
            }

            if (createdCount > 0)
            {
                ToastNotifier.ShowSuccess("SAB Развертки", reportBuilder.ToString(), 15);
            }
            else
            {
                ToastNotifier.ShowWarning("SAB Развертки", reportBuilder.ToString(), 15);
            }
        }
    }
}
