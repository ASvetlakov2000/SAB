using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Models
{
    public class ElevationSettings
    {
        public ElementId ViewTemplateId { get; set; }

        public ElementId ElevationViewFamilyTypeId { get; set; }

        public int ViewScale { get; set; }

        public double TopOffsetMm { get; set; }

        public double BottomOffsetMm { get; set; }

        public double LeftOffsetMm { get; set; }

        public double RightOffsetMm { get; set; }

        public double ViewDepthMm { get; set; }

        public double MarkerOffsetMm { get; set; }

        public bool CreateSheet { get; set; }

        public ElementId TitleBlockTypeId { get; set; }

        public ElementId PlanCornerMarkTypeId { get; set; }

        public ElementId SheetCornerMarkTypeId { get; set; }

        public int? SheetFormatAValue { get; set; }

        public SheetLayoutSettings SheetLayoutSettings { get; set; }
    }
}
