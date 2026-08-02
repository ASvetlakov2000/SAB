using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using SAB.DoorWindowExplanations.Models;

namespace SAB.DoorWindowExplanations.Services
{
    public class DoorWindowGeometryService
    {
        private const double DirectionTolerance = 1e-9;
        private const double BoundsToleranceFeet = 1.0 / 3048.0;

        public void PopulateCoordinateSystem(DoorWindowSelectionData selection)
        {
            if (selection == null || selection.Element == null)
            {
                throw new ArgumentNullException(nameof(selection));
            }

            FamilyInstance familyInstance = selection.Element as FamilyInstance;
            Transform transform = selection.SourceToHostTransform ?? Transform.Identity;

            XYZ sourceOrigin = GetSourceOrigin(selection.Element);
            XYZ hostOrigin = transform.OfPoint(sourceOrigin);
            XYZ sourceWidth = GetSourceWallDirection(familyInstance, sourceOrigin);
            if (sourceWidth == null || sourceWidth.GetLength() < DirectionTolerance)
            {
                sourceWidth = familyInstance != null ? familyInstance.HandOrientation : XYZ.BasisX;
            }

            XYZ width = FlattenAndNormalize(transform.OfVector(sourceWidth), XYZ.BasisX);
            XYZ actualFacing = familyInstance != null
                ? FlattenAndNormalize(transform.OfVector(familyInstance.FacingOrientation), XYZ.BasisY)
                : XYZ.BasisY;

            XYZ facing = XYZ.BasisZ.CrossProduct(width).Normalize();
            if (facing.DotProduct(actualFacing) < 0.0)
            {
                width = width.Negate();
                facing = XYZ.BasisZ.CrossProduct(width).Normalize();
            }

            selection.Origin = hostOrigin;
            selection.WidthDirection = width;
            selection.FacingDirection = facing;
        }

        public DoorWindowOrientedBounds GetAutomaticBounds(DoorWindowSelectionData selection)
        {
            if (selection == null || selection.Element == null)
            {
                throw new ArgumentNullException(nameof(selection));
            }

            List<XYZ> sourcePoints = CollectGeometryPoints(selection.Element);
            if (sourcePoints.Count > 0)
            {
                try
                {
                    return BuildAutomaticBounds(selection, sourcePoints);
                }
                catch (InvalidOperationException)
                {
                    sourcePoints.Clear();
                }
            }

            AddBoundingBoxPoints(selection.Element.get_BoundingBox(null), sourcePoints);
            if (sourcePoints.Count == 0)
            {
                throw new InvalidOperationException("Revit не вернул габариты выбранного элемента.");
            }

            return BuildAutomaticBounds(selection, sourcePoints);
        }

        private DoorWindowOrientedBounds BuildAutomaticBounds(
            DoorWindowSelectionData selection,
            IList<XYZ> sourcePoints)
        {
            Transform transform = selection.SourceToHostTransform ?? Transform.Identity;
            List<XYZ> hostPoints = new List<XYZ>();
            for (int i = 0; i < sourcePoints.Count; i++)
            {
                hostPoints.Add(transform.OfPoint(sourcePoints[i]));
            }

            return BuildBounds(selection, hostPoints, null);
        }

