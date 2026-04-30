using Autodesk.Revit.DB;

namespace SAB.RoomGeometryTools.Models
{
    /// <summary>
    /// DTO для выпадающих списков стилей Revit.
    /// </summary>
    public class RevitStyleItem
    {
        public ElementId ElementId { get; set; } = ElementId.InvalidElementId;

        public string Name { get; set; } = string.Empty;

        public override string ToString()
        {
            return Name;
        }
    }
}

