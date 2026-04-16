using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services;
using RevitLibraryBuilder.Services.Csv;
using RevitLibraryBuilder.Services.Views;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using asBIM;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Exports PNG images for all legend components on active Legend view.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ExportSystemFamilyThumbnailTemplateCommand : IExternalCommand
    {
        private const string ExportFolderName = "PNG_Pirogi";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                string commandTitle = "Export Legend Component Images";
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Active UIDocument is not available.";
                    ShowErrorNotification(commandTitle, message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null || document.ActiveView == null)
                {
                    message = "Document or active view is not available.";
                    ShowErrorNotification(commandTitle, message);
                    return Result.Failed;
                }

                View activeView = document.ActiveView;

                if (activeView.ViewType != ViewType.Legend)
                {
                    message = "Open a Legend view before running this command.";
                    ShowErrorNotification(commandTitle, message);
                    return Result.Failed;
                }

                // Block responsible for selecting output folder for image export.
                string selectedRootFolder = OpenFolder.SelectFolderPath(
                    "Select folder for exporting legend component images",
                    ExportFolderName);

                if (string.IsNullOrWhiteSpace(selectedRootFolder))
                {
                    return Result.Cancelled;
                }

                string outputFolder = BuildExportFolderPath(selectedRootFolder);
                Directory.CreateDirectory(outputFolder);

                LegendComponentImageExportService exportService = new LegendComponentImageExportService();
                LegendComponentImageExportResult exportResult =
                    exportService.ExportAllFromActiveLegend(uiDocument, activeView, outputFolder);

                if (!string.IsNullOrWhiteSpace(exportResult.FatalError))
                {
                    message = exportResult.FatalError;
                    ShowErrorNotification(commandTitle, exportResult.FatalError);
                    return Result.Failed;
                }

                // Block responsible for writing report with detailed reasons.
                string reportPath = WriteExportReport(outputFolder, exportResult);

                // Блок сохранения пути в runtime-памяти для dashboard.
                ThumbnailFoldersRuntimeStore.SetSystemFamilyImagesFolder(outputFolder);

                string summary = BuildSummary(exportResult, reportPath);

                if (exportResult.SkippedCount > 0)
                {
                    ToastNotifier.ShowFolderLinkWarning(
                        "Legend image export completed with issues",
                        summary,
                        outputFolder,
                        18);
                }
                else
                {
                    ToastNotifier.ShowFolderLinkSuccess(
                        "Legend image export completed",
                        summary,
                        outputFolder,
                        16);
                }

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ShowErrorNotification("Export Legend Component Images", exception.Message);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Builds human-readable summary for final user notification.
        /// </summary>
        private static string BuildSummary(LegendComponentImageExportResult exportResult, string reportPath)
        {
            StringBuilder summary = new StringBuilder();
            summary.AppendLine("Total legend components on view: " + exportResult.TotalLegendComponentsOnView);
            summary.AppendLine("Exported images: " + exportResult.ExportedCount);
            summary.AppendLine("Skipped items: " + exportResult.SkippedCount);
            summary.AppendLine("Sanitized file names: " + exportResult.RenamedCount);

            if (!string.IsNullOrWhiteSpace(reportPath) && File.Exists(reportPath))
            {
                summary.AppendLine("Report: " + Path.GetFileName(reportPath));
            }

            if (exportResult.SkippedDetails.Count > 0)
            {
                summary.AppendLine();
                summary.AppendLine("Skip details:");

                int limit = Math.Min(10, exportResult.SkippedDetails.Count);

                for (int i = 0; i < limit; i++)
                {
                    summary.AppendLine((i + 1) + ". " + exportResult.SkippedDetails[i]);
                }

                if (exportResult.SkippedDetails.Count > limit)
                {
                    summary.AppendLine("... and " + (exportResult.SkippedDetails.Count - limit) + " more. See report file.");
                }
            }

            return summary.ToString().Trim();
        }

        /// <summary>
        /// Creates detailed CSV report with export status by each legend component.
        /// </summary>
        private static string WriteExportReport(string outputFolder, LegendComponentImageExportResult exportResult)
        {
            if (exportResult == null || exportResult.ReportItems == null || exportResult.ReportItems.Count == 0)
            {
                return string.Empty;
            }

            try
            {
                string reportFileName = "Отчет_экспорта_PNG_пирогов_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv";
                string reportPath = Path.Combine(outputFolder, reportFileName);

                List<string> header = new List<string>
                {
                    "OriginalTypeName",
                    "NormalizedFileName",
                    "ExportedFileName",
                    "Status",
                    "ErrorText"
                };

                List<List<string>> rows = new List<List<string>>();

                for (int i = 0; i < exportResult.ReportItems.Count; i++)
                {
                    LegendComponentImageExportReportItem item = exportResult.ReportItems[i];

                    rows.Add(new List<string>
                    {
                        item.OriginalTypeName ?? string.Empty,
                        item.NormalizedFileName ?? string.Empty,
                        item.ExportedFileName ?? string.Empty,
                        item.Status ?? string.Empty,
                        item.ErrorText ?? string.Empty
                    });
                }

                CsvTableService csvTableService = new CsvTableService();
                csvTableService.Write(reportPath, header, rows);

                return reportPath;
            }
            catch
            {
                // Report generation must not break successful export flow.
                return string.Empty;
            }
        }

        /// <summary>
        /// Creates dedicated output folder path inside user-selected root path.
        /// </summary>
        private static string BuildExportFolderPath(string rootFolder)
        {
            return Path.Combine(rootFolder, ExportFolderName);
        }

        private static void ShowErrorNotification(string title, string text)
        {
            ToastNotifier.ShowError(title, text, 12);
        }
    }
}