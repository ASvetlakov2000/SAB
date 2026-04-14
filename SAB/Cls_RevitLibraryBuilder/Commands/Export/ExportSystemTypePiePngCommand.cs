using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services.Views;
using System;
using System.IO;
using System.Windows.Forms;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Команда выгрузки PNG пирогов системных типов.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ExportSystemTypePiePngCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Active UIDocument is not available.";
                    ToastNotifier.ShowError("Выгрузка PNG пирогов", message, 12);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null || document.ActiveView == null)
                {
                    message = "Document or active view is not available.";
                    ToastNotifier.ShowError("Выгрузка PNG пирогов", message, 12);
                    return Result.Failed;
                }

                string outputFolder = RequestOutputFolder();

                if (string.IsNullOrWhiteSpace(outputFolder))
                {
                    return Result.Cancelled;
                }

                SystemTypePieLegendExportService service = new SystemTypePieLegendExportService();
                SystemTypePieLegendExportResult result = service.Export(uiDocument, outputFolder);

                ToastNotifier.ShowFolderLinkSuccess(
                    "Выгрузка PNG пирогов",
                    result.BuildSummaryText(),
                    outputFolder,
                    14);

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ToastNotifier.ShowError("Выгрузка PNG пирогов", exception.Message, 12);
                return Result.Failed;
            }
        }

        private static string RequestOutputFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                // Блок выбора постоянного пути сохранения PNG и CSV.
                dialog.Description = "Выберите папку для выгрузки PNG пирогов";

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return null;
                }

                string path = dialog.SelectedPath;

                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                {
                    return null;
                }

                return path;
            }
        }
    }
}

