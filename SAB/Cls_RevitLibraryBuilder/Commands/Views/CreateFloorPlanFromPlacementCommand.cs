using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services.Views;

namespace RevitLibraryBuilder.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreateFloorPlanFromPlacementCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = data.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Active UIDocument is not available.";
                    TaskDialog.Show("Create Floor Plan", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null)
                {
                    message = "Document is not available.";
                    TaskDialog.Show("Create Floor Plan", message);
                    return Result.Failed;
                }

                View activeView = document.ActiveView;

                if (activeView == null)
                {
                    message = "Active view is not available.";
                    TaskDialog.Show("Create Floor Plan", message);
                    return Result.Failed;
                }

                FloorPlanViewService service = new FloorPlanViewService();
                ViewPlan createdView;

                using (Transaction transaction = new Transaction(document, "Create Floor Plan From Placement"))
                {
                    transaction.Start();
                    createdView = service.Create(document, "Placement Result", activeView);
                    transaction.Commit();
                }

                if (createdView == null)
                {
                    message = "The floor plan view could not be created.";
                    TaskDialog.Show("Create Floor Plan", message);
                    return Result.Failed;
                }

                ShowSuccessNotification(
                    "Create Floor Plan",
                    "Created view: " + createdView.Name);

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Create Floor Plan", exception.ToString());
                return Result.Failed;
            }
        }

        // Block responsible for post-execution notification
        private static void ShowSuccessNotification(string title, string message)
        {
            try
            {
                ToastNotifier.ShowSuccess(title, message, 5);
            }
            catch
            {
                TaskDialog.Show(title, message);
            }
        }
    }
}
