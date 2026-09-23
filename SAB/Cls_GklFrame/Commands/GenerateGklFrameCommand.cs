using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SAB.GklFrame.Models;

namespace SAB.GklFrame.Commands
{
    [Transaction(TransactionMode.Manual)]
    public sealed class GenerateGklFrameCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return FrameCommandRunner.Run(commandData, FrameCommandMode.Generate, ref message);
        }
    }
}
