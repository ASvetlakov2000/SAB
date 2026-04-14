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
    /// Импорт CSV и пакетное переименование типоразмеров/семейств.
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
                    message = "Active UIDocument is not available.";
                    TaskDialog.Show("Импорт наименований типоразмеров", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null || document.ActiveView == null)
                {
                    message = "Document or active view is not available.";
                    TaskDialog.Show("Импорт наименований типоразмеров", message);
                    return Result.Failed;
                }

                string csvFilePath = RequestCsvFilePath();

                if (string.IsNullOrWhiteSpace(csvFilePath))
                {
                    return Result.Cancelled;
                }

                TypeNamingCsvService csvService = new TypeNamingCsvService();
                List<TypeNamingCsvModel> rows = csvService.ImportRows(csvFilePath);

                if (rows.Count == 0)
                {
                    TaskDialog.Show("Импорт наименований типоразмеров", "В CSV нет строк для обработки.");
                    return Result.Cancelled;
                }

                TypeNamingApplyService applyService = new TypeNamingApplyService();
                TypeNamingApplyResult applyResult = applyService.Apply(document, rows);

                string reportPath = csvService.WriteErrorReport(csvFilePath, applyResult.Errors);

                ShowResultNotification(applyResult, reportPath);
                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Импорт наименований типоразмеров", exception.ToString());
                return Result.Failed;
            }
        }

        private static string RequestCsvFilePath()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Выберите CSV для переименования типоразмеров";
                dialog.Filter = "CSV (*.csv)|*.csv";
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

                try
                {
                    ToastNotifier.ShowFolderLinkWarning(
                        "Импорт наименований завершен",
                        summary + "\nСформирован отчет: Проблемные наименования.csv\n",
                        folder,
                        12);
                    return;
                }
                catch
                {
                    // fallback to TaskDialog below
                }
            }

            TaskDialog.Show("Импорт наименований завершен", summary);
        }
    }
}
