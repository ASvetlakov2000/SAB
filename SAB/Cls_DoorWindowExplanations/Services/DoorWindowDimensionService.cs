using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using SAB.DoorWindowExplanations.Models;

namespace SAB.DoorWindowExplanations.Services
{
    public class DoorWindowDimensionService
    {
        private const double DirectionTolerance = 0.92;
        private const double PositionToleranceFeet = 1.0 / 3048.0;

        public int CreateFrontDimensions(
            Document document,
            DoorWindowSelectionData selection,
            DoorWindowOrientedBounds bounds,
            ViewSection view,
            DoorWindowViewSettings settings,
            IList<string> warnings)
        {
            if (document == null || selection == null || bounds == null || view == null || settings == null)
            {
                return 0;
            }

            if (selection.IsLinked)
            {
                AddWarning(warnings, selection, "размеры пропущены: геометрию связанного файла нельзя использовать как обычные ссылки текущего документа");
                return 0;
            }

            DimensionReferenceCandidate left;
            DimensionReferenceCandidate right;
            DimensionReferenceCandidate bottom;
            DimensionReferenceCandidate top;
            List<DimensionReferenceCandidate> verticalGridReferences = new List<DimensionReferenceCandidate>();
            List<DimensionReferenceCandidate> horizontalGridReferences = new List<DimensionReferenceCandidate>();

            if (selection.IsCurtainWall)
            {
                CurtainWallDimensionReferences curtainReferences = CollectCurtainWallReferences(
                    selection,
                    view,
                    warnings);
                left = curtainReferences.LeftOuterFace;
                right = curtainReferences.RightOuterFace;
                bottom = curtainReferences.BottomOuterFace;
                top = curtainReferences.TopOuterFace;
                verticalGridReferences.AddRange(curtainReferences.VerticalInternalCenters);
                horizontalGridReferences.AddRange(curtainReferences.HorizontalInternalCenters);
            }
            else
            {
                List<DimensionReferenceCandidate> widthBoundaryCandidates = new List<DimensionReferenceCandidate>();
                List<DimensionReferenceCandidate> heightBoundaryCandidates = new List<DimensionReferenceCandidate>();
                CollectBoundaryFaceReferences(
                    selection,
                    view,
                    widthBoundaryCandidates,
                    heightBoundaryCandidates);
                left = GetExtreme(widthBoundaryCandidates, true);
                right = GetExtreme(widthBoundaryCandidates, false);
                bottom = GetExtreme(heightBoundaryCandidates, true);
                top = GetExtreme(heightBoundaryCandidates, false);
            }

            bool hasWidthReferences = left != null && right != null;
            bool hasHeightReferences = bottom != null && top != null;
            if (!hasWidthReferences)
            {
                AddWarning(
                    warnings,
                    selection,
                    selection.IsCurtainWall
                        ? "не найдены наружные грани левого и правого крайних импостов"
                        : "не найдены левая и правая габаритные грани элемента");
            }

            if (!hasHeightReferences)
            {
                AddWarning(
                    warnings,
                    selection,
                    selection.IsCurtainWall
                        ? "не найдены наружные грани нижнего и верхнего крайних импостов"
                        : "не найдены нижняя и верхняя габаритные грани элемента");
            }

            if (!hasWidthReferences && !hasHeightReferences)
            {
                return 0;
            }

            int created = 0;
            double horizontalDetailedOffset = ToFeet(
                settings.DetailedDimensionOffsetPaperMm * Math.Max(1, view.Scale));
            double horizontalOverallOffset = ToFeet(
                settings.OverallDimensionOffsetPaperMm * Math.Max(1, view.Scale));
            double verticalDetailedOffset = ToFeet(
                settings.SideDetailedDimensionOffsetPaperMm * Math.Max(1, view.Scale));
            double verticalOverallOffset = ToFeet(
                settings.SideOverallDimensionOffsetPaperMm * Math.Max(1, view.Scale));
            Transform viewInverse = view.CropBox.Transform.Inverse;
            double bottomContour = hasHeightReferences
                ? bottom.Position
                : GetHeightCoordinate(bounds, viewInverse, bounds.MinHeight);
            double topContour = hasHeightReferences
                ? top.Position
                : GetHeightCoordinate(bounds, viewInverse, bounds.MaxHeight);
            double leftContour = hasWidthReferences
                ? left.Position
                : GetWidthCoordinate(bounds, viewInverse, bounds.MinWidth);
            double rightContour = hasWidthReferences
                ? right.Position
                : GetWidthCoordinate(bounds, viewInverse, bounds.MaxWidth);
            DimensionType dimensionType = ResolveDimensionType(document, settings);
            int horizontalChainCreated = 0;
            int verticalChainCreated = 0;
            List<DimensionReferenceCandidate> widthChain = GetUniqueCandidates(
                MergeCandidates(left, verticalGridReferences, right));
            List<DimensionReferenceCandidate> heightChain = GetUniqueCandidates(
                MergeCandidates(bottom, horizontalGridReferences, top));

            if (selection.IsCurtainWall && hasWidthReferences && widthChain.Count > 2)
            {
                horizontalChainCreated = TryCreateDimension(
                    document,
                    view,
                    widthChain,
                    false,
                    horizontalDetailedOffset,
                    settings.HorizontalDimensionSide == DoorWindowHorizontalDimensionSide.Top
                        ? topContour
                        : bottomContour,
                    settings.HorizontalDimensionSide == DoorWindowHorizontalDimensionSide.Top,
                    dimensionType,
                    selection,
                    "цепочку шагов вертикальных импостов",
                    warnings);
            }

            if (selection.IsCurtainWall && hasHeightReferences && heightChain.Count > 2)
            {
                verticalChainCreated = TryCreateDimension(
                    document,
                    view,
                    heightChain,
                    true,
                    verticalDetailedOffset,
                    settings.VerticalDimensionSide == DoorWindowVerticalDimensionSide.Right
                        ? rightContour
                        : leftContour,
                    settings.VerticalDimensionSide == DoorWindowVerticalDimensionSide.Right,
                    dimensionType,
                    selection,
                    "цепочку шагов горизонтальных импостов",
                    warnings);
            }

            created += horizontalChainCreated + verticalChainCreated;

            if (hasWidthReferences)
            {
                created += TryCreateDimension(
                    document,
                    view,
                    new List<DimensionReferenceCandidate> { left, right },
                    false,
                    horizontalChainCreated > 0 ? horizontalOverallOffset : horizontalDetailedOffset,
                    settings.HorizontalDimensionSide == DoorWindowHorizontalDimensionSide.Top
                        ? topContour
                        : bottomContour,
                    settings.HorizontalDimensionSide == DoorWindowHorizontalDimensionSide.Top,
                    dimensionType,
                    selection,
                    "общий размер ширины",
                    warnings);
            }

            if (hasHeightReferences)
            {
                created += TryCreateDimension(
                    document,
                    view,
                    new List<DimensionReferenceCandidate> { bottom, top },
                    true,
                    verticalChainCreated > 0 ? verticalOverallOffset : verticalDetailedOffset,
                    settings.VerticalDimensionSide == DoorWindowVerticalDimensionSide.Right
                        ? rightContour
                        : leftContour,
                    settings.VerticalDimensionSide == DoorWindowVerticalDimensionSide.Right,
                    dimensionType,
                    selection,
                    "общий размер высоты",
                    warnings);
            }
            return created;
        }

