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
    /// Выгрузка шаблона CSV для миниатюр системных семейств.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ExportSystemFamilyThumbnailTemplateCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Active UIDocument is not available.";
                    ShowErrorNotification("Шаблон миниатюр системных семейств", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null || document.ActiveView == null)
                {
                    message = "Document or active view is not available.";
                    ShowErrorNotification("Шаблон миниатюр системных семейств", message);
                    return Result.Failed;
                }

                TypeCollectorService collector = new TypeCollectorService();
                List<ElementType> allTypes = collector.CollectAllTypes(document);
                FamilyThumbnailCsvExportService thumbnailService = new FamilyThumbnailCsvExportService();

                if (!thumbnailService.HasSystemFamilies(allTypes))
                {
                    ToastNotifier.ShowWarning("Шаблон миниатюр системных семейств", "Системные семейства не найдены.", 10);
                    return Result.Cancelled;
                }

                using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
                {
                    // Блок выбора папки выгрузки шаблона CSV
                    folderDialog.Description = "Выберите папку для выгрузки шаблона миниатюр системных семейств";

                    if (folderDialog.ShowDialog() != DialogResult.OK)
                    {
                        return Result.Cancelled;
                    }

                    string outputFolder = folderDialog.SelectedPath;
                    string csvPath = thumbnailService.WriteSystemFamilyThumbnailTemplate(outputFolder, document.Title, allTypes);
                    string csvFolder = Path.GetDirectoryName(csvPath) ?? outputFolder;

                    ToastNotifier.ShowFolderLinkSuccess(
                        "Выгрузка шаблона завершена",
                        "Шаблон CSV для системных семейств сохранен:\n",
                        csvFolder,
                        12);
                }

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ShowErrorNotification("Шаблон миниатюр системных семейств", exception.Message);
                return Result.Failed;
            }
        }

        private static void ShowErrorNotification(string title, string text)
        {
            ToastNotifier.ShowError(title, text, 12);
        }
    }
}
