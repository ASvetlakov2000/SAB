using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace SAB.DoorWindowExplanations.Services
{
    public class DoorWindowContourSelectionService
    {
        public IList<CurveElement> PickContourLines(UIDocument uiDocument)
        {
            IList<Reference> references = uiDocument.Selection.PickObjects(
                ObjectType.Element,
                new CurveElementSelectionFilter(),
                "Выберите линии контура двери или окна на активной развертке и нажмите «Готово»");

            List<CurveElement> lines = new List<CurveElement>();
            if (references == null)
            {
                return lines;
            }

            for (int i = 0; i < references.Count; i++)
            {
                CurveElement curveElement = uiDocument.Document.GetElement(references[i]) as CurveElement;
                if (curveElement != null)
                {
                    lines.Add(curveElement);
                }
            }

            return lines;
        }

        private class CurveElementSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                return element is CurveElement;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return true;
            }
        }
    }
}
