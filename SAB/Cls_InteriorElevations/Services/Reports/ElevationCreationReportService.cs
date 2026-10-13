using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Services.Elevations;
using SAB.InteriorElevations.Services.Sheets;
using SAB.UI.Notifications;

namespace SAB.InteriorElevations.Services.Reports
{
    // Detached values only: the modeless notification never retains Revit elements.
    public sealed class ElevationCreationNotificationData
    {
        public int CreatedViews { get; set; }
        public int FailedViews { get; set; }
        public int PlacedViewports { get; set; }
        public int PlanCornerMarks { get; set; }
        public int SheetCornerMarks { get; set; }
        public bool SheetPlacementRequested { get; set; }
        public bool HasTargetSheet { get; set; }
        public bool SheetWasCreated { get; set; }
        public string TargetSheet { get; set; }
        public IList<string> Sheets { get; set; }
        public int UnplacedViews { get; set; }
        public bool ForcedPlacementUsed { get; set; }
        public bool AutomaticFallbackUsed { get; set; }
        public int ColumnsCount { get; set; }
        public IList<string> Warnings { get; set; }
        public IList<string> FailedViewDetails { get; set; }
    }

    public class ElevationCreationReportService
    {
        public static NotificationMessage BuildNotification(ElevationCreationNotificationData result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var summary = new List<string>();
            var warnings = (result.Warnings ?? new string[0]).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
            if (result.CreatedViews > 0) summary.Add("Создано развёрток: " + result.CreatedViews + ".");
            if (result.PlanCornerMarks > 0) summary.Add("Марок углов на плане: " + result.PlanCornerMarks + ".");
            if (result.SheetCornerMarks > 0) summary.Add("Марок углов на листе: " + result.SheetCornerMarks + ".");
            if (result.HasTargetSheet && !string.IsNullOrWhiteSpace(result.TargetSheet))
                summary.Add((result.SheetWasCreated ? "Создан лист: " : "Использован лист: ") + result.TargetSheet);
            if (result.PlacedViewports > 0) summary.Add("Размещено видовых экранов: " + result.PlacedViewports + ".");
            if (result.Sheets != null && result.Sheets.Count > 1)
                summary.Add("Листов с развёртками: " + result.Sheets.Count + "." + Environment.NewLine + string.Join(Environment.NewLine, result.Sheets));
            else if (result.Sheets != null && result.Sheets.Count == 1 && !string.Equals(result.Sheets[0], result.TargetSheet, StringComparison.Ordinal))
                summary.Add("Лист с развёртками: " + result.Sheets[0]);
            if (result.ForcedPlacementUsed && result.ColumnsCount > 0 && result.PlacedViewports > 0)
                summary.Add("Расстановка: по " + result.ColumnsCount + " видов в строке.");
            if (result.FailedViews > 0) warnings.Add("Не удалось создать развёрток: " + result.FailedViews + ".");
            if (result.FailedViewDetails != null) warnings.AddRange(result.FailedViewDetails.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (result.CreatedViews == 0) warnings.Add("Развёртки не созданы.");
            if (result.UnplacedViews > 0) warnings.Add("Не размещено развёрток: " + result.UnplacedViews + ".");
            if (result.SheetPlacementRequested && result.CreatedViews > 0)
            {
                if (!result.HasTargetSheet) warnings.Add("Лист не создан или не выбран; развёртки на листе не размещены.");
                else if (result.PlacedViewports == 0) warnings.Add("Виды созданы, но размещение на листе не выполнено.");
                else if (result.PlacedViewports < result.CreatedViews && result.UnplacedViews == 0)
                    warnings.Add("Часть развёрток не размещена на листах.");
            }
            if (result.AutomaticFallbackUsed) warnings.Add("Автоматическая компоновка не выполнена; использована расстановка по ручным настройкам.");
            warnings = warnings.Distinct(StringComparer.Ordinal).ToList();
            var tone = result.CreatedViews == 0 ? NotificationTone.Error : warnings.Count > 0 ? NotificationTone.Warning : NotificationTone.Success;
            return new NotificationMessage("SAB · Развёртки", tone == NotificationTone.Success ? "Завершено" : tone == NotificationTone.Warning ? "Завершено с предупреждениями" : "Не удалось создать развёртки",
                tone, string.Join(Environment.NewLine, summary), warnings: string.Join(Environment.NewLine, warnings.Select(s => "• " + s)));
        }

        public void ShowFinalReport(
            int selectedLinesCount, ElevationViewCreationResult creationResult, ViewSheet targetSheet,
            bool sheetWasCreated, bool sheetPlacementRequested, int placedViewportCount,
            int placedPlanMarksCount, int placedSheetMarksCount, IList<string> warnings,
            IList<ViewSheet> placedSheets = null, int unplacedViewsCount = 0,
            bool forcedPlacementUsed = false, bool automaticFallbackUsed = false, int columnsCount = 0)
        {
            var data = new ElevationCreationNotificationData
            {
                CreatedViews = creationResult != null ? creationResult.CreatedViews.Count : 0,
                FailedViews = creationResult != null ? creationResult.FailedViews.Count : 0,
                PlacedViewports = placedViewportCount, PlanCornerMarks = placedPlanMarksCount, SheetCornerMarks = placedSheetMarksCount,
                SheetPlacementRequested = sheetPlacementRequested, HasTargetSheet = targetSheet != null, SheetWasCreated = sheetWasCreated,
                TargetSheet = targetSheet == null ? null : targetSheet.SheetNumber + " | " + targetSheet.Name,
                Sheets = placedSheets == null ? null : placedSheets.Where(s => s != null).Select(s => s.SheetNumber + " | " + s.Name).Distinct().ToList(),
                UnplacedViews = unplacedViewsCount, ForcedPlacementUsed = forcedPlacementUsed, AutomaticFallbackUsed = automaticFallbackUsed,
                ColumnsCount = columnsCount, Warnings = warnings,
                FailedViewDetails = creationResult == null ? null : creationResult.FailedViews.Select(v => "Развёртка " + (string.IsNullOrWhiteSpace(v.ViewName) ? "№" + v.Index : v.ViewName) + ": " + (string.IsNullOrWhiteSpace(v.FailureReason) ? "вид не создан." : v.FailureReason)).ToList()
            };
            var message = BuildNotification(data);
            try
            {
                Directory.CreateDirectory(SheetLayoutDiagnostics.DirectoryPath);
                File.WriteAllText(Path.Combine(SheetLayoutDiagnostics.DirectoryPath, "last-creation-report.txt"),
                    message.Outcome + Environment.NewLine + "Выбрано линий: " + selectedLinesCount + Environment.NewLine + message.Summary + Environment.NewLine + message.Warnings);
            }
            catch { /* Diagnostics must not prevent displaying the result. */ }
            NotificationStack.Show(Process.GetCurrentProcess().MainWindowHandle, message);
        }
    }
}
