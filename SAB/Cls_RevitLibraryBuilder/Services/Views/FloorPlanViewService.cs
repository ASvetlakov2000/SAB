using Autodesk.Revit.DB;
using System.Linq;

namespace Services.Views
{
    public class FloorPlanViewService
    {
        public ViewPlan Create(Document doc, string viewName)
        {
            Level level = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault();

            if (level == null)
                return null;

            ViewFamilyType vft = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .First(x => x.ViewFamily == ViewFamily.FloorPlan);

            ViewPlan view = ViewPlan.Create(doc, vft.Id, level.Id);

            view.Name = viewName;

            return view;
        }
    }
}