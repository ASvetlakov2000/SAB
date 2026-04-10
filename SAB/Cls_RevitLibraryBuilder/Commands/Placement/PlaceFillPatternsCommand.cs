using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Services.Placement;
using Services.Views;
using RevitLibraryBuilder.Services.PostActions;

[Transaction(TransactionMode.Manual)]
public class PlaceFillPatternsCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
    {
        Document doc = data.Application.ActiveUIDocument.Document;

        View view = doc.ActiveView;

        using (Transaction t = new Transaction(doc, "Fill Patterns"))
        {
            t.Start();

            FillPatternPlacer.Place(doc, view);

            t.Commit();
        }

        PostActionViewService.AskAndCreateView(doc);

        return Result.Succeeded;
    }
}