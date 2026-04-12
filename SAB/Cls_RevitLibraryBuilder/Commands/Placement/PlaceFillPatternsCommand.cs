using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Services.Placement;

namespace RevitLibraryBuilder.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class PlaceFillPatternsCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                FillPatternPlacer fillPatternPlacer = new FillPatternPlacer();
                return fillPatternPlacer.Execute(commandData, ref message);
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Place Fill Patterns", exception.ToString());
                return Result.Failed;
            }
        }
    }
}
