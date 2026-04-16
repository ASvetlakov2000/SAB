using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services;
using RevitLibraryBuilder.Services.Csv;
using RevitLibraryBuilder.Services.Views;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using asBIM;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Экспорт PNG изображений для всех компонентов легенды на активном виде Легенда.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ExportSystemFamilyThumbnailTemplateCommand : IExternalCommand
    {
        private const string ExportFolderName = "PNG_Pirogi";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                const string commandTitle = "Экспорт изображений компонентов легенды";
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Активный UIDocument недоступен.";
                    ShowErrorNotification(commandTitle, message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null || document.ActiveView == null)
                {
                    message = "Документ или активный вид недоступен.";
                    ShowErrorNotification(commandTitle, message);
                    return Result.Failed;
                }

                View activeView = document.ActiveView;

                if (activeView.ViewType != ViewType.Legend)
                {
                    message = "Перед запуском команды откройте вид Легенда.";
                    ShowErrorNotification(commandTitle, message);
                    return Result.Failed;
                }

                // Блок выбора папки для экспорта изображений.
                string selectedRootFolder = OpenFolder.SelectFolderPath(
                    "Выберите папку для экспорта изображений компонентов легенды",
                    ExportFolderName);

                if (string.IsNullOrWhiteSpace(selectedRootFolder))
                {
                    return Result.Cancelled;
                }

                string outputFolder = BuildExportFolderPath(selectedRootFolder);
                Directory.CreateDirectory(outputFolder);

                LegendComponentImageExportService exportService = new LegendComponentImageExportService();
                LegendComponentImageExportResult exportResult =
                    exportService.ExportAllFromActiveLegend(uiDocument, activeView, outputFolder);

                if (!string.IsNullOrWhiteSpace(exportResult.FatalError))
                {
                    message = exportResult.FatalError;
                    ShowErrorNotification(commandTitle, exportResult.FatalError);
                    return Result.Failed;
                }

                // Блок формирования только одного отчета: проблемные наименования типоразмеров.
                string problemNamesReportPath = WriteProblemTypeNameReport(outputFolder, exportResult);

                // Блок создания отдельного вида для ручной корректировки проблемных типоразмеров.
                string problematicLegendViewName = string.Empty;

                if (exportResult.ProblemNameIssues.Count > 0)
                {
                    problematicLegendViewName = exportService.CreateProblematicTypesLegendView(
                        document,
                        activeView,
                        exportResult.ProblemNameIssues);
                }

                // Блок сохранения пути в runtime-памяти для dashboard.
                ThumbnailFoldersRuntimeStore.SetSystemFamilyImagesFolder(outputFolder);

                string summary = BuildSummary(exportResult, problematicLegendViewName);

                if (!string.IsNullOrWhiteSpace(problemNamesReportPath) && File.Exists(problemNamesReportPath))
                {
                    summary += "\n\nОтчет о проблемных наименованиях типоразмеров по ссылке ниже";
                }

                if (exportResult.SkippedCount > 0)
                {
                    ToastNotifier.ShowFolderLinkWarning(
                        "Экспорт изображений легенды завершен с замечаниями",
                        summary,
                        string.IsNullOrWhiteSpace(problemNamesReportPath) ? outputFolder : problemNamesReportPath,
                        18);
                }
                else
                {
                    ToastNotifier.ShowFolderLinkSuccess(
                        "Экспорт изображений легенды завершен",
                        summary,
                        outputFolder,
                        16);
                }

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ShowErrorNotification("Экспорт изображений компонентов легенды", exception.Message);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Формирует короткую итоговую сводку без перечисления проблемных элементов.
        /// </summary>
        private static string BuildSummary(
            LegendComponentImageExportResult exportResult,
            string problematicLegendViewName)
        {
            StringBuilder summary = new StringBuilder();
            summary.AppendLine("Всего компонентов легенды на виде: " + exportResult.TotalLegendComponentsOnView);
            summary.AppendLine("Экспортировано изображений: " + exportResult.ExportedCount);
            summary.AppendLine("Пропущено: " + exportResult.SkippedCount);
            summary.AppendLine("Проблемных имен типов: " + exportResult.ProblematicNamesCount);

            if (!string.IsNullOrWhiteSpace(problematicLegendViewName))
            {
                summary.AppendLine("Создан вид легенды для ручной корректировки: " + problematicLegendViewName);
            }

            return summary.ToString().Trim();
        }

        /// <summary>
        /// Записывает CSV отчет только по проблемным именам типов (запрещенные символы Windows).
        /// </summary>
        private static string WriteProblemTypeNameReport(string outputFolder, LegendComponentImageExportResult exportResult)
        {
            if (exportResult == null || exportResult.ProblemNameIssues == null || exportResult.ProblemNameIssues.Count == 0)
            {
                return string.Empty;
            }

            try
            {
                string reportFileName =
                    "Проблемные_типы_запрещенные_символы_" +
                    DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) +
                    ".csv";

                string reportPath = Path.Combine(outputFolder, reportFileName);

                List<string> header = new List<string>
                {
                    "ИмяТипа",
                    "Причина"
                };

                List<List<string>> rows = new List<List<string>>();

                for (int i = 0; i < exportResult.ProblemNameIssues.Count; i++)
                {
                    LegendComponentImageProblemNameIssue issue = exportResult.ProblemNameIssues[i];

                    rows.Add(new List<string>
                    {
                        issue.TypeName ?? string.Empty,
                        issue.ErrorText ?? string.Empty
                    });
                }

                CsvTableService csvTableService = new CsvTableService();
                csvTableService.Write(reportPath, header, rows);

                return reportPath;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Создает путь к папке экспорта внутри выбранной пользователем директории.
        /// </summary>
        private static string BuildExportFolderPath(string rootFolder)
        {
            return Path.Combine(rootFolder, ExportFolderName);
        }

        private static void ShowErrorNotification(string title, string text)
        {
            ToastNotifier.ShowError(title, text, 12);
        }
    }
}