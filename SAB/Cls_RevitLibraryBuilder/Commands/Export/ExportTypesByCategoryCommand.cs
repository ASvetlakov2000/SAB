using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Services;
using System;
using System.Windows.Forms;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services.Revit;

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
                Document doc = uidoc.Document;

                // 🔹 Сбор типов
                TypeCollectorService collector = new TypeCollectorService();
                var types = collector.CollectAllTypes(doc);

                // 🔹 Проверка
                if (types == null || types.Count == 0)
                {
                    MessageBox.Show("Типы элементов не найдены.", "Экспорт", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return Result.Cancelled;
                }

                // 🔹 Выбор папки
                using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "Выберите папку для экспорта CSV (по категориям)";

                    if (folderDialog.ShowDialog() != DialogResult.OK)
                        return Result.Cancelled;

                    string outputFolder = folderDialog.SelectedPath;

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
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}