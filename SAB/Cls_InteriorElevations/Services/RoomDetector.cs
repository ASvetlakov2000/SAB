using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace InteriorElevations.Services
{
    public static class RoomDetector
    {
        public static Room PickRoom(UIDocument uidoc)
        {
            Reference pickedRef =
                uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoomFilter(),
                    "Выберите помещение");

            if (pickedRef == null)
                return null;

            return uidoc.Document.GetElement(pickedRef) as Room;
        }

        private class RoomFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Room;

            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}