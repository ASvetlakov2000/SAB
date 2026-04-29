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
    /// Экспорт CSV стилей линий.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ExportLineStylesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Активный UIDocument недоступен.";
                    TaskDialog.Show("Экспорт линий", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null)
                {
                    message = "Документ недоступен.";
                    TaskDialog.Show("Экспорт линий", message);
                    return Result.Failed;
                }

                string selectedFolder = OpenFolder.SelectFolderPath(
                    "Выберите папку для экспорта линий",
                    "ctg_lines-patterns");

                if (string.IsNullOrWhiteSpace(selectedFolder))
                {
                    return Result.Cancelled;
                }

                string outputFolder = ExportFolderRoutingService.ResolveLineFillExportFolder(selectedFolder);
                ExportFolderRoutingService.ConfigureThumbnailFoldersForLineFillExport(outputFolder);

                CsvFillPatternExportService service = new CsvFillPatternExportService();
                string outputFilePath = service.ExportLineStylesCsv(document, outputFolder);

                ToastNotifier.ShowFolderLinkSuccess(
                    "Экспорт завершен",
                    "CSV линий сохранен:",
                    System.IO.Path.GetDirectoryName(outputFilePath) ?? outputFolder,
                    10);

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Экспорт линий", exception.ToString());
                return Result.Failed;
            }
        }
    }
}
