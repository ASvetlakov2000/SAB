using System.Collections.Generic;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Geometry
{
    public class ElevationGeometryService
    {
        public List<ElevationLineData> BuildElevationLineData(IList<DetailLine> detailLines, IList<string> warnings)
        {
            List<ElevationLineData> lineDataList = new List<ElevationLineData>();

            if (detailLines == null || detailLines.Count == 0)
            {
                return lineDataList;
            }

            int currentIndex = 1;
            for (int i = 0; i < detailLines.Count; i++)
            {
                DetailLine detailLine = detailLines[i];
                Curve sourceCurve = GetCurve(detailLine);
                Line sourceLine = sourceCurve as Line;

                if (sourceLine == null)
                {
                    if (warnings != null)
                    {
                        warnings.Add("Линия " + RevitElementIdUtils.GetElementIdValue(detailLine.Id) + " не является прямым отрезком и была пропущена.");
                    }

                    continue;
                }

                XYZ startPoint = sourceLine.GetEndPoint(0);
                XYZ endPoint = sourceLine.GetEndPoint(1);
                XYZ rawDirection = endPoint - startPoint;

                if (rawDirection.GetLength() <= 1e-9)
                {
                    if (warnings != null)
                    {
                        warnings.Add("Линия " + RevitElementIdUtils.GetElementIdValue(detailLine.Id) + " имеет нулевую длину и была пропущена.");
                    }

                    continue;
                }

                XYZ lineDirection = rawDirection.Normalize();
                XYZ midPoint = new XYZ(
                    (startPoint.X + endPoint.X) / 2.0,
                    (startPoint.Y + endPoint.Y) / 2.0,
                    (startPoint.Z + endPoint.Z) / 2.0);

                ElevationLineData lineData = new ElevationLineData();
                lineData.LineElementId = detailLine.Id;
                lineData.SourceCurve = sourceLine;
                lineData.StartPoint = startPoint;
                lineData.EndPoint = endPoint;
                lineData.MidPoint = midPoint;
                lineData.LineDirection = lineDirection;
                lineData.LineLength = sourceLine.Length;
                lineData.Index = currentIndex;
                lineData.EndIndex = currentIndex + 1;

                lineDataList.Add(lineData);
                currentIndex++;
            }

            return lineDataList;
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
