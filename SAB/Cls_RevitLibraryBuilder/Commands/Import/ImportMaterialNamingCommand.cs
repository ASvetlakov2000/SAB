using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Models;
using RevitLibraryBuilder.Services.Csv;
using RevitLibraryBuilder.Services.Revit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Импорт XLSX/CSV и пакетное переименование/удаление материалов.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ImportMaterialNamingCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Active UIDocument is not available.";
                    ShowErrorNotification("Импорт наименований материалов", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null || document.ActiveView == null)
                {
                    message = "Document or active view is not available.";
                    ShowErrorNotification("Импорт наименований материалов", message);
                    return Result.Failed;
                }

                string inputFilePath = RequestInputFilePath();

                if (string.IsNullOrWhiteSpace(inputFilePath))
                {
                    return Result.Cancelled;
                }

                MaterialNamingCsvService namingService = new MaterialNamingCsvService();
                List<MaterialNamingCsvModel> rows = namingService.ImportRows(inputFilePath);

                if (rows.Count == 0)
                {
                    ToastNotifier.ShowWarning("Импорт наименований материалов", "В файле нет строк для обработки.", 10);
                    return Result.Cancelled;
                }

                MaterialNamingApplyService applyService = new MaterialNamingApplyService();
                MaterialNamingApplyResult applyResult = applyService.Apply(document, rows);

                string reportPath = namingService.WriteErrorReport(inputFilePath, applyResult.Errors);

                ShowResultNotification(applyResult, reportPath);
                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ShowErrorNotification("Импорт наименований материалов", exception.Message);
                return Result.Failed;
            }
        }

        private static string RequestInputFilePath()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Выберите XLSX для материалов";
                dialog.Filter = "XLSX (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv";
                dialog.Multiselect = false;

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return null;
                }

                return dialog.FileName;
            }
        }

        // Блок отображения итогов обработки материалов и ошибок
        private static void ShowResultNotification(MaterialNamingApplyResult result, string reportPath)
        {
            string summary =
                "Переименовано материалов: " + result.RenamedMaterialsCount + "\n" +
                "Обновлено описаний: " + result.UpdatedDescriptionsCount + "\n" +
                "Удалено материалов: " + result.DeletedMaterialsCount + "\n" +
                "Ошибок: " + result.Errors.Count;

            if (!string.IsNullOrWhiteSpace(reportPath) && File.Exists(reportPath))
            {
                string folder = Path.GetDirectoryName(reportPath) ?? string.Empty;

                ToastNotifier.ShowFolderLinkWarning(
                    "Импорт материалов завершен",
                    summary + "\nСформирован отчет: Проблемные наименования.csv\n",
                    folder,
                    12);
                return;
            }

            if (result.Errors.Count > 0)
            {
                ToastNotifier.ShowWarning("Импорт материалов завершен", summary, 12);
            }
            else
            {
                ToastNotifier.ShowSuccess("Импорт материалов завершен", summary, 12);
            }
        }

        private static void ShowErrorNotification(string title, string text)
        {
            ToastNotifier.ShowError(title, text, 12);
        }
    }
}
