using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Services;
using System;
using System.Windows.Forms;

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

                TypeCollectorService collector = new TypeCollectorService();
                var types = collector.CollectAllTypes(doc);

                if (types == null || types.Count == 0)
                {
                    TaskDialog.Show("Экспорт", "Типы элементов не найдены.");
                    return Result.Cancelled;
                }

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = "RevitLibraryExport.csv"
                };

                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                    return Result.Cancelled;

                CsvExportService exportService = new CsvExportService();
                exportService.ExportToCsv(types, saveFileDialog.FileName);

                TaskDialog.Show("Экспорт завершен", $"CSV файл создан:\n{saveFileDialog.FileName}");

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