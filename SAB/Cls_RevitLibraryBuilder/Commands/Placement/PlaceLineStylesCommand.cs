using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Services.Placement;

namespace RevitLibraryBuilder.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class PlaceLineStylesCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                LineStylePlacer lineStylePlacer = new LineStylePlacer();
                return lineStylePlacer.Execute(commandData, ref message);
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Place Line Styles", exception.ToString());
                return Result.Failed;
            }
        }
    }
}
