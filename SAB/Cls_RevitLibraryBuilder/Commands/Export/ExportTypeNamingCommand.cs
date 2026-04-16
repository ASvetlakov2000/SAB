using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services.Csv;
using RevitLibraryBuilder.Services.Revit;
using System;
using System.Collections.Generic;
using asBIM;

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

                // Блок выбора папки через диалог с полем пути и предзаполненным именем файла.
                string outputFolder = OpenFolder.SelectFolderPath(
                    "Выберите папку для XLSX выгрузки наименований типоразмеров",
                    BuildSuggestedFileName(document.Title, "TYPE_NAMING.xlsx"));

                if (string.IsNullOrWhiteSpace(outputFolder))
                {
                    return Result.Cancelled;
                }

                TypeNamingCsvService typeNamingService = new TypeNamingCsvService();
                string filePath = typeNamingService.WriteTypeNamingXlsx(outputFolder, document.Title, allTypes);

                ToastNotifier.ShowFolderLinkSuccess(
                    "Выгрузка завершена",
                    "XLSX для переименования типоразмеров сохранен:\n",
                    System.IO.Path.GetDirectoryName(filePath) ?? outputFolder,
                    10);

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

        private static string BuildSuggestedFileName(string documentTitle, string suffix)
        {
            string safeTitle = string.IsNullOrWhiteSpace(documentTitle) ? "Project" : documentTitle.Trim();

            foreach (char invalidCharacter in System.IO.Path.GetInvalidFileNameChars())
            {
                safeTitle = safeTitle.Replace(invalidCharacter, '_');
            }

            return safeTitle + "_" + suffix;
        }
    }
}
