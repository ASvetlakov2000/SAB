using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services;
using RevitLibraryBuilder.Services.Views;
using System;
using System.IO;
using System.Text;
using Forms = System.Windows.Forms;

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

                using (Forms.FolderBrowserDialog folderDialog = new Forms.FolderBrowserDialog())
                {
                    // Block responsible for selecting output folder for image export.
                    folderDialog.Description = "Select folder for exporting legend component images";

                    if (folderDialog.ShowDialog() != Forms.DialogResult.OK)
                    {
                        return Result.Cancelled;
                    }

                    string selectedRootFolder = folderDialog.SelectedPath;
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

                    // Блок сохранения пути в runtime-памяти для dashboard.
                    ThumbnailFoldersRuntimeStore.SetSystemFamilyImagesFolder(outputFolder);

                    string summary = BuildSummary(exportResult);

                    if (exportResult.SkippedCount > 0)
                    {
                        ToastNotifier.ShowWarning("Legend image export completed", summary, 16);
                    }
                    else
                    {
                        ToastNotifier.ShowFolderLinkSuccess(
                            "Legend image export completed",
                            summary + "\n\nOutput folder:",
                            outputFolder,
                            14);
                    }
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
        private static string BuildSummary(LegendComponentImageExportResult exportResult)
        {
            StringBuilder summary = new StringBuilder();
            summary.AppendLine("Total legend components on view: " + exportResult.TotalLegendComponentsOnView);
            summary.AppendLine("Exported images: " + exportResult.ExportedCount);
            summary.AppendLine("Skipped items: " + exportResult.SkippedCount);

            if (exportResult.SkippedDetails.Count > 0)
            {
                summary.AppendLine();
                summary.AppendLine("Skip details:");

                for (int i = 0; i < exportResult.SkippedDetails.Count; i++)
                {
                    summary.AppendLine((i + 1) + ". " + exportResult.SkippedDetails[i]);
                }
            }

            return summary.ToString().Trim();
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
