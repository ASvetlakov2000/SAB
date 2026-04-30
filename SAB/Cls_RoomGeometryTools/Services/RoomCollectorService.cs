using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System.Collections.Generic;

namespace SAB.RoomGeometryTools.Services
{
    /// <summary>
    /// Сервис сбора помещений и базовой валидации вида.
    /// </summary>
    public class RoomCollectorService
    {
        public IList<Room> GetAllRooms(Document document)
        {
            List<Room> rooms = new List<Room>();

            if (document == null)
            {
                return rooms;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType();

            foreach (Element element in collector)
            {
                Room room = element as Room;
                if (room == null)
                {
                    continue;
                }

                rooms.Add(room);
            }

            return rooms;
        }

        public IList<Room> GetRoomsOnActiveView(Document document, View activeView)
        {
            List<Room> rooms = new List<Room>();

            if (document == null || activeView == null)
            {
                return rooms;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document, activeView.Id);
            collector.OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType();

            foreach (Element element in collector)
            {
                Room room = element as Room;
                if (room == null)
                {
                    continue;
                }

                rooms.Add(room);
            }

            return rooms;
        }

        public Room GetSelectedRoom(UIDocument uiDocument)
        {
            if (uiDocument == null || uiDocument.Document == null)
            {
                return null;
            }

            ICollection<ElementId> selectedIds = uiDocument.Selection.GetElementIds();
            if (selectedIds == null || selectedIds.Count == 0)
            {
                return null;
            }

            foreach (ElementId selectedId in selectedIds)
            {
                Room room = uiDocument.Document.GetElement(selectedId) as Room;
                if (room != null)
                {
                    return room;
                }
            }

            return null;
        }

        public bool IsValidPlanViewForDetailCurves(View view, out string message)
        {
            message = string.Empty;

            if (view == null)
            {
                message = "Активный вид не найден.";
                return false;
            }

            if (view.IsTemplate)
            {
                message = "Активный вид является шаблоном. Выберите рабочий план.";
                return false;
            }

            ViewPlan planView = view as ViewPlan;
            if (planView == null)
            {
                message = "Активный вид должен быть планом этажа или потолка.";
                return false;
            }

            if (planView.ViewType != ViewType.FloorPlan && planView.ViewType != ViewType.CeilingPlan)
            {
                message = "Активный вид должен быть планом этажа или потолка.";
                return false;
            }

            return true;
        }
    }
}

