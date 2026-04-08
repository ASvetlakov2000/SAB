using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Services;
using System;
using System.Linq;
using System.Windows.Forms;

namespace RevitLibraryBuilder.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ImportTypesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;

                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv"
                };

                if (openFileDialog.ShowDialog() != DialogResult.OK)
                    return Result.Cancelled;

                CsvImportService importService = new CsvImportService();
                var importedElements = importService.ImportFromCsv(openFileDialog.FileName);

                if (importedElements.Count == 0)
                {
                    TaskDialog.Show("Импорт", "CSV не содержит элементов для расстановки.");
                    return Result.Cancelled;
                }

                // Берём первый уровень для расстановки
                Level firstLevel = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .FirstOrDefault();

                if (firstLevel == null)
                {
                    TaskDialog.Show("Импорт", "Уровни в проекте не найдены.");
                    return Result.Cancelled;
                }

                ElementPlacementService placementService = new ElementPlacementService(doc);
                placementService.PlaceElements(importedElements, firstLevel);

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