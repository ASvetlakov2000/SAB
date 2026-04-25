using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Marks
{
    public class SheetCornerMarkPlacementService
    {
        public int PlaceSheetCornerMarks(
            Document document,
            ViewSheet sheet,
            RoomData roomData,
            ElementId sheetCornerMarkTypeId,
            IList<ElevationViewData> createdViews,
            ViewportPlacementResult placementResult,
            IList<string> warnings)
        {
            if (document == null || sheet == null || roomData == null || createdViews == null || placementResult == null)
            {
                return 0;
            }

            FamilySymbol symbol = document.GetElement(sheetCornerMarkTypeId) as FamilySymbol;
            if (symbol == null)
            {
                if (warnings != null)
                {
                    warnings.Add("Не найден тип семейства марки угла на листе.");
                }

                return 0;
            }

            if (!IsFamilyMatches(symbol, CornerMarkConstants.SheetFamilyName))
            {
                if (warnings != null)
                {
                    warnings.Add(
                        "Выбран неверный тип семейства для марок на листе. Ожидается семейство '" +
                        CornerMarkConstants.SheetFamilyName + "'.");
                }
            }

            if (!symbol.IsActive)
            {
                symbol.Activate();
                document.Regenerate();
            }

            Dictionary<long, ElevationViewData> viewDataByViewId = BuildViewDictionary(createdViews);
            int placedCount = 0;

            for (int index = 0; index < placementResult.PlacedViewports.Count; index++)
            {
                PlacedViewportData placedViewport = placementResult.PlacedViewports[index];
                if (placedViewport == null || placedViewport.ViewId == null || placedViewport.ViewId == ElementId.InvalidElementId)
                {
                    continue;
                }

                long viewIdValue = RevitElementIdUtils.GetElementIdValue(placedViewport.ViewId);
                ElevationViewData viewData;
                if (!viewDataByViewId.TryGetValue(viewIdValue, out viewData) || viewData == null)
                {
                    continue;
                }

                if (TryPlaceCornerMark(document, sheet, symbol, placedViewport.TopLeft, roomData.RoomNumber, viewData.StartCornerNumber, warnings))
                {
                    placedCount++;
                }

                if (TryPlaceCornerMark(document, sheet, symbol, placedViewport.TopRight, roomData.RoomNumber, viewData.EndCornerNumber, warnings))
                {
                    placedCount++;
                }
            }

            return placedCount;
        }

        private Dictionary<long, ElevationViewData> BuildViewDictionary(IList<ElevationViewData> createdViews)
        {
            Dictionary<long, ElevationViewData> dictionary = new Dictionary<long, ElevationViewData>();

            for (int index = 0; index < createdViews.Count; index++)
            {
                ElevationViewData viewData = createdViews[index];
                if (viewData == null || viewData.ViewId == null || viewData.ViewId == ElementId.InvalidElementId)
                {
                    continue;
                }

                long key = RevitElementIdUtils.GetElementIdValue(viewData.ViewId);
                if (!dictionary.ContainsKey(key))
                {
                    dictionary.Add(key, viewData);
                }
            }

            return dictionary;
        }

        private bool TryPlaceCornerMark(
            Document document,
            ViewSheet sheet,
            FamilySymbol symbol,
            XYZ placementPoint,
            string roomNumber,
            int cornerNumber,
            IList<string> warnings)
        {
            try
            {
                FamilyInstance markInstance = document.Create.NewFamilyInstance(placementPoint, symbol, sheet);
                if (markInstance == null)
                {
                    if (warnings != null)
                    {
                        warnings.Add("Не удалось создать марку угла на листе в точке " + FormatPoint(placementPoint) + ".");
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
                        "Ошибка размещения марки угла на листе (угол " + cornerNumber + "): " +
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
