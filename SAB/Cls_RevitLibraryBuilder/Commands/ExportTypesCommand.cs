using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Services.Csv;
using RevitLibraryBuilder.Services.Revit;
using System;
using System.Linq;
using System.Windows.Forms;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Команда экспорта типов элементов в CSV
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ExportTypesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Получаем документ Revit
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;

                // Собираем все типы
                TypeCollectorService collector = new TypeCollectorService();
                var types = collector.CollectAllTypes(doc);

                // Проверка на пустоту
                if (types == null || types.Count == 0)
                {
                    TaskDialog.Show("Экспорт", "Типы элементов не найдены.");
                    return Result.Cancelled;
                }

                // Диалог сохранения файла
                SaveFileDialog dialog = new SaveFileDialog
                {
                    Filter = "CSV (*.csv)|*.csv",
                    FileName = "RevitExport.csv"
                };

                if (dialog.ShowDialog() != DialogResult.OK)
                    return Result.Cancelled;

                // Экспорт в CSV
                CsvExportService export = new CsvExportService();
                export.ExportToCsv(types, dialog.FileName);

                TaskDialog.Show("Готово", "CSV успешно создан");

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