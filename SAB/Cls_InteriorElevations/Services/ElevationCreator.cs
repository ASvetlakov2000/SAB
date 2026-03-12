using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
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
            int lineIndex,
            int startPointIndex)
        {
            //---------------------------------------------------------
            // 1. Геометрия линии
            //---------------------------------------------------------

            Curve curve = line.GeometryCurve;

            XYZ start = curve.GetEndPoint(0);
            XYZ end = curve.GetEndPoint(1);

            //---------------------------------------------------------
            // 2. Направление линии
            //---------------------------------------------------------

            XYZ lineDirection = (end - start).Normalize();

            //---------------------------------------------------------
            // 3. Центр линии
            //---------------------------------------------------------

            XYZ center = (start + end) / 2;

            //---------------------------------------------------------
            // 4. Смещение маркера
            //---------------------------------------------------------

            double offset = UnitUtils.ConvertToInternalUnits(150, UnitTypeId.Millimeters);

            XYZ markerPoint = center + lineDirection * offset;

            //---------------------------------------------------------
            // 5. Создание ElevationMarker
            //---------------------------------------------------------

            ElevationMarker marker =
                ElevationMarker.CreateElevationMarker(
                    doc,
                    elevationType.Id,
                    markerPoint,
                    100);

            //---------------------------------------------------------
            // 6. Поворот маркера
            //---------------------------------------------------------

            Line axis =
                Line.CreateBound(markerPoint, markerPoint + XYZ.BasisZ);

            double angle = XYZ.BasisX.AngleTo(lineDirection);

            if (XYZ.BasisX.CrossProduct(lineDirection).Z < 0)
                angle = -angle;

            ElementTransformUtils.RotateElement(
                doc,
                marker.Id,
                axis,
                angle);

            //---------------------------------------------------------
            // 7. Определяем индекс elevation
            //---------------------------------------------------------

            int elevationIndex =
                Math.Abs(lineDirection.X) > Math.Abs(lineDirection.Y)
                ? (lineDirection.X > 0 ? 1 : 3)
                : (lineDirection.Y > 0 ? 0 : 2);

            //---------------------------------------------------------
            // 8. Создаём ViewSection
            //---------------------------------------------------------

            ViewSection elevation =
                marker.CreateElevation(
                    doc,
                    planView.Id,
                    elevationIndex);

            //---------------------------------------------------------
            // 9. Формируем имя вида
            //---------------------------------------------------------

            string roomNumber =
                room.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.AsString()
                ?? "NoNumber";

            string viewName =
                $"Elev_r{roomNumber}_{startPointIndex}-{startPointIndex + 1}";

            elevation.Name = viewName;

            //---------------------------------------------------------
            // 10. Описание
            //---------------------------------------------------------

            elevation
                .get_Parameter(BuiltInParameter.VIEW_DESCRIPTION)?
                .Set(viewName);
        }
    }
}