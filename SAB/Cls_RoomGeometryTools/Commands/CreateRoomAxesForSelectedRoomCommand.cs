using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SAB.RoomGeometryTools.Services;

namespace SAB.RoomGeometryTools.Commands
{
    /// <summary>
    /// Команда построения осей по выбранному помещению.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CreateRoomAxesForSelectedRoomCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RoomGeometryToolsOrchestratorService orchestrator = new RoomGeometryToolsOrchestratorService();
            return orchestrator.Run(commandData, ref message, RoomGeometryStartupAction.CreateAxesForSelectedRoom);
        }
    }
}

