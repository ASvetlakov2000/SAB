using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

[Transaction(TransactionMode.Manual)]
public class CreateViewFromCategoryCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
    {
        Document doc = data.Application.ActiveUIDocument.Document;

        using (Transaction t = new Transaction(doc, "Create View"))
        {
            t.Start();

            var service = new Services.Views.FloorPlanViewService();

            service.Create(doc, "BIM Export View");

            t.Commit();
        }

        return Result.Succeeded;
    }
}