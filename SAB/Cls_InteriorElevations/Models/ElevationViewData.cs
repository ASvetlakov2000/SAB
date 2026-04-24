using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Models
{
    public class ElevationViewData
    {
        public ElementId SourceLineId { get; set; }

        public ElementId ViewId { get; set; }

        public ViewSection ViewSection { get; set; }

        public int Index { get; set; }

        public string ViewName { get; set; }

        public bool IsCreated { get; set; }

        public bool CropApplied { get; set; }

        public bool TemplateApplied { get; set; }

        public string FailureReason { get; set; }
    }
}