        private double GetHeightCoordinate(
            DoorWindowOrientedBounds bounds,
            Transform viewInverse,
            double height)
        {
            XYZ point = bounds.Origin + XYZ.BasisZ * height;
            return viewInverse.OfPoint(point).Y;
        }

        private double GetWidthCoordinate(
            DoorWindowOrientedBounds bounds,
            Transform viewInverse,
            double width)
        {
            XYZ point = bounds.Origin + bounds.WidthDirection * width;
            return viewInverse.OfPoint(point).X;
        }

        private CurtainWallDimensionReferences CollectCurtainWallReferences(
            DoorWindowSelectionData selection,
            ViewSection view,
            IList<string> warnings)
        {
            CurtainWallDimensionReferences result = new CurtainWallDimensionReferences();
            Wall wall = selection.Element as Wall;
            CurtainGrid curtainGrid = wall != null ? wall.CurtainGrid : null;
            if (curtainGrid == null)
            {
                return result;
            }

            List<DimensionReferenceCandidate> verticalGridReferences = new List<DimensionReferenceCandidate>();
            List<DimensionReferenceCandidate> horizontalGridReferences = new List<DimensionReferenceCandidate>();
            CollectCurtainGridReferences(
                selection,
                view,
                verticalGridReferences,
                horizontalGridReferences);

            List<MullionAxisGroup> verticalGroups = new List<MullionAxisGroup>();
            List<MullionAxisGroup> horizontalGroups = new List<MullionAxisGroup>();
            Transform inverse = view.CropBox.Transform.Inverse;
            ICollection<ElementId> mullionIds = curtainGrid.GetMullionIds();
            foreach (ElementId mullionId in mullionIds)
            {
                Mullion mullion = selection.SourceDocument.GetElement(mullionId) as Mullion;
                Curve locationCurve = mullion != null ? mullion.LocationCurve : null;
                if (locationCurve == null || !locationCurve.IsBound)
                {
                    continue;
                }

                XYZ localStart = inverse.OfPoint(locationCurve.GetEndPoint(0));
                XYZ localEnd = inverse.OfPoint(locationCurve.GetEndPoint(1));
                XYZ localDirection = localEnd - localStart;
                if (localDirection.GetLength() < 1e-9)
                {
                    continue;
                }

                bool isVertical = Math.Abs(localDirection.Y) >= Math.Abs(localDirection.X);
                double axisPosition = isVertical
                    ? (localStart.X + localEnd.X) / 2.0
                    : (localStart.Y + localEnd.Y) / 2.0;
                List<DimensionReferenceCandidate> widthFaces = new List<DimensionReferenceCandidate>();
                List<DimensionReferenceCandidate> heightFaces = new List<DimensionReferenceCandidate>();
                CollectElementFaceReferences(mullion, view, widthFaces, heightFaces);
                DimensionReferenceCandidate minimumFace = GetExtreme(
                    isVertical ? widthFaces : heightFaces,
                    true);
                DimensionReferenceCandidate maximumFace = GetExtreme(
                    isVertical ? widthFaces : heightFaces,
                    false);
                MullionAxisGroup group = FindOrCreateAxisGroup(
                    isVertical ? verticalGroups : horizontalGroups,
                    axisPosition);
                DimensionReferenceCandidate mullionAxis = locationCurve.Reference != null
                    ? new DimensionReferenceCandidate(locationCurve.Reference, axisPosition, 1)
                    : null;
                group.Add(minimumFace, maximumFace, mullionAxis);
            }

            verticalGroups = verticalGroups.OrderBy(group => group.Position).ToList();
            horizontalGroups = horizontalGroups.OrderBy(group => group.Position).ToList();
            if (verticalGroups.Count >= 2)
            {
                result.ExpectedVerticalInternalCenters = Math.Max(0, verticalGroups.Count - 2);
                result.LeftOuterFace = verticalGroups[0].MinimumOuterFace;
                result.RightOuterFace = verticalGroups[verticalGroups.Count - 1].MaximumOuterFace;
                for (int index = 1; index < verticalGroups.Count - 1; index++)
                {
                    DimensionReferenceCandidate center = BuildCenterCandidate(
                        verticalGroups[index],
                        verticalGridReferences);
                    if (center != null)
                    {
                        result.VerticalInternalCenters.Add(center);
                    }
                }
            }

            if (horizontalGroups.Count >= 2)
            {
                result.ExpectedHorizontalInternalCenters = Math.Max(0, horizontalGroups.Count - 2);
                result.BottomOuterFace = horizontalGroups[0].MinimumOuterFace;
                result.TopOuterFace = horizontalGroups[horizontalGroups.Count - 1].MaximumOuterFace;
                for (int index = 1; index < horizontalGroups.Count - 1; index++)
                {
                    DimensionReferenceCandidate center = BuildCenterCandidate(
                        horizontalGroups[index],
                        horizontalGridReferences);
                    if (center != null)
                    {
                        result.HorizontalInternalCenters.Add(center);
                    }
                }
            }

            if (verticalGroups.Count < 2 || horizontalGroups.Count < 2)
            {
                AddWarning(
                    warnings,
                    selection,
                    "недостаточно крайних импостов: вертикальных осей " + verticalGroups.Count +
                    ", горизонтальных осей " + horizontalGroups.Count);
            }

            if (result.VerticalInternalCenters.Count < result.ExpectedVerticalInternalCenters)
            {
                AddWarning(
                    warnings,
                    selection,
                    "часть вертикальных импостов не имеет доступной геометрической ссылки линии сетки: найдено " +
                    result.VerticalInternalCenters.Count + " из " + result.ExpectedVerticalInternalCenters);
            }

            if (result.HorizontalInternalCenters.Count < result.ExpectedHorizontalInternalCenters)
            {
                AddWarning(
                    warnings,
                    selection,
                    "часть горизонтальных импостов не имеет доступной геометрической ссылки линии сетки: найдено " +
                    result.HorizontalInternalCenters.Count + " из " + result.ExpectedHorizontalInternalCenters);
            }

            return result;
        }

