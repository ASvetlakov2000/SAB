using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Selection
{
    public class DetailLineSelectionResult
    {
        public DetailLineSelectionResult()
        {
            Lines = new List<DetailLine>();
            Warnings = new List<string>();
        }

        public List<DetailLine> Lines { get; private set; }

        public List<string> Warnings { get; private set; }
    }

    public class DetailLineSelectionService
    {
        public DetailLineSelectionResult GetSelectedLines(UIDocument uiDocument, View activeView)
        {
            DetailLineSelectionResult result = new DetailLineSelectionResult();

            if (uiDocument == null || activeView == null)
            {
                result.Warnings.Add("Unable to read current selection because document or active view is null.");
                return result;
            }

            Document document = uiDocument.Document;
            ICollection<ElementId> selectedIds = uiDocument.Selection.GetElementIds();

            if (selectedIds == null || selectedIds.Count == 0)
            {
                result.Warnings.Add("No elements are selected. Select detail lines in the active plan view and run the command again.");
                return result;
            }

            foreach (ElementId elementId in selectedIds)
            {
                Element element = document.GetElement(elementId);
                DetailLine detailLine = element as DetailLine;

                if (detailLine == null)
                {
                    result.Warnings.Add("Element " + RevitElementIdUtils.GetElementIdValue(elementId) + " is not a detail line and was skipped.");
                    continue;
                }

                if (!RevitElementIdUtils.AreEqual(detailLine.OwnerViewId, activeView.Id))
                {
                    result.Warnings.Add("Detail line " + RevitElementIdUtils.GetElementIdValue(elementId) + " is not placed in the active view and was skipped.");
                    continue;
                }

                Curve sourceCurve = GetCurve(detailLine);
                if (sourceCurve == null)
                {
                    result.Warnings.Add("Detail line " + RevitElementIdUtils.GetElementIdValue(elementId) + " has no valid curve and was skipped.");
                    continue;
                }

                Line line = sourceCurve as Line;
                if (line == null)
                {
                    result.Warnings.Add("Detail line " + RevitElementIdUtils.GetElementIdValue(elementId) + " is not linear and was skipped.");
                    continue;
                }

                if (line.Length <= 1e-9)
                {
                    result.Warnings.Add("Detail line " + RevitElementIdUtils.GetElementIdValue(elementId) + " has zero length and was skipped.");
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
    }
}
