using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitLibraryBuilder.Commands.Regulations
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ConfigureRegulationsLandingCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return RegulationsLandingWorkflow.Run(commandData, true, ref message);
        }
    }
}