        private MullionAxisGroup FindOrCreateAxisGroup(
            IList<MullionAxisGroup> groups,
            double position)
        {
            for (int index = 0; index < groups.Count; index++)
            {
                if (Math.Abs(groups[index].Position - position) <= PositionToleranceFeet)
                {
                    return groups[index];
                }
            }

            MullionAxisGroup group = new MullionAxisGroup(position);
            groups.Add(group);
            return group;
        }

        private DimensionReferenceCandidate BuildCenterCandidate(
            MullionAxisGroup group,
            IList<DimensionReferenceCandidate> gridReferences)
        {
            if (group == null)
            {
                return null;
            }

            double maximumDistance = ToFeet(10.0);
            DimensionReferenceCandidate nearest = group.CenterReference;
            double nearestDistance = nearest != null ? 0.0 : double.MaxValue;
            if (gridReferences != null)
            {
                for (int index = 0; index < gridReferences.Count; index++)
                {
                    double distance = Math.Abs(gridReferences[index].Position - group.Position);
                    if (distance > maximumDistance)
                    {
                        continue;
                    }

                    if (nearest == null ||
                        gridReferences[index].Priority < nearest.Priority ||
                        (gridReferences[index].Priority == nearest.Priority && distance < nearestDistance))
                    {
                        nearest = gridReferences[index];
                        nearestDistance = distance;
                    }
                }
            }

            return nearest;
        }

