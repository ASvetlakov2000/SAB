using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services;
using RevitLibraryBuilder.Services.Csv;
using System;
using asBIM;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Экспорт CSV штриховок.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ExportFillPatternsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Активный UIDocument недоступен.";
                    TaskDialog.Show("Экспорт штриховок", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null)
                {
                    message = "Документ недоступен.";
                    TaskDialog.Show("Экспорт штриховок", message);
                    return Result.Failed;
                }

                string selectedFolder = OpenFolder.SelectFolderPath(
                    "Выберите папку для экспорта штриховок",
                    "ctg_lines-patterns");

                if (string.IsNullOrWhiteSpace(selectedFolder))
                {
                    return Result.Cancelled;
                }

                string outputFolder = ExportFolderRoutingService.ResolveLineFillExportFolder(selectedFolder);
                ExportFolderRoutingService.ConfigureThumbnailFoldersForLineFillExport(outputFolder);

                CsvFillPatternExportService service = new CsvFillPatternExportService();
                string outputFilePath = service.ExportFillPatternsCsv(document, outputFolder);

                ToastNotifier.ShowFolderLinkSuccess(
                    "Экспорт завершен",
                    "CSV штриховок сохранен:",
                    System.IO.Path.GetDirectoryName(outputFilePath) ?? outputFolder,
                    10);

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Экспорт штриховок", exception.ToString());
                return Result.Failed;
            }
        }
    }
}
