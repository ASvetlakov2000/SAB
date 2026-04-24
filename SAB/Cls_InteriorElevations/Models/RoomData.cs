using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Models
{
    public class RoomData
    {
        public ElementId RoomElementId { get; set; }

        public string RoomName { get; set; }

        public string RoomNumber { get; set; }

        public ElementId LevelId { get; set; }
    }
}
