using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Rooms
{
    public class RoomDetectionService
    {
        public bool TryPickRoomData(
            UIDocument uiDocument,
            bool pickFromLink,
            out RoomData roomData,
            out string errorMessage)
        {
            roomData = null;
            errorMessage = string.Empty;

            if (uiDocument == null || uiDocument.Document == null)
            {
                errorMessage = "Не удалось получить активный документ Revit.";
                return false;
            }

            Reference pickedReference;
            try
            {
                pickedReference = uiDocument.Selection.PickObject(
                    pickFromLink ? ObjectType.LinkedElement : ObjectType.Element,
                    pickFromLink
                        ? (ISelectionFilter)new LinkedRoomSelectionFilter(uiDocument.Document)
                        : new RoomSelectionFilter(),
                    pickFromLink
                        ? "Выберите помещение в связанном файле для создания разверток"
                        : "Выберите помещение для создания разверток");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                errorMessage = string.Empty;
                return false;
            }

            if (pickedReference == null)
            {
                errorMessage = "Помещение не выбрано.";
                return false;
            }

            ElementId linkInstanceId = ElementId.InvalidElementId;
            Room room;
            if (pickFromLink)
            {
                RevitLinkInstance linkInstance = uiDocument.Document.GetElement(pickedReference.ElementId) as RevitLinkInstance;
                Document linkedDocument = linkInstance != null ? linkInstance.GetLinkDocument() : null;
                if (linkedDocument == null)
                {
                    errorMessage = "Связанный файл не загружен или недоступен.";
                    return false;
                }

                linkInstanceId = linkInstance.Id;
                room = linkedDocument.GetElement(pickedReference.LinkedElementId) as Room;
            }
            else
            {
                room = uiDocument.Document.GetElement(pickedReference) as Room;
            }

            if (room == null)
            {
                errorMessage = "Выбранный элемент не является помещением.";
                return false;
            }

            roomData = BuildRoomData(room, linkInstanceId);
            return true;
        }

        private RoomData BuildRoomData(Room room, ElementId linkInstanceId)
        {
            string roomName = room.get_Parameter(BuiltInParameter.ROOM_NAME) != null
                ? room.get_Parameter(BuiltInParameter.ROOM_NAME).AsString()
                : room.Name;

            string roomNumber = room.get_Parameter(BuiltInParameter.ROOM_NUMBER) != null
                ? room.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString()
                : string.Empty;

            RoomData roomData = new RoomData();
            roomData.RoomElementId = room.Id;
            roomData.LinkInstanceId = linkInstanceId;
            roomData.RoomName = RevitNameUtils.SanitizeName(roomName, "Без имени");
            roomData.RoomNumber = RevitNameUtils.SanitizeName(roomNumber, "Без номера");
            roomData.LevelId = room.LevelId;

            return roomData;
        }

        private class RoomSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                return element is Room;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }

        private class LinkedRoomSelectionFilter : ISelectionFilter
        {
            private readonly Document _hostDocument;

            public LinkedRoomSelectionFilter(Document hostDocument)
            {
                _hostDocument = hostDocument;
            }

            public bool AllowElement(Element element)
            {
                return element is RevitLinkInstance;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                if (_hostDocument == null || reference == null)
                {
                    return false;
                }

                RevitLinkInstance linkInstance = _hostDocument.GetElement(reference.ElementId) as RevitLinkInstance;
                Document linkedDocument = linkInstance != null ? linkInstance.GetLinkDocument() : null;
                return linkedDocument != null &&
                       linkedDocument.GetElement(reference.LinkedElementId) is Room;
            }
        }
    }
}