        private void CollectElementFaceReferences(
            Element element,
            ViewSection view,
            IList<DimensionReferenceCandidate> widthCandidates,
            IList<DimensionReferenceCandidate> heightCandidates)
        {
            if (element == null)
            {
                return;
            }

            Options options = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine
            };
            GeometryElement geometry = null;
            try
            {
                geometry = element.get_Geometry(options);
            }
            catch
            {
                return;
            }

            CollectPlanarFaces(
                geometry,
                Transform.Identity,
                view,
                widthCandidates,
                heightCandidates);
        }

        private void CollectBoundaryFaceReferences(
            DoorWindowSelectionData selection,
            ViewSection view,
            IList<DimensionReferenceCandidate> widthCandidates,
            IList<DimensionReferenceCandidate> heightCandidates)
        {
            List<Element> elements = new List<Element> { selection.Element };

            Options options = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            for (int index = 0; index < elements.Count; index++)
            {
                GeometryElement geometry = null;
                try
                {
                    geometry = elements[index].get_Geometry(options);
                }
                catch
                {
                    continue;
                }

                CollectPlanarFaces(
                    geometry,
                    Transform.Identity,
                    view,
                    widthCandidates,
                    heightCandidates);
            }
        }

        private void CollectPlanarFaces(
            GeometryElement geometry,
            Transform geometryToModel,
            ViewSection view,
            IList<DimensionReferenceCandidate> widthCandidates,
            IList<DimensionReferenceCandidate> heightCandidates)
        {
            if (geometry == null)
            {
                return;
            }

            Transform inverse = view.CropBox.Transform.Inverse;
            foreach (GeometryObject geometryObject in geometry)
            {
                GeometryInstance instance = geometryObject as GeometryInstance;
                if (instance != null)
                {
                    GeometryElement symbolGeometry = null;
                    try
                    {
                        symbolGeometry = instance.GetSymbolGeometry();
                    }
                    catch
                    {
                        symbolGeometry = null;
                    }

                    if (symbolGeometry != null)
                    {
                        CollectPlanarFaces(
                            symbolGeometry,
                            geometryToModel.Multiply(instance.Transform),
                            view,
                            widthCandidates,
                            heightCandidates);
                    }
                    continue;
                }

                Solid solid = geometryObject as Solid;
                if (solid == null || solid.Faces == null || solid.Faces.Size == 0)
                {
                    continue;
                }

                foreach (Face face in solid.Faces)
                {
                    PlanarFace planarFace = face as PlanarFace;
                    if (planarFace == null || planarFace.Reference == null)
                    {
                        continue;
                    }

                    XYZ normal = geometryToModel.OfVector(planarFace.FaceNormal).Normalize();
                    XYZ point = geometryToModel.OfPoint(planarFace.Origin);
                    XYZ localPoint = inverse.OfPoint(point);
                    if (Math.Abs(normal.DotProduct(view.RightDirection)) >= DirectionTolerance)
                    {
                        widthCandidates.Add(new DimensionReferenceCandidate(planarFace.Reference, localPoint.X));
                    }

                    if (Math.Abs(normal.DotProduct(view.UpDirection)) >= DirectionTolerance)
                    {
                        heightCandidates.Add(new DimensionReferenceCandidate(planarFace.Reference, localPoint.Y));
                    }
                }
            }
        }

