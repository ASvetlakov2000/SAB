using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services.Csv;
using RevitLibraryBuilder.Services.Views;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Command places legend components by predefined categories on active Legend view.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class PlaceLegendComponentsByCategoriesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                // Block responsible for active document and view validation.
                if (uiDocument == null)
                {
                    message = "Active UIDocument is not available.";
                    ToastNotifier.ShowError("Place Legend Components", message, 12);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null)
                {
                    message = "Document is not available.";
                    ToastNotifier.ShowError("Place Legend Components", message, 12);
                    return Result.Failed;
                }

                View activeView = document.ActiveView;

                if (activeView == null)
                {
                    message = "Active view is not available.";
                    ToastNotifier.ShowError("Place Legend Components", message, 12);
                    return Result.Failed;
                }

                if (activeView.ViewType != ViewType.Legend)
                {
                    message = "Open a Legend view before running this command.";
                    ToastNotifier.ShowError("Place Legend Components", message, 12);
                    return Result.Failed;
                }

                LegendComponentPlacementService placementService = new LegendComponentPlacementService();
                LegendComponentPlacementResult placementResult;

                // Block responsible for transaction boundaries around placement changes.
                using (Transaction transaction = new Transaction(document, "Place legend components by categories"))
                {
                    transaction.Start();

                    placementResult = placementService.PlaceByCategories(document, activeView);

                    if (!string.IsNullOrWhiteSpace(placementResult.FatalError))
                    {
                        transaction.RollBack();
                        message = placementResult.FatalError;
                        ToastNotifier.ShowError("Place Legend Components", placementResult.FatalError, 14);
                        return Result.Failed;
                    }

                    transaction.Commit();
                }

                string reportPath = WritePlacementReport(document, placementResult);
                string summaryText = BuildSummaryText(placementResult, reportPath);

                // Block responsible for final user notification.
                if (placementResult.SkippedDetails.Count > 0)
                {
                    string folderToOpen = ResolveFolderForNotification(document, reportPath);

                    if (!string.IsNullOrWhiteSpace(folderToOpen))
                    {
                        ToastNotifier.ShowFolderLinkWarning(
                            "Legend component placement completed with issues",
                            summaryText,
                            folderToOpen,
                            18);
                    }
                    else
                    {
                        ToastNotifier.ShowWarning("Legend component placement completed with issues", summaryText, 18);
                    }
                }
                else
                {
                    ToastNotifier.ShowSuccess("Legend component placement completed", summaryText, 12);
                }

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ToastNotifier.ShowError("Place Legend Components", message, 12);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Builds user summary including optional report file reference.
        /// </summary>
        private static string BuildSummaryText(LegendComponentPlacementResult placementResult, string reportPath)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Requested categories: " + placementResult.RequestedCategoriesCount);
            builder.AppendLine("Requested types: " + placementResult.RequestedTypeCount);
            builder.AppendLine("Placed components: " + placementResult.PlacedCount);
            builder.AppendLine("Skipped items: " + placementResult.SkippedDetails.Count);

            if (!string.IsNullOrWhiteSpace(reportPath) && File.Exists(reportPath))
            {
                builder.AppendLine("Report: " + Path.GetFileName(reportPath));
            }

            if (placementResult.SkippedDetails.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Skip details:");

                int limit = Math.Min(10, placementResult.SkippedDetails.Count);

                for (int i = 0; i < limit; i++)
                {
                    builder.AppendLine((i + 1) + ". " + placementResult.SkippedDetails[i]);
                }

                if (placementResult.SkippedDetails.Count > limit)
                {
                    builder.AppendLine("... and " + (placementResult.SkippedDetails.Count - limit) + " more. See report file.");
                }
            }

            return builder.ToString().Trim();
        }

        /// <summary>
        /// Writes CSV report with all skipped legend component placements.
        /// </summary>
        private static string WritePlacementReport(Document document, LegendComponentPlacementResult placementResult)
        {
            if (placementResult == null || placementResult.Issues == null || placementResult.Issues.Count == 0)
            {
                return string.Empty;
            }

            try
            {
                string reportFolder = ResolveReportFolder(document);

                if (string.IsNullOrWhiteSpace(reportFolder))
                {
                    return string.Empty;
                }

                Directory.CreateDirectory(reportFolder);

                string reportFileName = "Отчет_расстановки_компонентов_легенды_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv";
                string reportPath = Path.Combine(reportFolder, reportFileName);

                List<string> header = new List<string>
                {
                    "Category",
                    "TypeName",
                    "ErrorText"
                };

                List<List<string>> rows = new List<List<string>>();

                for (int i = 0; i < placementResult.Issues.Count; i++)
                {
                    LegendComponentPlacementIssue issue = placementResult.Issues[i];

                    rows.Add(new List<string>
                    {
                        issue.Category ?? string.Empty,
                        issue.TypeName ?? string.Empty,
                        issue.ErrorText ?? string.Empty
                    });
                }

                CsvTableService csvTableService = new CsvTableService();
                csvTableService.Write(reportPath, header, rows);

                return reportPath;
            }
            catch
            {
                // Report generation must not break placement workflow.
                return string.Empty;
            }
        }

        /// <summary>
        /// Resolves folder for skipped-item report.
        /// </summary>
        private static string ResolveReportFolder(Document document)
        {
            if (document != null && !string.IsNullOrWhiteSpace(document.PathName))
            {
                string modelFolder = Path.GetDirectoryName(document.PathName);

                if (!string.IsNullOrWhiteSpace(modelFolder))
                {
                    return modelFolder;
                }
            }

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            if (!string.IsNullOrWhiteSpace(desktop))
            {
                return desktop;
            }

            return Path.GetTempPath();
        }

        /// <summary>
        /// Resolves folder path for toast link.
        /// </summary>
        private static string ResolveFolderForNotification(Document document, string reportPath)
        {
            if (!string.IsNullOrWhiteSpace(reportPath))
            {
                string reportFolder = Path.GetDirectoryName(reportPath);

                if (!string.IsNullOrWhiteSpace(reportFolder) && Directory.Exists(reportFolder))
                {
                    return reportFolder;
                }
            }

            string fallback = ResolveReportFolder(document);

            if (!string.IsNullOrWhiteSpace(fallback) && Directory.Exists(fallback))
            {
                return fallback;
            }

            return string.Empty;
        }
    }
}