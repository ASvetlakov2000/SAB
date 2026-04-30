using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using SAB.RoomGeometryTools.Models;
using SAB.RoomGeometryTools.Utils;
using System;
using System.Collections.Generic;

namespace SAB.RoomGeometryTools.Services
{
    /// <summary>
    /// Сервис проверки углов помещений.
    /// </summary>
    public class RoomAngleCheckService
    {
        // Блок точности проверки прямого угла. Значение специально оставлено очень малым.
        public const double AngleEpsilonDegrees = 1e-6;

        private readonly RoomBoundaryService _roomBoundaryService;

        public RoomAngleCheckService(RoomBoundaryService roomBoundaryService)
        {
            _roomBoundaryService = roomBoundaryService ?? new RoomBoundaryService();
        }

        public IList<RoomAngleIssue> CheckRooms(IList<Room> rooms)
        {
            List<RoomAngleIssue> issues = new List<RoomAngleIssue>();

            if (rooms == null)
            {
                return issues;
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                Room room = rooms[i];
                IList<RoomAngleIssue> roomIssues = CheckRoom(room);

                for (int j = 0; j < roomIssues.Count; j++)
                {
                    issues.Add(roomIssues[j]);
                }
            }

            return issues;
        }

        public IList<RoomAngleIssue> CheckRoom(Room room)
        {
            List<RoomAngleIssue> issues = new List<RoomAngleIssue>();

            if (room == null)
            {
                return issues;
            }

            RoomBoundaryPolygon polygon = _roomBoundaryService.GetRoomBoundaryPolygon(room);
            if (!string.IsNullOrWhiteSpace(polygon.ErrorMessage))
            {
                issues.Add(CreateIssue(room, 0.0, 0.0, XYZ.Zero, XYZ.Zero, XYZ.Zero, XYZ.Zero, XYZ.Zero, polygon.ErrorMessage));
                return issues;
            }

            if (polygon.HasNonLinearSegments)
            {
                issues.Add(CreateIssue(
                    room,
                    0.0,
                    0.0,
                    polygon.OuterVertices.Count > 0 ? polygon.OuterVertices[0] : XYZ.Zero,
                    XYZ.Zero,
                    XYZ.Zero,
                    XYZ.Zero,
                    XYZ.Zero,
                    "Обнаружены нелинейные сегменты границы. Проверка на 90° не может быть выполнена строго."));
                return issues;
            }

            IList<XYZ> vertices = polygon.OuterVertices;
            if (vertices == null || vertices.Count < 3)
            {
                issues.Add(CreateIssue(room, 0.0, 0.0, XYZ.Zero, XYZ.Zero, XYZ.Zero, XYZ.Zero, XYZ.Zero, "Недостаточно вершин для проверки углов."));
                return issues;
            }

            for (int i = 0; i < vertices.Count; i++)
            {
                XYZ previous = vertices[(i - 1 + vertices.Count) % vertices.Count];
                XYZ current = vertices[i];
                XYZ next = vertices[(i + 1) % vertices.Count];

                double angleDegrees;
                if (!GeometryAngleUtils.TryGetInternalAngleDegreesXY(previous, current, next, out angleDegrees))
                {
                    issues.Add(CreateIssue(
                        room,
                        0.0,
                        0.0,
                        current,
                        previous,
                        current,
                        current,
                        next,
                        "Не удалось вычислить угол в одной из вершин."));
                    continue;
                }

                if (!GeometryAngleUtils.IsRightAngle(angleDegrees, AngleEpsilonDegrees))
                {
                    double deviation = Math.Abs(angleDegrees - 90.0);
                    string message = "Угол не равен 90°. Фактическое значение: " + angleDegrees.ToString("0.######");
                    issues.Add(CreateIssue(room, angleDegrees, deviation, current, previous, current, current, next, message));
                }
            }

            return issues;
        }

        private static RoomAngleIssue CreateIssue(
            Room room,
            double angle,
            double deviation,
            XYZ vertex,
            XYZ firstSegmentStart,
            XYZ firstSegmentEnd,
            XYZ secondSegmentStart,
            XYZ secondSegmentEnd,
            string message)
        {
            Level level = room != null ? room.Document.GetElement(room.LevelId) as Level : null;
            return new RoomAngleIssue
            {
                RoomId = room != null ? room.Id : ElementId.InvalidElementId,
                LevelName = level != null ? level.Name : "Без уровня",
                RoomNumber = RevitParameterUtils.GetRoomNumber(room),
                RoomName = RevitParameterUtils.GetRoomName(room),
                ActualAngleDegrees = angle,
                DeviationFrom90Degrees = deviation,
                VertexPoint = vertex ?? XYZ.Zero,
                FirstSegmentStart = firstSegmentStart ?? XYZ.Zero,
                FirstSegmentEnd = firstSegmentEnd ?? XYZ.Zero,
                SecondSegmentStart = secondSegmentStart ?? XYZ.Zero,
                SecondSegmentEnd = secondSegmentEnd ?? XYZ.Zero,
                Message = message ?? string.Empty
            };
        }
    }
}

