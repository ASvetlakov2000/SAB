using System.Collections.Generic;
using System;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Geometry
{
    public class ElevationGeometryService
    {
        private const double SplitWallPointToleranceMm = 1.0;
        private const double SplitWallAngleToleranceDegrees = 0.1;

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

            PrepareSplitWallSegments(lineDataList);
            return lineDataList;
        }

        public int PrepareSplitWallSegments(IList<ElevationLineData> lineDataList)
        {
            if (lineDataList == null || lineDataList.Count == 0)
            {
                return 0;
            }

            double pointTolerance = UnitConversionUtils.MillimetersToFeet(SplitWallPointToleranceMm);
            double minimumParallelDot = Math.Cos(
                SplitWallAngleToleranceDegrees * Math.PI / 180.0);

            int splitConnectionCount = 0;
            int wallSequenceIndex = 0;

            for (int index = 0; index < lineDataList.Count; index++)
            {
                ElevationLineData currentLine = lineDataList[index];
                if (currentLine == null)
                {
                    continue;
                }

                currentLine.IsSplitWallContinuation = false;

                ElevationLineData previousLine = index > 0
                    ? lineDataList[index - 1]
                    : null;

                bool continuesPreviousWall = IsOrderedSplitWallConnection(
                    previousLine,
                    currentLine,
                    pointTolerance,
                    minimumParallelDot);

                if (continuesPreviousWall)
                {
                    currentLine.IsSplitWallContinuation = true;
                    currentLine.WallSequenceIndex = previousLine.WallSequenceIndex > 0
                        ? previousLine.WallSequenceIndex
                        : Math.Max(1, wallSequenceIndex);
                    wallSequenceIndex = Math.Max(wallSequenceIndex, currentLine.WallSequenceIndex);
                    splitConnectionCount++;
                    continue;
                }

                wallSequenceIndex++;
                currentLine.WallSequenceIndex = wallSequenceIndex;
            }

            return splitConnectionCount;
        }

        private bool IsOrderedSplitWallConnection(
            ElevationLineData previousLine,
            ElevationLineData currentLine,
            double pointTolerance,
            double minimumParallelDot)
        {
            if (previousLine == null || currentLine == null ||
                previousLine.StartPoint == null || previousLine.EndPoint == null ||
                currentLine.StartPoint == null || currentLine.EndPoint == null)
            {
                return false;
            }

            // Порядок задается направлением выбранных линий и не исправляется автоматически:
            // конец первой линии должен совпасть именно с началом второй.
            if (previousLine.EndPoint.DistanceTo(currentLine.StartPoint) > pointTolerance)
            {
                return false;
            }

            XYZ previousDirection = BuildHorizontalDirection(
                previousLine.StartPoint,
                previousLine.EndPoint);
            XYZ currentDirection = BuildHorizontalDirection(
                currentLine.StartPoint,
                currentLine.EndPoint);
            if (previousDirection.GetLength() <= 1e-9 || currentDirection.GetLength() <= 1e-9)
            {
                return false;
            }

            double directionDot = previousDirection.DotProduct(currentDirection);

            // Модуль dot проверяет параллельность, а положительный знак — что вторая линия
            // продолжается после первой. Обратное направление означало бы наложение.
            bool areParallel = Math.Abs(directionDot) >= minimumParallelDot;
            bool continueWithoutOverlap = directionDot >= minimumParallelDot;
            return areParallel && continueWithoutOverlap;
        }

        private XYZ BuildHorizontalDirection(XYZ startPoint, XYZ endPoint)
        {
            if (startPoint == null || endPoint == null)
            {
                return XYZ.Zero;
            }

            XYZ direction = new XYZ(
                endPoint.X - startPoint.X,
                endPoint.Y - startPoint.Y,
                0.0);
            return direction.GetLength() > 1e-9
                ? direction.Normalize()
                : XYZ.Zero;
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
