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
                if (warnings != null)
                {
                    warnings.Add("Не найден тип семейства марки угла на плане.");
                }

                return 0;
            }

            if (!IsFamilyMatches(symbol, CornerMarkConstants.PlanFamilyName))
            {
                if (warnings != null)
                {
                    warnings.Add(
                        "Выбран неверный тип семейства для марок плана. Ожидается семейство '" +
                        CornerMarkConstants.PlanFamilyName + "'.");
                }
            }

            if (!symbol.IsActive)
            {
                symbol.Activate();
                document.Regenerate();
            }

            int placedCount = 0;
            HashSet<int> placedCornerNumbers = new HashSet<int>();

            for (int index = 0; index < elevationLines.Count; index++)
            {
                ElevationLineData lineData = elevationLines[index];
                if (lineData == null)
                {
                    continue;
                }

                int startCornerNumber = lineData.Index;
                int endCornerNumber = lineData.Index + 1;

                if (!placedCornerNumbers.Contains(startCornerNumber))
                {
                    if (TryPlaceCornerMark(document, planView, symbol, lineData.StartPoint, roomData.RoomNumber, startCornerNumber, warnings))
                    {
                        placedCornerNumbers.Add(startCornerNumber);
                        placedCount++;
                    }
                }

                if (!placedCornerNumbers.Contains(endCornerNumber))
                {
                    if (TryPlaceCornerMark(document, planView, symbol, lineData.EndPoint, roomData.RoomNumber, endCornerNumber, warnings))
                    {
                        placedCornerNumbers.Add(endCornerNumber);
                        placedCount++;
                    }
                }
            }

            return placedCount;
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
                    if (warnings != null)
                    {
                        warnings.Add("Не удалось создать марку угла на плане в точке " + FormatPoint(placementPoint) + ".");
                    }

                    return false;
                }

                SetParameter(markInstance, CornerMarkConstants.RoomNumberParameterName, roomNumber, warnings);
                SetParameter(markInstance, CornerMarkConstants.CornerNumberParameterName, cornerNumber.ToString(), warnings);
                return true;
            }
            catch (Exception exception)
            {
                if (warnings != null)
                {
                    warnings.Add(
                        "Ошибка размещения марки угла на плане (угол " + cornerNumber + "): " +
                        exception.Message);
                }

                return false;
            }
        }

        private bool IsFamilyMatches(FamilySymbol symbol, string familyName)
        {
            if (symbol == null || string.IsNullOrWhiteSpace(familyName))
            {
                return false;
            }

            string currentFamilyName = symbol.Family != null ? symbol.Family.Name : symbol.FamilyName;
            return string.Equals(currentFamilyName, familyName, StringComparison.OrdinalIgnoreCase);
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
                if (warnings != null)
                {
                    warnings.Add("Параметр '" + parameterName + "' отсутствует у семейства марки угла.");
                }

                return;
            }

            if (parameter.IsReadOnly)
            {
                if (warnings != null)
                {
                    warnings.Add("Параметр '" + parameterName + "' доступен только для чтения.");
                }

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
                if (warnings != null)
                {
                    warnings.Add("Не удалось заполнить параметр '" + parameterName + "': " + exception.Message);
                }
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
    }
}
