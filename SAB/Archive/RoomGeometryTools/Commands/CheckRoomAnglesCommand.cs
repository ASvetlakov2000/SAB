using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SAB.RoomGeometryTools.Services;

namespace SAB.RoomGeometryTools.Commands
{
    /// <summary>
    /// Команда проверки углов помещений на 90 градусов.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CheckRoomAnglesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RoomGeometryToolsOrchestratorService orchestrator = new RoomGeometryToolsOrchestratorService();
            return orchestrator.Run(commandData, ref message, RoomGeometryStartupAction.CheckAngles);
        }
    }
}

