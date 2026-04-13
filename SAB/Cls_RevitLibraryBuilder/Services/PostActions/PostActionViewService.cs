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
        public static void RunAfterPlacement(
            Document document,
            string categoryNameFromCsv,
            int placedCount,
            string commandName)
        {
            RunAfterPlacement(document, categoryNameFromCsv, placedCount, commandName, null, null, 0);
        }

        public static void RunAfterPlacement(
            Document document,
            string categoryNameFromCsv,
            int placedCount,
            string commandName,
            string sourceCsvFilePath,
            string typeNameOriginal,
            int rowIndex)
        {
            if (document == null)
            {
                TaskDialog.Show("Create View", "Document is not available.");
                return;
            }

            if (placedCount <= 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(categoryNameFromCsv))
            {
                TaskDialog.Show("Create View", "Category value from CSV is missing.");
                return;
            }

            View sourceView = document.ActiveView;

            if (sourceView == null)
            {
                TaskDialog.Show("Create View", "Active view is not available.");
                return;
            }

            try
            {
                string generatedViewNamePreview = FloorPlanViewService.BuildViewNameByCategory(categoryNameFromCsv);
                bool createView = ViewCreationDialogService.Ask(generatedViewNamePreview);

                if (!createView)
                {
                    return;
                }

                CreateByCategory(
                    document,
                    categoryNameFromCsv,
                    sourceView,
                    sourceCsvFilePath,
                    typeNameOriginal,
                    rowIndex);
            }
            catch (Exception exception)
            {
                TaskDialog.Show("Create View", exception.ToString());
            }
        }

        public static ViewPlan CreateByCategory(
            Document document,
            string categoryNameFromCsv,
            View sourceView)
        {
            return CreateByCategory(document, categoryNameFromCsv, sourceView, null, null, 0);
        }

        public static ViewPlan CreateByCategory(
            Document document,
            string categoryNameFromCsv,
            View sourceView,
            string sourceCsvFilePath,
            string typeNameOriginal,
            int rowIndex)
        {
            if (document == null)
            {
                TaskDialog.Show("Create View", "Document is not available.");
                return null;
            }

            if (sourceView == null)
            {
                sourceView = document.ActiveView;
            }

            if (sourceView == null)
            {
                TaskDialog.Show("Create View", "Source view is not available.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(categoryNameFromCsv))
            {
                TaskDialog.Show("Create View", "Category value from CSV is missing.");
                return null;
            }

            try
            {
                ViewPlan createdView;

                using (Transaction transaction = new Transaction(document, "Create Placement Result View"))
                {
                    transaction.Start();

                    FloorPlanViewService service = new FloorPlanViewService();

                    // Block responsible for passing category into post-action workflow
                    createdView = service.CreateByCategory(
                        document,
                        categoryNameFromCsv,
                        sourceView,
                        sourceCsvFilePath,
                        typeNameOriginal,
                        rowIndex);

                    if (createdView == null)
                    {
                        transaction.RollBack();
                        TaskDialog.Show("Create View", "The floor plan view could not be created.");
                        return null;
                    }

                    transaction.Commit();
                }

                ShowSuccessNotification(
                    "Completed",
                    "Created placement result view: " + createdView.Name);

                return createdView;
            }
            catch (Exception exception)
            {
                TaskDialog.Show("Create View", exception.ToString());
                return null;
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
