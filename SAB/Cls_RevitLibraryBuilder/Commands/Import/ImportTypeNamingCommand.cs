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
    /// Импорт XLSX/CSV и пакетное переименование типоразмеров/семейств.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ImportTypeNamingCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Активный UIDocument недоступен.";
                    ShowErrorNotification("Импорт наименований типоразмеров", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null || document.ActiveView == null)
                {
                    message = "Документ или активный вид недоступен.";
                    ShowErrorNotification("Импорт наименований типоразмеров", message);
                    return Result.Failed;
                }

                string inputFilePath = RequestInputFilePath();

                if (string.IsNullOrWhiteSpace(inputFilePath))
                {
                    return Result.Cancelled;
                }

                TypeNamingCsvService namingService = new TypeNamingCsvService();
                List<TypeNamingCsvModel> rows = namingService.ImportRows(inputFilePath);

                if (rows.Count == 0)
                {
                    ToastNotifier.ShowWarning("Импорт наименований типоразмеров", "В файле нет строк для обработки.", 10);
                    return Result.Cancelled;
                }

                TypeNamingApplyService applyService = new TypeNamingApplyService();
                TypeNamingApplyResult applyResult = applyService.Apply(document, rows);

                string reportPath = namingService.WriteErrorReport(inputFilePath, applyResult.Errors);

                ShowResultNotification(applyResult, reportPath);
                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ShowErrorNotification("Импорт наименований типоразмеров", exception.Message);
                return Result.Failed;
            }
        }

        private static string RequestInputFilePath()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Выберите XLSX для переименования типоразмеров";
                dialog.Filter = "XLSX (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv";
                dialog.Multiselect = false;

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return null;
                }

                return dialog.FileName;
            }
        }

        // Блок отображения итогов выполнения и пути к отчету ошибок
        private static void ShowResultNotification(TypeNamingApplyResult result, string reportPath)
        {
            string summary =
                "Переименовано семейств: " + result.RenamedFamiliesCount + "\n" +
                "Переименовано типоразмеров: " + result.RenamedTypesCount + "\n" +
                "Ошибок: " + result.Errors.Count;

            if (!string.IsNullOrWhiteSpace(reportPath) && File.Exists(reportPath))
            {
                string folder = Path.GetDirectoryName(reportPath) ?? string.Empty;

                ToastNotifier.ShowFolderLinkWarning(
                    "Импорт наименований завершен",
                    summary + "\nСформирован отчет: Проблемные наименования.csv\n",
                    folder,
                    12);
                return;
            }

            if (result.Errors.Count > 0)
            {
                ToastNotifier.ShowWarning("Импорт наименований завершен", summary, 12);
            }
            else
            {
                ToastNotifier.ShowSuccess("Импорт наименований завершен", summary, 12);
            }
        }

        private static void ShowErrorNotification(string title, string text)
        {
            ToastNotifier.ShowError(title, text, 12);
        }
    }
}
