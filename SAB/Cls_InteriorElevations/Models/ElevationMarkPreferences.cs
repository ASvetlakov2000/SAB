using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Models
{
    public class ElevationMarkPreferences
    {
        public bool OnlyCornerNumber { get; set; }

        public bool BelowView { get; set; }

        public ElementId PlanMarkTypeId { get; set; }

        public ElementId SheetMarkTypeId { get; set; }
    }
}
