using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Services.Csv;
using RevitLibraryBuilder.Services.Placement;
using System.Linq;
using System.Windows.Forms;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Импорт и размещение по точкам
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ImportByPointCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            Document doc = data.Application.ActiveUIDocument.Document;

            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "CSV (*.csv)|*.csv"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return Result.Cancelled;

            // Импорт CSV
            var csv = new CsvImportService().ImportFromCsv(dialog.FileName);

            // Получаем уровень
            Level level = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault();

            // Получаем сервис
            var service = PlacementServiceFactory.Create("Point", doc);

            // Выполняем размещение
            service.Place(csv, level);

            return Result.Succeeded;
        }
    }
}