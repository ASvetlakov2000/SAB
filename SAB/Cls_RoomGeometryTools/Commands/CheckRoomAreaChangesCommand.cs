using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SAB.RoomGeometryTools.Services;

namespace SAB.RoomGeometryTools.Commands
{
    /// <summary>
    /// Команда проверки изменения площади помещений.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CheckRoomAreaChangesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RoomGeometryToolsOrchestratorService orchestrator = new RoomGeometryToolsOrchestratorService();
            return orchestrator.Run(commandData, ref message, RoomGeometryStartupAction.CheckAreaChanges);
        }
    }
}

