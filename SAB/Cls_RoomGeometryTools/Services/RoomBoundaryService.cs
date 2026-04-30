using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using SAB.RoomGeometryTools.Models;
using SAB.RoomGeometryTools.Utils;
using System;
using System.Collections.Generic;

namespace SAB.RoomGeometryTools.Services
{
    /// <summary>
    /// Сервис чтения границ помещений и подготовки полигонов для расчетов.
    /// </summary>
    public class RoomBoundaryService
    {
        public RoomBoundaryPolygon GetRoomBoundaryPolygon(Room room)
        {
            RoomBoundaryPolygon polygon = new RoomBoundaryPolygon();

            if (room == null)
            {
                polygon.ErrorMessage = "Помещение не найдено.";
                return polygon;
            }

            polygon.RoomId = room.Id;

            SpatialElementBoundaryOptions boundaryOptions = new SpatialElementBoundaryOptions();
            IList<IList<BoundarySegment>> loops = room.GetBoundarySegments(boundaryOptions);

            if (loops == null || loops.Count == 0)
            {
                polygon.ErrorMessage = "У помещения отсутствует граница.";
                return polygon;
            }

            List<IList<XYZ>> allLoops = new List<IList<XYZ>>();
            List<IList<Line>> allLoopLines = new List<IList<Line>>();

            for (int loopIndex = 0; loopIndex < loops.Count; loopIndex++)
            {
                IList<BoundarySegment> loop = loops[loopIndex];
                if (loop == null || loop.Count == 0)
                {
                    continue;
                }

                List<XYZ> loopVertices = new List<XYZ>();
                List<Line> loopLines = new List<Line>();

                for (int segmentIndex = 0; segmentIndex < loop.Count; segmentIndex++)
                {
                    BoundarySegment boundarySegment = loop[segmentIndex];
                    if (boundarySegment == null)
                    {
                        continue;
                    }

                    Curve curve = boundarySegment.GetCurve();
                    if (curve == null)
                    {
                        continue;
                    }

                    Line line = curve as Line;
                    if (line == null)
                    {
                        // Блок фиксации нелинейных сегментов. Ось для таких комнат не строим.
                        polygon.HasNonLinearSegments = true;
                        XYZ start = curve.GetEndPoint(0);
                        XYZ end = curve.GetEndPoint(1);
                        line = Line.CreateBound(start, end);
                    }

                    XYZ startPoint = line.GetEndPoint(0);
                    loopVertices.Add(new XYZ(startPoint.X, startPoint.Y, 0.0));
                    loopLines.Add(line);
                }

                if (loopVertices.Count >= 3)
                {
                    allLoops.Add(loopVertices);
                    allLoopLines.Add(loopLines);
                }
            }

            if (allLoops.Count == 0)
            {
                polygon.ErrorMessage = "Не удалось сформировать замкнутую границу помещения.";
                return polygon;
            }

            int outerIndex = FindOuterLoopIndex(allLoops);
            if (outerIndex < 0)
            {
                polygon.ErrorMessage = "Не удалось определить внешний контур помещения.";
                return polygon;
            }

            polygon.OuterVertices = allLoops[outerIndex];
            polygon.OuterLines = allLoopLines[outerIndex];

            for (int i = 0; i < allLoops.Count; i++)
            {
                if (i == outerIndex)
                {
                    continue;
                }

                polygon.InnerLoops.Add(allLoops[i]);
            }

            return polygon;
        }

        private static int FindOuterLoopIndex(IList<IList<XYZ>> loops)
        {
            if (loops == null || loops.Count == 0)
            {
                return -1;
            }

            int bestIndex = -1;
            double bestArea = 0.0;

            for (int i = 0; i < loops.Count; i++)
            {
                IList<XYZ> loop = loops[i];
                if (loop == null || loop.Count < 3)
                {
                    continue;
                }

                double area = Math.Abs(PolygonUtils.CalculateSignedAreaXY(loop));
                if (area > bestArea)
                {
                    bestArea = area;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }
    }
}

