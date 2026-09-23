using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using SAB.GklFrame.Models;

namespace SAB.GklFrame.Services
{
    public interface IWallOpeningCollector
    {
        bool TryCollect(
            Wall sourceWall,
            Line wallLine,
            double baseElevationInternal,
            double wallHeightInternal,
            out IList<WallOpeningData> openings,
            out string error);
    }

    public sealed class WallOpeningCollector : IWallOpeningCollector
    {
        public bool TryCollect(
            Wall sourceWall,
            Line wallLine,
            double baseElevationInternal,
            double wallHeightInternal,
            out IList<WallOpeningData> openings,
            out string error)
        {
            openings = new List<WallOpeningData>();
            error = string.Empty;

            XYZ start = wallLine.GetEndPoint(0);
            XYZ rawDirection = wallLine.GetEndPoint(1) - start;
            XYZ direction = new XYZ(rawDirection.X, rawDirection.Y, 0.0).Normalize();
            double wallLength = new XYZ(rawDirection.X, rawDirection.Y, 0.0).GetLength();
            double tolerance = RevitUnitService.MillimetersToInternal(FrameModuleConstants.GeometryToleranceMm);

            FilteredElementCollector collector = new FilteredElementCollector(sourceWall.Document)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType();

            foreach (FamilyInstance door in collector.OfType<FamilyInstance>())
            {
                if (door.Host == null || door.Host.Id != sourceWall.Id)
                {
                    continue;
                }

                LocationPoint location = door.Location as LocationPoint;
                if (location == null)
                {
                    error = "Дверь " + door.Id.IntegerValue + " не имеет поддерживаемой точки размещения.";
                    return false;
                }

                double width = GetTypeOrInstanceDouble(door, BuiltInParameter.DOOR_WIDTH);
                double height = GetTypeOrInstanceDouble(door, BuiltInParameter.DOOR_HEIGHT);
                if (width <= tolerance || height <= tolerance)
                {
                    error = "Не удалось получить ширину или высоту двери " + door.Id.IntegerValue + ".";
                    return false;
                }

                double sill = GetTypeOrInstanceDouble(door, BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM);
                if (Math.Abs(sill) > tolerance)
                {
                    error = "Дверь " + door.Id.IntegerValue + " имеет ненулевую отметку порога; этот случай пока не поддерживается.";
                    return false;
                }

                // Координата вдоль стены — скалярная проекция вектора от начала
                // LocationCurve до точки вставки двери на нормализованное направление стены.
                double centerOffset = (location.Point - start).DotProduct(direction);
                double openingStart = centerOffset - width / 2.0;
                double openingEnd = centerOffset + width / 2.0;
                if (openingStart < -tolerance || openingEnd > wallLength + tolerance)
                {
                    error = "Габарит двери " + door.Id.IntegerValue + " выходит за пределы прямого участка стены.";
                    return false;
                }

                if (height >= wallHeightInternal - tolerance)
                {
                    error = "Дверь " + door.Id.IntegerValue + " достигает верха стены; над проёмом не остаётся каркаса.";
                    return false;
                }

                openings.Add(new WallOpeningData
                {
                    SourceElementId = door.Id,
                    SourceUniqueId = door.UniqueId,
                    StartOffsetInternal = Math.Max(0.0, openingStart),
                    EndOffsetInternal = Math.Min(wallLength, openingEnd),
                    BottomOffsetInternal = 0.0,
                    TopOffsetInternal = height,
                    OpeningType = OpeningType.Door
                });
            }

            openings = openings.OrderBy(item => item.StartOffsetInternal).ToList();
            for (int index = 1; index < openings.Count; index++)
            {
                if (openings[index].StartOffsetInternal < openings[index - 1].EndOffsetInternal + tolerance)
                {
                    error = "Пересекающиеся или расположенные вплотную дверные проёмы пока не поддерживаются.";
                    return false;
                }
            }

            return true;
        }

        private static double GetTypeOrInstanceDouble(FamilyInstance instance, BuiltInParameter builtInParameter)
        {
            Parameter parameter = instance.get_Parameter(builtInParameter);
            if (parameter != null && parameter.StorageType == StorageType.Double && parameter.AsDouble() > 0.0)
            {
                return parameter.AsDouble();
            }

            Element type = instance.Document.GetElement(instance.GetTypeId());
            parameter = type != null ? type.get_Parameter(builtInParameter) : null;
            return parameter != null && parameter.StorageType == StorageType.Double
                ? parameter.AsDouble()
                : 0.0;
        }
    }
}