        private void CollectCurtainGridReferences(
            DoorWindowSelectionData selection,
            ViewSection view,
            IList<DimensionReferenceCandidate> verticalGridReferences,
            IList<DimensionReferenceCandidate> horizontalGridReferences)
        {
            Wall wall = selection.Element as Wall;
            CurtainGrid curtainGrid = wall != null ? wall.CurtainGrid : null;
            if (curtainGrid == null)
            {
                return;
            }

            AddCurtainGridLines(selection.SourceDocument, curtainGrid.GetUGridLineIds(), view, verticalGridReferences, horizontalGridReferences);
            AddCurtainGridLines(selection.SourceDocument, curtainGrid.GetVGridLineIds(), view, verticalGridReferences, horizontalGridReferences);
        }

        private void AddCurtainGridLines(
            Document document,
            ICollection<ElementId> ids,
            ViewSection view,
            IList<DimensionReferenceCandidate> verticalGridReferences,
            IList<DimensionReferenceCandidate> horizontalGridReferences)
        {
            if (ids == null)
            {
                return;
            }

            Transform inverse = view.CropBox.Transform.Inverse;
            foreach (ElementId id in ids)
            {
                CurtainGridLine gridLine = document.GetElement(id) as CurtainGridLine;
                if (gridLine == null)
                {
                    continue;
                }

                bool addedReference = false;
                Curve fullCurve = null;
                try
                {
                    fullCurve = gridLine.FullCurve;
                    addedReference |= TryAddCurtainGridCurve(
                        fullCurve,
                        inverse,
                        verticalGridReferences,
                        horizontalGridReferences,
                        0);
                }
                catch
                {
                    fullCurve = null;
                }

                try
                {
                    foreach (Curve segment in gridLine.ExistingSegmentCurves)
                    {
                        addedReference |= TryAddCurtainGridCurve(
                            segment,
                            inverse,
                            verticalGridReferences,
                            horizontalGridReferences,
                            0);
                    }
                }
                catch
                {
                    // Некоторые составные линии сетки не отдают сегменты напрямую.
                }

                Options viewOptions = new Options
                {
                    ComputeReferences = true,
                    IncludeNonVisibleObjects = true,
                    View = view
                };
                addedReference |= CollectCurtainGridGeometryReferences(
                    gridLine,
                    viewOptions,
                    inverse,
                    verticalGridReferences,
                    horizontalGridReferences);

                Options modelOptions = new Options
                {
                    ComputeReferences = true,
                    IncludeNonVisibleObjects = true,
                    DetailLevel = ViewDetailLevel.Fine
                };
                addedReference |= CollectCurtainGridGeometryReferences(
                    gridLine,
                    modelOptions,
                    inverse,
                    verticalGridReferences,
                    horizontalGridReferences);

                if (!addedReference && fullCurve != null && fullCurve.IsBound)
                {
                    TryAddCurtainGridElementReference(
                        gridLine,
                        fullCurve,
                        inverse,
                        verticalGridReferences,
                        horizontalGridReferences);
                }
            }
        }

        private bool CollectCurtainGridGeometryReferences(
            CurtainGridLine gridLine,
            Options options,
            Transform inverse,
            IList<DimensionReferenceCandidate> verticalGridReferences,
            IList<DimensionReferenceCandidate> horizontalGridReferences)
        {
            GeometryElement geometry = null;
            try
            {
                geometry = gridLine.get_Geometry(options);
            }
            catch
            {
                geometry = null;
            }

            return CollectCurtainGridGeometryReferences(
                geometry,
                Transform.Identity,
                inverse,
                verticalGridReferences,
                horizontalGridReferences);
        }

