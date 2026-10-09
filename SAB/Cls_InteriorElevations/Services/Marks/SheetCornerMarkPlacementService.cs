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
            bool onlyCornerNumber,
            bool belowView,
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

            if (!CornerMarkConstants.IsAnnotationSymbol(symbol))
            {
                if (warnings != null)
                {
                    warnings.Add(
                        "Выбран неверный тип марки угла на листе. Ожидается категория '" +
                        CornerMarkConstants.GetAnnotationCategoryNameForMessage() + "'.");
                }

                return 0;
            }

            FamilySymbol leftSymbol = symbol;
            FamilySymbol rightSymbol = symbol;
            TryResolveProjectSpecificSideSymbols(
                document,
                symbol,
                warnings,
                out leftSymbol,
                out rightSymbol);

            ActivateSymbol(document, leftSymbol);
            ActivateSymbol(document, rightSymbol);

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

                XYZ leftPoint = belowView ? placedViewport.BottomLeft : placedViewport.TopLeft;
                XYZ rightPoint = belowView ? placedViewport.BottomRight : placedViewport.TopRight;

                if (TryPlaceCornerMark(document, sheet, leftSymbol, leftPoint, roomData.RoomNumber, viewData.StartCornerNumber, onlyCornerNumber, warnings, placedViewport.SheetAnnotationIds))
                {
                    placedCount++;
                }

                if (TryPlaceCornerMark(document, sheet, rightSymbol, rightPoint, roomData.RoomNumber, viewData.EndCornerNumber, onlyCornerNumber, warnings, placedViewport.SheetAnnotationIds))
                {
                    placedCount++;
                }
            }

            return placedCount;
        }

        private void TryResolveProjectSpecificSideSymbols(
            Document document,
            FamilySymbol selectedSymbol,
            IList<string> warnings,
            out FamilySymbol leftSymbol,
            out FamilySymbol rightSymbol)
        {
            leftSymbol = selectedSymbol;
            rightSymbol = selectedSymbol;

            if (document == null || selectedSymbol == null || selectedSymbol.Family == null)
            {
                return;
            }

            if (!string.Equals(
                    selectedSymbol.Family.Name,
                    CornerMarkConstants.ProjectSpecificCornerMarkFamilyName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            FamilySymbol resolvedLeftSymbol = null;
            FamilySymbol resolvedRightSymbol = null;
            ISet<ElementId> symbolIds = selectedSymbol.Family.GetFamilySymbolIds();
            foreach (ElementId symbolId in symbolIds)
            {
                FamilySymbol candidate = document.GetElement(symbolId) as FamilySymbol;
                if (candidate == null)
                {
                    continue;
                }

                if (string.Equals(candidate.Name, CornerMarkConstants.LeftCornerMarkTypeName, StringComparison.OrdinalIgnoreCase))
                {
                    resolvedLeftSymbol = candidate;
                }
                else if (string.Equals(candidate.Name, CornerMarkConstants.RightCornerMarkTypeName, StringComparison.OrdinalIgnoreCase))
                {
                    resolvedRightSymbol = candidate;
                }
            }

            if (resolvedLeftSymbol != null)
            {
                leftSymbol = resolvedLeftSymbol;
            }

            if (resolvedRightSymbol != null)
            {
                rightSymbol = resolvedRightSymbol;
            }

            if (resolvedLeftSymbol == null || resolvedRightSymbol == null)
            {
                AddWarning(
                    warnings,
                    "Для семейства '" + CornerMarkConstants.ProjectSpecificCornerMarkFamilyName +
                    "' не найдены оба типоразмера 'Left' и 'Right'. Для отсутствующей стороны использован выбранный типоразмер.");
            }
        }

        private void ActivateSymbol(Document document, FamilySymbol symbol)
        {
            if (document == null || symbol == null || symbol.IsActive)
            {
                return;
            }

            symbol.Activate();
            document.Regenerate();
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
            bool onlyCornerNumber,
            IList<string> warnings,
            IList<ElementId> createdIds)
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

                createdIds.Add(markInstance.Id);
                if (!onlyCornerNumber)
                {
                    SetParameter(markInstance, CornerMarkConstants.RoomNumberParameterName, roomNumber, warnings);
                }
                SetParameter(markInstance, CornerMarkConstants.CornerNumberParameterName, cornerNumber.ToString(), warnings);

                document.Regenerate();
                LocationPoint locationPoint = markInstance.Location as LocationPoint;
                if (locationPoint != null && placementPoint != null &&
                    locationPoint.Point.DistanceTo(placementPoint) > 1e-9)
                {
                    locationPoint.Point = placementPoint;
                }

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

        private void AddWarning(IList<string> warnings, string message)
        {
            if (warnings == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            warnings.Add(message);
        }
    }
}
