using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Selection
{
    public class DetailLineSelectionResult
    {
        public DetailLineSelectionResult()
        {
            Lines = new List<DetailLine>();
            Warnings = new List<string>();
            IsCancelled = false;
        }

        public List<DetailLine> Lines { get; private set; }

        public List<string> Warnings { get; private set; }

        public bool IsCancelled { get; set; }
    }

    public class DetailLineSelectionService
    {
        public DetailLineSelectionResult PickDetailLines(UIDocument uiDocument, View activeView)
        {
            DetailLineSelectionResult result = new DetailLineSelectionResult();

            if (uiDocument == null || activeView == null)
            {
                result.Warnings.Add("Не удалось начать выбор линий, потому что документ или активный вид недоступен.");
                return result;
            }

            IList<Reference> pickedReferences;
            try
            {
                pickedReferences = uiDocument.Selection.PickObjects(
                    ObjectType.Element,
                    new DetailLineSelectionFilter(activeView.Id),
                    "Выберите линии, вдоль которых будут созданы развертки");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                result.IsCancelled = true;
                return result;
            }

            if (pickedReferences == null || pickedReferences.Count == 0)
            {
                result.Warnings.Add("Линии не выбраны.");
                return result;
            }

            Document document = uiDocument.Document;

            for (int i = 0; i < pickedReferences.Count; i++)
            {
                Reference reference = pickedReferences[i];
                if (reference == null)
                {
                    continue;
                }

                Element element = document.GetElement(reference);
                DetailLine detailLine = element as DetailLine;

                if (detailLine == null)
                {
                    result.Warnings.Add("Элемент не является линией детализации и был пропущен.");
                    continue;
                }

                if (!RevitElementIdUtils.AreEqual(detailLine.OwnerViewId, activeView.Id))
                {
                    result.Warnings.Add("Линия " + RevitElementIdUtils.GetElementIdValue(detailLine.Id) + " находится не в активном виде и была пропущена.");
                    continue;
                }

                Curve sourceCurve = GetCurve(detailLine);
                if (sourceCurve == null)
                {
                    result.Warnings.Add("Линия " + RevitElementIdUtils.GetElementIdValue(detailLine.Id) + " не содержит корректной кривой и была пропущена.");
                    continue;
                }

                Line line = sourceCurve as Line;
                if (line == null)
                {
                    result.Warnings.Add("Линия " + RevitElementIdUtils.GetElementIdValue(detailLine.Id) + " не является прямым отрезком и была пропущена.");
                    continue;
                }

                if (line.Length <= 1e-9)
                {
                    result.Warnings.Add("Линия " + RevitElementIdUtils.GetElementIdValue(detailLine.Id) + " имеет нулевую длину и была пропущена.");
                    continue;
                }

                result.Lines.Add(detailLine);
            }

            result.Lines.Sort(
                delegate(DetailLine left, DetailLine right)
                {
                    return RevitElementIdUtils.Compare(left.Id, right.Id);
                });

            return result;
        }

        private Curve GetCurve(DetailLine detailLine)
        {
            if (detailLine == null)
            {
                return null;
            }

            if (detailLine.GeometryCurve != null)
            {
                return detailLine.GeometryCurve;
            }

            LocationCurve locationCurve = detailLine.Location as LocationCurve;
            if (locationCurve != null)
            {
                return locationCurve.Curve;
            }

            return null;
        }

        private class DetailLineSelectionFilter : ISelectionFilter
        {
            private readonly ElementId _activeViewId;

            public DetailLineSelectionFilter(ElementId activeViewId)
            {
                _activeViewId = activeViewId;
            }

            public bool AllowElement(Element element)
            {
                DetailLine detailLine = element as DetailLine;
                if (detailLine == null)
                {
                    return false;
                }

                return RevitElementIdUtils.AreEqual(detailLine.OwnerViewId, _activeViewId);
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
