using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using SAB.GklFrame.Models;

namespace SAB.GklFrame.Services
{
    public interface ICurtainWallFactory
    {
        Wall Create(Document document, SourceWallData sourceWall, FrameGenerationOptions options);
    }

    public sealed class CurtainWallFactory : ICurtainWallFactory
    {
        private readonly IFrameMetadataService _metadataService;

        public CurtainWallFactory(IFrameMetadataService metadataService)
        {
            _metadataService = metadataService;
        }

        public Wall Create(Document document, SourceWallData sourceWall, FrameGenerationOptions options)
        {
            WallType templateType = document.GetElement(options.CalculationCurtainWallTypeId) as WallType;
            if (templateType == null || templateType.Kind != WallKind.Curtain)
            {
                throw new InvalidOperationException("Выбранный тип стены не является Curtain Wall.");
            }

            FrameMullionTypeSet mullionTypes = FrameMullionTypeResolver.Resolve(
                document,
                sourceWall.CoreWidthInternal,
                sourceWall.Openings.Any(item => item.OpeningType == OpeningType.Door),
                options.MullionCatalogDocument);
            WallType wallType = GetOrCreateCalculationType(document, templateType, sourceWall, mullionTypes);

            IList<WallOpeningData> openings = options.ProcessDoors
                ? sourceWall.Openings
                : new List<WallOpeningData>();
            double tolerance = RevitUnitService.MillimetersToInternal(FrameModuleConstants.GeometryToleranceMm);
            IList<WallOpeningData> bottomOpenings = openings
                .Where(item => item.BottomOffsetInternal <= tolerance)
                .ToList();

            Wall calculationWall;
            if (bottomOpenings.Count == 0)
            {
                double baseOffset = sourceWall.BaseElevationInternal -
                                    ((Level)document.GetElement(sourceWall.BaseLevelId)).Elevation;
                calculationWall = Wall.Create(
                    document,
                    sourceWall.LocationLine,
                    wallType.Id,
                    sourceWall.BaseLevelId,
                    sourceWall.HeightInternal,
                    baseOffset,
                    false,
                    false);
            }
            else
            {
                // Проёмы от низа формируются выемками наружного профиля стены.
                // Нижнего импоста и панели в этих выемках не существует.
                IList<Curve> profile = BuildNotchedProfile(sourceWall, bottomOpenings);
                calculationWall = Wall.Create(
                    document,
                    profile,
                    wallType.Id,
                    sourceWall.BaseLevelId,
                    false);
            }

            if (calculationWall == null)
            {
                throw new InvalidOperationException("Revit не создал расчётный Curtain Wall.");
            }

            document.Regenerate();
            calculationWall = RefreshMissingBorderMullions(
                document, calculationWall, templateType, wallType, sourceWall);
            _metadataService.MarkCalculationWall(calculationWall, sourceWall);
            document.Regenerate();

            CurtainGrid grid = calculationWall.CurtainGrid;
            if (grid == null)
            {
                throw new InvalidOperationException("У созданной стены отсутствует CurtainGrid.");
            }

            if (grid.GetUGridLineIds().Count > 0 || grid.GetVGridLineIds().Count > 0)
            {
                throw new InvalidOperationException(
                    "Выбранный тип витража содержит автоматическую сетку. " +
                    "Для расчётного каркаса нужен тип без автоматических U/V grid lines.");
            }

            // Линии по краям проёмов ставятся до вырезания пустоты, чтобы боковые
            // стойки шли от низа до верха стены, а горизонтали упирались в них.
            foreach (WallOpeningData opening in openings)
            {
                AddOpeningJambGridLine(
                    document, grid, sourceWall, opening, opening.StartOffsetInternal,
                    opening.OpeningType == OpeningType.Door ? mullionTypes.DoorStud : mullionTypes.Stud);
                AddOpeningJambGridLine(
                    document, grid, sourceWall, opening, opening.EndOffsetInternal,
                    opening.OpeningType == OpeningType.Door ? mullionTypes.DoorStudInverted : mullionTypes.Stud);
            }

            foreach (WallOpeningData opening in openings.Where(item => item.BottomOffsetInternal > tolerance))
            {
                try
                {
                    Opening created = document.Create.NewOpening(
                        calculationWall,
                        PointAt(sourceWall, opening.StartOffsetInternal, opening.BottomOffsetInternal),
                        PointAt(sourceWall, opening.EndOffsetInternal, opening.TopOffsetInternal));
                    if (created == null)
                    {
                        throw new InvalidOperationException("Revit не создал отверстие.");
                    }

                    document.Regenerate();
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        "Не удалось перенести внутренний проём " + opening.SourceUniqueId +
                        " в расчётный витраж: " + exception.Message,
                        exception);
                }
            }

