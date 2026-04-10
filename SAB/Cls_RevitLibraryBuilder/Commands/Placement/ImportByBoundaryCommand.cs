using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Linq;
using System.Windows.Forms;
using RevitLibraryBuilder.Services.Csv;
using RevitLibraryBuilder.Services.Placement;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Импорт и размещение по границе
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ImportByBoundaryCommand : IExternalCommand
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

            var csv = new CsvImportService().ImportFromCsv(dialog.FileName);

            Level level = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault();

            var service = PlacementServiceFactory.Create("Boundary", doc);

            service.Place(csv, level);

            return Result.Succeeded;
        }
    }
}