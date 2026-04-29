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
    /// Экспорт CSV системных семейств.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ExportSystemFamiliesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Активный UIDocument недоступен.";
                    TaskDialog.Show("Экспорт системных семейств", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null)
                {
                    message = "Документ недоступен.";
                    TaskDialog.Show("Экспорт системных семейств", message);
                    return Result.Failed;
                }

                string selectedFolder = OpenFolder.SelectFolderPath(
                    "Выберите папку для экспорта системных семейств",
                    "ctg_system families");

                if (string.IsNullOrWhiteSpace(selectedFolder))
                {
                    return Result.Cancelled;
                }

                SystemFamiliesCsvExportService service = new SystemFamiliesCsvExportService();
                List<string> exportedFiles = service.Export(document, selectedFolder);
                string outputFolder = ExportFolderRoutingService.ResolveSystemFamiliesExportFolder(selectedFolder);

                if (exportedFiles == null || exportedFiles.Count == 0)
                {
                    TaskDialog.Show(
                        "Экспорт системных семейств",
                        "Не найдено типов для экспорта в категориях:\n- Стены\n- Перекрытия\n- Потолки\n- Крыши");
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
                TaskDialog.Show("Экспорт системных семейств", exception.ToString());
                return Result.Failed;
            }
        }
    }
}
