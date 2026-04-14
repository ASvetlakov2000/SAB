using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services.Revit;
using System;
using System.IO;
using System.Windows.Forms;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Загрузка PNG пирогов в системный параметр "Изображение типоразмера".
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ImportSystemTypePiePngCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Active UIDocument is not available.";
                    ToastNotifier.ShowError("Загрузить PNG пироги в типы конструкций", message, 12);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null)
                {
                    message = "Document is not available.";
                    ToastNotifier.ShowError("Загрузить PNG пироги в типы конструкций", message, 12);
                    return Result.Failed;
                }

                string imageFolderPath = RequestImageFolderPath();

                if (string.IsNullOrWhiteSpace(imageFolderPath))
                {
                    return Result.Cancelled;
                }

                SystemTypePieImageLoadService loadService = new SystemTypePieImageLoadService();
                SystemTypePieImageLoadResult loadResult = loadService.LoadFromFolder(document, imageFolderPath);
                ShowResultNotification(loadResult, imageFolderPath);
                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ToastNotifier.ShowError("Загрузить PNG пироги в типы конструкций", exception.Message, 12);
                return Result.Failed;
            }
        }

        private static string RequestImageFolderPath()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Выберите папку с PNG изображениями пирогов";

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return null;
                }

                if (string.IsNullOrWhiteSpace(dialog.SelectedPath) || !Directory.Exists(dialog.SelectedPath))
                {
                    return null;
                }

                return dialog.SelectedPath;
            }
        }

        private static void ShowResultNotification(SystemTypePieImageLoadResult result, string imageFolderPath)
        {
            string summaryText = result.BuildSummaryText();

            if (!string.IsNullOrWhiteSpace(result.ReportPath) && File.Exists(result.ReportPath))
            {
                string folder = Path.GetDirectoryName(result.ReportPath) ?? imageFolderPath;

                ToastNotifier.ShowFolderLinkWarning(
                    "Загрузка PNG пирогов завершена",
                    summaryText + "\nСформирован отчет по проблемам.",
                    folder,
                    14);

                return;
            }

            if (result.Errors.Count > 0)
            {
                ToastNotifier.ShowWarning("Загрузка PNG пирогов завершена", summaryText, 12);
            }
            else
            {
                ToastNotifier.ShowFolderLinkSuccess(
                    "Загрузка PNG пирогов завершена",
                    summaryText,
                    imageFolderPath,
                    12);
            }
        }
    }
}