        public DoorWindowOrientedBounds GetManualBounds(
            DoorWindowSelectionData selection,
            IList<CurveElement> contourLines,
            double planDepthFeet)
        {
            if (selection == null)
            {
                throw new ArgumentNullException(nameof(selection));
            }

            if (contourLines == null || contourLines.Count == 0)
            {
                throw new InvalidOperationException("Не выбраны линии контура двери или окна.");
            }

            List<XYZ> points = new List<XYZ>();
            for (int i = 0; i < contourLines.Count; i++)
            {
                Curve curve = contourLines[i] != null ? contourLines[i].GeometryCurve : null;
                if (curve == null || !curve.IsBound)
                {
                    continue;
                }

                IList<XYZ> tessellated = curve.Tessellate();
                if (tessellated != null && tessellated.Count > 0)
                {
                    points.AddRange(tessellated);
                }
                else
                {
                    points.Add(curve.GetEndPoint(0));
                    points.Add(curve.GetEndPoint(1));
                }
            }

            if (points.Count < 2)
            {
                throw new InvalidOperationException("В выбранном контуре нет корректных линий.");
            }

            DoorWindowOrientedBounds bounds = BuildBounds(selection, points, planDepthFeet);
            if (bounds.MaxWidth - bounds.MinWidth < BoundsToleranceFeet ||
                bounds.MaxHeight - bounds.MinHeight < BoundsToleranceFeet)
            {
                throw new InvalidOperationException(
                    "Выбранный контур не задаёт ненулевые ширину и высоту. " +
                    "Выберите линии прямоугольника на развертке.");
            }

            return bounds;
        }

        private DoorWindowOrientedBounds BuildBounds(
            DoorWindowSelectionData selection,
            IList<XYZ> hostPoints,
            double? manualPlanDepthFeet)
        {
            DoorWindowOrientedBounds bounds = new DoorWindowOrientedBounds();
            bounds.Origin = selection.Origin;
            bounds.WidthDirection = selection.WidthDirection;
            bounds.FacingDirection = selection.FacingDirection;
            bounds.MinWidth = double.MaxValue;
            bounds.MaxWidth = double.MinValue;
            bounds.MinDepth = double.MaxValue;
            bounds.MaxDepth = double.MinValue;
            bounds.MinHeight = double.MaxValue;
            bounds.MaxHeight = double.MinValue;

            for (int i = 0; i < hostPoints.Count; i++)
            {
                XYZ delta = hostPoints[i] - bounds.Origin;
                double width = delta.DotProduct(bounds.WidthDirection);
                double depth = delta.DotProduct(bounds.FacingDirection);
                double height = delta.DotProduct(XYZ.BasisZ);

                bounds.MinWidth = Math.Min(bounds.MinWidth, width);
                bounds.MaxWidth = Math.Max(bounds.MaxWidth, width);
                bounds.MinDepth = Math.Min(bounds.MinDepth, depth);
                bounds.MaxDepth = Math.Max(bounds.MaxDepth, depth);
                bounds.MinHeight = Math.Min(bounds.MinHeight, height);
                bounds.MaxHeight = Math.Max(bounds.MaxHeight, height);
            }

            if (manualPlanDepthFeet.HasValue)
            {
                double halfDepth = Math.Max(manualPlanDepthFeet.Value, BoundsToleranceFeet) / 2.0;
                bounds.MinDepth = -halfDepth;
                bounds.MaxDepth = halfDepth;
            }

            ValidateBounds(bounds);
            return bounds;
        }

        private void ValidateBounds(DoorWindowOrientedBounds bounds)
        {
            if (bounds.MaxWidth - bounds.MinWidth < BoundsToleranceFeet ||
                bounds.MaxDepth - bounds.MinDepth < BoundsToleranceFeet ||
                bounds.MaxHeight - bounds.MinHeight < BoundsToleranceFeet)
            {
                throw new InvalidOperationException("Габариты выбранного элемента вырождены или не читаются.");
            }
        }

        private List<XYZ> CollectGeometryPoints(Element element)
        {
            List<XYZ> points = new List<XYZ>();
            try
            {
                Options options = new Options();
                options.DetailLevel = ViewDetailLevel.Fine;
                options.IncludeNonVisibleObjects = false;
                GeometryElement geometry = element.get_Geometry(options);
                CollectGeometryPoints(geometry, points);
            }
            catch
            {
                points.Clear();
            }

            return points;
        }

