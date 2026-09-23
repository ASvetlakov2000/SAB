using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using SAB.GklFrame.Models;

namespace SAB.GklFrame.Services
{
    public interface ICurtainGridGenerator
    {
        GeneratedFrameData Generate(
            Document document,
            Wall calculationWall,
            SourceWallData sourceWall,
            FrameGenerationOptions options);
    }

    public sealed class CurtainGridGenerator : ICurtainGridGenerator
    {
        private readonly IFrameMetadataService _metadataService;
        private readonly IMullionGeometryService _geometryService;
        private readonly IFrameParameterWriter _parameterWriter;

        public CurtainGridGenerator(
            IFrameMetadataService metadataService,
            IMullionGeometryService geometryService,
            IFrameParameterWriter parameterWriter)
        {
            _metadataService = metadataService;
            _geometryService = geometryService;
            _parameterWriter = parameterWriter;
        }

        public GeneratedFrameData Generate(
            Document document,
            Wall calculationWall,
            SourceWallData sourceWall,
            FrameGenerationOptions options)
        {
            CurtainGrid grid = calculationWall.CurtainGrid;
            if (grid == null)
            {
                throw new InvalidOperationException("Не удалось получить CurtainGrid расчётной стены.");
            }

            FrameMullionTypeSet mullionTypes = FrameMullionTypeResolver.Resolve(
                document,
                sourceWall.CoreWidthInternal,
                sourceWall.Openings.Any(item => item.OpeningType == OpeningType.Door),
                options.MullionCatalogDocument);

            double spacing = RevitUnitService.MillimetersToInternal(options.StudSpacingMm);
            if (spacing <= 1e-7)
            {
                throw new InvalidOperationException("Шаг стоек должен быть больше нуля.");
            }

            // Grid line создаётся во всех существующих сегментах профиля стены. В выемке
            // двери сегмента нет, поэтому стойка автоматически остаётся только над дверью.
            foreach (double offset in GetStudOffsets(sourceWall, spacing))
            {
                double positionHeight = GetGridLinePositionHeight(sourceWall, offset);
                XYZ position = PointAt(sourceWall, offset, positionHeight);
                CurtainGridLine gridLine = AddVerticalGridLine(document, grid, position);
                AddMullionsToExistingSegments(gridLine, mullionTypes.Stud);
            }

            document.Regenerate();

            GeneratedFrameData result = new GeneratedFrameData
            {
                SourceWallId = sourceWall.SourceElementId,
                CalculationWallId = calculationWall.Id
            };

            HashSet<ElementId> uniqueIds = new HashSet<ElementId>(grid.GetMullionIds());
            Dictionary<ElementId, FrameMemberPurpose> purposeById = new Dictionary<ElementId, FrameMemberPurpose>();
            Dictionary<ElementId, ElementId> expectedTypeById = new Dictionary<ElementId, ElementId>();
            foreach (ElementId id in uniqueIds)
            {
                Mullion mullion = document.GetElement(id) as Mullion;
                if (mullion == null)
                {
                    continue;
                }

                WallOpeningData opening;
                FrameMemberPurpose purpose = Classify(mullion, sourceWall, out opening);
                purposeById[id] = purpose;
                MullionType expectedType = SelectMullionType(mullion, sourceWall, opening, purpose, mullionTypes);
                SetMullionType(mullion, expectedType);
                expectedTypeById[id] = expectedType.Id;
                _metadataService.MarkMullion(mullion, sourceWall, opening, purpose);
                _parameterWriter.WriteLength(mullion, _geometryService.GetLength(mullion));
                result.MullionIds.Add(id);
            }

            document.Regenerate();
            ValidateGeometry(document, sourceWall, result.MullionIds, purposeById, expectedTypeById);
            return result;
        }

