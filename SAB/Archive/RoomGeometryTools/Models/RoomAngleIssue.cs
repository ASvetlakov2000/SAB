using Autodesk.Revit.DB;

namespace SAB.RoomGeometryTools.Models
{
    /// <summary>
    /// Проблема с углом помещения.
    /// </summary>
    public class RoomAngleIssue
    {
        public ElementId RoomId { get; set; } = ElementId.InvalidElementId;

        public string LevelName { get; set; } = string.Empty;

        public string RoomNumber { get; set; } = string.Empty;

        public string RoomName { get; set; } = string.Empty;

        public double ActualAngleDegrees { get; set; }

        public double DeviationFrom90Degrees { get; set; }

        public XYZ VertexPoint { get; set; } = XYZ.Zero;

        public XYZ FirstSegmentStart { get; set; } = XYZ.Zero;

        public XYZ FirstSegmentEnd { get; set; } = XYZ.Zero;

        public XYZ SecondSegmentStart { get; set; } = XYZ.Zero;

        public XYZ SecondSegmentEnd { get; set; } = XYZ.Zero;

        public string Message { get; set; } = string.Empty;
    }
}

