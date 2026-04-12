using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Services.Csv;

namespace RevitLibraryBuilder.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ExportLineAndFillPatternsCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                CsvFillPatternExportService csvFillPatternExportService = new CsvFillPatternExportService();
                return csvFillPatternExportService.ExecuteExport(commandData, ref message);
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Export Line And Fill Patterns", exception.ToString());
                return Result.Failed;
            }
        }
    }
}
