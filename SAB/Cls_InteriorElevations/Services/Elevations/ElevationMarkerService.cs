using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Elevations
{
    public class ElevationMarkerService
    {
        public ViewSection CreateElevationForLine(
            Document document,
            ViewPlan planView,
            ElevationLineData lineData,
            ElementId elevationViewFamilyTypeId,
            int viewScale,
            IList<string> warnings)
        {
            if (document == null || planView == null || lineData == null)
            {
                return null;
            }

            ElevationMarker marker = ElevationMarker.CreateElevationMarker(
                document,
                elevationViewFamilyTypeId,
                lineData.MarkerPoint,
                viewScale);

            int elevationIndex = GetAvailableMarkerIndex(marker);
            if (elevationIndex < 0)
            {
                if (warnings != null)
                {
                    warnings.Add(
                        "Failed to create elevation for line " + RevitElementIdUtils.GetElementIdValue(lineData.LineElementId) +
                        ": marker has no available elevation index.");
                }

                return null;
            }

            ViewSection viewSection = marker.CreateElevation(document, planView.Id, elevationIndex);

            // Important logic block: marker rotation is used to align the created view with inside room direction.
            RotateMarkerToDirection(document, marker, lineData.MarkerPoint, lineData.InsideNormal, viewSection, warnings, lineData.LineElementId);
            return viewSection;
        }

        private int GetAvailableMarkerIndex(ElevationMarker marker)
        {
            for (int index = 0; index < 4; index++)
            {
                if (marker.IsAvailableIndex(index))
                {
                    return index;
                }
            }

            return -1;
        }

        private void RotateMarkerToDirection(
            Document document,
            ElevationMarker marker,
            XYZ markerPoint,
            XYZ targetInsideNormal,
            ViewSection viewSection,
            IList<string> warnings,
            ElementId lineElementId)
        {
            XYZ currentDirection = GetHorizontalDirection(viewSection.ViewDirection);
            XYZ targetDirection = GetHorizontalDirection(targetInsideNormal);

            if (currentDirection.GetLength() <= 1e-9 || targetDirection.GetLength() <= 1e-9)
            {
                if (warnings != null)
                {
                    warnings.Add(
                        "Could not rotate elevation marker for line " + RevitElementIdUtils.GetElementIdValue(lineElementId) +
                        " because direction vectors are invalid.");
                }

                return;
            }

            double angle = CalculateSignedAngle(currentDirection, targetDirection);
            if (Math.Abs(angle) > 1e-9)
            {
                Line axis = Line.CreateBound(markerPoint, markerPoint + XYZ.BasisZ);
                ElementTransformUtils.RotateElement(document, marker.Id, axis, angle);
                document.Regenerate();
            }

            XYZ alignedDirection = GetHorizontalDirection(viewSection.ViewDirection);
            double dotAfterRotation = alignedDirection.DotProduct(targetDirection);

            if (dotAfterRotation < 0.99)
            {
                // Fallback block: if the first rotation still leaves opposite orientation, rotate by 180 degrees.
                Line axis = Line.CreateBound(markerPoint, markerPoint + XYZ.BasisZ);
                ElementTransformUtils.RotateElement(document, marker.Id, axis, Math.PI);
                document.Regenerate();

                alignedDirection = GetHorizontalDirection(viewSection.ViewDirection);
                dotAfterRotation = alignedDirection.DotProduct(targetDirection);
            }

            if (dotAfterRotation < 0.95 && warnings != null)
            {
                warnings.Add(
                    "Elevation direction check for line " + RevitElementIdUtils.GetElementIdValue(lineElementId) +
                    " did not reach strict alignment threshold.");
            }
        }

        private XYZ GetHorizontalDirection(XYZ source)
        {
            XYZ horizontal = new XYZ(source.X, source.Y, 0.0);
            if (horizontal.GetLength() <= 1e-9)
            {
                return XYZ.Zero;
            }

            return horizontal.Normalize();
        }

        private double CalculateSignedAngle(XYZ fromDirection, XYZ toDirection)
        {
            double angle = fromDirection.AngleTo(toDirection);
            double crossZ = fromDirection.CrossProduct(toDirection).Z;
            if (crossZ < 0.0)
            {
                angle = -angle;
            }

            return angle;
        }
    }
}
