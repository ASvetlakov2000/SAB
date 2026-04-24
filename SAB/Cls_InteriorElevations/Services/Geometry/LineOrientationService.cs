using System;
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

            double markerOffsetFeet = UnitConversionUtils.MillimetersToFeet(markerOffsetMm);
            if (markerOffsetFeet <= 1e-9)
            {
                markerOffsetFeet = UnitConversionUtils.MillimetersToFeet(200.0);
                if (warnings != null)
                {
                    warnings.Add("Marker offset was invalid, default value 200 mm was used.");
                }
            }

            double probeDistanceFeet = Math.Max(markerOffsetFeet, UnitConversionUtils.MillimetersToFeet(150.0));
            XYZ roomCenter = TryGetRoomCenter(document, roomData);

            for (int i = 0; i < lines.Count; i++)
            {
                ElevationLineData lineData = lines[i];

                XYZ direction = new XYZ(lineData.LineDirection.X, lineData.LineDirection.Y, 0.0);
                if (direction.GetLength() <= 1e-9)
                {
                    if (warnings != null)
                    {
                        warnings.Add("Line " + RevitElementIdUtils.GetElementIdValue(lineData.LineElementId) + " has invalid XY direction and was skipped.");
                    }

                    return false;
                }

                direction = direction.Normalize();

                XYZ normalA = new XYZ(-direction.Y, direction.X, 0.0).Normalize();
                XYZ normalB = new XYZ(direction.Y, -direction.X, 0.0).Normalize();

                XYZ probePointA = lineData.MidPoint + normalA * probeDistanceFeet;
                XYZ probePointB = lineData.MidPoint + normalB * probeDistanceFeet;

                bool isAInside = IsPointInsideTargetRoom(document, roomData, probePointA);
                bool isBInside = IsPointInsideTargetRoom(document, roomData, probePointB);

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
                    // Fallback logic: choose the normal that points toward the detected room center.
                    insideNormal = ChooseNormalByRoomCenter(normalA, normalB, lineData.MidPoint, roomCenter);

                    if (warnings != null)
                    {
                        warnings.Add(
                            "Line " + RevitElementIdUtils.GetElementIdValue(lineData.LineElementId) +
                            " inside direction was resolved by fallback rule.");
                    }
                }

                lineData.InsideNormal = insideNormal;
                lineData.MarkerPoint = lineData.MidPoint + insideNormal * markerOffsetFeet;
                lineData.RoomData = roomData;
            }

            return true;
        }

        private bool IsPointInsideTargetRoom(Document document, RoomData roomData, XYZ point)
        {
            Room room = document.GetRoomAtPoint(point);
            if (room == null)
            {
                return false;
            }

            return RevitElementIdUtils.AreEqual(room.Id, roomData.RoomElementId);
        }

        private XYZ TryGetRoomCenter(Document document, RoomData roomData)
        {
            if (document == null || roomData == null)
            {
                return XYZ.Zero;
            }

            Room room = document.GetElement(roomData.RoomElementId) as Room;
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
