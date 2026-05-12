using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace SAB.InteriorElevations.Services.Marks
{
    public class RoomPlanRoomTagPlacementService
    {
        public int PlaceRoomTag(
            Document document,
            ViewPlan planView,
            Room room,
            ElementId roomTagTypeId,
            IList<string> warnings)
        {
            if (document == null || planView == null || room == null)
            {
                return 0;
            }

            if (roomTagTypeId == null || roomTagTypeId == ElementId.InvalidElementId)
            {
                return 0;
            }

            FamilySymbol roomTagType = document.GetElement(roomTagTypeId) as FamilySymbol;
            if (roomTagType == null)
            {
                AddWarning(warnings, "Не найден выбранный тип марки помещения для план-схемы.");
                return 0;
            }

            if (roomTagType.Category == null || roomTagType.Category.Id.IntegerValue != (int)BuiltInCategory.OST_RoomTags)
            {
                AddWarning(warnings, "Выбранный тип не относится к категории марок помещений.");
                return 0;
            }

            LocationPoint roomLocationPoint = room.Location as LocationPoint;
            if (roomLocationPoint == null || roomLocationPoint.Point == null)
            {
                AddWarning(warnings, "Не удалось определить точку размещения помещения для установки марки.");
                return 0;
            }

            try
            {
                UV tagPoint = new UV(roomLocationPoint.Point.X, roomLocationPoint.Point.Y);
                RoomTag roomTag = document.Create.NewRoomTag(new LinkElementId(room.Id), tagPoint, planView.Id);
                if (roomTag == null)
                {
                    AddWarning(warnings, "Не удалось создать марку помещения на план-схеме.");
                    return 0;
                }

                try
                {
                    roomTag.ChangeTypeId(roomTagTypeId);
                }
                catch (Exception typeException)
                {
                    AddWarning(warnings, "Марка помещения создана, но не удалось применить выбранный тип: " + typeException.Message);
                }

                return 1;
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Ошибка размещения марки помещения на план-схеме: " + exception.Message);
                return 0;
            }
        }

        private void AddWarning(IList<string> warnings, string text)
        {
            if (warnings == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            warnings.Add(text);
        }
    }
}
