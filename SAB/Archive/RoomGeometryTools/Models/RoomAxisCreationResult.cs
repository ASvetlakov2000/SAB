using Autodesk.Revit.DB;

namespace SAB.RoomGeometryTools.Models
{
    /// <summary>
    /// Результат построения осей помещения.
    /// </summary>
    public class RoomAxisCreationResult
    {
        public ElementId RoomId { get; set; } = ElementId.InvalidElementId;

        public string LevelName { get; set; } = string.Empty;

        public string RoomNumber { get; set; } = string.Empty;

        public string RoomName { get; set; } = string.Empty;

        public bool IsSuccess { get; set; }

        public int CreatedAxisCount { get; set; }

        public string Status => IsSuccess ? "Успех" : "Пропущено";

        public string Message { get; set; } = string.Empty;
    }
}

