using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace SAB.InteriorElevations.Services.Rooms
{
    /// <summary>
    /// Сервис выбора помещений для построения план-схем.
    /// Работает только в режиме выбора одного помещения пользователем.
    /// </summary>
    public class RoomPlanSelectionService
    {
        public IList<Room> PickSingleRoom(UIDocument uiDocument, out bool isCancelled, out string errorMessage)
        {
            isCancelled = false;
            errorMessage = string.Empty;

            List<Room> result = new List<Room>();

            if (uiDocument == null || uiDocument.Document == null)
            {
                errorMessage = "Не удалось получить активный документ Revit.";
                return result;
            }

            Reference pickedReference;
            try
            {
                pickedReference = uiDocument.Selection.PickObject(
                    ObjectType.Element,
                    new RoomSelectionFilter(),
                    "Выберите помещение для создания план-схемы");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                isCancelled = true;
                return result;
            }

            if (pickedReference == null)
            {
                errorMessage = "Помещение не выбрано.";
                return result;
            }

            Room room = uiDocument.Document.GetElement(pickedReference) as Room;
            if (room == null)
            {
                errorMessage = "Выбранный элемент не является помещением.";
                return result;
            }

            result.Add(room);
            return result;
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

