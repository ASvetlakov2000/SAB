using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Linq;
using System.Windows.Forms;
using RevitLibraryBuilder.Services.Csv;
using RevitLibraryBuilder.Services.Placement;
using RevitLibraryBuilder.Services.PostActions;

namespace RevitLibraryBuilder.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ImportByLineCommand : IExternalCommand
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

            var service = PlacementServiceFactory.Create("Line", doc);
            service.Place(csv, level);

            PostActionViewService.AskAndCreateView(doc);

            return Result.Succeeded;
        }
    }
}