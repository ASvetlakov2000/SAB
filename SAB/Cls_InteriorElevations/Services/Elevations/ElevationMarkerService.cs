using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Elevations
{
    public class ElevationMarkerService
    {
        private const double MaximumStep = Math.PI / 6.0;
        private const double AngularTolerance = Math.PI / 1800.0;

        public ViewSection CreateElevationForLine(Document document, ViewPlan planView, ElevationLineData lineData,
            ElementId elevationViewFamilyTypeId, int viewScale, out ElementId markerElementId, IList<string> warnings)
        {
            markerElementId = ElementId.InvalidElementId;
            if (document == null || planView == null || lineData == null) return null;
            using (var attempt = new SubTransaction(document))
            {
                attempt.Start();
                var marker = ElevationMarker.CreateElevationMarker(document, elevationViewFamilyTypeId, lineData.MarkerPoint, viewScale);
                int index = GetAvailableMarkerIndex(marker);
                if (index < 0)
                {
                    if (warnings != null) warnings.Add("У маркера развёртки нет свободного индекса вида.");
                    attempt.RollBack(); return null;
                }
                var view = marker.CreateElevation(document, planView.Id, index);
                document.Regenerate();
                if (!AlignMarker(document, marker, lineData.MarkerPoint, lineData.InsideNormal, view))
                {
                    if (warnings != null) warnings.Add("Линия " + RevitElementIdUtils.GetElementIdValue(lineData.LineElementId) +
                        ": Revit не подтвердил выбранное направление. Маркер и вид отменены.");
                    attempt.RollBack(); return null;
                }
                if (attempt.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Revit не подтвердил создание маркера развёртки.");
                markerElementId = marker.Id; return view;
            }
        }

        public bool IsDirectionAligned(ViewSection view, XYZ target)
        {
            XYZ current = Horizontal(view != null ? view.ViewDirection : null), desired = Horizontal(target);
            return current.GetLength() > 1e-9 && desired.GetLength() > 1e-9 && Math.Abs(SignedAngle(current, desired)) <= AngularTolerance;
        }

        private bool AlignMarker(Document document, ElevationMarker marker, XYZ point, XYZ target, ViewSection view)
        {
            XYZ desired = Horizontal(target);
            if (desired.GetLength() <= 1e-9) return false;
            var axis = Line.CreateBound(point, point + XYZ.BasisZ);
            for (int correction = 0; correction < 3; correction++)
            {
                XYZ current = Horizontal(view.ViewDirection);
                if (current.GetLength() <= 1e-9) return false;
                double remaining = SignedAngle(current, desired);
                if (Math.Abs(remaining) <= AngularTolerance) return true;
                // Avoid large-angle marker rotations. Correct the measured residual, never a blind PI.
                while (Math.Abs(remaining) > 1e-9)
                {
                    double step = Math.Sign(remaining) * Math.Min(Math.Abs(remaining), MaximumStep);
                    ElementTransformUtils.RotateElement(document, marker.Id, axis, step);
                    document.Regenerate(); remaining -= step;
                }
            }
            return IsDirectionAligned(view, desired);
        }

        private int GetAvailableMarkerIndex(ElevationMarker marker)
        {
            for (int index = 0; index < 4; index++) if (marker.IsAvailableIndex(index)) return index;
            return -1;
        }

        private static XYZ Horizontal(XYZ source)
        {
            if (source == null || double.IsNaN(source.X) || double.IsInfinity(source.X) || double.IsNaN(source.Y) || double.IsInfinity(source.Y)) return XYZ.Zero;
            var direction = new XYZ(source.X, source.Y, 0);
            return direction.GetLength() > 1e-9 ? direction.Normalize() : XYZ.Zero;
        }

        private static double SignedAngle(XYZ from, XYZ to)
        {
            return Math.Atan2(from.CrossProduct(to).Z, from.DotProduct(to));
        }
    }
}
