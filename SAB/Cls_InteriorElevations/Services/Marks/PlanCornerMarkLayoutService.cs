using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Marks
{
    /// <summary>
    /// Рассчитывает положение центра семейства марки с фиксированной двухсегментной
    /// выноской. Сервис не знает имен семейства и типоразмеров: связь четырех
    /// ориентаций с конкретным семейством выполняется отдельным адаптером.
    /// </summary>
    public class PlanCornerMarkLayoutService
    {
        private const double GeometryTolerance = 1e-9;

        public PlanCornerMarkLayoutResult Calculate(
            IList<ElevationLineData> elevationLines,
            XYZ viewRightDirection,
            XYZ viewUpDirection,
            int viewScale,
            PlanCornerMarkLayoutSettings settings,
            IList<string> warnings)
        {
            PlanCornerMarkLayoutResult result = new PlanCornerMarkLayoutResult();

            if (elevationLines == null || elevationLines.Count == 0)
            {
                AddWarning(warnings, "Невозможно рассчитать марки: линии контура не переданы.");
                return result;
            }

            string settingsError;
            if (!TryValidateSettings(settings, viewScale, out settingsError))
            {
                AddWarning(warnings, settingsError);
                return result;
            }

            XYZ right;
            XYZ up;
            if (!TryBuildViewBasis(viewRightDirection, viewUpDirection, out right, out up))
            {
                AddWarning(warnings, "Невозможно рассчитать марки: оси плана заданы некорректно.");
                return result;
            }

            double endpointToleranceFeet = UnitConversionUtils.MillimetersToFeet(settings.EndpointToleranceModelMm);
            List<ContourData> contours = BuildClosedContours(elevationLines, endpointToleranceFeet, warnings);
            result.ClosedContourCount = contours.Count;
            if (contours.Count == 0)
            {
                AddWarning(
                    warnings,
                    "Для марок углов не найден корректный замкнутый контур. Будет использовано резервное размещение по направлениям линий.");
            }

            double scale = viewScale;
            double horizontalFeet = UnitConversionUtils.MillimetersToFeet(settings.HorizontalShoulderPaperMm * scale);
            double diagonalProjectionFeet = UnitConversionUtils.MillimetersToFeet(settings.DiagonalProjectionPaperMm * scale);
            double clearanceFeet = UnitConversionUtils.MillimetersToFeet(settings.MinimumOriginClearancePaperMm * scale);

            for (int contourIndex = 0; contourIndex < contours.Count; contourIndex++)
            {
                ContourData contour = contours[contourIndex];
                for (int nodeIndex = 0; nodeIndex < contour.Nodes.Count; nodeIndex++)
                {
                    ContourNode node = contour.Nodes[nodeIndex];
                    PlanCornerMarkLayoutItem placement;
                    if (TryChoosePlacement(
                        contour.Nodes,
                        node,
                        right,
                        up,
                        horizontalFeet,
                        diagonalProjectionFeet,
                        clearanceFeet,
                        settings.RequireWholeLeaderInsideRoom,
                        out placement))
                    {
                        result.Placements.Add(placement);
                    }
                }
            }

            // Даже поврежденный или незамкнутый набор линий не должен оставлять
            // пропуски в марках. Для неохваченных углов строим резервную позицию
            // по внутренним нормалям линий, а при их отсутствии — к центру точек.
            AddLooseFallbackPlacements(
                elevationLines,
                result,
                right,
                up,
                horizontalFeet,
                diagonalProjectionFeet,
                endpointToleranceFeet,
                warnings);

            return result;
        }

        private void AddLooseFallbackPlacements(
            IList<ElevationLineData> elevationLines,
            PlanCornerMarkLayoutResult result,
            XYZ right,
            XYZ up,
            double horizontalFeet,
            double diagonalProjectionFeet,
            double pointToleranceFeet,
            IList<string> warnings)
        {
            List<LooseCornerSeed> seeds = new List<LooseCornerSeed>();
            XYZ pointsSum = XYZ.Zero;
            int pointCount = 0;

            for (int lineIndex = 0; lineIndex < elevationLines.Count; lineIndex++)
            {
                ElevationLineData line = elevationLines[lineIndex];
                if (line == null)
                {
                    continue;
                }

                XYZ insideNormal = FlattenAndNormalize(line.InsideNormal);
                if (line.StartPoint != null)
                {
                    AddLooseCornerSeed(
                        seeds,
                        line.StartPoint,
                        line.Index,
                        insideNormal,
                        pointToleranceFeet);
                    pointsSum += line.StartPoint;
                    pointCount++;
                }

                if (line.EndPoint != null)
                {
                    AddLooseCornerSeed(
                        seeds,
                        line.EndPoint,
                        line.EndIndex > 0 ? line.EndIndex : line.Index + 1,
                        insideNormal,
                        pointToleranceFeet);
                    pointsSum += line.EndPoint;
                    pointCount++;
                }
            }

            if (seeds.Count == 0)
            {
                return;
            }

            XYZ pointsCenter = pointCount > 0
                ? pointsSum / pointCount
                : seeds[0].Point;
            double nominalReach = Math.Sqrt(
                Math.Pow(horizontalFeet + diagonalProjectionFeet, 2.0) +
                Math.Pow(diagonalProjectionFeet, 2.0));
            int addedFallbackCount = 0;

            for (int seedIndex = 0; seedIndex < seeds.Count; seedIndex++)
            {
                LooseCornerSeed seed = seeds[seedIndex];
                if (IsCornerAlreadyPlaced(result.Placements, seed, pointToleranceFeet))
                {
                    continue;
                }

                XYZ direction = FlattenAndNormalize(seed.InsideDirectionSum);
                if (direction.GetLength() <= GeometryTolerance)
                {
                    direction = FlattenAndNormalize(pointsCenter - seed.Point);
                }

                if (direction.GetLength() <= GeometryTolerance)
                {
                    direction = FlattenAndNormalize(right + up);
                }

                XYZ originPoint = seed.Point + direction * nominalReach;
                PlanCornerMarkOrientation orientation;
                XYZ elbowPoint;
                XYZ tipPoint;
                double tipDeviation;
                FindClosestOrientation(
                    originPoint,
                    seed.Point,
                    right,
                    up,
                    horizontalFeet,
                    diagonalProjectionFeet,
                    out orientation,
                    out elbowPoint,
                    out tipPoint,
                    out tipDeviation);

                PlanCornerMarkLayoutItem fallback = new PlanCornerMarkLayoutItem();
                fallback.CornerNumber = seed.CornerNumber;
                fallback.CornerPoint = seed.Point;
                fallback.FamilyOriginPoint = originPoint;
                fallback.LeaderElbowPoint = elbowPoint;
                fallback.LeaderTipPoint = tipPoint;
                fallback.Orientation = orientation;
                fallback.OriginClearanceFeet = 0.0;
                fallback.IsFallback = true;
                fallback.FallbackReason =
                    "Контур не удалось восстановить; использовано направление внутренних нормалей линий.";
                fallback.TipDeviationFeet = tipDeviation;
                result.Placements.Add(fallback);
                addedFallbackCount++;
            }

            if (addedFallbackCount > 0)
            {
                AddWarning(
                    warnings,
                    "Резервно размещено марок из-за незамкнутого или поврежденного контура: " +
                    addedFallbackCount + ". Марки не пропущены.");
            }
        }

        private void AddLooseCornerSeed(
            IList<LooseCornerSeed> seeds,
            XYZ point,
            int cornerNumber,
            XYZ insideDirection,
            double toleranceFeet)
        {
            for (int index = 0; index < seeds.Count; index++)
            {
                LooseCornerSeed seed = seeds[index];
                if (HorizontalDistance(seed.Point, point) > toleranceFeet)
                {
                    continue;
                }

                if (cornerNumber > 0 && (seed.CornerNumber <= 0 || cornerNumber < seed.CornerNumber))
                {
                    seed.CornerNumber = cornerNumber;
                }

                seed.InsideDirectionSum += insideDirection;
                return;
            }

            LooseCornerSeed createdSeed = new LooseCornerSeed();
            createdSeed.Point = point;
            createdSeed.CornerNumber = cornerNumber;
            createdSeed.InsideDirectionSum = insideDirection;
            seeds.Add(createdSeed);
        }

        private bool IsCornerAlreadyPlaced(
            IList<PlanCornerMarkLayoutItem> placements,
            LooseCornerSeed seed,
            double toleranceFeet)
        {
            for (int index = 0; index < placements.Count; index++)
            {
                PlanCornerMarkLayoutItem placement = placements[index];
                if (placement == null)
                {
                    continue;
                }

                if (placement.CornerNumber == seed.CornerNumber ||
                    HorizontalDistance(placement.CornerPoint, seed.Point) <= toleranceFeet)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryChoosePlacement(
            IList<ContourNode> polygonNodes,
            ContourNode cornerNode,
            XYZ right,
            XYZ up,
            double horizontalFeet,
            double diagonalProjectionFeet,
            double requiredClearanceFeet,
            bool requireWholeLeaderInside,
            out PlanCornerMarkLayoutItem bestPlacement)
        {
            bestPlacement = null;
            double bestClearance = double.MinValue;
            PlanCornerMarkLayoutItem bestOriginInsidePlacement = null;
            double bestOriginInsideClearance = double.MinValue;

            PlanCornerMarkOrientation[] orientations =
            {
                PlanCornerMarkOrientation.TipUpperRight,
                PlanCornerMarkOrientation.TipLowerRight,
                PlanCornerMarkOrientation.TipUpperLeft,
                PlanCornerMarkOrientation.TipLowerLeft
            };

            for (int index = 0; index < orientations.Length; index++)
            {
                PlanCornerMarkOrientation orientation = orientations[index];
                double horizontalSign = IsTipRight(orientation) ? 1.0 : -1.0;
                double verticalSign = IsTipUpper(orientation) ? 1.0 : -1.0;

                XYZ elbowOffset = right * (horizontalSign * horizontalFeet);
                XYZ diagonalOffset =
                    right * (horizontalSign * diagonalProjectionFeet) +
                    up * (verticalSign * diagonalProjectionFeet);

                // Наконечник семейства должен точно совпасть с геометрическим углом.
                XYZ originPoint = cornerNode.Point - elbowOffset - diagonalOffset;
                XYZ elbowPoint = originPoint + elbowOffset;

                if (!IsPointStrictlyInsidePolygon(originPoint, polygonNodes))
                {
                    continue;
                }

                double actualClearance = GetMinimumDistanceToPolygon(originPoint, polygonNodes);
                PlanCornerMarkLayoutItem candidate = new PlanCornerMarkLayoutItem();
                candidate.CornerNumber = cornerNode.CornerNumber;
                candidate.CornerPoint = cornerNode.Point;
                candidate.FamilyOriginPoint = originPoint;
                candidate.LeaderElbowPoint = elbowPoint;
                candidate.LeaderTipPoint = cornerNode.Point;
                candidate.Orientation = orientation;
                candidate.OriginClearanceFeet = actualClearance;
                candidate.IsFallback = false;
                candidate.FallbackReason = string.Empty;
                candidate.TipDeviationFeet = 0.0;

                if (bestOriginInsidePlacement == null ||
                    actualClearance > bestOriginInsideClearance + GeometryTolerance)
                {
                    bestOriginInsidePlacement = candidate;
                    bestOriginInsideClearance = actualClearance;
                }

                bool hasRequiredClearance = actualClearance + GeometryTolerance >= requiredClearanceFeet;
                bool leaderIsInside = !requireWholeLeaderInside ||
                    (IsSegmentInsidePolygon(originPoint, elbowPoint, polygonNodes, false) &&
                     IsSegmentInsidePolygon(elbowPoint, cornerNode.Point, polygonNodes, true));
                if (!hasRequiredClearance || !leaderIsInside)
                {
                    continue;
                }

                if (bestPlacement != null && actualClearance <= bestClearance + GeometryTolerance)
                {
                    continue;
                }

                bestPlacement = candidate;
                bestClearance = actualClearance;
            }

            if (bestPlacement != null)
            {
                return true;
            }

            if (bestOriginInsidePlacement != null)
            {
                bestOriginInsidePlacement.IsFallback = true;
                bestOriginInsidePlacement.FallbackReason =
                    "Центр марки находится внутри помещения; требование к запасу или всей выноске ослаблено.";
                bestPlacement = bestOriginInsidePlacement;
                return true;
            }

            bestPlacement = BuildAdaptiveFallbackPlacement(
                polygonNodes,
                cornerNode,
                right,
                up,
                horizontalFeet,
                diagonalProjectionFeet);
            return bestPlacement != null;
        }

        private PlanCornerMarkLayoutItem BuildAdaptiveFallbackPlacement(
            IList<ContourNode> polygonNodes,
            ContourNode cornerNode,
            XYZ right,
            XYZ up,
            double horizontalFeet,
            double diagonalProjectionFeet)
        {
            double nominalReach = Math.Sqrt(
                Math.Pow(horizontalFeet + diagonalProjectionFeet, 2.0) +
                Math.Pow(diagonalProjectionFeet, 2.0));
            nominalReach = Math.Max(nominalReach, GeometryTolerance * 100.0);

            XYZ bestPoint = null;
            PlanCornerMarkOrientation bestOrientation = PlanCornerMarkOrientation.TipUpperRight;
            double bestScore = double.MaxValue;
            double bestClearance = 0.0;

            // Ищем внутри локальной окрестности угла. Угловой шаг 5 градусов
            // позволяет работать и с острыми, и с вогнутыми углами.
            const int directionCount = 72;
            const int radialStepCount = 24;
            for (int radialIndex = 1; radialIndex <= radialStepCount; radialIndex++)
            {
                double radius = nominalReach * radialIndex / radialStepCount;
                for (int directionIndex = 0; directionIndex < directionCount; directionIndex++)
                {
                    double angle = 2.0 * Math.PI * directionIndex / directionCount;
                    XYZ direction = right * Math.Cos(angle) + up * Math.Sin(angle);
                    XYZ candidatePoint = cornerNode.Point + direction * radius;
                    if (!IsPointStrictlyInsidePolygon(candidatePoint, polygonNodes))
                    {
                        continue;
                    }

                    double clearance = GetMinimumDistanceToPolygon(candidatePoint, polygonNodes);
                    PlanCornerMarkOrientation orientation;
                    XYZ elbowPoint;
                    XYZ tipPoint;
                    double tipDeviation;
                    FindClosestOrientation(
                        candidatePoint,
                        cornerNode.Point,
                        right,
                        up,
                        horizontalFeet,
                        diagonalProjectionFeet,
                        out orientation,
                        out elbowPoint,
                        out tipPoint,
                        out tipDeviation);

                    // Главная цель резерва — не потерять марку и оставить ее центр
                    // внутри. Далее минимизируем ошибку наконечника, а при равенстве
                    // предпочитаем точку с большим запасом до границы.
                    double score = tipDeviation - Math.Min(clearance, nominalReach) * 0.05;
                    if (score >= bestScore - GeometryTolerance)
                    {
                        continue;
                    }

                    bestScore = score;
                    bestPoint = candidatePoint;
                    bestOrientation = orientation;
                    bestClearance = clearance;
                }
            }

            if (bestPoint == null)
            {
                bestPoint = FindAnyInteriorPoint(polygonNodes);
                if (bestPoint == null)
                {
                    return null;
                }
            }

            PlanCornerMarkOrientation finalOrientation;
            XYZ finalElbowPoint;
            XYZ finalTipPoint;
            double finalTipDeviation;
            FindClosestOrientation(
                bestPoint,
                cornerNode.Point,
                right,
                up,
                horizontalFeet,
                diagonalProjectionFeet,
                out finalOrientation,
                out finalElbowPoint,
                out finalTipPoint,
                out finalTipDeviation);

            PlanCornerMarkLayoutItem fallback = new PlanCornerMarkLayoutItem();
            fallback.CornerNumber = cornerNode.CornerNumber;
            fallback.CornerPoint = cornerNode.Point;
            fallback.FamilyOriginPoint = bestPoint;
            fallback.LeaderElbowPoint = finalElbowPoint;
            fallback.LeaderTipPoint = finalTipPoint;
            fallback.Orientation = finalOrientation;
            fallback.OriginClearanceFeet = bestClearance > 0.0
                ? bestClearance
                : GetMinimumDistanceToPolygon(bestPoint, polygonNodes);
            fallback.IsFallback = true;
            fallback.FallbackReason =
                "Номинальная выноска не помещается. Центр марки адаптивно смещен внутрь помещения.";
            fallback.TipDeviationFeet = finalTipDeviation;
            return fallback;
        }

        private void FindClosestOrientation(
            XYZ originPoint,
            XYZ targetCornerPoint,
            XYZ right,
            XYZ up,
            double horizontalFeet,
            double diagonalProjectionFeet,
            out PlanCornerMarkOrientation bestOrientation,
            out XYZ bestElbowPoint,
            out XYZ bestTipPoint,
            out double bestTipDeviation)
        {
            PlanCornerMarkOrientation[] orientations =
            {
                PlanCornerMarkOrientation.TipUpperRight,
                PlanCornerMarkOrientation.TipLowerRight,
                PlanCornerMarkOrientation.TipUpperLeft,
                PlanCornerMarkOrientation.TipLowerLeft
            };

            bestOrientation = orientations[0];
            bestElbowPoint = originPoint;
            bestTipPoint = originPoint;
            bestTipDeviation = double.MaxValue;

            for (int index = 0; index < orientations.Length; index++)
            {
                PlanCornerMarkOrientation orientation = orientations[index];
                double horizontalSign = IsTipRight(orientation) ? 1.0 : -1.0;
                double verticalSign = IsTipUpper(orientation) ? 1.0 : -1.0;
                XYZ elbowPoint = originPoint + right * (horizontalSign * horizontalFeet);
                XYZ tipPoint = elbowPoint +
                    right * (horizontalSign * diagonalProjectionFeet) +
                    up * (verticalSign * diagonalProjectionFeet);
                double tipDeviation = HorizontalDistance(tipPoint, targetCornerPoint);
                if (tipDeviation >= bestTipDeviation - GeometryTolerance)
                {
                    continue;
                }

                bestOrientation = orientation;
                bestElbowPoint = elbowPoint;
                bestTipPoint = tipPoint;
                bestTipDeviation = tipDeviation;
            }
        }

        private XYZ FindAnyInteriorPoint(IList<ContourNode> polygonNodes)
        {
            if (polygonNodes == null || polygonNodes.Count < 3)
            {
                return null;
            }

            double minimumY = double.MaxValue;
            double maximumY = double.MinValue;
            for (int index = 0; index < polygonNodes.Count; index++)
            {
                minimumY = Math.Min(minimumY, polygonNodes[index].Point.Y);
                maximumY = Math.Max(maximumY, polygonNodes[index].Point.Y);
            }

            XYZ bestPoint = null;
            double bestHalfWidth = double.MinValue;
            const int scanlineCount = 41;
            for (int scanIndex = 1; scanIndex < scanlineCount; scanIndex++)
            {
                double y = minimumY + (maximumY - minimumY) * scanIndex / scanlineCount;
                List<double> intersections = new List<double>();
                for (int edgeIndex = 0; edgeIndex < polygonNodes.Count; edgeIndex++)
                {
                    XYZ first = polygonNodes[edgeIndex].Point;
                    XYZ second = polygonNodes[(edgeIndex + 1) % polygonNodes.Count].Point;
                    if ((first.Y > y) == (second.Y > y))
                    {
                        continue;
                    }

                    double x = first.X + (y - first.Y) * (second.X - first.X) / (second.Y - first.Y);
                    intersections.Add(x);
                }

                intersections.Sort();
                for (int pairIndex = 0; pairIndex + 1 < intersections.Count; pairIndex += 2)
                {
                    double firstX = intersections[pairIndex];
                    double secondX = intersections[pairIndex + 1];
                    double halfWidth = (secondX - firstX) / 2.0;
                    if (halfWidth <= bestHalfWidth)
                    {
                        continue;
                    }

                    XYZ candidate = new XYZ((firstX + secondX) / 2.0, y, polygonNodes[0].Point.Z);
                    if (!IsPointStrictlyInsidePolygon(candidate, polygonNodes))
                    {
                        continue;
                    }

                    bestPoint = candidate;
                    bestHalfWidth = halfWidth;
                }
            }

            return bestPoint;
        }

        private List<ContourData> BuildClosedContours(
            IList<ElevationLineData> elevationLines,
            double endpointToleranceFeet,
            IList<string> warnings)
        {
            List<GraphNode> graphNodes = new List<GraphNode>();
            List<GraphEdge> graphEdges = new List<GraphEdge>();

            for (int lineIndex = 0; lineIndex < elevationLines.Count; lineIndex++)
            {
                ElevationLineData line = elevationLines[lineIndex];
                if (line == null || line.StartPoint == null || line.EndPoint == null)
                {
                    continue;
                }

                if (HorizontalDistance(line.StartPoint, line.EndPoint) <= GeometryTolerance)
                {
                    continue;
                }

                int startNodeIndex = FindOrCreateNode(
                    graphNodes,
                    line.StartPoint,
                    line.Index,
                    endpointToleranceFeet,
                    warnings);
                int endNodeIndex = FindOrCreateNode(
                    graphNodes,
                    line.EndPoint,
                    line.EndIndex > 0 ? line.EndIndex : line.Index + 1,
                    endpointToleranceFeet,
                    warnings);

                if (startNodeIndex == endNodeIndex)
                {
                    continue;
                }

                GraphEdge edge = new GraphEdge();
                edge.FirstNodeIndex = startNodeIndex;
                edge.SecondNodeIndex = endNodeIndex;
                edge.IsVisited = false;
                int edgeIndex = graphEdges.Count;
                graphEdges.Add(edge);
                graphNodes[startNodeIndex].EdgeIndexes.Add(edgeIndex);
                graphNodes[endNodeIndex].EdgeIndexes.Add(edgeIndex);
            }

            List<ContourData> contours = new List<ContourData>();
            int invalidComponentCount = 0;
            for (int edgeIndex = 0; edgeIndex < graphEdges.Count; edgeIndex++)
            {
                if (graphEdges[edgeIndex].IsVisited)
                {
                    continue;
                }

                List<int> componentEdges = CollectComponentEdges(edgeIndex, graphNodes, graphEdges);
                ContourData contour;
                if (!TryBuildContour(componentEdges, graphNodes, graphEdges, out contour))
                {
                    invalidComponentCount++;
                    continue;
                }

                contours.Add(contour);
            }

            if (invalidComponentCount > 0)
            {
                AddWarning(
                    warnings,
                    "Групп линий без корректного замкнутого контура: " + invalidComponentCount +
                    ". Для их углов будет использовано резервное размещение марок.");
            }

            return contours;
        }

        private List<int> CollectComponentEdges(
            int initialEdgeIndex,
            IList<GraphNode> nodes,
            IList<GraphEdge> edges)
        {
            List<int> result = new List<int>();
            Queue<int> pendingEdges = new Queue<int>();
            pendingEdges.Enqueue(initialEdgeIndex);
            edges[initialEdgeIndex].IsVisited = true;

            while (pendingEdges.Count > 0)
            {
                int edgeIndex = pendingEdges.Dequeue();
                result.Add(edgeIndex);
                GraphEdge edge = edges[edgeIndex];
                int[] nodeIndexes = { edge.FirstNodeIndex, edge.SecondNodeIndex };

                for (int nodeOffset = 0; nodeOffset < nodeIndexes.Length; nodeOffset++)
                {
                    GraphNode node = nodes[nodeIndexes[nodeOffset]];
                    for (int adjacentIndex = 0; adjacentIndex < node.EdgeIndexes.Count; adjacentIndex++)
                    {
                        int adjacentEdgeIndex = node.EdgeIndexes[adjacentIndex];
                        if (edges[adjacentEdgeIndex].IsVisited)
                        {
                            continue;
                        }

                        edges[adjacentEdgeIndex].IsVisited = true;
                        pendingEdges.Enqueue(adjacentEdgeIndex);
                    }
                }
            }

            return result;
        }

        private bool TryBuildContour(
            IList<int> componentEdgeIndexes,
            IList<GraphNode> graphNodes,
            IList<GraphEdge> graphEdges,
            out ContourData contour)
        {
            contour = null;
            if (componentEdgeIndexes == null || componentEdgeIndexes.Count < 3)
            {
                return false;
            }

            HashSet<int> componentNodeIndexes = new HashSet<int>();
            for (int index = 0; index < componentEdgeIndexes.Count; index++)
            {
                GraphEdge edge = graphEdges[componentEdgeIndexes[index]];
                componentNodeIndexes.Add(edge.FirstNodeIndex);
                componentNodeIndexes.Add(edge.SecondNodeIndex);
            }

            if (componentNodeIndexes.Count != componentEdgeIndexes.Count)
            {
                return false;
            }

            foreach (int nodeIndex in componentNodeIndexes)
            {
                GraphNode node = graphNodes[nodeIndex];
                int componentDegree = 0;
                for (int edgeOffset = 0; edgeOffset < node.EdgeIndexes.Count; edgeOffset++)
                {
                    if (Contains(componentEdgeIndexes, node.EdgeIndexes[edgeOffset]))
                    {
                        componentDegree++;
                    }
                }

                if (componentDegree != 2)
                {
                    return false;
                }
            }

            int startNodeIndex = GetNodeWithSmallestCornerNumber(componentNodeIndexes, graphNodes);
            int currentNodeIndex = startNodeIndex;
            int previousEdgeIndex = -1;
            List<ContourNode> orderedNodes = new List<ContourNode>();

            do
            {
                GraphNode currentNode = graphNodes[currentNodeIndex];
                ContourNode contourNode = new ContourNode();
                contourNode.Point = currentNode.Point;
                contourNode.CornerNumber = currentNode.CornerNumber;
                orderedNodes.Add(contourNode);

                int nextEdgeIndex = FindNextComponentEdge(
                    currentNode,
                    previousEdgeIndex,
                    componentEdgeIndexes);
                if (nextEdgeIndex < 0)
                {
                    return false;
                }

                GraphEdge nextEdge = graphEdges[nextEdgeIndex];
                int nextNodeIndex = nextEdge.FirstNodeIndex == currentNodeIndex
                    ? nextEdge.SecondNodeIndex
                    : nextEdge.FirstNodeIndex;

                previousEdgeIndex = nextEdgeIndex;
                currentNodeIndex = nextNodeIndex;

                if (orderedNodes.Count > componentNodeIndexes.Count)
                {
                    return false;
                }
            }
            while (currentNodeIndex != startNodeIndex);

            if (orderedNodes.Count != componentNodeIndexes.Count || Math.Abs(GetSignedArea(orderedNodes)) <= GeometryTolerance)
            {
                return false;
            }

            contour = new ContourData();
            contour.Nodes = orderedNodes;
            return true;
        }

        private int FindOrCreateNode(
            IList<GraphNode> nodes,
            XYZ point,
            int cornerNumber,
            double toleranceFeet,
            IList<string> warnings)
        {
            for (int index = 0; index < nodes.Count; index++)
            {
                GraphNode node = nodes[index];
                if (HorizontalDistance(node.Point, point) > toleranceFeet)
                {
                    continue;
                }

                if (cornerNumber > 0 && node.CornerNumber > 0 && node.CornerNumber != cornerNumber)
                {
                    AddWarning(
                        warnings,
                        "В одной геометрической точке найдены разные номера угла: " +
                        node.CornerNumber + " и " + cornerNumber + ". Использован меньший номер.");
                    node.CornerNumber = Math.Min(node.CornerNumber, cornerNumber);
                }
                else if (node.CornerNumber <= 0)
                {
                    node.CornerNumber = cornerNumber;
                }

                return index;
            }

            GraphNode createdNode = new GraphNode();
            createdNode.Point = point;
            createdNode.CornerNumber = cornerNumber;
            createdNode.EdgeIndexes = new List<int>();
            nodes.Add(createdNode);
            return nodes.Count - 1;
        }

        private bool IsSegmentInsidePolygon(
            XYZ start,
            XYZ end,
            IList<ContourNode> polygon,
            bool allowEndOnBoundary)
        {
            if (!IsPointInsideOrOnBoundary(start, polygon))
            {
                return false;
            }

            if (!allowEndOnBoundary && !IsPointInsideOrOnBoundary(end, polygon))
            {
                return false;
            }

            // Разбиваем отрезок во всех точках пересечения с границей. Если середина
            // каждого полученного интервала внутри полигона, даже узкий вогнутый
            // участок не сможет остаться незамеченным между редкими пробными точками.
            List<double> parameters = new List<double>();
            parameters.Add(0.0);
            parameters.Add(1.0);

            for (int edgeIndex = 0; edgeIndex < polygon.Count; edgeIndex++)
            {
                XYZ edgeStart = polygon[edgeIndex].Point;
                XYZ edgeEnd = polygon[(edgeIndex + 1) % polygon.Count].Point;
                AddSegmentIntersectionParameters(start, end, edgeStart, edgeEnd, parameters);
            }

            parameters.Sort();
            for (int index = 0; index < parameters.Count - 1; index++)
            {
                double first = parameters[index];
                double second = parameters[index + 1];
                if (second - first <= GeometryTolerance)
                {
                    continue;
                }

                double midpointParameter = (first + second) / 2.0;
                XYZ midpoint = start + (end - start) * midpointParameter;
                if (!IsPointInsideOrOnBoundary(midpoint, polygon))
                {
                    return false;
                }
            }

            return true;
        }

        private void AddSegmentIntersectionParameters(
            XYZ segmentStart,
            XYZ segmentEnd,
            XYZ edgeStart,
            XYZ edgeEnd,
            IList<double> parameters)
        {
            double segmentX = segmentEnd.X - segmentStart.X;
            double segmentY = segmentEnd.Y - segmentStart.Y;
            double edgeX = edgeEnd.X - edgeStart.X;
            double edgeY = edgeEnd.Y - edgeStart.Y;
            double offsetX = edgeStart.X - segmentStart.X;
            double offsetY = edgeStart.Y - segmentStart.Y;
            double cross = Cross2D(segmentX, segmentY, edgeX, edgeY);

            if (Math.Abs(cross) <= GeometryTolerance)
            {
                if (Math.Abs(Cross2D(offsetX, offsetY, segmentX, segmentY)) > GeometryTolerance)
                {
                    return;
                }

                double lengthSquared = segmentX * segmentX + segmentY * segmentY;
                if (lengthSquared <= GeometryTolerance)
                {
                    return;
                }

                double firstParameter = (offsetX * segmentX + offsetY * segmentY) / lengthSquared;
                double secondParameter =
                    ((edgeEnd.X - segmentStart.X) * segmentX +
                     (edgeEnd.Y - segmentStart.Y) * segmentY) /
                    lengthSquared;
                AddUniqueParameter(parameters, firstParameter);
                AddUniqueParameter(parameters, secondParameter);
                return;
            }

            double segmentParameter = Cross2D(offsetX, offsetY, edgeX, edgeY) / cross;
            double edgeParameter = Cross2D(offsetX, offsetY, segmentX, segmentY) / cross;
            if (segmentParameter < -GeometryTolerance || segmentParameter > 1.0 + GeometryTolerance ||
                edgeParameter < -GeometryTolerance || edgeParameter > 1.0 + GeometryTolerance)
            {
                return;
            }

            AddUniqueParameter(parameters, segmentParameter);
        }

        private void AddUniqueParameter(IList<double> parameters, double parameter)
        {
            double clampedParameter = Math.Max(0.0, Math.Min(1.0, parameter));
            for (int index = 0; index < parameters.Count; index++)
            {
                if (Math.Abs(parameters[index] - clampedParameter) <= GeometryTolerance)
                {
                    return;
                }
            }

            parameters.Add(clampedParameter);
        }

        private double Cross2D(double firstX, double firstY, double secondX, double secondY)
        {
            return firstX * secondY - firstY * secondX;
        }

        private bool IsPointStrictlyInsidePolygon(XYZ point, IList<ContourNode> polygon)
        {
            if (GetMinimumDistanceToPolygon(point, polygon) <= GeometryTolerance)
            {
                return false;
            }

            return IsPointInsideOrOnBoundary(point, polygon);
        }

        private bool IsPointInsideOrOnBoundary(XYZ point, IList<ContourNode> polygon)
        {
            bool isInside = false;
            for (int index = 0, previousIndex = polygon.Count - 1; index < polygon.Count; previousIndex = index++)
            {
                XYZ first = polygon[previousIndex].Point;
                XYZ second = polygon[index].Point;
                if (DistanceToSegment(point, first, second) <= GeometryTolerance)
                {
                    return true;
                }

                bool crossesRay = (first.Y > point.Y) != (second.Y > point.Y);
                if (!crossesRay)
                {
                    continue;
                }

                double intersectionX =
                    (second.X - first.X) * (point.Y - first.Y) /
                    (second.Y - first.Y) + first.X;
                if (point.X < intersectionX)
                {
                    isInside = !isInside;
                }
            }

            return isInside;
        }

        private double GetMinimumDistanceToPolygon(XYZ point, IList<ContourNode> polygon)
        {
            double minimumDistance = double.MaxValue;
            for (int index = 0; index < polygon.Count; index++)
            {
                XYZ start = polygon[index].Point;
                XYZ end = polygon[(index + 1) % polygon.Count].Point;
                minimumDistance = Math.Min(minimumDistance, DistanceToSegment(point, start, end));
            }

            return minimumDistance;
        }

        private double DistanceToSegment(XYZ point, XYZ start, XYZ end)
        {
            double deltaX = end.X - start.X;
            double deltaY = end.Y - start.Y;
            double lengthSquared = deltaX * deltaX + deltaY * deltaY;
            if (lengthSquared <= GeometryTolerance)
            {
                return HorizontalDistance(point, start);
            }

            double parameter =
                ((point.X - start.X) * deltaX + (point.Y - start.Y) * deltaY) /
                lengthSquared;
            parameter = Math.Max(0.0, Math.Min(1.0, parameter));

            double closestX = start.X + parameter * deltaX;
            double closestY = start.Y + parameter * deltaY;
            double offsetX = point.X - closestX;
            double offsetY = point.Y - closestY;
            return Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
        }

        private bool TryValidateSettings(
            PlanCornerMarkLayoutSettings settings,
            int viewScale,
            out string error)
        {
            error = string.Empty;
            if (settings == null)
            {
                error = "Невозможно рассчитать марки: настройки геометрии выноски не переданы.";
                return false;
            }

            if (viewScale <= 0)
            {
                error = "Невозможно рассчитать марки: масштаб плана должен быть больше нуля.";
                return false;
            }

            if (settings.HorizontalShoulderPaperMm < 0.0 ||
                settings.DiagonalProjectionPaperMm <= 0.0 ||
                settings.MinimumOriginClearancePaperMm < 0.0 ||
                settings.EndpointToleranceModelMm <= 0.0)
            {
                error = "Невозможно рассчитать марки: размеры выноски и допуски заданы некорректно.";
                return false;
            }

            return true;
        }

        private bool TryBuildViewBasis(
            XYZ viewRightDirection,
            XYZ viewUpDirection,
            out XYZ right,
            out XYZ up)
        {
            right = FlattenAndNormalize(viewRightDirection);
            up = FlattenAndNormalize(viewUpDirection);
            if (right.GetLength() <= GeometryTolerance || up.GetLength() <= GeometryTolerance)
            {
                return false;
            }

            // Убираем возможную численную неортогональность осей вида.
            up = up - right * up.DotProduct(right);
            if (up.GetLength() <= GeometryTolerance)
            {
                return false;
            }

            up = up.Normalize();
            return true;
        }

        private XYZ FlattenAndNormalize(XYZ vector)
        {
            if (vector == null)
            {
                return XYZ.Zero;
            }

            XYZ flattened = new XYZ(vector.X, vector.Y, 0.0);
            return flattened.GetLength() > GeometryTolerance
                ? flattened.Normalize()
                : XYZ.Zero;
        }

        private double HorizontalDistance(XYZ first, XYZ second)
        {
            double deltaX = first.X - second.X;
            double deltaY = first.Y - second.Y;
            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        private double GetSignedArea(IList<ContourNode> nodes)
        {
            double areaTwice = 0.0;
            for (int index = 0; index < nodes.Count; index++)
            {
                XYZ current = nodes[index].Point;
                XYZ next = nodes[(index + 1) % nodes.Count].Point;
                areaTwice += current.X * next.Y - next.X * current.Y;
            }

            return areaTwice / 2.0;
        }

        private bool IsTipRight(PlanCornerMarkOrientation orientation)
        {
            return orientation == PlanCornerMarkOrientation.TipUpperRight ||
                   orientation == PlanCornerMarkOrientation.TipLowerRight;
        }

        private bool IsTipUpper(PlanCornerMarkOrientation orientation)
        {
            return orientation == PlanCornerMarkOrientation.TipUpperRight ||
                   orientation == PlanCornerMarkOrientation.TipUpperLeft;
        }

        private bool Contains(IList<int> values, int value)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (values[index] == value)
                {
                    return true;
                }
            }

            return false;
        }

        private int GetNodeWithSmallestCornerNumber(
            IEnumerable<int> nodeIndexes,
            IList<GraphNode> nodes)
        {
            int selectedNodeIndex = -1;
            int selectedCornerNumber = int.MaxValue;
            foreach (int nodeIndex in nodeIndexes)
            {
                int cornerNumber = nodes[nodeIndex].CornerNumber;
                if (selectedNodeIndex < 0 || cornerNumber < selectedCornerNumber)
                {
                    selectedNodeIndex = nodeIndex;
                    selectedCornerNumber = cornerNumber;
                }
            }

            return selectedNodeIndex;
        }

        private int FindNextComponentEdge(
            GraphNode node,
            int previousEdgeIndex,
            IList<int> componentEdgeIndexes)
        {
            for (int index = 0; index < node.EdgeIndexes.Count; index++)
            {
                int edgeIndex = node.EdgeIndexes[index];
                if (edgeIndex != previousEdgeIndex && Contains(componentEdgeIndexes, edgeIndex))
                {
                    return edgeIndex;
                }
            }

            return -1;
        }

        private void AddWarning(IList<string> warnings, string text)
        {
            if (warnings != null && !string.IsNullOrWhiteSpace(text))
            {
                warnings.Add(text);
            }
        }

        private class GraphNode
        {
            public XYZ Point { get; set; }

            public int CornerNumber { get; set; }

            public List<int> EdgeIndexes { get; set; }
        }

        private class GraphEdge
        {
            public int FirstNodeIndex { get; set; }

            public int SecondNodeIndex { get; set; }

            public bool IsVisited { get; set; }
        }

        private class ContourNode
        {
            public XYZ Point { get; set; }

            public int CornerNumber { get; set; }
        }

        private class ContourData
        {
            public List<ContourNode> Nodes { get; set; }
        }

        private class LooseCornerSeed
        {
            public XYZ Point { get; set; }

            public int CornerNumber { get; set; }

            public XYZ InsideDirectionSum { get; set; }
        }
    }
}
