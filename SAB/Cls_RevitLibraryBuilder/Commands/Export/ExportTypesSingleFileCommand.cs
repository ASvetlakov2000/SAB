using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Services;
using System;
using System.Linq;
using System.Windows.Forms;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services.Revit;
using SAB; // 🔹 подключаем список категорий

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Экспорт типов — ОДИН CSV со всеми категориями (ТОЛЬКО из AllUsedCategoryList)
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ExportTypesSingleFileCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // 🔹 Получаем документ
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;

                // 🔹 Сбор всех типов
                TypeCollectorService collector = new TypeCollectorService();
                var allTypes = collector.CollectAllTypes(doc);

                // 🔹 Получаем список разрешённых категорий
                var allowedCategories = AllUsedCategoryList.categoryList
                    .Select(bic => Category.GetCategory(doc, bic)) // получаем Category из BuiltInCategory
                    .Where(c => c != null)                         // убираем null (если категории нет в проекте)
                    .Select(c => c.Id.IntegerValue)                // берём ID категории
                    .ToHashSet();                                  // для быстрого поиска

                // 🔹 Фильтруем типы
                var filteredTypes = allTypes
                    .Where(t => t.Category != null &&
                                allowedCategories.Contains(t.Category.Id.IntegerValue))
                    .ToList();

                // 🔹 Проверка
                if (filteredTypes.Count == 0)
                {
                    MessageBox.Show("Типы из выбранного списка категорий не найдены.",
                        "Экспорт", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return Result.Cancelled;
                }

                // 🔹 Выбор папки
                using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "Выберите папку для экспорта одного CSV";

                    if (folderDialog.ShowDialog() != DialogResult.OK)
                        return Result.Cancelled;

                    string outputFolder = folderDialog.SelectedPath;

                    // 🔹 Экспорт
                    CsvExportService exportService = new CsvExportService();
                    exportService.ExportToSingleCsv(filteredTypes, doc, outputFolder);

                    // 🔹 Уведомление
                    ToastNotifier.ShowFolderLinkSuccess(
                        "Экспорт завершен",
                        "\nCSV (только выбранные категории) сохранен:\n",
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