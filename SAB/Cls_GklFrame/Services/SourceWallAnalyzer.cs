using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using SAB.GklFrame.Models;

namespace SAB.GklFrame.Services
{
    public interface ISourceWallAnalyzer
    {
        bool TryAnalyze(Wall wall, out SourceWallData data, out string error);
    }

    public sealed class SourceWallAnalyzer : ISourceWallAnalyzer
    {
        private readonly IWallOpeningCollector _openingCollector;

        public SourceWallAnalyzer(IWallOpeningCollector openingCollector)
        {
            _openingCollector = openingCollector;
        }

        public bool TryAnalyze(Wall wall, out SourceWallData data, out string error)
        {
            data = null;
            error = string.Empty;

            if (wall == null)
            {
                error = "Элемент не является стеной.";
                return false;
            }

            if (wall.WallType == null || wall.WallType.Kind == WallKind.Curtain || wall.CurtainGrid != null)
            {
                error = "Расчётные витражи и Curtain Wall нельзя использовать как исходные стены.";
                return false;
            }

            if (wall.IsStackedWall || wall.IsStackedWallMember)
            {
                error = "Составные стены пока не поддерживаются.";
                return false;
            }

            if (wall.CrossSection != WallCrossSection.Vertical)
            {
                error = "Наклонные стены пока не поддерживаются.";
                return false;
            }

            LocationCurve location = wall.Location as LocationCurve;
            Line line = location != null ? location.Curve as Line : null;
            if (line == null || !line.IsBound)
            {
                error = "Требуется прямолинейная стена с валидной LocationCurve.";
                return false;
            }

            XYZ rawDirection = line.GetEndPoint(1) - line.GetEndPoint(0);
            XYZ horizontalDirection = new XYZ(rawDirection.X, rawDirection.Y, 0.0);
            if (horizontalDirection.GetLength() <= 1e-9 || Math.Abs(rawDirection.Z) > 1e-7)
            {
                error = "Требуется горизонтальная прямая линия расположения стены.";
                return false;
            }

            Parameter baseConstraint = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
            Level baseLevel = baseConstraint != null
                ? wall.Document.GetElement(baseConstraint.AsElementId()) as Level
                : null;
            if (baseLevel == null)
            {
                error = "Не удалось определить нижний уровень стены.";
                return false;
            }

            double baseOffset = GetDouble(wall, BuiltInParameter.WALL_BASE_OFFSET);
            double baseElevation = baseLevel.Elevation + baseOffset;
            double height;
            if (!TryGetWallHeight(wall, baseElevation, out height) || height <= 1e-7)
            {
                error = "Не удалось определить постоянную высоту стены.";
                return false;
            }

            CompoundStructure structure = wall.WallType.GetCompoundStructure();
            double coreWidth = 0.0;
            if (structure != null)
            {
                int firstCoreLayer = structure.GetFirstCoreLayerIndex();
                int lastCoreLayer = structure.GetLastCoreLayerIndex();
                for (int index = firstCoreLayer; index >= 0 && index <= lastCoreLayer; index++)
                {
                    coreWidth += structure.GetLayerWidth(index);
                }
            }

            if (coreWidth <= 1e-7)
            {
                error = "Не удалось определить толщину сердцевины стены.";
                return false;
            }

            IList<WallOpeningData> openings;
            string openingError;
            if (!_openingCollector.TryCollect(wall, line, baseElevation, height, out openings, out openingError))
            {
                error = openingError;
                return false;
            }

            data = new SourceWallData
            {
                SourceWall = wall,
                SourceElementId = wall.Id,
                SourceUniqueId = wall.UniqueId,
                LocationLine = line,
                StartPoint = line.GetEndPoint(0),
                Direction = horizontalDirection.Normalize(),
                BaseLevelId = baseLevel.Id,
                BaseElevationInternal = baseElevation,
                HeightInternal = height,
                LengthInternal = horizontalDirection.GetLength(),
                CoreWidthInternal = coreWidth,
                WallTypeName = wall.WallType.Name,
                WallMark = GetString(wall, BuiltInParameter.ALL_MODEL_MARK),
                LevelName = baseLevel.Name,
                Openings = openings,
                TConnectionOffsetsInternal = FindTConnections(wall, line, baseElevation, height)
            };
            return true;
        }

