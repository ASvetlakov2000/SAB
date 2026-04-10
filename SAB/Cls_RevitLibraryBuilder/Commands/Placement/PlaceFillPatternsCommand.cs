using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Services.Placement;
using UI.Dialogs;
using Services.Views;

[Transaction(TransactionMode.Manual)]
public class PlaceFillPatternsCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
    {
        Document doc = data.Application.ActiveUIDocument.Document;

        using (Transaction t = new Transaction(doc, "Fill Patterns"))
        {
            t.Start();

            View view = doc.ActiveView;

            FillPatternPlacer.Place(doc, view);

            t.Commit();
        }

        bool createView = ViewCreationDialogService.Ask("Штриховки");

        if (createView)
        {
            using (Transaction t = new Transaction(doc, "Create View"))
            {
                t.Start();

                var service = new FloorPlanViewService();
                service.Create(doc, "Штриховки");

                t.Commit();
            }

            Helpers.Notifications.ToastNotifications.ToastNotifier
                .ShowSuccess("Готово", "Вид для расставленной категории создан", 5);
        }

        return Result.Succeeded;
    }
}