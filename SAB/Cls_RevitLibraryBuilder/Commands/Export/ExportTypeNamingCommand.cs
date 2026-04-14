using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services;
using RevitLibraryBuilder.Services.Revit;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Выгрузка наименований типоразмеров для последующего пакетного переименования.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ExportTypeNamingCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Active UIDocument is not available.";
                    TaskDialog.Show("Выгрузка наименований типоразмеров", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null || document.ActiveView == null)
                {
                    message = "Document or active view is not available.";
                    TaskDialog.Show("Выгрузка наименований типоразмеров", message);
                    return Result.Failed;
                }

                TypeCollectorService typeCollectorService = new TypeCollectorService();
                List<ElementType> allTypes = typeCollectorService.CollectAllTypes(document);

                if (allTypes == null || allTypes.Count == 0)
                {
                    TaskDialog.Show("Выгрузка наименований типоразмеров", "Типоразмеры не найдены.");
                    return Result.Cancelled;
                }

                using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
                {
                    // Блок выбора папки для CSV наименований
                    folderDialog.Description = "Выберите папку для CSV выгрузки наименований типоразмеров";

                    if (folderDialog.ShowDialog() != DialogResult.OK)
                    {
                        return Result.Cancelled;
                    }

                    string outputFolder = folderDialog.SelectedPath;

                    CsvExportService csvExportService = new CsvExportService();
                    csvExportService.ExportToSingleCsv(
                        allTypes,
                        document,
                        outputFolder,
                        CsvExportService.TypeCsvExportMode.Naming);

                    ShowSuccessNotification(
                        "Выгрузка завершена",
                        "CSV для переименования типоразмеров сохранен:\n",
                        outputFolder);
                }

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Выгрузка наименований типоразмеров", exception.ToString());
                return Result.Failed;
            }
        }

        private static void ShowSuccessNotification(string title, string text, string folderPath)
        {
            try
            {
                ToastNotifier.ShowFolderLinkSuccess(title, text, folderPath, 10);
            }
            catch
            {
                TaskDialog.Show(title, text + folderPath);
            }
        }
    }
}
