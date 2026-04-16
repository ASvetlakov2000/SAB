using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services;
using RevitLibraryBuilder.Services.Revit;
using System;
using System.Collections.Generic;
using asBIM;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// PNG export for loadable family thumbnails (without CSV).
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
                LoadableFamilyThumbnailExportService thumbnailService = new LoadableFamilyThumbnailExportService();

                if (!thumbnailService.HasLoadableFamilies(allTypes))
                {
                    ToastNotifier.ShowWarning("Выгрузка миниатюр семейств", "Загружаемые семейства не найдены.", 10);
                    return Result.Cancelled;
                }

                // Блок выбора корневой папки через диалог с полем пути.
                string rootFolder = OpenFolder.SelectFolderPath(
                    "Выберите папку для выгрузки миниатюр загружаемых семейств",
                    "PNG_Family");

                if (string.IsNullOrWhiteSpace(rootFolder))
                {
                    return Result.Cancelled;
                }

                LoadableFamilyThumbnailExportResult exportResult =
                    thumbnailService.ExportToFolder(document, allTypes, rootFolder);

                if (exportResult.ExportedCount == 0)
                {
                    ToastNotifier.ShowWarning("Выгрузка миниатюр семейств", "Не удалось выгрузить миниатюры.", 10);
                    return Result.Cancelled;
                }

                // Блок сохранения пути для дальнейшего использования в dashboard.
                ThumbnailFoldersRuntimeStore.SetLoadableFamilyImagesFolder(exportResult.OutputFolderPath);

                ToastNotifier.ShowFolderLinkSuccess(
                    "Выгрузка миниатюр завершена",
                    "PNG миниатюры загружаемых семейств сохранены:\n",
                    exportResult.OutputFolderPath,
                    12);

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
