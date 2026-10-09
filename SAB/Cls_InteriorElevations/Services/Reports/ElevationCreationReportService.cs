using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using SAB.InteriorElevations.Services.Elevations;
using SAB.InteriorElevations.Services.Sheets;

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
            IList<string> warnings,
            IList<ViewSheet> placedSheets = null,
            int unplacedViewsCount = 0,
            bool forcedPlacementUsed = false,
            bool automaticFallbackUsed = false,
            int columnsCount = 0)
        {
            int createdCount = creationResult != null ? creationResult.CreatedViews.Count : 0;
            int failedCount = creationResult != null ? creationResult.FailedViews.Count : 0;
            string planPlacementWarning = null;
            if (warnings != null)
                foreach (string warning in warnings)
                    if (warning.StartsWith("Не удалось разместить план-схему:", StringComparison.Ordinal))
                    { planPlacementWarning = warning; break; }

            StringBuilder reportBuilder = new StringBuilder();
            reportBuilder.AppendLine("Отчет SAB по созданию разверток");
            reportBuilder.AppendLine();
            reportBuilder.AppendLine("Выбрано линий: " + selectedLinesCount);
            reportBuilder.AppendLine("Создано разверток: " + createdCount);
            reportBuilder.AppendLine("Не удалось создать: " + failedCount);
            reportBuilder.AppendLine("Марок углов на плане: " + placedPlanMarksCount);
            reportBuilder.AppendLine("Марок углов на листе: " + placedSheetMarksCount);
            if (forcedPlacementUsed)
                reportBuilder.AppendLine("Расстановка: по " + columnsCount + " видов в строке" +
                    (automaticFallbackUsed ? " (переход после неудачи автоматической компоновки)." : "."));

            if (targetSheet != null)
            {
                reportBuilder.AppendLine(
                    (sheetWasCreated ? "Создан лист: " : "Использован лист: ") +
                    targetSheet.SheetNumber + " | " + targetSheet.Name);
                reportBuilder.AppendLine("Размещено видовых экранов: " + placedViewportCount);
                if (placedSheets != null && (placedSheets.Count > 1 ||
                    (placedSheets.Count == 1 && !placedSheets[0].Id.Equals(targetSheet.Id))))
                {
                    reportBuilder.AppendLine("Листов с развертками: " + placedSheets.Count);
                    foreach (ViewSheet sheet in placedSheets)
                        reportBuilder.AppendLine("  " + sheet.SheetNumber + " | " + sheet.Name);
                }
                if (unplacedViewsCount > 0) reportBuilder.AppendLine("Не размещено разверток: " + unplacedViewsCount);
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
                foreach (string warning in warnings) reportBuilder.AppendLine("• " + warning);
            }

            try
            {
                Directory.CreateDirectory(SheetLayoutDiagnostics.DirectoryPath);
                File.WriteAllText(Path.Combine(SheetLayoutDiagnostics.DirectoryPath, "last-creation-report.txt"), reportBuilder.ToString());
            }
            catch { /* The report must still be shown if writing diagnostics fails. */ }

            if (sheetPlacementRequested && createdCount > 0 && (placedViewportCount == 0 || unplacedViewsCount > 0 || automaticFallbackUsed || planPlacementWarning != null))
            {
                string placementMessage = warnings != null && warnings.Count > 0
                    ? warnings[warnings.Count - 1] : "Причина указана в подробном отчете.";
                if (automaticFallbackUsed && warnings != null)
                    foreach (string warning in warnings)
                        if (warning.StartsWith("Автоматическая компоновка не выполнена:", StringComparison.Ordinal))
                        { placementMessage = warning; break; }
                if (planPlacementWarning != null) placementMessage = planPlacementWarning;
                var dialog = new TaskDialog("SAB Развертки")
                {
                    MainIcon = TaskDialogIcon.TaskDialogIconWarning,
                    MainInstruction = placedViewportCount == 0 ? "Виды созданы, но размещение на листах не выполнено." :
                        unplacedViewsCount > 0 ? "Часть видов не удалось разместить." : planPlacementWarning != null ?
                        "Развертки размещены, но план-схему не удалось разместить." : "Виды размещены по ручным настройкам после неудачи автоматической компоновки.",
                    MainContent = "Создано разверток: " + createdCount + ". Размещено видовых экранов: " + placedViewportCount + "." +
                        Environment.NewLine + placementMessage,
                    ExpandedContent = reportBuilder.ToString(),
                    CommonButtons = TaskDialogCommonButtons.Close
                };
                dialog.Show();
            }

            if (createdCount > 0 && unplacedViewsCount == 0 && !automaticFallbackUsed && planPlacementWarning == null && (!sheetPlacementRequested || placedViewportCount > 0))
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
