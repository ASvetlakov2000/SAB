using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Geometry
{
    public sealed class ContourLineOrientationService
    {
        public void Assign(IList<ElevationLineData> lines, RoomData roomData, double markerOffsetMm, IList<string> warnings)
        {
            var segments = lines.Select(line => new ContourSegment(
                new ContourPoint(line.StartPoint.X, line.StartPoint.Y),
                new ContourPoint(line.EndPoint.X, line.EndPoint.Y))).ToList();
            double requested = UnitConversionUtils.MillimetersToFeet(markerOffsetMm);
            var result = ContourDirectionGeometry.Build(segments, requested, UnitConversionUtils.MillimetersToFeet(1));
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i]; var normal = result.Normals[i];
                line.InsideNormal = new XYZ(normal.X, normal.Y, 0);
                line.MarkerDistance = result.MarkerDistances[i];
                line.MarkerPoint = line.MidPoint + line.InsideNormal * line.MarkerDistance;
                line.RoomData = roomData;
                line.IsOpenContour = !result.IsClosed;
                line.UsesVirtualClosure = result.UsesVirtualClosure;
                if (line.MarkerDistance < requested - 1e-8 && warnings != null)
                    warnings.Add("Линия " + RevitElementIdUtils.GetElementIdValue(line.LineElementId) + ": смещение маркера ограничено до " +
                        UnitConversionUtils.FeetToMillimeters(line.MarkerDistance).ToString("F0") + " мм по границе выбранного контура.");
            }
        }
    }
}
