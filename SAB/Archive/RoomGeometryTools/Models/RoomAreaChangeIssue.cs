using Autodesk.Revit.DB;

namespace SAB.RoomGeometryTools.Models
{
    /// <summary>
    /// Изменение площади помещения относительно утвержденной.
    /// </summary>
    public class RoomAreaChangeIssue
    {
        public ElementId RoomId { get; set; } = ElementId.InvalidElementId;

        public string LevelName { get; set; } = string.Empty;

        public string RoomNumber { get; set; } = string.Empty;

        public string RoomName { get; set; } = string.Empty;

        public double ApprovedAreaSquareMeters { get; set; }

        public double CurrentAreaSquareMeters { get; set; }

        public double DeltaAreaSquareMeters { get; set; }

        public double DeltaPercent { get; set; }

        public string Message { get; set; } = string.Empty;
    }
}

