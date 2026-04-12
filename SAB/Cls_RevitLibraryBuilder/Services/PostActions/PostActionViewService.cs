using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services.Views;
using SAB.Cls_RevitLibraryBuilder.UI.Dialogs;

namespace RevitLibraryBuilder.Services.PostActions
{
    /// <summary>
    /// Unified post-action service for placement commands.
    /// </summary>
    public static class PostActionViewService
    {
        public static void AskAndCreateView(Document document)
        {
            if (document == null)
            {
                TaskDialog.Show("Create View", "Document is not available.");
                return;
            }

            View activeView = document.ActiveView;

            if (activeView == null)
            {
                TaskDialog.Show("Create View", "Active view is not available.");
                return;
            }

            bool createView;

            try
            {
                // Block responsible for asking the user whether a result view should be created
                createView = ViewCreationDialogService.Ask("Placement Result");
            }
            catch (Exception dialogException)
            {
                TaskDialog.Show(
                    "Create View",
                    "The confirmation dialog could not be opened.\n\n" + dialogException.Message);
                return;
            }

            if (!createView)
            {
                return;
            }

            try
            {
                ViewPlan createdView;

                // Block responsible for creating a result floor plan view
                using (Transaction transaction = new Transaction(document, "Create Placement Result View"))
                {
                    transaction.Start();

                    FloorPlanViewService service = new FloorPlanViewService();
                    createdView = service.Create(document, "Placement Result", activeView);

                    if (createdView == null)
                    {
                        transaction.RollBack();
                        TaskDialog.Show("Create View", "The floor plan view could not be created.");
                        return;
                    }

                    transaction.Commit();
                }

                ShowSuccessNotification(
                    "Completed",
                    "Created placement result view: " + createdView.Name);
            }
            catch (Exception exception)
            {
                TaskDialog.Show("Create View", exception.ToString());
            }
        }

        // Block responsible for post-execution notification with TaskDialog fallback
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