        internal static CurtainGridLine AddVerticalGridLine(
            Document document,
            CurtainGrid grid,
            XYZ position)
        {
            Exception firstError = null;
            foreach (bool isUGridLine in new[] { false, true })
            {
                CurtainGridLine line = null;
                try
                {
                    line = grid.AddGridLine(isUGridLine, position, false);
                    document.Regenerate();
                    if (line != null && IsVertical(line.FullCurve))
                    {
                        return line;
                    }

                    if (line != null)
                    {
                        document.Delete(line.Id);
                        document.Regenerate();
                    }
                }
                catch (Exception exception)
                {
                    firstError = firstError ?? exception;
                    if (line != null && document.GetElement(line.Id) != null)
                    {
                        document.Delete(line.Id);
                        document.Regenerate();
                    }
                }
            }

            throw new InvalidOperationException(
                "Revit не смог создать вертикальную Curtain Grid Line в заданной позиции.",
                firstError);
        }

        internal static void AddMullionsToExistingSegments(CurtainGridLine gridLine, MullionType mullionType)
        {
            List<Curve> segments = new List<Curve>();
            foreach (Curve curve in gridLine.ExistingSegmentCurves)
            {
                segments.Add(curve);
            }

            foreach (Curve segment in segments)
            {
                gridLine.AddMullions(segment, mullionType, true);
            }
        }

        private static IList<double> GetStudOffsets(SourceWallData sourceWall, double maximumSpacing)
        {
            double tolerance = RevitUnitService.MillimetersToInternal(FrameModuleConstants.GeometryToleranceMm);
            List<double> anchors = new List<double> { 0.0, sourceWall.LengthInternal };
            foreach (WallOpeningData opening in sourceWall.Openings)
            {
                anchors.Add(opening.StartOffsetInternal);
                anchors.Add((opening.StartOffsetInternal + opening.EndOffsetInternal) / 2.0);
                anchors.Add(opening.EndOffsetInternal);
            }

            anchors.AddRange(sourceWall.TConnectionOffsetsInternal ?? new List<double>());
            anchors.Sort();
            List<double> distinctAnchors = new List<double>();
            foreach (double anchor in anchors)
            {
                if (distinctAnchors.Count == 0 ||
                    anchor - distinctAnchors[distinctAnchors.Count - 1] > tolerance)
                {
                    distinctAnchors.Add(anchor);
                }
            }

            List<double> offsets = new List<double>();
            for (int index = 1; index < distinctAnchors.Count; index++)
            {
                double first = distinctAnchors[index - 1];
                double second = distinctAnchors[index];
                double requiredBayCount = Math.Ceiling((second - first) / maximumSpacing);
                if (requiredBayCount > 10000.0)
                {
                    throw new InvalidOperationException(
                        "Максимальный шаг слишком мал для этой стены: потребуется более 10 000 пролётов.");
                }

                int bayCount = Math.Max(1, (int)requiredBayCount);
                for (int bay = 1; bay < bayCount; bay++)
                {
                    double offset = first + (second - first) * bay / bayCount;
                    if (!IsOpeningBoundary(sourceWall, offset))
                    {
                        offsets.Add(offset);
                    }
                }
            }

            foreach (double junction in sourceWall.TConnectionOffsetsInternal ?? new List<double>())
            {
                if (!IsOpeningBoundary(sourceWall, junction))
                {
                    offsets.Add(junction);
                }
            }

            // Центр каждого проёма обязателен даже при ширине меньше максимального
            // шага: сетка оставляет отдельные стойки над и под пустотой.
            foreach (WallOpeningData opening in sourceWall.Openings)
            {
                offsets.Add((opening.StartOffsetInternal + opening.EndOffsetInternal) / 2.0);
            }

            offsets.Sort();
            return offsets.Where((offset, index) =>
                offset > tolerance && offset < sourceWall.LengthInternal - tolerance &&
                (index == 0 || offset - offsets[index - 1] > tolerance)).ToList();
        }

