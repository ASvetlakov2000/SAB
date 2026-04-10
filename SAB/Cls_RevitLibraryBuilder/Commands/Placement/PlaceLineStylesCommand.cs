using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Linq;
using RevitLibraryBuilder.Services.PostActions;

namespace Commands
{
    [Transaction(TransactionMode.Manual)]
    public class PlaceLineStylesCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            View view = doc.ActiveView;

            var curves = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(DetailCurve))
                .Cast<DetailCurve>()
                .ToList();

            if (!curves.Any())
                return Result.Succeeded;

            Category linesCategory = doc.Settings.Categories
                .get_Item(BuiltInCategory.OST_Lines);

            using (Transaction t = new Transaction(doc, "Apply Line Styles"))
            {
                t.Start();

                foreach (var dc in curves)
                {
                    GraphicsStyle gs = dc.LineStyle as GraphicsStyle;

                    if (gs == null)
                        continue;

                    string styleName = gs.GraphicsStyleCategory?.Name;

                    if (string.IsNullOrEmpty(styleName))
                        continue;

                    Category targetSubCategory = linesCategory
                        .SubCategories
                        .Cast<Category>()
                        .FirstOrDefault(x => x.Name == styleName);

                    if (targetSubCategory == null)
                        continue;

                    GraphicsStyle targetGs =
                        targetSubCategory.GetGraphicsStyle(GraphicsStyleType.Projection);

                    if (targetGs == null)
                        continue;

                    dc.LineStyle = targetGs;
                }

                t.Commit();
            }

            PostActionViewService.AskAndCreateView(doc);

            return Result.Succeeded;
        }
    }
}