        private bool CollectCurtainGridGeometryReferences(
            GeometryElement geometry,
            Transform geometryToModel,
            Transform inverse,
            IList<DimensionReferenceCandidate> verticalGridReferences,
            IList<DimensionReferenceCandidate> horizontalGridReferences)
        {
            if (geometry == null)
            {
                return false;
            }

            bool addedReference = false;
            foreach (GeometryObject geometryObject in geometry)
            {
                GeometryInstance instance = geometryObject as GeometryInstance;
                if (instance != null)
                {
                    GeometryElement symbolGeometry = null;
                    try
                    {
                        symbolGeometry = instance.GetSymbolGeometry();
                    }
                    catch
                    {
                        symbolGeometry = null;
                    }

                    if (symbolGeometry != null)
                    {
                        addedReference |= CollectCurtainGridGeometryReferences(
                            symbolGeometry,
                            geometryToModel.Multiply(instance.Transform),
                            inverse,
                            verticalGridReferences,
                            horizontalGridReferences);
                    }

                    continue;
                }

                Curve curve = geometryObject as Curve;
                if (curve == null || !curve.IsBound || curve.Reference == null)
                {
                    continue;
                }

                Curve modelCurve = curve.CreateTransformed(geometryToModel);
                addedReference |= TryAddCurtainGridCurve(
                    modelCurve,
                    curve.Reference,
                    inverse,
                    verticalGridReferences,
                    horizontalGridReferences,
                    0);
            }

            return addedReference;
        }

        private bool TryAddCurtainGridCurve(
            Curve curve,
            Transform inverse,
            IList<DimensionReferenceCandidate> verticalGridReferences,
            IList<DimensionReferenceCandidate> horizontalGridReferences,
            int priority)
        {
            return TryAddCurtainGridCurve(
                curve,
                curve != null ? curve.Reference : null,
                inverse,
                verticalGridReferences,
                horizontalGridReferences,
                priority);
        }

        private bool TryAddCurtainGridCurve(
            Curve curve,
            Reference reference,
            Transform inverse,
            IList<DimensionReferenceCandidate> verticalGridReferences,
            IList<DimensionReferenceCandidate> horizontalGridReferences,
            int priority)
        {
            if (curve == null || !curve.IsBound || reference == null)
            {
                return false;
            }

            XYZ localStart = inverse.OfPoint(curve.GetEndPoint(0));
            XYZ localEnd = inverse.OfPoint(curve.GetEndPoint(1));
            XYZ localDirection = localEnd - localStart;
            if (localDirection.GetLength() < 1e-9)
            {
                return false;
            }

            bool isVertical = Math.Abs(localDirection.Y) >= Math.Abs(localDirection.X);
            double position = isVertical
                ? (localStart.X + localEnd.X) / 2.0
                : (localStart.Y + localEnd.Y) / 2.0;
            DimensionReferenceCandidate candidate = new DimensionReferenceCandidate(reference, position, priority);
            if (isVertical)
            {
                verticalGridReferences.Add(candidate);
            }
            else
            {
                horizontalGridReferences.Add(candidate);
            }

            return true;
        }

        private void TryAddCurtainGridElementReference(
            CurtainGridLine gridLine,
            Curve fullCurve,
            Transform inverse,
            IList<DimensionReferenceCandidate> verticalGridReferences,
            IList<DimensionReferenceCandidate> horizontalGridReferences)
        {
            try
            {
                TryAddCurtainGridCurve(
                    fullCurve,
                    new Reference(gridLine),
                    inverse,
                    verticalGridReferences,
                    horizontalGridReferences,
                    2);
            }
            catch
            {
                // Если ссылка на элемент не считается геометрической, останется резерв по оси импоста.
            }
        }

