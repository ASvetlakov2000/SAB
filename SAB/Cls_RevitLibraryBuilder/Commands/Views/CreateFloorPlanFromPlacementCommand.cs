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
                    message = "Активный UIDocument недоступен.";
                    TaskDialog.Show("Создание плана этажа", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null)
                {
                    message = "Документ недоступен.";
                    TaskDialog.Show("Создание плана этажа", message);
                    return Result.Failed;
                }

                View sourceView = document.ActiveView;

                if (sourceView == null)
                {
                    message = "Активный вид недоступен.";
                    TaskDialog.Show("Создание плана этажа", message);
                    return Result.Failed;
                }

                // Block responsible for delegating floor plan creation to post-action pipeline
                ViewPlan createdView = PostActionViewService.CreateByCategory(
                    document,
                    "Generic Models",
                    sourceView,
                    null,
                    "ManualCommand",
                    0);

                if (createdView == null)
                {
                    message = "Не удалось создать вид плана этажа.";
                    return Result.Failed;
                }

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Создание плана этажа", exception.ToString());
                return Result.Failed;
            }
        }
    }
}
