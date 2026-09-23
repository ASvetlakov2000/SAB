using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace SAB.RoomGeometryTools.Models
{
    /// <summary>
    /// Геометрия границы помещения, подготовленная для расчетов.
    /// </summary>
    public class RoomBoundaryPolygon
    {
        public ElementId RoomId { get; set; } = ElementId.InvalidElementId;

        public IList<XYZ> OuterVertices { get; set; } = new List<XYZ>();

        public IList<IList<XYZ>> InnerLoops { get; set; } = new List<IList<XYZ>>();

        public IList<Line> OuterLines { get; set; } = new List<Line>();

        public bool HasNonLinearSegments { get; set; }

        public string ErrorMessage { get; set; } = string.Empty;
    }
}

