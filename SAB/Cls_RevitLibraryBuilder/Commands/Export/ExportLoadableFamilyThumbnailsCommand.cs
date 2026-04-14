using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services.Csv;
using RevitLibraryBuilder.Services.Revit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Выгрузка CSV + PNG миниатюр для загружаемых семейств.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ExportLoadableFamilyThumbnailsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Active UIDocument is not available.";
                    ShowErrorNotification("Выгрузка миниатюр семейств", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null || document.ActiveView == null)
                {
                    message = "Document or active view is not available.";
                    ShowErrorNotification("Выгрузка миниатюр семейств", message);
                    return Result.Failed;
                }

                TypeCollectorService collector = new TypeCollectorService();
                List<ElementType> allTypes = collector.CollectAllTypes(document);
                FamilyThumbnailCsvExportService thumbnailService = new FamilyThumbnailCsvExportService();

                if (!thumbnailService.HasLoadableFamilies(allTypes))
                {
                    ToastNotifier.ShowWarning("Выгрузка миниатюр семейств", "Загружаемые семейства не найдены.", 10);
                    return Result.Cancelled;
                }

                using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
                {
                    // Блок выбора папки экспорта CSV и PNG миниатюр
                    folderDialog.Description = "Выберите папку для выгрузки миниатюр загружаемых семейств";

                    if (folderDialog.ShowDialog() != DialogResult.OK)
                    {
                        return Result.Cancelled;
                    }

                    string outputFolder = folderDialog.SelectedPath;
                    string csvPath = thumbnailService.WriteLoadableFamilyThumbnails(outputFolder, document.Title, allTypes);
                    string csvFolder = Path.GetDirectoryName(csvPath) ?? outputFolder;

                    ToastNotifier.ShowFolderLinkSuccess(
                        "Выгрузка миниатюр завершена",
                        "CSV и PNG миниатюры загружаемых семейств сохранены:\n",
                        csvFolder,
                        12);
                }

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ShowErrorNotification("Выгрузка миниатюр семейств", exception.Message);
                return Result.Failed;
            }
        }

        private static void ShowErrorNotification(string title, string text)
        {
            ToastNotifier.ShowError(title, text, 12);
        }
    }
}
