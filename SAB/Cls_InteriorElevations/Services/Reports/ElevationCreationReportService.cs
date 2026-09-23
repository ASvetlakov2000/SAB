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
            ViewSheet targetSheet,
            bool sheetWasCreated,
            bool sheetPlacementRequested,
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

            if (targetSheet != null)
            {
                reportBuilder.AppendLine(
                    (sheetWasCreated ? "Создан лист: " : "Использован лист: ") +
                    targetSheet.SheetNumber + " | " + targetSheet.Name);
                reportBuilder.AppendLine("Размещено видовых экранов: " + placedViewportCount);
            }
            else
            {
                reportBuilder.AppendLine(sheetPlacementRequested
                    ? "Лист: не создан или не выбран"
                    : "Размещение на листе: отключено");
                reportBuilder.AppendLine("Размещено видовых экранов: 0");
                if (sheetPlacementRequested && warnings != null && warnings.Count > 0)
                {
                    reportBuilder.AppendLine("Причина: " + warnings[warnings.Count - 1]);
                }
            }

            if (targetSheet != null && createdCount > 0 && placedViewportCount == 0 &&
                warnings != null && warnings.Count > 0)
            {
                reportBuilder.AppendLine("Причина отсутствия видов на листе: " + warnings[warnings.Count - 1]);
            }

            if (warnings != null && warnings.Count > 0)
            {
                reportBuilder.AppendLine();
                reportBuilder.AppendLine("Предупреждения: " + warnings.Count);
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
