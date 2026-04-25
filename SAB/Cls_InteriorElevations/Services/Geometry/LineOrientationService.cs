using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Geometry
{
    public class LineOrientationService
    {
        public bool TryAssignInsideNormals(Document document, IList<ElevationLineData> lines, RoomData roomData, double markerOffsetMm, IList<string> warnings)
        {
            if (document == null || lines == null || lines.Count == 0 || roomData == null)
            {
                return false;
            }

            Room selectedRoom = document.GetElement(roomData.RoomElementId) as Room;
            if (selectedRoom == null)
            {
                if (warnings != null)
                {
                    warnings.Add("Не удалось получить выбранное помещение для расчета направления разверток.");
                }

                return false;
            }

            double probeDistanceFeet = UnitConversionUtils.MillimetersToFeet(150.0);
            double markerOffsetFeet = UnitConversionUtils.MillimetersToFeet(markerOffsetMm);
            XYZ roomCenter = TryGetRoomCenter(selectedRoom);

            for (int i = 0; i < lines.Count; i++)
            {
                ElevationLineData lineData = lines[i];

                XYZ direction = new XYZ(lineData.LineDirection.X, lineData.LineDirection.Y, 0.0);
                if (direction.GetLength() <= 1e-9)
                {
                    if (warnings != null)
                    {
                        warnings.Add("Линия " + RevitElementIdUtils.GetElementIdValue(lineData.LineElementId) + " имеет некорректное направление в плоскости XY.");
                    }

                    return false;
                }

                direction = direction.Normalize();

                XYZ normalA = new XYZ(-direction.Y, direction.X, 0.0).Normalize();
                XYZ normalB = new XYZ(direction.Y, -direction.X, 0.0).Normalize();

                XYZ probePointA = lineData.MidPoint + normalA * probeDistanceFeet;
                XYZ probePointB = lineData.MidPoint + normalB * probeDistanceFeet;

                bool isAInside = selectedRoom.IsPointInRoom(probePointA);
                bool isBInside = selectedRoom.IsPointInRoom(probePointB);

                XYZ insideNormal;
                if (isAInside && !isBInside)
                {
                    insideNormal = normalA;
                }
                else if (!isAInside && isBInside)
                {
                    insideNormal = normalB;
                }
                else
                {
                    // Если проверка по точкам не дала однозначный ответ, используем центр помещения.
                    insideNormal = ChooseNormalByRoomCenter(normalA, normalB, lineData.MidPoint, roomCenter);
                }

                lineData.InsideNormal = insideNormal;

                // Блок смещения точки установки ElevationMarker от середины линии по перпендикуляру.
                // По формуле: normalDirection = lineDirection.CrossProduct(XYZ.BasisZ).Normalize().
                XYZ normalDirection = direction.CrossProduct(XYZ.BasisZ);
                if (normalDirection.GetLength() <= 1e-9)
                {
                    normalDirection = insideNormal;
                }
                else
                {
                    normalDirection = normalDirection.Normalize();
                }

                // Приводим перпендикуляр к направлению "внутрь помещения", чтобы сохранить корректный взгляд развертки.
                if (normalDirection.DotProduct(insideNormal) < 0.0)
                {
                    normalDirection = -normalDirection;
                }

                lineData.MarkerPoint = lineData.MidPoint + normalDirection * markerOffsetFeet;
                lineData.RoomData = roomData;
            }

            return true;
        }

        private XYZ TryGetRoomCenter(Room room)
        {
            if (room == null)
            {
                return XYZ.Zero;
            }

            LocationPoint locationPoint = room.Location as LocationPoint;
            if (locationPoint != null)
            {
                return locationPoint.Point;
            }

            BoundingBoxXYZ roomBox = room.get_BoundingBox(null);
            if (roomBox != null)
            {
                return new XYZ(
                    (roomBox.Min.X + roomBox.Max.X) / 2.0,
                    (roomBox.Min.Y + roomBox.Max.Y) / 2.0,
                    (roomBox.Min.Z + roomBox.Max.Z) / 2.0);
            }

            return XYZ.Zero;
        }

        private XYZ ChooseNormalByRoomCenter(XYZ normalA, XYZ normalB, XYZ lineMidPoint, XYZ roomCenter)
        {
            XYZ roomVector = new XYZ(roomCenter.X - lineMidPoint.X, roomCenter.Y - lineMidPoint.Y, 0.0);
            if (roomVector.GetLength() <= 1e-9)
            {
                return normalA;
            }

            roomVector = roomVector.Normalize();
            double dotA = roomVector.DotProduct(normalA);
            double dotB = roomVector.DotProduct(normalB);
            return dotA >= dotB ? normalA : normalB;
        }
    }
}
