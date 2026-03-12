using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using InteriorElevations.Models;
using System;

namespace InteriorElevations.Services
{
    public static class ElevationCreator
    {
        public static void CreateElevationFromLine(
            Document doc,
            ViewPlan planView,
            ViewFamilyType elevationType,
            DetailLine line,
            Room room,
            ElevationSettings settings,
            int lineIndex,
            int startPointIndex)
        {
            // 1. Геометрия линии
            Curve curve = line.GeometryCurve;
            XYZ start = curve.GetEndPoint(0);
            XYZ end = curve.GetEndPoint(1);

            // 2. Направление линии и центр
            XYZ lineDirection = (end - start).Normalize();
            XYZ center = (start + end) / 2;

            // 3. Смещение от линии
            double offset = UnitUtils.ConvertToInternalUnits(settings.CropOffsetLine, UnitTypeId.Millimeters);
            XYZ markerPoint = center + lineDirection * offset;

            // 4. Создание ElevationMarker
            ElevationMarker marker = ElevationMarker.CreateElevationMarker(
                doc, elevationType.Id, markerPoint, settings.DefaultViewScale);

            // 5. Поворот маркера вдоль линии
            Line axis = Line.CreateBound(markerPoint, markerPoint + XYZ.BasisZ);
            double angle = XYZ.BasisX.AngleTo(lineDirection);
            if (XYZ.BasisX.CrossProduct(lineDirection).Z < 0) angle = -angle;
            ElementTransformUtils.RotateElement(doc, marker.Id, axis, angle);

            // 6. Определяем индекс elevation
            int elevationIndex = Math.Abs(lineDirection.X) > Math.Abs(lineDirection.Y)
                ? (lineDirection.X > 0 ? 1 : 3)
                : (lineDirection.Y > 0 ? 0 : 2);

            // 7. Создаём ViewSection
            ViewSection elevation = marker.CreateElevation(doc, planView.Id, elevationIndex);

            // 8. Устанавливаем CropBox с учётом уровней и отступов
            double minZ = planView.GenLevel.Elevation + UnitUtils.ConvertToInternalUnits(settings.CropOffsetBottom, UnitTypeId.Millimeters);
            double maxZ = planView.GenLevel.Elevation + UnitUtils.ConvertToInternalUnits(settings.CropOffsetTop, UnitTypeId.Millimeters);
            double sideOffset = UnitUtils.ConvertToInternalUnits(settings.CropOffsetSide, UnitTypeId.Millimeters);

            XYZ min = new XYZ(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), minZ) - lineDirection.CrossProduct(XYZ.BasisZ) * sideOffset;
            XYZ max = new XYZ(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y), maxZ) + lineDirection.CrossProduct(XYZ.BasisZ) * sideOffset;

            elevation.CropBox = new BoundingBoxXYZ { Min = min, Max = max };
            elevation.CropBoxActive = true;

            // 9. Имя вида
            string roomNumber = room.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.AsString() ?? "Нет номера";
            elevation.Name = string.Format(settings.ViewNameFormat, roomNumber, startPointIndex, startPointIndex + 1);

            elevation.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION)?.Set($"Elev {lineIndex}.1_{startPointIndex}-{startPointIndex + 1}");
        }
    }
}