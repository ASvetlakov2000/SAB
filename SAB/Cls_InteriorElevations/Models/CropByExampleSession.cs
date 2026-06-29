using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Models
{
    public class CropByExampleSession
    {
        public ElementId SourcePlanViewId { get; set; }

        public ElementId SourcePlanLevelId { get; set; }

        public ElementId SourceLineId { get; set; }

        public ElementId SampleViewId { get; set; }

        public ElevationSettings Settings { get; set; }
    }
}
