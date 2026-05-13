using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Models
{
    public class ElevationFlipResult
    {
        public ElevationFlipResult()
        {
            IsSuccess = false;
            Message = string.Empty;
            SourceViewId = ElementId.InvalidElementId;
            ResultViewId = ElementId.InvalidElementId;
            SourceViewportId = ElementId.InvalidElementId;
            ResultViewportId = ElementId.InvalidElementId;
            IsSourcePlacedOnSheet = false;
            SheetNumber = string.Empty;
            SheetName = string.Empty;
            ResultViewName = string.Empty;
            RotationAngleDegrees = 0.0;
        }

        public bool IsSuccess { get; set; }

        public string Message { get; set; }

        public ElementId SourceViewId { get; set; }

        public ElementId ResultViewId { get; set; }

        public ElementId SourceViewportId { get; set; }

        public ElementId ResultViewportId { get; set; }

        public bool IsSourcePlacedOnSheet { get; set; }

        public string SheetNumber { get; set; }

        public string SheetName { get; set; }

        public string ResultViewName { get; set; }

        public double RotationAngleDegrees { get; set; }
    }
}
