using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using SAB.RoomGeometryTools.Models;
using SAB.RoomGeometryTools.Utils;
using System.Collections.Generic;

namespace SAB.RoomGeometryTools.Services
{
    /// <summary>
    /// Сервис проверки проблем размещения помещений.
    /// </summary>
    public class RoomPlacementCheckService
    {
        public IList<RoomPlacementIssue> CheckRooms(IList<Room> rooms)
        {
            List<RoomPlacementIssue> issues = new List<RoomPlacementIssue>();

            if (rooms == null)
            {
                return issues;
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                Room room = rooms[i];
                IList<RoomPlacementIssue> roomIssues = CheckRoom(room);

                for (int j = 0; j < roomIssues.Count; j++)
                {
                    issues.Add(roomIssues[j]);
                }
            }

            return issues;
        }

        public IList<RoomPlacementIssue> CheckRoom(Room room)
        {
            List<RoomPlacementIssue> issues = new List<RoomPlacementIssue>();

            if (room == null)
            {
                return issues;
            }

            Level level = room.Document.GetElement(room.LevelId) as Level;
            string levelName = level != null ? level.Name : "Без уровня";
            string roomNumber = RevitParameterUtils.GetRoomNumber(room);
            string roomName = RevitParameterUtils.GetRoomName(room);

            if (room.Location == null)
            {
                issues.Add(CreateIssue(room, levelName, roomNumber, roomName, "Помещение не размещено (Location == null)."));
            }

            if (room.Area <= 0.0)
            {
                issues.Add(CreateIssue(room, levelName, roomNumber, roomName, "Площадь помещения меньше или равна нулю."));
            }

            SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions();
            IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(options);

            if (boundaries == null)
            {
                issues.Add(CreateIssue(room, levelName, roomNumber, roomName, "Границы помещения не определены (null)."));
            }
            else if (boundaries.Count == 0)
            {
                issues.Add(CreateIssue(room, levelName, roomNumber, roomName, "Границы помещения пустые."));
            }

            return issues;
        }

        private static RoomPlacementIssue CreateIssue(Room room, string levelName, string roomNumber, string roomName, string message)
        {
            return new RoomPlacementIssue
            {
                RoomId = room != null ? room.Id : ElementId.InvalidElementId,
                LevelName = levelName ?? "Без уровня",
                RoomNumber = roomNumber ?? string.Empty,
                RoomName = roomName ?? string.Empty,
                Message = message ?? string.Empty
            };
        }
    }
}

