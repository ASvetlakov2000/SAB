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
    [Transaction(TransactionMode.Manual)]
    public class ExportTypesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;

                // Собираем все типы элементов
                TypeCollectorService collector = new TypeCollectorService();
                var types = collector.CollectAllTypes(doc);

                if (types == null || types.Count == 0)
                {
                    MessageBox.Show("Типы элементов не найдены.", "Экспорт", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return Result.Cancelled;
                }

                // Выбор папки для сохранения CSV
                using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "Выберите папку для экспорта CSV файлов";

                    if (folderDialog.ShowDialog() != DialogResult.OK)
                        return Result.Cancelled;

                    string outputFolder = folderDialog.SelectedPath;

                    // Экспортируем CSV с разделением по категориям из AllCategoriesByPlacement
                    CsvExportService exportService = new CsvExportService();
                    exportService.ExportToCsv(types, doc, outputFolder);
                    
                    
                    // Кликабельная ссылка для открытия папки
                    ToastNotifier.ShowFolderLinkSuccess(
                        "Экспорт завершен",
                        "\nCSV файлы сохранены в папке:\n",
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