        private static void SetMullionType(Mullion mullion, MullionType requiredType)
        {
            bool alreadyCorrectType = mullion.GetTypeId().Equals(requiredType.Id);

            if (mullion.Lock)
            {
                if (!mullion.Lockable)
                {
                    throw new InvalidOperationException(
                        "Импост " + mullion.Id.IntegerValue + " заблокирован и не поддерживает снятие блокировки.");
                }

                // Автоматические импосты типа витража закреплены. После назначения
                // индивидуального типа оставляем их откреплёнными: повторное закрепление
                // может вернуть тип, заданный сеткой витража.
                mullion.Lock = false;
            }

            if (!alreadyCorrectType)
            {
                mullion.MullionType = requiredType;
            }
        }

        private static MullionType SelectMullionType(
            Mullion mullion,
            SourceWallData sourceWall,
            WallOpeningData opening,
            FrameMemberPurpose purpose,
            FrameMullionTypeSet types)
        {
            if (purpose == FrameMemberPurpose.BottomTrack)
            {
                return types.Track;
            }

            if (purpose == FrameMemberPurpose.TopTrack)
            {
                return types.TrackInverted;
            }

            if (purpose == FrameMemberPurpose.DoorHeader)
            {
                return types.DoorStud;
            }

            double tolerance = RevitUnitService.MillimetersToInternal(FrameModuleConstants.GeometryToleranceMm);
            Curve curve = mullion.LocationCurve;
            double offset = curve != null
                ? ProjectOffset(sourceWall, Midpoint(curve.GetEndPoint(0), curve.GetEndPoint(1)))
                : double.NaN;

            if (purpose == FrameMemberPurpose.DoorJamb && opening != null)
            {
                return Math.Abs(offset - opening.EndOffsetInternal) <= tolerance
                    ? types.DoorStudInverted
                    : types.DoorStud;
            }

            return purpose == FrameMemberPurpose.Stud &&
                   Math.Abs(offset - sourceWall.LengthInternal) <= tolerance
                ? types.StudInverted
                : types.Stud;
        }

        private static FrameMemberPurpose Classify(
            Mullion mullion,
            SourceWallData sourceWall,
            out WallOpeningData relatedOpening)
        {
            relatedOpening = null;
            Curve curve = mullion.LocationCurve;
            if (curve == null)
            {
                return FrameMemberPurpose.Custom;
            }

            XYZ first = curve.GetEndPoint(0);
            XYZ second = curve.GetEndPoint(1);
            double verticalSpan = Math.Abs(second.Z - first.Z);
            double horizontalSpan = new XYZ(second.X - first.X, second.Y - first.Y, 0.0).GetLength();
            double tolerance = RevitUnitService.MillimetersToInternal(FrameModuleConstants.GeometryToleranceMm);

            if (verticalSpan > horizontalSpan)
            {
                double offset = ProjectOffset(sourceWall, Midpoint(first, second));
                foreach (WallOpeningData opening in sourceWall.Openings)
                {
                    if (Math.Abs(offset - opening.StartOffsetInternal) <= tolerance ||
                        Math.Abs(offset - opening.EndOffsetInternal) <= tolerance)
                    {
                        relatedOpening = opening;
                        return opening.OpeningType == OpeningType.Door
                            ? FrameMemberPurpose.DoorJamb
                            : FrameMemberPurpose.OpeningJamb;
                    }
                }

                return FrameMemberPurpose.Stud;
            }

            double elevation = (first.Z + second.Z) / 2.0;
            if (Math.Abs(elevation - sourceWall.BaseElevationInternal) <= tolerance)
            {
                return FrameMemberPurpose.BottomTrack;
            }

            if (Math.Abs(elevation - (sourceWall.BaseElevationInternal + sourceWall.HeightInternal)) <= tolerance)
            {
                return FrameMemberPurpose.TopTrack;
            }

            foreach (WallOpeningData opening in sourceWall.Openings)
            {
                double headerElevation = sourceWall.BaseElevationInternal + opening.TopOffsetInternal;
                if (Math.Abs(elevation - headerElevation) <= tolerance &&
                    CurveOverlapsOpening(first, second, sourceWall, opening, tolerance))
                {
                    relatedOpening = opening;
                    return opening.OpeningType == OpeningType.Door
                        ? FrameMemberPurpose.DoorHeader
                        : FrameMemberPurpose.OpeningHeader;
                }

                double sillElevation = sourceWall.BaseElevationInternal + opening.BottomOffsetInternal;
                if (opening.BottomOffsetInternal > tolerance &&
                    Math.Abs(elevation - sillElevation) <= tolerance &&
                    CurveOverlapsOpening(first, second, sourceWall, opening, tolerance))
                {
                    relatedOpening = opening;
                    return FrameMemberPurpose.OpeningSill;
                }
            }

            return FrameMemberPurpose.HorizontalBrace;
        }

