using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services.Csv;
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
                    ShowErrorNotification("Выгрузка наименований типоразмеров", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null || document.ActiveView == null)
                {
                    message = "Document or active view is not available.";
                    ShowErrorNotification("Выгрузка наименований типоразмеров", message);
                    return Result.Failed;
                }

                TypeCollectorService typeCollectorService = new TypeCollectorService();
                List<ElementType> allTypes = typeCollectorService.CollectAllTypes(document);

                if (allTypes == null || allTypes.Count == 0)
                {
                    ToastNotifier.ShowWarning("Выгрузка наименований типоразмеров", "Типоразмеры не найдены.", 10);
                    return Result.Cancelled;
                }

                using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
                {
                    // Блок выбора папки для XLSX наименований
                    folderDialog.Description = "Выберите папку для XLSX выгрузки наименований типоразмеров";

                    if (folderDialog.ShowDialog() != DialogResult.OK)
                    {
                        return Result.Cancelled;
                    }

                    string outputFolder = folderDialog.SelectedPath;

                    TypeNamingCsvService typeNamingService = new TypeNamingCsvService();
                    string filePath = typeNamingService.WriteTypeNamingXlsx(outputFolder, document.Title, allTypes);

                    ToastNotifier.ShowFolderLinkSuccess(
                        "Выгрузка завершена",
                        "XLSX для переименования типоразмеров сохранен:\n",
                        System.IO.Path.GetDirectoryName(filePath) ?? outputFolder,
                        10);
                }

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ShowErrorNotification("Выгрузка наименований типоразмеров", exception.Message);
                return Result.Failed;
            }
        }

        private static void ShowErrorNotification(string title, string text)
        {
            ToastNotifier.ShowError(title, text, 12);
        }
    }
}
