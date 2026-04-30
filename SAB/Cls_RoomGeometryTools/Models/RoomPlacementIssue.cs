using Autodesk.Revit.DB;

namespace SAB.RoomGeometryTools.Models
{
    /// <summary>
    /// Проблема размещения помещения.
    /// </summary>
    public class RoomPlacementIssue
    {
        public ElementId RoomId { get; set; } = ElementId.InvalidElementId;

        public string LevelName { get; set; } = string.Empty;

        public string RoomNumber { get; set; } = string.Empty;

        public string RoomName { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }
}

