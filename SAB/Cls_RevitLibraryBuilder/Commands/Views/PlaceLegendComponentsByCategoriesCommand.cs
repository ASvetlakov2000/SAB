using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services.Views;
using System;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Command places legend components by predefined categories on active Legend view.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class PlaceLegendComponentsByCategoriesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                // Block responsible for active document and view validation.
                if (uiDocument == null)
                {
                    message = "Active UIDocument is not available.";
                    ToastNotifier.ShowError("Place Legend Components", message, 12);
                    TaskDialog.Show("Place Legend Components", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null)
                {
                    message = "Document is not available.";
                    ToastNotifier.ShowError("Place Legend Components", message, 12);
                    TaskDialog.Show("Place Legend Components", message);
                    return Result.Failed;
                }

                View activeView = document.ActiveView;

                if (activeView == null)
                {
                    message = "Active view is not available.";
                    ToastNotifier.ShowError("Place Legend Components", message, 12);
                    TaskDialog.Show("Place Legend Components", message);
                    return Result.Failed;
                }

                if (activeView.ViewType != ViewType.Legend)
                {
                    message = "Open a Legend view before running this command.";
                    ToastNotifier.ShowError("Place Legend Components", message, 12);
                    TaskDialog.Show("Place Legend Components", message);
                    return Result.Failed;
                }

                LegendComponentPlacementService placementService = new LegendComponentPlacementService();
                LegendComponentPlacementResult placementResult;

                // Block responsible for transaction boundaries around placement changes.
                using (Transaction transaction = new Transaction(document, "Place legend components by categories"))
                {
                    transaction.Start();

                    placementResult = placementService.PlaceByCategories(document, activeView);

                    if (!string.IsNullOrWhiteSpace(placementResult.FatalError))
                    {
                        transaction.RollBack();
                        message = placementResult.FatalError;
                        ToastNotifier.ShowError("Place Legend Components", placementResult.FatalError, 14);
                        TaskDialog.Show("Place Legend Components", placementResult.FatalError);
                        return Result.Failed;
                    }

                    transaction.Commit();
                }

                // Block responsible for final user notification.
                string summaryText = placementResult.BuildSummaryText();

                if (placementResult.SkippedDetails.Count > 0)
                {
                    ToastNotifier.ShowWarning("Legend component placement completed", summaryText, 16);
                    TaskDialog.Show("Place Legend Components", summaryText);
                }
                else
                {
                    ToastNotifier.ShowSuccess("Legend component placement completed", summaryText, 12);
                }

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ToastNotifier.ShowError("Place Legend Components", exception.Message, 12);
                TaskDialog.Show("Place Legend Components", exception.ToString());
                return Result.Failed;
            }
        }
    }
}
