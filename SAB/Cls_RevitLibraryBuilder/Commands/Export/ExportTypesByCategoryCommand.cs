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
    /// Экспорт типов — отдельные CSV по категориям.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ExportTypesByCategoryCommand : IExternalCommand
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
                var types = collector.CollectAllTypes(doc);

                if (types == null || types.Count == 0)
                {
                    MessageBox.Show("Типы элементов не найдены.", "Экспорт", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return Result.Cancelled;
                }

                string outputFolder = OpenFolder.SelectFolderPath(
                    "Выберите папку для экспорта CSV (по категориям)",
                    "ctg");

                if (string.IsNullOrWhiteSpace(outputFolder))
                {
                    return Result.Cancelled;
                }

                outputFolder = ExportFolderRoutingService.ResolveCategoryExportFolder(outputFolder);
                ExportFolderRoutingService.ConfigureThumbnailFoldersForCategoryExport(outputFolder);

                CsvExportService exportService = new CsvExportService();
                exportService.ExportToCsv(types, doc, outputFolder);

                ToastNotifier.ShowFolderLinkSuccess(
                    "Экспорт завершен",
                    "\nCSV файлы (по категориям) сохранены:\n",
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
    }
}
