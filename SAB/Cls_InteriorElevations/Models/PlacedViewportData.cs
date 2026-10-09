using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace SAB.InteriorElevations.Models
{
    public class PlacedViewportData
    {
        public ElementId SheetId { get; set; }

        public List<ElementId> SheetAnnotationIds { get; private set; } = new List<ElementId>();

        public ElementId ViewportId { get; set; }

        public ElementId ViewId { get; set; }

        public XYZ TopLeft { get; set; }

        public XYZ TopRight { get; set; }

        public XYZ BottomLeft { get; set; }

        public XYZ BottomRight { get; set; }

        public XYZ Center { get; set; }
    }
}