        private static void ValidateGeometry(
            Document document,
            SourceWallData sourceWall,
            IEnumerable<ElementId> mullionIds,
            IDictionary<ElementId, FrameMemberPurpose> purposeById,
            IDictionary<ElementId, ElementId> expectedTypeById)
        {
            double tolerance = RevitUnitService.MillimetersToInternal(FrameModuleConstants.GeometryToleranceMm);
            Dictionary<string, bool[]> openingEdges = sourceWall.Openings
                .ToDictionary(item => item.SourceUniqueId, item => new bool[8]);
            bool hasTopTrack = false;
            bool hasBottomTrack = false;
            List<double> horizontalElevationsMm = new List<double>();

            foreach (ElementId id in mullionIds)
            {
                Mullion mullion = document.GetElement(id) as Mullion;
                Curve curve = mullion != null ? mullion.LocationCurve : null;
                if (curve == null)
                {
                    throw new InvalidOperationException("Импост " + id.IntegerValue + " не имеет LocationCurve.");
                }

                if (!mullion.GetTypeId().Equals(expectedTypeById[id]))
                {
                    throw new InvalidOperationException(
                        "Импост " + id.IntegerValue +
                        " сменил назначенный тип при обновлении расчётного витража.");
                }

                if (mullion.Lock)
                {
                    throw new InvalidOperationException(
                        "Импост " + id.IntegerValue +
                        " снова заблокирован после обновления расчётного витража.");
                }

                XYZ first = curve.GetEndPoint(0);
                XYZ second = curve.GetEndPoint(1);
                double firstOffset = ProjectOffset(sourceWall, first);
                double secondOffset = ProjectOffset(sourceWall, second);
                double minOffset = Math.Min(firstOffset, secondOffset);
                double maxOffset = Math.Max(firstOffset, secondOffset);
                double minZ = Math.Min(first.Z, second.Z);
                double maxZ = Math.Max(first.Z, second.Z);
                FrameMemberPurpose purpose = purposeById[id];
                hasTopTrack = hasTopTrack || purpose == FrameMemberPurpose.TopTrack;
                hasBottomTrack = hasBottomTrack || purpose == FrameMemberPurpose.BottomTrack;
                if (!IsVertical(curve))
                {
                    horizontalElevationsMm.Add(Math.Round(
                        RevitUnitService.InternalToMillimeters((first.Z + second.Z) / 2.0 -
                                                               sourceWall.BaseElevationInternal), 1));
                }

                foreach (WallOpeningData opening in sourceWall.Openings)
                {
                    double bottom = sourceWall.BaseElevationInternal + opening.BottomOffsetInternal;
                    double top = sourceWall.BaseElevationInternal + opening.TopOffsetInternal;
                    bool[] edges = openingEdges[opening.SourceUniqueId];
                    if (IsVertical(curve))
                    {
                        double offset = (firstOffset + secondOffset) / 2.0;
                        if (offset > opening.StartOffsetInternal + tolerance &&
                            offset < opening.EndOffsetInternal - tolerance &&
                            maxZ > bottom + tolerance && minZ < top - tolerance)
                        {
                            throw new InvalidOperationException(
                                "Импост " + id.IntegerValue + " пересекает проём " + opening.SourceUniqueId + ".");
                        }

                        if (minZ <= bottom + tolerance && maxZ >= top - tolerance)
                        {
                            edges[0] = edges[0] ||
                                       Math.Abs(offset - opening.StartOffsetInternal) <= tolerance;
                            edges[1] = edges[1] ||
                                       Math.Abs(offset - opening.EndOffsetInternal) <= tolerance;
                        }

                        if (minZ <= sourceWall.BaseElevationInternal + tolerance &&
                            maxZ >= sourceWall.BaseElevationInternal + sourceWall.HeightInternal - tolerance)
                        {
                            edges[4] = edges[4] ||
                                       Math.Abs(offset - opening.StartOffsetInternal) <= tolerance;
                            edges[5] = edges[5] ||
                                       Math.Abs(offset - opening.EndOffsetInternal) <= tolerance;
                        }

                        double center = (opening.StartOffsetInternal + opening.EndOffsetInternal) / 2.0;
                        if (Math.Abs(offset - center) <= tolerance)
                        {
                            edges[6] = edges[6] ||
                                       (opening.BottomOffsetInternal > tolerance &&
                                        minZ <= sourceWall.BaseElevationInternal + tolerance &&
                                        maxZ >= bottom - tolerance);
                            edges[7] = edges[7] ||
                                       (opening.TopOffsetInternal < sourceWall.HeightInternal - tolerance &&
                                        minZ <= top + tolerance &&
                                        maxZ >= sourceWall.BaseElevationInternal +
                                                sourceWall.HeightInternal - tolerance);
                        }
                    }
                    else
                    {
                        double elevation = (first.Z + second.Z) / 2.0;
                        bool overlaps = maxOffset > opening.StartOffsetInternal + tolerance &&
                                        minOffset < opening.EndOffsetInternal - tolerance;
                        if (overlaps && elevation > bottom + tolerance && elevation < top - tolerance)
                        {
                            throw new InvalidOperationException(
                                "Горизонтальный импост " + id.IntegerValue +
                                " пересекает проём " + opening.SourceUniqueId + ".");
                        }

                        if (Math.Abs(minOffset - opening.StartOffsetInternal) <= tolerance &&
                            Math.Abs(maxOffset - opening.EndOffsetInternal) <= tolerance)
                        {
                            edges[2] = edges[2] || Math.Abs(elevation - top) <= tolerance;
                            edges[3] = edges[3] || Math.Abs(elevation - bottom) <= tolerance;
                        }

                        if (opening.BottomOffsetInternal <= tolerance && overlaps &&
                            Math.Abs(elevation - bottom) <= tolerance)
                        {
                            throw new InvalidOperationException(
                                "Нижний импост " + id.IntegerValue +
                                " пересекает проём от пола " + opening.SourceUniqueId + ".");
                        }
                    }
                }
            }

            if (!hasTopTrack || !hasBottomTrack)
            {
                throw new InvalidOperationException(
                    "Revit не создал граничные импосты после повторного применения типа витража " +
                    "(нижний: " + (hasBottomTrack ? "есть" : "нет") +
                    ", верхний: " + (hasTopTrack ? "есть" : "нет") +
                    "; фактические отметки горизонтальных импостов от низа стены, мм: " +
                    (horizontalElevationsMm.Count > 0
                        ? string.Join(", ", horizontalElevationsMm.Distinct().OrderBy(value => value).Take(12))
                        : "отсутствуют") + ").");
            }

            foreach (WallOpeningData opening in sourceWall.Openings)
            {
                bool[] edges = openingEdges[opening.SourceUniqueId];
                if (!edges[0] || !edges[1] || !edges[2] ||
                    (opening.BottomOffsetInternal > tolerance && !edges[3]))
                {
                    throw new InvalidOperationException(
                        "Проём " + opening.SourceUniqueId +
                        " не имеет полного обрамления импостами. Проверьте border mullions расчётного WallType.");
                }

                if (!edges[4] || !edges[5])
                {
                    throw new InvalidOperationException(
                        "Боковые стойки проёма " + opening.SourceUniqueId +
                        " не проходят цельными от низа до верха стены.");
                }

                if ((opening.BottomOffsetInternal > tolerance && !edges[6]) ||
                    (opening.TopOffsetInternal < sourceWall.HeightInternal - tolerance && !edges[7]))
                {
                    throw new InvalidOperationException(
                        "Не созданы стойки по центру над и под проёмом " + opening.SourceUniqueId + ".");
                }
            }
        }