        private int TryCreateDimension(
            Document document,
            ViewSection view,
            IList<DimensionReferenceCandidate> candidates,
            bool vertical,
            double offset,
            double contourCoordinate,
            bool positiveSide,
            DimensionType dimensionType,
            DoorWindowSelectionData selection,
            string description,
            IList<string> warnings)
        {
            List<DimensionReferenceCandidate> unique = GetUniqueCandidates(candidates);
            if (unique.Count < 2)
            {
                if (selection.IsCurtainWall || description.StartsWith("общий", StringComparison.Ordinal))
                {
                    AddWarning(warnings, selection, "не создана " + description + ": недостаточно ссылок");
                }

                return 0;
            }

            using (SubTransaction subTransaction = new SubTransaction(document))
            {
                subTransaction.Start();
                try
                {
                    BoundingBoxXYZ crop = view.CropBox;
                    double fixedCoordinate = contourCoordinate + (positiveSide ? offset : -offset);
                    double minimum = unique.Min(item => item.Position);
                    double maximum = unique.Max(item => item.Position);
                    double localZ = 0.0;
                    XYZ start = vertical
                        ? crop.Transform.OfPoint(new XYZ(fixedCoordinate, minimum, localZ))
                        : crop.Transform.OfPoint(new XYZ(minimum, fixedCoordinate, localZ));
                    XYZ end = vertical
                        ? crop.Transform.OfPoint(new XYZ(fixedCoordinate, maximum, localZ))
                        : crop.Transform.OfPoint(new XYZ(maximum, fixedCoordinate, localZ));
                    ReferenceArray references = new ReferenceArray();
                    for (int index = 0; index < unique.Count; index++)
                    {
                        references.Append(unique[index].Reference);
                    }

                    Line line = Line.CreateBound(start, end);
                    Dimension dimension = dimensionType != null
                        ? document.Create.NewDimension(view, line, references, dimensionType)
                        : document.Create.NewDimension(view, line, references);
                    if (dimension == null)
                    {
                        subTransaction.RollBack();
                        AddWarning(warnings, selection, "не создана " + description + ": Revit вернул пустой размер");
                        return 0;
                    }

                    document.Regenerate();
                    TransactionStatus status = subTransaction.Commit();
                    if (status != TransactionStatus.Committed)
                    {
                        AddWarning(
                            warnings,
                            selection,
                            "не создана " + description +
                            ": Revit откатил размер при проверке геометрических ссылок (" +
                            DescribeReferenceTypes(unique) + ")");
                        return 0;
                    }

                    return 1;
                }
                catch (Exception exception)
                {
                    if (subTransaction.GetStatus() == TransactionStatus.Started)
                    {
                        subTransaction.RollBack();
                    }

                    AddWarning(
                        warnings,
                        selection,
                        "не создана " + description + ": " + exception.Message +
                        " (" + DescribeReferenceTypes(unique) + ")");
                    return 0;
                }
            }
        }

        private string DescribeReferenceTypes(IList<DimensionReferenceCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return "ссылок нет";
            }

