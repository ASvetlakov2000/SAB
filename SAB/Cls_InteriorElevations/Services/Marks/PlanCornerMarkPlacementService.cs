using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Marks
{
    public class PlanCornerMarkPlacementService
    {
        public int PlacePlanCornerMarks(
            Document document,
            ViewPlan planView,
            IList<ElevationLineData> elevationLines,
            RoomData roomData,
            ElementId planCornerMarkTypeId,
            IList<string> warnings)
        {
            if (document == null || planView == null || elevationLines == null || roomData == null)
            {
                return 0;
            }

            FamilySymbol symbol = document.GetElement(planCornerMarkTypeId) as FamilySymbol;
            if (symbol == null)
            {
                AddWarning(warnings, "Не найден тип семейства марки угла на плане.");
                return 0;
            }

            if (!CornerMarkConstants.IsAnnotationSymbol(symbol))
            {
                AddWarning(
                    warnings,
                    "Выбран неверный тип марки угла на плане. Ожидается категория '" +
                    CornerMarkConstants.GetAnnotationCategoryNameForMessage() + "'.");
                return 0;
            }

            if (!symbol.IsActive)
            {
                symbol.Activate();
                document.Regenerate();
            }

            int placedCount = 0;
            HashSet<int> placedCornerNumbers = new HashSet<int>();
            List<XYZ> occupiedCornerPoints = new List<XYZ>();
            double pointToleranceFeet = UnitConversionUtils.MillimetersToFeet(1.0);

            for (int index = 0; index < elevationLines.Count; index++)
            {
                ElevationLineData lineData = elevationLines[index];
                if (lineData == null)
                {
                    continue;
                }

                int startCornerNumber = lineData.Index;
                int endCornerNumber = lineData.EndIndex > 0
                    ? lineData.EndIndex
                    : lineData.Index + 1;

                if (!placedCornerNumbers.Contains(startCornerNumber) &&
                    !IsPointOccupied(occupiedCornerPoints, lineData.StartPoint, pointToleranceFeet))
                {
                    if (TryPlaceCornerMark(document, planView, symbol, lineData.StartPoint, roomData.RoomNumber, startCornerNumber, warnings))
                    {
                        placedCornerNumbers.Add(startCornerNumber);
                        occupiedCornerPoints.Add(lineData.StartPoint);
                        placedCount++;
                    }
                }

                if (!placedCornerNumbers.Contains(endCornerNumber) &&
                    !IsPointOccupied(occupiedCornerPoints, lineData.EndPoint, pointToleranceFeet))
                {
                    if (TryPlaceCornerMark(document, planView, symbol, lineData.EndPoint, roomData.RoomNumber, endCornerNumber, warnings))
                    {
                        placedCornerNumbers.Add(endCornerNumber);
                        occupiedCornerPoints.Add(lineData.EndPoint);
                        placedCount++;
                    }
                }
            }

            return placedCount;
        }

        private bool IsPointOccupied(IList<XYZ> occupiedPoints, XYZ testPoint, double toleranceFeet)
        {
            if (occupiedPoints == null || testPoint == null)
            {
                return false;
            }

            for (int i = 0; i < occupiedPoints.Count; i++)
            {
                XYZ occupiedPoint = occupiedPoints[i];
                if (occupiedPoint == null)
                {
                    continue;
                }

                if (occupiedPoint.DistanceTo(testPoint) <= toleranceFeet)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryPlaceCornerMark(
            Document document,
            ViewPlan planView,
            FamilySymbol symbol,
            XYZ placementPoint,
            string roomNumber,
            int cornerNumber,
            IList<string> warnings)
        {
            try
            {
                FamilyInstance markInstance = document.Create.NewFamilyInstance(placementPoint, symbol, planView);
                if (markInstance == null)
                {
                    AddWarning(warnings, "Не удалось создать марку угла на плане в точке " + FormatPoint(placementPoint) + ".");
                    return false;
                }

                SetParameter(markInstance, CornerMarkConstants.RoomNumberParameterName, roomNumber, warnings);
                SetParameter(markInstance, CornerMarkConstants.CornerNumberParameterName, cornerNumber.ToString(), warnings);
                return true;
            }
            catch (Exception exception)
            {
                AddWarning(
                    warnings,
                    "Ошибка размещения марки угла на плане (угол " + cornerNumber + "): " + exception.Message);
                return false;
            }
        }

        private void SetParameter(FamilyInstance markInstance, string parameterName, string value, IList<string> warnings)
        {
            if (markInstance == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            Parameter parameter = markInstance.LookupParameter(parameterName);
            if (parameter == null)
            {
                AddWarning(warnings, "Параметр '" + parameterName + "' отсутствует у семейства марки угла.");
                return;
            }

            if (parameter.IsReadOnly)
            {
                AddWarning(warnings, "Параметр '" + parameterName + "' доступен только для чтения.");
                return;
            }

            try
            {
                if (parameter.StorageType == StorageType.String)
                {
                    parameter.Set(value ?? string.Empty);
                }
                else if (parameter.StorageType == StorageType.Integer)
                {
                    int intValue;
                    if (int.TryParse(value, out intValue))
                    {
                        parameter.Set(intValue);
                    }
                }
                else
                {
                    parameter.SetValueString(value ?? string.Empty);
                }
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось заполнить параметр '" + parameterName + "': " + exception.Message);
            }
        }

        private string FormatPoint(XYZ point)
        {
            if (point == null)
            {
                return "<null>";
            }

            return "(" + point.X.ToString("F3") + ", " + point.Y.ToString("F3") + ", " + point.Z.ToString("F3") + ")";
        }

        private void AddWarning(IList<string> warnings, string text)
        {
            if (warnings == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            warnings.Add(text);
        }
    }
}