        private static bool CurveOverlapsOpening(
            XYZ first,
            XYZ second,
            SourceWallData sourceWall,
            WallOpeningData opening,
            double tolerance)
        {
            double firstOffset = ProjectOffset(sourceWall, first);
            double secondOffset = ProjectOffset(sourceWall, second);
            double min = Math.Min(firstOffset, secondOffset);
            double max = Math.Max(firstOffset, secondOffset);
            return max > opening.StartOffsetInternal + tolerance &&
                   min < opening.EndOffsetInternal - tolerance;
        }

        private static bool IsVertical(Curve curve)
        {
            if (curve == null)
            {
                return false;
            }

            XYZ first = curve.GetEndPoint(0);
            XYZ second = curve.GetEndPoint(1);
            double verticalSpan = Math.Abs(second.Z - first.Z);
            double horizontalSpan = new XYZ(second.X - first.X, second.Y - first.Y, 0.0).GetLength();
            return verticalSpan > horizontalSpan;
        }

        private static double ProjectOffset(SourceWallData sourceWall, XYZ point)
        {
            XYZ rawStart = sourceWall.LocationLine.GetEndPoint(0);
            XYZ start = new XYZ(rawStart.X, rawStart.Y, sourceWall.BaseElevationInternal);
            return (point - start).DotProduct(sourceWall.Direction);
        }