            string types = string.Join(
                ", ",
                candidates
                    .Where(candidate => candidate != null && candidate.Reference != null)
                    .Select(candidate => candidate.Reference.ElementReferenceType.ToString())
                    .Distinct());
            return "ссылок " + candidates.Count + "; типы: " +
                   (string.IsNullOrWhiteSpace(types) ? "не определены" : types);
        }

        private List<DimensionReferenceCandidate> MergeCandidates(
            DimensionReferenceCandidate first,
            IList<DimensionReferenceCandidate> middle,
            DimensionReferenceCandidate last)
        {
            List<DimensionReferenceCandidate> result = new List<DimensionReferenceCandidate>();
            if (first != null)
            {
                result.Add(first);
            }

            if (middle != null)
            {
                result.AddRange(middle);
            }

            if (last != null)
            {
                result.Add(last);
            }

            return result;
        }

        private List<DimensionReferenceCandidate> GetUniqueCandidates(IList<DimensionReferenceCandidate> candidates)
        {
            List<DimensionReferenceCandidate> result = new List<DimensionReferenceCandidate>();
            if (candidates == null)
            {
                return result;
            }

            foreach (DimensionReferenceCandidate candidate in candidates.Where(item => item != null).OrderBy(item => item.Position))
            {
                if (result.Count == 0 || Math.Abs(result[result.Count - 1].Position - candidate.Position) > PositionToleranceFeet)
                {
                    result.Add(candidate);
                }
            }

            return result;
        }

        private DimensionReferenceCandidate GetExtreme(IList<DimensionReferenceCandidate> candidates, bool minimum)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            return minimum
                ? candidates.OrderBy(item => item.Position).First()
                : candidates.OrderByDescending(item => item.Position).First();
        }

        private DimensionType ResolveDimensionType(Document document, DoorWindowViewSettings settings)
        {
            DimensionType selectedType = null;
            if (settings.DimensionTypeIdValue >= 0)
            {
                selectedType = document.GetElement(new ElementId(settings.DimensionTypeIdValue)) as DimensionType;
            }

            if (selectedType == null && !string.IsNullOrWhiteSpace(settings.DimensionTypeName))
            {
                selectedType = new FilteredElementCollector(document)
                    .OfClass(typeof(DimensionType))
                    .Cast<DimensionType>()
                    .FirstOrDefault(type => string.Equals(type.Name, settings.DimensionTypeName, StringComparison.CurrentCultureIgnoreCase));
            }

            if (selectedType == null || settings.DimensionTextHeightMm <= 0.0)
            {
                return selectedType;
            }

            double requestedHeight = UnitUtils.ConvertToInternalUnits(
                settings.DimensionTextHeightMm,
                UnitTypeId.Millimeters);
            Parameter sourceTextSize = selectedType.get_Parameter(BuiltInParameter.TEXT_SIZE);
            if (sourceTextSize == null || sourceTextSize.IsReadOnly)
            {
                return selectedType;
            }

            string derivedName = selectedType.Name + " · SAB " +
                                 settings.DimensionTextHeightMm.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) +
                                 " мм";
            DimensionType derivedType = new FilteredElementCollector(document)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .FirstOrDefault(type => string.Equals(type.Name, derivedName, StringComparison.CurrentCultureIgnoreCase));
            if (derivedType == null)
            {
                derivedType = selectedType.Duplicate(derivedName) as DimensionType;
            }

            Parameter derivedTextSize = derivedType != null
                ? derivedType.get_Parameter(BuiltInParameter.TEXT_SIZE)
                : null;
            if (derivedTextSize != null && !derivedTextSize.IsReadOnly)
            {
                derivedTextSize.Set(requestedHeight);
            }

            return derivedType ?? selectedType;
        }

        private void AddWarning(IList<string> warnings, DoorWindowSelectionData selection, string message)
        {
            if (warnings != null)
            {
                warnings.Add((selection != null ? selection.DisplayName.Replace("\n", " ") : "Элемент") + ": " + message + ".");
            }
        }

        private double ToFeet(double millimeters)
        {
            return UnitUtils.ConvertToInternalUnits(Math.Max(0.0, millimeters), UnitTypeId.Millimeters);
        }

        private class DimensionReferenceCandidate
        {
            public DimensionReferenceCandidate(Reference reference, double position)
                : this(reference, position, 0)
            {
            }

            public DimensionReferenceCandidate(Reference reference, double position, int priority)
            {
                Reference = reference;
                Position = position;
                Priority = priority;
            }

            public Reference Reference { get; private set; }

            public double Position { get; private set; }

            public int Priority { get; private set; }
        }

        private class CurtainWallDimensionReferences
        {
            public CurtainWallDimensionReferences()
            {
                VerticalInternalCenters = new List<DimensionReferenceCandidate>();
                HorizontalInternalCenters = new List<DimensionReferenceCandidate>();
            }

            public DimensionReferenceCandidate LeftOuterFace { get; set; }

            public DimensionReferenceCandidate RightOuterFace { get; set; }

            public DimensionReferenceCandidate BottomOuterFace { get; set; }

            public DimensionReferenceCandidate TopOuterFace { get; set; }

            public IList<DimensionReferenceCandidate> VerticalInternalCenters { get; private set; }

            public IList<DimensionReferenceCandidate> HorizontalInternalCenters { get; private set; }

            public int ExpectedVerticalInternalCenters { get; set; }

            public int ExpectedHorizontalInternalCenters { get; set; }
        }

        private class MullionAxisGroup
        {
            public MullionAxisGroup(double position)
            {
                Position = position;
            }

            public double Position { get; private set; }

            public DimensionReferenceCandidate MinimumOuterFace { get; private set; }

            public DimensionReferenceCandidate MaximumOuterFace { get; private set; }

            public DimensionReferenceCandidate CenterReference { get; private set; }

            public void Add(
                DimensionReferenceCandidate minimumOuterFace,
                DimensionReferenceCandidate maximumOuterFace,
                DimensionReferenceCandidate centerReference)
            {
                if (minimumOuterFace != null &&
                    (MinimumOuterFace == null || minimumOuterFace.Position < MinimumOuterFace.Position))
                {
                    MinimumOuterFace = minimumOuterFace;
                }

                if (maximumOuterFace != null &&
                    (MaximumOuterFace == null || maximumOuterFace.Position > MaximumOuterFace.Position))
                {
                    MaximumOuterFace = maximumOuterFace;
                }

                if (centerReference != null &&
                    (CenterReference == null || centerReference.Priority < CenterReference.Priority))
                {
                    CenterReference = centerReference;
                }
            }
        }
    }
}
