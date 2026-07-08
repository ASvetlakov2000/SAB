using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace SyncReminderTest
{
    [Transaction(TransactionMode.Manual)]
    public class SettingsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (commandData == null || commandData.Application == null)
            {
                message = "Revit application is not available.";
                return Result.Failed;
            }

            if (App.CurrentController == null)
            {
                TaskDialog.Show("Sync Reminder", "Sync reminder controller is not available.");
                return Result.Cancelled;
            }

            App.CurrentController.ShowSettingsWindow(commandData.Application);
            return Result.Succeeded;
        }
    }
}