        private static XYZ Midpoint(XYZ first, XYZ second)
        {
            return new XYZ(
                (first.X + second.X) / 2.0,
                (first.Y + second.Y) / 2.0,
                (first.Z + second.Z) / 2.0);
        }

        private static XYZ PointAt(SourceWallData sourceWall, double offset, double height)
        {
            XYZ rawStart = sourceWall.LocationLine.GetEndPoint(0);
            XYZ basePoint = new XYZ(rawStart.X, rawStart.Y, sourceWall.BaseElevationInternal);
            return basePoint + sourceWall.Direction.Multiply(offset) + XYZ.BasisZ.Multiply(height);
        }

        private static bool IsOpeningBoundary(SourceWallData sourceWall, double offset)
        {
            double tolerance = RevitUnitService.MillimetersToInternal(FrameModuleConstants.GeometryToleranceMm);
            return sourceWall.Openings.Any(opening =>
                Math.Abs(offset - opening.StartOffsetInternal) <= tolerance ||
                Math.Abs(offset - opening.EndOffsetInternal) <= tolerance);
        }

        private static double GetGridLinePositionHeight(SourceWallData sourceWall, double offset)
        {
            double tolerance = RevitUnitService.MillimetersToInternal(FrameModuleConstants.GeometryToleranceMm);
            List<WallOpeningData> blocked = sourceWall.Openings
                .Where(item => offset > item.StartOffsetInternal + tolerance &&
                               offset < item.EndOffsetInternal - tolerance)
                .OrderBy(item => item.BottomOffsetInternal)
                .ToList();
            if (blocked.Count == 0)
            {
                return sourceWall.HeightInternal / 2.0;
            }

            double cursor = 0.0;
            double bestStart = 0.0;
            double bestEnd = 0.0;
            foreach (WallOpeningData opening in blocked)
            {
                if (opening.BottomOffsetInternal - cursor > bestEnd - bestStart)
                {
                    bestStart = cursor;
                    bestEnd = opening.BottomOffsetInternal;
                }

                cursor = Math.Max(cursor, opening.TopOffsetInternal);
            }

            if (sourceWall.HeightInternal - cursor > bestEnd - bestStart)
            {
                bestStart = cursor;
                bestEnd = sourceWall.HeightInternal;
            }

            if (bestEnd - bestStart <= tolerance)
            {
                throw new InvalidOperationException(
                    "На отметке стойки нет свободного участка между проёмами.");
            }

            return (bestStart + bestEnd) / 2.0;
        }
    }
}