        private static IList<double> FindTConnections(
            Wall sourceWall,
            Line sourceLine,
            double baseElevation,
            double height)
        {
            // Торец непараллельной стены, подходящий к середине этой стены,
            // требует отдельной стойки на проходящей стене. При угловом
            // соединении торец попадает к её краю, где стойка уже граничная.
            XYZ start = sourceLine.GetEndPoint(0);
            XYZ end = sourceLine.GetEndPoint(1);
            XYZ direction = new XYZ(end.X - start.X, end.Y - start.Y, 0.0).Normalize();
            double length = new XYZ(end.X - start.X, end.Y - start.Y, 0.0).GetLength();
            double tolerance = RevitUnitService.MillimetersToInternal(FrameModuleConstants.GeometryToleranceMm);
            double endMargin = Math.Max(tolerance, sourceWall.Width / 2.0);
            List<double> offsets = new List<double>();

            foreach (Wall other in new FilteredElementCollector(sourceWall.Document)
                .OfClass(typeof(Wall)).Cast<Wall>())
            {
                if (other.Id.Equals(sourceWall.Id) || other.CurtainGrid != null || other.IsStackedWall)
                {
                    continue;
                }

                LocationCurve location = other.Location as LocationCurve;
                Line otherLine = location != null ? location.Curve as Line : null;
                BoundingBoxXYZ box = other.get_BoundingBox(null);
                if (otherLine == null || box == null ||
                    box.Max.Z <= baseElevation + tolerance ||
                    box.Min.Z >= baseElevation + height - tolerance)
                {
                    continue;
                }

                XYZ otherVector = otherLine.GetEndPoint(1) - otherLine.GetEndPoint(0);
                XYZ otherDirection = new XYZ(otherVector.X, otherVector.Y, 0.0);
                if (otherDirection.GetLength() <= 1e-9 ||
                    Math.Abs(otherDirection.Normalize().DotProduct(direction)) > 0.996)
                {
                    continue;
                }

                double contactDistance = Math.Max(sourceWall.Width, other.Width) / 2.0 + tolerance;
                for (int endpoint = 0; endpoint < 2; endpoint++)
                {
                    XYZ point = otherLine.GetEndPoint(endpoint);
                    double offset = (point - start).DotProduct(direction);
                    if (offset <= endMargin || offset >= length - endMargin)
                    {
                        continue;
                    }

                    XYZ nearest = start + direction.Multiply(offset);
                    double distance = new XYZ(point.X - nearest.X, point.Y - nearest.Y, 0.0).GetLength();
                    if (distance > contactDistance)
                    {
                        continue;
                    }

                    XYZ far = otherLine.GetEndPoint(1 - endpoint);
                    double farOffset = (far - start).DotProduct(direction);
                    XYZ farNearest = start + direction.Multiply(farOffset);
                    double farDistance = new XYZ(far.X - farNearest.X, far.Y - farNearest.Y, 0.0).GetLength();
                    if (farDistance > distance + contactDistance &&
                        offsets.All(existing => Math.Abs(existing - offset) > tolerance))
                    {
                        offsets.Add(offset);
                    }
                }
            }

            offsets.Sort();
            return offsets;
        }

        private static bool TryGetWallHeight(Wall wall, double baseElevation, out double height)
        {
            height = 0.0;
            Parameter topConstraint = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE);
            ElementId topLevelId = topConstraint != null ? topConstraint.AsElementId() : ElementId.InvalidElementId;
            Level topLevel = topLevelId != ElementId.InvalidElementId
                ? wall.Document.GetElement(topLevelId) as Level
                : null;

            if (topLevel != null)
            {
                double topOffset = GetDouble(wall, BuiltInParameter.WALL_TOP_OFFSET);
                height = topLevel.Elevation + topOffset - baseElevation;
                return height > 1e-7;
            }

            Parameter unconnectedHeight = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
            if (unconnectedHeight == null || unconnectedHeight.StorageType != StorageType.Double)
            {
                return false;
            }

            height = unconnectedHeight.AsDouble();
            return height > 1e-7;
        }

        private static double GetDouble(Element element, BuiltInParameter builtInParameter)
        {
            Parameter parameter = element.get_Parameter(builtInParameter);
            return parameter != null && parameter.StorageType == StorageType.Double
                ? parameter.AsDouble()
                : 0.0;
        }

        private static string GetString(Element element, BuiltInParameter builtInParameter)
        {
            Parameter parameter = element.get_Parameter(builtInParameter);
            return parameter != null ? parameter.AsString() ?? string.Empty : string.Empty;
        }
    }
}
