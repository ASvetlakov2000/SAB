using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services;
using RevitLibraryBuilder.Services.Csv;
using System;
using System.Collections.Generic;
using asBIM;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Экспорт CSV загружаемых семейств.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ExportLoadableFamiliesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Активный UIDocument недоступен.";
                    TaskDialog.Show("Экспорт загружаемых семейств", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null)
                {
                    message = "Документ недоступен.";
                    TaskDialog.Show("Экспорт загружаемых семейств", message);
                    return Result.Failed;
                }

                string selectedFolder = OpenFolder.SelectFolderPath(
                    "Выберите папку для экспорта загружаемых семейств",
                    "ctg_loadable families");

                if (string.IsNullOrWhiteSpace(selectedFolder))
                {
                    return Result.Cancelled;
                }

                LoadableFamiliesCsvExportService service = new LoadableFamiliesCsvExportService();
                List<string> exportedFiles = service.Export(document, selectedFolder);
                string outputFolder = ExportFolderRoutingService.ResolveLoadableFamiliesExportFolder(selectedFolder);

                if (exportedFiles == null || exportedFiles.Count == 0)
                {
                    TaskDialog.Show(
                        "Экспорт загружаемых семейств",
                        "Не найдено загружаемых типов для экспорта.");
                    return Result.Cancelled;
                }

                ToastNotifier.ShowFolderLinkSuccess(
                    "Экспорт завершен",
                    "Создано CSV файлов: " + exportedFiles.Count,
                    outputFolder,
                    10);

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Экспорт загружаемых семейств", exception.ToString());
                return Result.Failed;
            }
        }
    }
}