            return calculationWall;
        }

        private static Wall RefreshMissingBorderMullions(
            Document document,
            Wall calculationWall,
            WallType templateType,
            WallType calculationType,
            SourceWallData sourceWall)
        {
            if (HasHorizontalBorderMullion(document, calculationWall, sourceWall, false) &&
                HasHorizontalBorderMullion(document, calculationWall, sourceWall, true))
            {
                return calculationWall;
            }

            // If Wall.Create has not produced the type-driven border mullions,
            // reapply the prepared type before adding manual grid lines or openings.
            calculationWall = ChangeWallType(document, calculationWall, templateType);
            document.Regenerate();
            calculationWall = ChangeWallType(document, calculationWall, calculationType);
            document.Regenerate();
            return calculationWall;
        }

        private static Wall ChangeWallType(Document document, Wall wall, WallType wallType)
        {
            ElementId replacementId = wall.ChangeTypeId(wallType.Id);
            if (replacementId != null && replacementId != ElementId.InvalidElementId &&
                !replacementId.Equals(wall.Id))
            {
                Wall replacement = document.GetElement(replacementId) as Wall;
                if (replacement == null)
                {
                    throw new InvalidOperationException("Revit не вернул стену после смены типа витража.");
                }

                return replacement;
            }

            return wall;
        }

        private static bool HasHorizontalBorderMullion(
            Document document,
            Wall wall,
            SourceWallData sourceWall,
            bool top)
        {
            CurtainGrid grid = wall.CurtainGrid;
            if (grid == null)
            {
                return false;
            }

            double borderElevation = sourceWall.BaseElevationInternal +
                                     (top ? sourceWall.HeightInternal : 0.0);
            double tolerance = RevitUnitService.MillimetersToInternal(FrameModuleConstants.GeometryToleranceMm);
            foreach (ElementId id in grid.GetMullionIds())
            {
                Mullion mullion = document.GetElement(id) as Mullion;
                Curve curve = mullion != null ? mullion.LocationCurve : null;
                if (curve == null)
                {
                    continue;
                }

                XYZ first = curve.GetEndPoint(0);
                XYZ second = curve.GetEndPoint(1);
                double horizontalSpan = new XYZ(second.X - first.X, second.Y - first.Y, 0.0).GetLength();
                if (horizontalSpan > Math.Abs(second.Z - first.Z) &&
                    Math.Abs((first.Z + second.Z) / 2.0 - borderElevation) <= tolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddOpeningJambGridLine(
            Document document,
            CurtainGrid grid,
            SourceWallData sourceWall,
            WallOpeningData opening,
            double offset,
            MullionType mullionType)
        {
            try
            {
                double freeHeight = opening.TopOffsetInternal < sourceWall.HeightInternal - 1e-7
                    ? (opening.TopOffsetInternal + sourceWall.HeightInternal) / 2.0
                    : opening.BottomOffsetInternal / 2.0;
                CurtainGridLine line = CurtainGridGenerator.AddVerticalGridLine(
                    document, grid, PointAt(sourceWall, offset, freeHeight));
                CurtainGridGenerator.AddMullionsToExistingSegments(line, mullionType);
                document.Regenerate();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Не удалось поставить цельную боковую стойку проёма на расстоянии " +
                    Math.Round(RevitUnitService.InternalToMillimeters(offset), 1) +
                    " мм от начала стены: " + exception.Message,
                    exception);
            }
        }

        private static WallType GetOrCreateCalculationType(
            Document document,
            WallType templateType,
            SourceWallData sourceWall,
            FrameMullionTypeSet types)
        {
            int widthMm = (int)Math.Round(RevitUnitService.InternalToMillimeters(sourceWall.CoreWidthInternal));
            string name = FrameModuleConstants.DefaultCurtainWallTypeName + "_" +
                templateType.Id.IntegerValue + "_" + widthMm;
            WallType wallType = new FilteredElementCollector(document)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));
            if (wallType == null)
            {
                wallType = templateType.Duplicate(name) as WallType;
            }

            if (wallType == null || wallType.Kind != WallKind.Curtain)
            {
                throw new InvalidOperationException("Не удалось подготовить тип расчётного витража " + name + ".");
            }

            SetMullionParameter(wallType, BuiltInParameter.AUTO_MULLION_INTERIOR_VERT, types.Stud);
            SetMullionParameter(wallType, BuiltInParameter.AUTO_MULLION_BORDER1_VERT, types.Stud);
            SetMullionParameter(wallType, BuiltInParameter.AUTO_MULLION_BORDER2_VERT, types.StudInverted);
            SetMullionParameter(wallType, BuiltInParameter.AUTO_MULLION_INTERIOR_HORIZ, types.Stud);
            SetMullionParameter(wallType, BuiltInParameter.AUTO_MULLION_BORDER1_HORIZ, types.Track);
            SetMullionParameter(wallType, BuiltInParameter.AUTO_MULLION_BORDER2_HORIZ, types.TrackInverted);
            document.Regenerate();
            return wallType;
        }

