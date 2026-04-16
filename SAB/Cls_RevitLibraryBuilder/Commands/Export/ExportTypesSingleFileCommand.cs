using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services;
using RevitLibraryBuilder.Services.Revit;
using SAB;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using asBIM;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Экспорт типов — один CSV со всеми категориями из AllUsedCategoryList.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ExportTypesSingleFileCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;

                if (uidoc == null)
                {
                    message = "Активный UIDocument недоступен.";
                    TaskDialog.Show("Экспорт", message);
                    return Result.Failed;
                }

                Document doc = uidoc.Document;

                if (doc == null)
                {
                    message = "Документ недоступен.";
                    TaskDialog.Show("Экспорт", message);
                    return Result.Failed;
                }

                TypeCollectorService collector = new TypeCollectorService();
                List<ElementType> allTypes = collector.CollectAllTypes(doc);

                HashSet<int> allowedCategoryIds = BuildAllowedCategoryIdSet(doc);
                List<ElementType> filteredTypes = new List<ElementType>();

                for (int i = 0; i < allTypes.Count; i++)
                {
                    ElementType type = allTypes[i];

                    if (type == null || type.Category == null)
                    {
                        continue;
                    }

                    int categoryId = type.Category.Id.IntegerValue;

                    if (allowedCategoryIds.Contains(categoryId))
                    {
                        filteredTypes.Add(type);
                    }
                }

                if (filteredTypes.Count == 0)
                {
                    MessageBox.Show(
                        "Типы из выбранного списка категорий не найдены.",
                        "Экспорт",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return Result.Cancelled;
                }

                string outputFolder = OpenFolder.SelectFolderPath(
                    "Выберите папку для экспорта одного CSV",
                    BuildSuggestedFileName(doc.Title, "ALL_CATEGORIES.csv"));

                if (string.IsNullOrWhiteSpace(outputFolder))
                {
                    return Result.Cancelled;
                }

                CsvExportService exportService = new CsvExportService();
                exportService.ExportToSingleCsv(filteredTypes, doc, outputFolder);

                ToastNotifier.ShowFolderLinkSuccess(
                    "Экспорт завершен",
                    "\nCSV (только выбранные категории) сохранен:\n",
                    outputFolder,
                    durationSeconds: 10);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Экспорт", ex.ToString());
                return Result.Failed;
            }
        }

        // Блок выбора категорий для экспорта (редактируется через AllUsedCategoryList)
        private static HashSet<int> BuildAllowedCategoryIdSet(Document document)
        {
            HashSet<int> ids = new HashSet<int>();

            foreach (BuiltInCategory builtInCategory in AllUsedCategoryList.categoryList)
            {
                try
                {
                    Category category = Category.GetCategory(document, builtInCategory);

                    if (category == null)
                    {
                        continue;
                    }

                    ids.Add(category.Id.IntegerValue);
                }
                catch
                {
                    // Категория может отсутствовать в конкретной версии Revit или шаблоне проекта.
                }
            }

            return ids;
        }

        private static string BuildSuggestedFileName(string documentTitle, string suffix)
        {
            string safeTitle = string.IsNullOrWhiteSpace(documentTitle) ? "Project" : documentTitle.Trim();

            foreach (char invalidCharacter in System.IO.Path.GetInvalidFileNameChars())
            {
                safeTitle = safeTitle.Replace(invalidCharacter, '_');
            }

            return safeTitle + "_" + suffix;
        }
    }
}
