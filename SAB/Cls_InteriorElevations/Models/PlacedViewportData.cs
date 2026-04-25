using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Models
{
    public class PlacedViewportData
    {
        public ElementId ViewportId { get; set; }

        public ElementId ViewId { get; set; }

        public XYZ TopLeft { get; set; }

        public XYZ TopRight { get; set; }

        public XYZ Center { get; set; }
    }
}
