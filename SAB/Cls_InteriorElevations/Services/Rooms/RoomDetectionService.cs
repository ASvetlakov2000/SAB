using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Rooms
{
    public class RoomDetectionService
    {
        public bool TryDetectRoom(Document document, IList<ElevationLineData> lines, out RoomData roomData, IList<string> warnings)
        {
            roomData = null;

            if (document == null || lines == null || lines.Count == 0)
            {
                return false;
            }

            List<XYZ> probePoints = BuildProbePoints(lines);
            for (int i = 0; i < probePoints.Count; i++)
            {
                Room room = TryGetRoomWithVerticalOffsets(document, probePoints[i]);
                if (room == null)
                {
                    continue;
                }

                roomData = BuildRoomData(room);
                return true;
            }

            if (warnings != null)
            {
                warnings.Add("Room detection failed. Verify that selected detail lines are drawn inside a room boundary in the current model phase.");
            }

            return false;
        }

        private List<XYZ> BuildProbePoints(IList<ElevationLineData> lines)
        {
            List<XYZ> points = new List<XYZ>();

            // Block 1: first attempt uses approximate contour center.
            XYZ contourCenter = CalculateContourCenter(lines);
            points.Add(contourCenter);

            // Block 2: fallback attempts use line midpoints and shifted midpoint points.
            for (int i = 0; i < lines.Count; i++)
            {
                ElevationLineData lineData = lines[i];
                points.Add(lineData.MidPoint);

                XYZ direction = new XYZ(lineData.LineDirection.X, lineData.LineDirection.Y, 0.0);
                if (direction.GetLength() <= 1e-9)
                {
                    continue;
                }

                direction = direction.Normalize();
                XYZ normalA = new XYZ(-direction.Y, direction.X, 0.0).Normalize();
                XYZ normalB = new XYZ(direction.Y, -direction.X, 0.0).Normalize();

                double probeDistance = UnitConversionUtils.MillimetersToFeet(250.0);
                points.Add(lineData.MidPoint + normalA * probeDistance);
                points.Add(lineData.MidPoint + normalB * probeDistance);
            }

            return points;
        }

        private XYZ CalculateContourCenter(IList<ElevationLineData> lines)
        {
            double sumX = 0.0;
            double sumY = 0.0;
            double sumZ = 0.0;
            int pointCount = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                ElevationLineData lineData = lines[i];

                sumX += lineData.StartPoint.X;
                sumY += lineData.StartPoint.Y;
                sumZ += lineData.StartPoint.Z;
                pointCount++;

                sumX += lineData.EndPoint.X;
                sumY += lineData.EndPoint.Y;
                sumZ += lineData.EndPoint.Z;
                pointCount++;
            }

            if (pointCount == 0)
            {
                return XYZ.Zero;
            }

            return new XYZ(sumX / pointCount, sumY / pointCount, sumZ / pointCount);
        }

        private Room TryGetRoomWithVerticalOffsets(Document document, XYZ sourcePoint)
        {
            double[] offsets =
            {
                0.0,
                UnitConversionUtils.MillimetersToFeet(50.0),
                UnitConversionUtils.MillimetersToFeet(150.0),
                UnitConversionUtils.MillimetersToFeet(300.0),
                UnitConversionUtils.MillimetersToFeet(-50.0)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                XYZ probePoint = new XYZ(sourcePoint.X, sourcePoint.Y, sourcePoint.Z + offsets[i]);
                Room room = document.GetRoomAtPoint(probePoint);
                if (room != null)
                {
                    return room;
                }
            }

            return null;
        }

        private RoomData BuildRoomData(Room room)
        {
            string roomName = room.get_Parameter(BuiltInParameter.ROOM_NAME) != null
                ? room.get_Parameter(BuiltInParameter.ROOM_NAME).AsString()
                : room.Name;

            string roomNumber = room.get_Parameter(BuiltInParameter.ROOM_NUMBER) != null
                ? room.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString()
                : string.Empty;

            RoomData roomData = new RoomData();
            roomData.RoomElementId = room.Id;
            roomData.RoomName = RevitNameUtils.SanitizeName(roomName, "Room");
            roomData.RoomNumber = RevitNameUtils.SanitizeName(roomNumber, "000");
            roomData.LevelId = room.LevelId;

            return roomData;
        }
    }
}
