using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Services.PostActions;

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

                View sourceView = document.ActiveView;

                if (sourceView == null)
                {
                    message = "Active view is not available.";
                    TaskDialog.Show("Create Floor Plan", message);
                    return Result.Failed;
                }

                ViewPlan createdView = PostActionViewService.CreateByCategory(
                    document,
                    "Generic Models",
                    sourceView,
                    null,
                    "ManualCommand",
                    0);

                if (createdView == null)
                {
                    message = "The floor plan view could not be created.";
                    return Result.Failed;
                }

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Create Floor Plan", exception.ToString());
                return Result.Failed;
            }
        }
    }
}
