using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services.Revit;
using System;
using System.IO;
using System.Text;
using asBIM;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Loads preview images into system family types through ALL_MODEL_TYPE_IMAGE.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class LoadSystemFamilyTypeImagesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            const string commandTitle = "Загрузка изображений типов";

            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Активный UIDocument недоступен.";
                    ToastNotifier.ShowError(commandTitle, message, 12);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null)
                {
                    message = "Документ недоступен.";
                    ToastNotifier.ShowError(commandTitle, message, 12);
                    return Result.Failed;
                }

                string selectedFolderPath = RequestFolderPath();

                if (string.IsNullOrWhiteSpace(selectedFolderPath))
                {
                    return Result.Cancelled;
                }

                SystemFamilyTypeImageLoaderService loaderService = new SystemFamilyTypeImageLoaderService();
                SystemFamilyTypeImageLoaderResult loadResult = loaderService.LoadFromFolder(document, selectedFolderPath);
                string summaryText = BuildSummary(loadResult);

                if (HasWarnings(loadResult))
                {
                    ToastNotifier.ShowFolderLinkWarning(commandTitle, summaryText + "\n\nПапка:", selectedFolderPath, 14);
                }
                else
                {
                    ToastNotifier.ShowFolderLinkSuccess(commandTitle, summaryText + "\n\nПапка:", selectedFolderPath, 12);
                }

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ToastNotifier.ShowError(commandTitle, exception.Message, 12);
                return Result.Failed;
            }
        }

        // Block responsible for selecting a folder with exported images.
        private static string RequestFolderPath()
        {
            return OpenFolder.SelectFolderPath(
                "Выберите папку с изображениями типов",
                "PNG_Pirogi");
        }

        // Block responsible for building user-friendly completion summary.
        private static string BuildSummary(SystemFamilyTypeImageLoaderResult loadResult)
        {
            StringBuilder summary = new StringBuilder();
            summary.AppendLine("Найдено файлов изображений: " + loadResult.TotalImageFilesFound);
            summary.AppendLine("Найдено целевых типов: " + loadResult.TotalSupportedRevitTypesFound);
            summary.AppendLine("Совпадений по имени: " + loadResult.MatchedPairsCount);
            summary.AppendLine("Успешно назначено: " + loadResult.SuccessfullyAssignedCount);
            summary.AppendLine("Уже назначено: " + loadResult.AlreadyAssignedCount);
            summary.AppendLine("Типов без изображения: " + loadResult.UnmatchedTypes.Count);
            summary.AppendLine("Изображений без типа: " + loadResult.UnmatchedImageFiles.Count);
            summary.AppendLine("Ошибок: " + loadResult.Errors.Count);

            if (loadResult.UnsupportedImageFiles.Count > 0)
            {
                summary.AppendLine("Неподдерживаемых форматов: " + loadResult.UnsupportedImageFiles.Count);
            }

            if (loadResult.Errors.Count > 0)
            {
                summary.AppendLine();
                summary.AppendLine("Первые ошибки:");

                int previewCount = Math.Min(10, loadResult.Errors.Count);

                for (int i = 0; i < previewCount; i++)
                {
                    summary.AppendLine((i + 1) + ". " + loadResult.Errors[i]);
                }
            }

            return summary.ToString().Trim();
        }

        private static bool HasWarnings(SystemFamilyTypeImageLoaderResult loadResult)
        {
            if (loadResult == null)
            {
                return true;
            }

            return
                loadResult.Errors.Count > 0 ||
                loadResult.UnmatchedTypes.Count > 0 ||
                loadResult.UnmatchedImageFiles.Count > 0 ||
                loadResult.UnsupportedImageFiles.Count > 0;
        }
    }
}
