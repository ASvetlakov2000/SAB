using Autodesk.Revit.DB;

using System;
using Autodesk.Revit.DB.Architecture;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Models
{
    public class RoomData
    {
        public ElementId RoomElementId { get; set; }

        public ElementId LinkInstanceId { get; set; }

        public bool IsLinkedRoom
        {
            get
            {
                return LinkInstanceId != null &&
                       !RevitElementIdUtils.AreEqual(LinkInstanceId, ElementId.InvalidElementId);
            }
        }

        public string RoomName { get; set; }

        public string RoomNumber { get; set; }

        public ElementId LevelId { get; set; }

        public bool TryResolveRoom(
            Document hostDocument,
            out Room room,
            out Transform roomToHost)
        {
            room = null;
            roomToHost = Transform.Identity;
            if (hostDocument == null || RoomElementId == null)
            {
                return false;
            }

            if (!IsLinkedRoom)
            {
                room = hostDocument.GetElement(RoomElementId) as Room;
                return room != null;
            }

            RevitLinkInstance linkInstance = hostDocument.GetElement(LinkInstanceId) as RevitLinkInstance;
            Document linkedDocument = linkInstance != null ? linkInstance.GetLinkDocument() : null;
            if (linkedDocument == null)
            {
                return false;
            }

            room = linkedDocument.GetElement(RoomElementId) as Room;
            roomToHost = linkInstance.GetTotalTransform();
            return room != null && roomToHost != null;
        }

        public bool IsOnHostLevel(Document hostDocument, ElementId hostLevelId)
        {
            if (hostDocument == null || hostLevelId == null)
            {
                return false;
            }

            if (!IsLinkedRoom)
            {
                return RevitElementIdUtils.AreEqual(LevelId, hostLevelId);
            }

            Room room;
            Transform roomToHost;
            if (!TryResolveRoom(hostDocument, out room, out roomToHost))
            {
                return false;
            }

            Level hostLevel = hostDocument.GetElement(hostLevelId) as Level;
            Level roomLevel = room.Document.GetElement(room.LevelId) as Level;
            if (hostLevel == null || roomLevel == null)
            {
                return false;
            }

            double roomElevationInHost = roomToHost.OfPoint(new XYZ(0.0, 0.0, roomLevel.Elevation)).Z;
            double toleranceFeet = UnitConversionUtils.MillimetersToFeet(300.0);
            return Math.Abs(roomElevationInHost - hostLevel.Elevation) <= toleranceFeet;
        }
    }
}
