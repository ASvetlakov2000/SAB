using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace SAB.SyncReminder
{
    [Transaction(TransactionMode.Manual)]
    public class SyncReminderSettingsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (commandData == null || commandData.Application == null)
            {
                message = "Revit application is not available.";
                return Result.Failed;
            }

            if (MainPanel.CurrentSyncReminderController == null)
            {
                TaskDialog.Show(
                    "SAB Sync Reminder",
                    "Sync reminder controller is not available. Restart Revit and check SAB startup diagnostics.");
                return Result.Cancelled;
            }

            MainPanel.CurrentSyncReminderController.ShowSettingsWindow(commandData.Application);
            return Result.Succeeded;
        }
    }
}
