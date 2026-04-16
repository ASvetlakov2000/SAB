using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Windows.Forms;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services;
using RevitLibraryBuilder.Services.Revit;
using asBIM;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Экспорт типов — ОТДЕЛЬНЫЕ CSV по категориям
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ExportTypesByCategoryCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // 🔹 Получаем документ
                UIDocument uidoc = commandData.Application.ActiveUIDocument;

                if (uidoc == null)
                {
                    message = "Active UIDocument is not available.";
                    TaskDialog.Show("Экспорт", message);
                    return Result.Failed;
                }

                Document doc = uidoc.Document;

                if (doc == null)
                {
                    message = "Document is not available.";
                    TaskDialog.Show("Экспорт", message);
                    return Result.Failed;
                }

                // 🔹 Сбор типов
                TypeCollectorService collector = new TypeCollectorService();
                var types = collector.CollectAllTypes(doc);

                // 🔹 Проверка
                if (types == null || types.Count == 0)
                {
                    MessageBox.Show("Типы элементов не найдены.", "Экспорт", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return Result.Cancelled;
                }

                // 🔹 Выбор папки через диалог с полем пути
                string outputFolder = OpenFolder.SelectFolderPath(
                    "Выберите папку для экспорта CSV (по категориям)",
                    "EXPORT_BY_CATEGORY.csv");

                if (string.IsNullOrWhiteSpace(outputFolder))
                    return Result.Cancelled;

                // 🔹 Экспорт
                CsvExportService exportService = new CsvExportService();
                exportService.ExportToCsv(types, doc, outputFolder);

                // 🔹 Уведомление
                ToastNotifier.ShowFolderLinkSuccess(
                    "Экспорт завершен",
                    "\nCSV файлы (по категориям) сохранены:\n",
                    outputFolder,
                    durationSeconds: 10
                );

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Экспорт", ex.ToString());
                return Result.Failed;
            }
        }
    }
}