        private void CollectGeometryPoints(GeometryElement geometry, IList<XYZ> points)
        {
            if (geometry == null)
            {
                return;
            }

            foreach (GeometryObject geometryObject in geometry)
            {
                Solid solid = geometryObject as Solid;
                if (solid != null)
                {
                    foreach (Edge edge in solid.Edges)
                    {
                        IList<XYZ> tessellated = edge.Tessellate();
                        for (int i = 0; i < tessellated.Count; i++)
                        {
                            points.Add(tessellated[i]);
                        }
                    }

                    continue;
                }

                Mesh mesh = geometryObject as Mesh;
                if (mesh != null)
                {
                    IList<XYZ> vertices = mesh.Vertices;
                    for (int i = 0; i < vertices.Count; i++)
                    {
                        points.Add(vertices[i]);
                    }

                    continue;
                }

                Curve curve = geometryObject as Curve;
                if (curve != null && curve.IsBound)
                {
                    IList<XYZ> tessellated = curve.Tessellate();
                    for (int i = 0; i < tessellated.Count; i++)
                    {
                        points.Add(tessellated[i]);
                    }

                    continue;
                }

                PolyLine polyLine = geometryObject as PolyLine;
                if (polyLine != null)
                {
                    IList<XYZ> coordinates = polyLine.GetCoordinates();
                    for (int i = 0; i < coordinates.Count; i++)
                    {
                        points.Add(coordinates[i]);
                    }

                    continue;
                }

                GeometryInstance instance = geometryObject as GeometryInstance;
                if (instance != null)
                {
                    CollectGeometryPoints(instance.GetInstanceGeometry(), points);
                }
            }
        }

        private void AddBoundingBoxPoints(BoundingBoxXYZ boundingBox, IList<XYZ> points)
        {
            if (boundingBox == null)
            {
                return;
            }

            Transform transform = boundingBox.Transform ?? Transform.Identity;
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        XYZ corner = new XYZ(
                            x == 0 ? boundingBox.Min.X : boundingBox.Max.X,
                            y == 0 ? boundingBox.Min.Y : boundingBox.Max.Y,
                            z == 0 ? boundingBox.Min.Z : boundingBox.Max.Z);
                        points.Add(transform.OfPoint(corner));
                    }
                }
            }
        }

        private XYZ GetSourceOrigin(Element element)
        {
            LocationPoint locationPoint = element.Location as LocationPoint;
            if (locationPoint != null)
            {
                return locationPoint.Point;
            }

            BoundingBoxXYZ boundingBox = element.get_BoundingBox(null);
            if (boundingBox != null)
            {
                XYZ center = (boundingBox.Min + boundingBox.Max) * 0.5;
                return (boundingBox.Transform ?? Transform.Identity).OfPoint(center);
            }

            throw new InvalidOperationException("Не удалось определить точку вставки элемента.");
        }

        private XYZ GetSourceWallDirection(FamilyInstance familyInstance, XYZ sourceOrigin)
        {
            Wall wall = familyInstance != null ? familyInstance.Host as Wall : null;
            LocationCurve locationCurve = wall != null ? wall.Location as LocationCurve : null;
            Curve curve = locationCurve != null ? locationCurve.Curve : null;
            if (curve == null)
            {
                return null;
            }

            try
            {
                IntersectionResult projection = curve.Project(sourceOrigin);
                double parameter = projection != null ? projection.Parameter : curve.GetEndParameter(0);
                Transform derivatives = curve.ComputeDerivatives(parameter, false);
                return derivatives.BasisX;
            }
            catch
            {
                return curve.GetEndPoint(1) - curve.GetEndPoint(0);
            }
        }

        private XYZ FlattenAndNormalize(XYZ vector, XYZ fallback)
        {
            XYZ flattened = vector != null ? new XYZ(vector.X, vector.Y, 0.0) : null;
            if (flattened == null || flattened.GetLength() < DirectionTolerance)
            {
                flattened = fallback;
            }

            return flattened.Normalize();
        }
    }
}
