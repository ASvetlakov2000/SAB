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
        public bool TryPickRoomData(UIDocument uiDocument, out RoomData roomData, out string errorMessage)
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
                    ObjectType.Element,
                    new RoomSelectionFilter(),
                    "Выберите помещение для создания разверток");
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

            Room room = uiDocument.Document.GetElement(pickedReference) as Room;
            if (room == null)
            {
                errorMessage = "Выбранный элемент не является помещением.";
                return false;
            }

            roomData = BuildRoomData(room);
            return true;
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
    }
}
