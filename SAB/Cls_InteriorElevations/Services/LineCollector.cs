using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Collections.Generic;
using System.Linq;

namespace InteriorElevations.Services
{
    public static class LineCollector
    {
        public static List<DetailLine> CollectSelectedLines(UIDocument uidoc)
        {
            Document doc = uidoc.Document;

            IList<Reference> pickedLines =
                uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new DetailLineSelectionFilter(),
                    "Выберите линии для создания разверток");

            if (pickedLines.Count == 0)
                return new List<DetailLine>();

            List<DetailLine> lines =
                pickedLines
                .Select(r => doc.GetElement(r) as DetailLine)
                .Where(l => l != null)
                .OrderBy(l => l.Id.IntegerValue)
                .ToList();

            return lines;
        }

        private class DetailLineSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is DetailLine;

            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}