        private static void SetMullionParameter(
            WallType wallType,
            BuiltInParameter parameterId,
            MullionType mullionType)
        {
            Parameter parameter = wallType.get_Parameter(parameterId);
            if (parameter != null && parameter.StorageType == StorageType.ElementId &&
                parameter.AsElementId().Equals(mullionType.Id))
            {
                return;
            }

            if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.ElementId ||
                !parameter.Set(mullionType.Id))
            {
                throw new InvalidOperationException(
                    "Не удалось назначить граничный или внутренний импост типу " + wallType.Name +
                    " (параметр " + parameterId + ").");
            }
        }

        private static IList<Curve> BuildNotchedProfile(
            SourceWallData sourceWall,
            IList<WallOpeningData> openings)
        {
            List<XYZ> points = new List<XYZ>();
            AddPoint(points, PointAt(sourceWall, 0.0, 0.0));

            foreach (WallOpeningData opening in openings)
            {
                AddPoint(points, PointAt(sourceWall, opening.StartOffsetInternal, 0.0));
                AddPoint(points, PointAt(sourceWall, opening.StartOffsetInternal, opening.TopOffsetInternal));
                AddPoint(points, PointAt(sourceWall, opening.EndOffsetInternal, opening.TopOffsetInternal));
                AddPoint(points, PointAt(sourceWall, opening.EndOffsetInternal, 0.0));
            }

            AddPoint(points, PointAt(sourceWall, sourceWall.LengthInternal, 0.0));
            AddPoint(points, PointAt(sourceWall, sourceWall.LengthInternal, sourceWall.HeightInternal));
            AddPoint(points, PointAt(sourceWall, 0.0, sourceWall.HeightInternal));

            List<Curve> curves = new List<Curve>();
            double tolerance = RevitUnitService.MillimetersToInternal(0.1);
            for (int index = 0; index < points.Count; index++)
            {
                XYZ first = points[index];
                XYZ second = points[(index + 1) % points.Count];
                if (first.DistanceTo(second) > tolerance)
                {
                    curves.Add(Line.CreateBound(first, second));
                }
            }

            return curves;
        }

        private static XYZ PointAt(SourceWallData sourceWall, double offset, double height)
        {
            XYZ rawStart = sourceWall.LocationLine.GetEndPoint(0);
            XYZ basePoint = new XYZ(rawStart.X, rawStart.Y, sourceWall.BaseElevationInternal);
            return basePoint + sourceWall.Direction.Multiply(offset) + XYZ.BasisZ.Multiply(height);
        }

        private static void AddPoint(IList<XYZ> points, XYZ point)
        {
            if (points.Count == 0 || points[points.Count - 1].DistanceTo(point) > 1e-9)
            {
                points.Add(point);
            }
        }
    }
}
