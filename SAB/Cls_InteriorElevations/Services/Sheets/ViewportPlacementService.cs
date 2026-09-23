using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Sheets
{
    public class ViewportPlacementService
    {
        // Revit API возвращает GetBoxOutline с внутренним техническим запасом 0.01 ft (~3.048 мм).
        // Для привязки марки к "истинной" границе видового экрана компенсируем этот запас.
        private const double ViewportOutlinePaddingFeet = 0.01;

        public ViewportPlacementResult PlaceViewsOnSheet(
            Document document,
            ViewSheet sheet,
            IList<ElevationViewData> createdViews,
            SheetLayoutSettings layoutSettings,
            ElementId viewportTypeId,
            IList<string> warnings)
        {
            return PlaceViewsOnSheet(
                document,
                sheet,
                createdViews,
                null,
                layoutSettings,
                viewportTypeId,
                warnings);
        }

        private ViewportPlacementResult PlaceViewsOnSheet(
            Document document,
            ViewSheet sheet,
            IList<ElevationViewData> createdViews,
            View trailingView,
            SheetLayoutSettings layoutSettings,
            ElementId viewportTypeId,
            IList<string> warnings)
        {
            ViewportPlacementResult result = new ViewportPlacementResult();

            if (document == null || sheet == null || layoutSettings == null)
            {
                return result;
            }

            int elevationViewCount = createdViews != null ? createdViews.Count : 0;
            int totalViewCount = elevationViewCount + (trailingView != null ? 1 : 0);
            if (totalViewCount == 0)
            {
                return result;
            }

            int columnsCount = Math.Max(1, layoutSettings.ColumnsCount);

            // Стартовые координаты для верхней левой границы первого вида на листе.
            double startXFeet = UnitConversionUtils.MillimetersToFeet(layoutSettings.StartXmm);
            double startYFeet = UnitConversionUtils.MillimetersToFeet(layoutSettings.StartYmm);

            // Шаги интерпретируются как зазоры между границами соседних видов.
            double gapXFeet = UnitConversionUtils.MillimetersToFeet(layoutSettings.StepXmm);
            double gapYFeet = UnitConversionUtils.MillimetersToFeet(layoutSettings.StepYmm);

            double rowTopY = startYFeet;
            int currentIndex = 0;

            while (currentIndex < totalViewCount)
            {
                double cursorX = startXFeet;
                double rowMaxHeight = 0.0;
                int rowPlacedCount = 0;

                for (int column = 0; column < columnsCount && currentIndex < totalViewCount; column++)
                {
                    bool isTrailingView = currentIndex >= elevationViewCount;
                    ElevationViewData elevationViewData = isTrailingView
                        ? null
                        : createdViews[currentIndex];
                    View view = isTrailingView
                        ? trailingView
                        : elevationViewData != null ? elevationViewData.ViewSection : null;
                    string viewName = isTrailingView
                        ? "план-схема"
                        : elevationViewData != null ? elevationViewData.ViewName : string.Empty;
                    currentIndex++;

                    if (view == null)
                    {
                        continue;
                    }

                    try
                    {
                        if (!Viewport.CanAddViewToSheet(document, sheet.Id, view.Id))
                        {
                            if (warnings != null)
                            {
                                warnings.Add(
                                    isTrailingView
                                        ? "План-схему нельзя разместить на листе."
                                        : "Вид " + viewName + " нельзя разместить на листе.");
                            }

                            continue;
                        }

                        // Временное размещение, чтобы получить реальный размер прямоугольника viewport.
                        Viewport viewport = Viewport.Create(document, sheet.Id, view.Id, new XYZ(startXFeet, startYFeet, 0.0));
                        if (viewport == null)
                        {
                            if (warnings != null)
                            {
                                warnings.Add(
                                    isTrailingView
                                        ? "Не удалось создать viewport для план-схемы."
                                        : "Не удалось создать viewport для вида " + viewName + ".");
                            }

                            continue;
                        }

                        if (!isTrailingView)
                        {
                            TryApplyViewportType(viewport, viewportTypeId, warnings);
                        }

                        document.Regenerate();

                        double viewportWidth;
                        double viewportHeight;
                        Outline initialOutline;
                        if (!TryGetViewportOutline(viewport, out initialOutline, out viewportWidth, out viewportHeight))
                        {
                            if (warnings != null)
                            {
                                warnings.Add(
                                    isTrailingView
                                        ? "Не удалось определить размер viewport план-схемы."
                                        : "Не удалось определить размер viewport для вида " + viewName + ".");
                            }

                            continue;
                        }

                        // Центр пересчитывается из требуемой позиции левой/верхней границы.
                        double targetCenterX = cursorX + viewportWidth / 2.0;
                        double targetCenterY = rowTopY - viewportHeight / 2.0;

                        XYZ currentCenter = viewport.GetBoxCenter();
                        XYZ targetCenter = new XYZ(targetCenterX, targetCenterY, currentCenter.Z);
                        XYZ moveVector = targetCenter - currentCenter;

                        if (moveVector.GetLength() > 1e-9)
                        {
                            ElementTransformUtils.MoveElement(document, viewport.Id, moveVector);
                        }

                        Outline finalOutline;
                        double finalWidth;
                        double finalHeight;
                        if (!TryGetViewportOutline(viewport, out finalOutline, out finalWidth, out finalHeight))
                        {
                            if (warnings != null)
                            {
                                warnings.Add(
                                    isTrailingView
                                        ? "Не удалось определить итоговые границы viewport план-схемы."
                                        : "Не удалось определить итоговые границы viewport для вида " + viewName + ".");
                            }

                            continue;
                        }

                        if (!isTrailingView)
                        {
                            TryPlaceViewportTitle(viewport, finalOutline, layoutSettings, warnings);
                        }

                        PlacedViewportData placedViewportData = new PlacedViewportData();
                        placedViewportData.ViewportId = viewport.Id;
                        placedViewportData.ViewId = view.Id;
                        placedViewportData.Center = viewport.GetBoxCenter();
                        XYZ topLeft;
                        XYZ topRight;
                        XYZ bottomLeft;
                        XYZ bottomRight;
                        BuildTrueCorners(finalOutline, out topLeft, out topRight, out bottomLeft, out bottomRight);
                        placedViewportData.TopLeft = topLeft;
                        placedViewportData.TopRight = topRight;
                        placedViewportData.BottomLeft = bottomLeft;
                        placedViewportData.BottomRight = bottomRight;
                        result.PlacedViewports.Add(placedViewportData);

                        // В следующую колонку переходим от правой границы текущего viewport + заданный зазор.
                        cursorX += finalWidth + gapXFeet;

                        if (finalHeight > rowMaxHeight)
                        {
                            rowMaxHeight = finalHeight;
                        }

                        result.PlacedCount++;
                        rowPlacedCount++;
                    }
                    catch (Exception exception)
                    {
                        if (warnings != null)
                        {
                            warnings.Add(
                                isTrailingView
                                    ? "Не удалось разместить план-схему на листе: " + exception.Message
                                    : "Не удалось разместить вид " + viewName + " на листе: " + exception.Message);
                        }
                    }
                }

                // Для следующего ряда отступаем от нижней границы самого высокого вида в текущем ряду.
                if (rowPlacedCount > 0)
                {
                    rowTopY -= rowMaxHeight + gapYFeet;
                }
            }

            return result;
        }

        public ViewportPlacementResult PlaceRoomViewGroupsOnSheet(
            Document document,
            ViewSheet sheet,
            IList<IList<ElevationViewData>> roomViewGroups,
            IList<View> roomPlanViews,
            SheetLayoutSettings layoutSettings,
            ElementId viewportTypeId,
            IList<string> warnings)
        {
            ViewportPlacementResult aggregateResult = new ViewportPlacementResult();
            if (document == null || sheet == null || roomViewGroups == null || layoutSettings == null)
            {
                return aggregateResult;
            }

            double nextRowTopFeet = UnitConversionUtils.MillimetersToFeet(layoutSettings.StartYmm);
            double gapYFeet = UnitConversionUtils.MillimetersToFeet(layoutSettings.StepYmm);

            for (int groupIndex = 0; groupIndex < roomViewGroups.Count; groupIndex++)
            {
                IList<ElevationViewData> roomViews = roomViewGroups[groupIndex];
                View roomPlanView = roomPlanViews != null && groupIndex < roomPlanViews.Count
                    ? roomPlanViews[groupIndex]
                    : null;
                SheetLayoutSettings groupLayout = CopyLayoutWithStartY(
                    layoutSettings,
                    UnitConversionUtils.FeetToMillimeters(nextRowTopFeet));

                ViewportPlacementResult groupResult = PlaceViewsOnSheet(
                    document,
                    sheet,
                    roomViews,
                    roomPlanView,
                    groupLayout,
                    viewportTypeId,
                    warnings);

                double lowestViewportY;
                if (TryGetLowestViewportY(document, groupResult, out lowestViewportY))
                {
                    nextRowTopFeet = lowestViewportY - gapYFeet;
                }
                else
                {
                    nextRowTopFeet -= gapYFeet;
                }

                MergePlacementResults(aggregateResult, groupResult);
            }

            return aggregateResult;
        }

        /// <summary>
        /// Размещает дополнительный вид (например, план-схему) на том же листе ниже блока разверток.
        /// </summary>
        public bool TryPlaceAdditionalViewOnSheet(
            Document document,
            ViewSheet sheet,
            View view,
            SheetLayoutSettings layoutSettings,
            ViewportPlacementResult placementResult,
            IList<string> warnings)
        {
            if (document == null || sheet == null || view == null || layoutSettings == null)
            {
                return false;
            }

            if (!Viewport.CanAddViewToSheet(document, sheet.Id, view.Id))
            {
                if (warnings != null)
                {
                    warnings.Add("План-схему нельзя разместить на листе.");
                }

                return false;
            }

            double startXFeet = UnitConversionUtils.MillimetersToFeet(layoutSettings.StartXmm);
            double startYFeet = UnitConversionUtils.MillimetersToFeet(layoutSettings.StartYmm);
            double gapYFeet = UnitConversionUtils.MillimetersToFeet(layoutSettings.StepYmm);

            double targetTopY = startYFeet;
            if (placementResult != null && placementResult.PlacedViewports.Count > 0)
            {
                double lowestViewportY;
                if (TryGetLowestViewportY(document, placementResult, out lowestViewportY))
                {
                    targetTopY = lowestViewportY - gapYFeet;
                }
            }

            return TryPlaceAdditionalViewAtPosition(
                document,
                sheet,
                view,
                startXFeet,
                targetTopY,
                placementResult,
                warnings);
        }

        private bool TryPlaceAdditionalViewAtPosition(
            Document document,
            ViewSheet sheet,
            View view,
            double targetLeftX,
            double targetTopY,
            ViewportPlacementResult placementResult,
            IList<string> warnings)
        {
            if (document == null || sheet == null || view == null)
            {
                return false;
            }

            if (!Viewport.CanAddViewToSheet(document, sheet.Id, view.Id))
            {
                if (warnings != null)
                {
                    warnings.Add("План-схему нельзя разместить на листе.");
                }

                return false;
            }

            try
            {
                Viewport viewport = Viewport.Create(
                    document,
                    sheet.Id,
                    view.Id,
                    new XYZ(targetLeftX, targetTopY, 0.0));
                if (viewport == null)
                {
                    if (warnings != null)
                    {
                        warnings.Add("Не удалось создать viewport для план-схемы.");
                    }

                    return false;
                }

                document.Regenerate();

                Outline outline;
                double width;
                double height;
                if (!TryGetViewportOutline(viewport, out outline, out width, out height))
                {
                    if (warnings != null)
                    {
                        warnings.Add("Не удалось определить габариты viewport план-схемы.");
                    }

                    return false;
                }

                double targetCenterX = targetLeftX + width / 2.0;
                double targetCenterY = targetTopY - height / 2.0;
                XYZ currentCenter = viewport.GetBoxCenter();
                XYZ targetCenter = new XYZ(targetCenterX, targetCenterY, currentCenter.Z);
                XYZ moveVector = targetCenter - currentCenter;
                if (moveVector.GetLength() > 1e-9)
                {
                    ElementTransformUtils.MoveElement(document, viewport.Id, moveVector);
                }

                document.Regenerate();

                if (placementResult != null)
                {
                    Outline finalOutline;
                    double finalWidth;
                    double finalHeight;
                    if (TryGetViewportOutline(viewport, out finalOutline, out finalWidth, out finalHeight))
                    {
                        PlacedViewportData placedViewportData = new PlacedViewportData();
                        placedViewportData.ViewportId = viewport.Id;
                        placedViewportData.ViewId = view.Id;
                        placedViewportData.Center = viewport.GetBoxCenter();

                        XYZ topLeft;
                        XYZ topRight;
                        XYZ bottomLeft;
                        XYZ bottomRight;
                        BuildTrueCorners(finalOutline, out topLeft, out topRight, out bottomLeft, out bottomRight);
                        placedViewportData.TopLeft = topLeft;
                        placedViewportData.TopRight = topRight;
                        placedViewportData.BottomLeft = bottomLeft;
                        placedViewportData.BottomRight = bottomRight;
                        placementResult.PlacedViewports.Add(placedViewportData);
                    }

                    placementResult.PlacedCount++;
                }

                return true;
            }
            catch (Exception exception)
            {
                if (warnings != null)
                {
                    warnings.Add("Не удалось разместить план-схему на листе: " + exception.Message);
                }

                return false;
            }
        }

        private SheetLayoutSettings CopyLayoutWithStartY(SheetLayoutSettings source, double startYmm)
        {
            SheetLayoutSettings copy = new SheetLayoutSettings();
            copy.ColumnsCount = source.ColumnsCount;
            copy.StartXmm = source.StartXmm;
            copy.StartYmm = startYmm;
            copy.StepXmm = source.StepXmm;
            copy.StepYmm = source.StepYmm;
            copy.ViewTitleAnchor = source.ViewTitleAnchor;
            copy.ViewTitleOffsetXmm = source.ViewTitleOffsetXmm;
            copy.ViewTitleOffsetYmm = source.ViewTitleOffsetYmm;
            return copy;
        }

        private void MergePlacementResults(ViewportPlacementResult target, ViewportPlacementResult source)
        {
            if (target == null || source == null)
            {
                return;
            }

            target.PlacedCount += source.PlacedCount;
            for (int index = 0; index < source.PlacedViewports.Count; index++)
            {
                target.PlacedViewports.Add(source.PlacedViewports[index]);
            }
        }

        private bool TryGetLowestViewportY(
            Document document,
            ViewportPlacementResult placementResult,
            out double lowestY)
        {
            lowestY = double.MaxValue;
            if (document == null || placementResult == null)
            {
                return false;
            }

            for (int index = 0; index < placementResult.PlacedViewports.Count; index++)
            {
                PlacedViewportData placedViewportData = placementResult.PlacedViewports[index];
                if (placedViewportData == null || placedViewportData.ViewportId == null ||
                    placedViewportData.ViewportId == ElementId.InvalidElementId)
                {
                    continue;
                }

                Viewport viewport = document.GetElement(placedViewportData.ViewportId) as Viewport;
                Outline outline = viewport != null ? viewport.GetBoxOutline() : null;
                if (outline == null || outline.MinimumPoint == null)
                {
                    continue;
                }

                lowestY = Math.Min(lowestY, outline.MinimumPoint.Y);
            }

            return lowestY < double.MaxValue;
        }

        private void TryApplyViewportType(Viewport viewport, ElementId viewportTypeId, IList<string> warnings)
        {
            if (viewport == null)
            {
                return;
            }

            try
            {
                ElementId targetTypeId = viewportTypeId;
                if (targetTypeId == null || RevitElementIdUtils.GetElementIdValue(targetTypeId) < 0)
                {
                    targetTypeId = GetOrCreateNoTitleViewportTypeId(viewport, warnings);
                }

                if (targetTypeId == null || RevitElementIdUtils.GetElementIdValue(targetTypeId) < 0)
                {
                    return;
                }

                if (viewport.CanHaveTypeAssigned() && viewport.IsValidType(targetTypeId))
                {
                    viewport.ChangeTypeId(targetTypeId);
                }
                else if (warnings != null)
                {
                    warnings.Add("Выбранный тип заголовка не подходит для видового экрана развертки.");
                }
            }
            catch (Exception exception)
            {
                if (warnings != null)
                {
                    warnings.Add("Не удалось назначить тип заголовка развертки: " + exception.Message);
                }
            }
        }

        private ElementId GetOrCreateNoTitleViewportTypeId(Viewport viewport, IList<string> warnings)
        {
            if (viewport == null || viewport.Document == null)
            {
                return ElementId.InvalidElementId;
            }

            Document document = viewport.Document;
            ICollection<ElementId> validTypeIds = null;
            try
            {
                validTypeIds = viewport.GetValidTypes();
            }
            catch
            {
                // Ниже остается текущий тип видового экрана как источник для дублирования.
            }

            if (validTypeIds != null)
            {
                foreach (ElementId validTypeId in validTypeIds)
                {
                    ElementType candidate = document.GetElement(validTypeId) as ElementType;
                    if (IsNoTitleViewportType(candidate))
                    {
                        return candidate.Id;
                    }
                }
            }

            ElementType sourceType = document.GetElement(viewport.GetTypeId()) as ElementType;
            if (sourceType == null)
            {
                if (warnings != null)
                {
                    warnings.Add("Не найден исходный тип видового экрана для создания варианта без заголовка.");
                }

                return ElementId.InvalidElementId;
            }

            for (int attempt = 0; attempt < 100; attempt++)
            {
                string typeName = attempt == 0
                    ? "SAB_Без заголовка"
                    : "SAB_Без заголовка_" + attempt.ToString("00");

                ElementType noTitleType;
                try
                {
                    noTitleType = sourceType.Duplicate(typeName);
                }
                catch
                {
                    continue;
                }

                if (noTitleType == null)
                {
                    continue;
                }

                Parameter showLabelParameter = GetParameterByBuiltInName(
                    noTitleType,
                    "VIEWPORT_ATTR_SHOW_LABEL");

                if (showLabelParameter != null &&
                    !showLabelParameter.IsReadOnly &&
                    showLabelParameter.StorageType == StorageType.Integer)
                {
                    try
                    {
                        showLabelParameter.Set(0);
                        return noTitleType.Id;
                    }
                    catch
                    {
                        // Не оставляем в проекте нерабочий технический тип.
                    }
                }

                try
                {
                    document.Delete(noTitleType.Id);
                }
                catch
                {
                    // Ошибка очистки не должна срывать создание разверток и листа.
                }
            }

            if (warnings != null)
            {
                warnings.Add("Не удалось создать тип видового экрана без заголовка. Виды размещены с типом Revit по умолчанию.");
            }

            return ElementId.InvalidElementId;
        }

        private bool IsNoTitleViewportType(ElementType viewportType)
        {
            if (viewportType == null)
            {
                return false;
            }

            Parameter showLabelParameter = GetParameterByBuiltInName(
                viewportType,
                "VIEWPORT_ATTR_SHOW_LABEL");

            if (showLabelParameter != null && showLabelParameter.StorageType == StorageType.Integer)
            {
                return showLabelParameter.AsInteger() == 0;
            }

            string typeName = viewportType.Name ?? string.Empty;
            return typeName.IndexOf("Без заголовка", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   typeName.IndexOf("No Title", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   string.Equals(typeName.Trim(), "Нет", StringComparison.OrdinalIgnoreCase);
        }

        private Parameter GetParameterByBuiltInName(Element element, string builtInParameterName)
        {
            if (element == null || string.IsNullOrWhiteSpace(builtInParameterName))
            {
                return null;
            }

            try
            {
                BuiltInParameter builtInParameter = (BuiltInParameter)Enum.Parse(
                    typeof(BuiltInParameter),
                    builtInParameterName,
                    true);
                return element.get_Parameter(builtInParameter);
            }
            catch
            {
                return null;
            }
        }

        private void TryPlaceViewportTitle(
            Viewport viewport,
            Outline viewportOutline,
            SheetLayoutSettings layoutSettings,
            IList<string> warnings)
        {
            if (viewport == null || viewportOutline == null || viewportOutline.MinimumPoint == null ||
                viewportOutline.MaximumPoint == null || layoutSettings == null)
            {
                return;
            }

            try
            {
                viewport.LabelOffset = BuildViewportTitleOffset(viewportOutline, layoutSettings);
            }
            catch (Exception exception)
            {
                if (warnings != null)
                {
                    warnings.Add("Не удалось разместить заголовок развертки: " + exception.Message);
                }
            }
        }

        private XYZ BuildViewportTitleOffset(Outline viewportOutline, SheetLayoutSettings layoutSettings)
        {
            double minimumX = viewportOutline.MinimumPoint.X;
            double minimumY = viewportOutline.MinimumPoint.Y;
            double maximumX = viewportOutline.MaximumPoint.X;
            double maximumY = viewportOutline.MaximumPoint.Y;

            double anchorX = minimumX;
            double anchorY = minimumY;
            if (layoutSettings.ViewTitleAnchor == ViewTitleAnchor.BottomCenter ||
                layoutSettings.ViewTitleAnchor == ViewTitleAnchor.TopCenter)
            {
                anchorX = (minimumX + maximumX) / 2.0;
            }
            else if (layoutSettings.ViewTitleAnchor == ViewTitleAnchor.BottomRight)
            {
                anchorX = maximumX;
            }

            double offsetX = UnitConversionUtils.MillimetersToFeet(layoutSettings.ViewTitleOffsetXmm);
            double offsetY = UnitConversionUtils.MillimetersToFeet(layoutSettings.ViewTitleOffsetYmm);

            if (layoutSettings.ViewTitleAnchor == ViewTitleAnchor.TopCenter)
            {
                anchorY = maximumY;
                offsetY = -offsetY;
            }

            return new XYZ(
                anchorX + offsetX - minimumX,
                anchorY + offsetY - minimumY,
                0.0);
        }

        private bool TryGetViewportOutline(Viewport viewport, out Outline outline, out double width, out double height)
        {
            outline = null;
            width = 0.0;
            height = 0.0;

            if (viewport == null)
            {
                return false;
            }

            outline = viewport.GetBoxOutline();
            if (outline == null || outline.MinimumPoint == null || outline.MaximumPoint == null)
            {
                return false;
            }

            width = Math.Abs(outline.MaximumPoint.X - outline.MinimumPoint.X);
            height = Math.Abs(outline.MaximumPoint.Y - outline.MinimumPoint.Y);
            return width > 1e-9 && height > 1e-9;
        }

        private void BuildTrueCorners(
            Outline outline,
            out XYZ topLeft,
            out XYZ topRight,
            out XYZ bottomLeft,
            out XYZ bottomRight)
        {
            topLeft = XYZ.Zero;
            topRight = XYZ.Zero;
            bottomLeft = XYZ.Zero;
            bottomRight = XYZ.Zero;

            if (outline == null || outline.MinimumPoint == null || outline.MaximumPoint == null)
            {
                return;
            }

            double width = Math.Abs(outline.MaximumPoint.X - outline.MinimumPoint.X);
            double height = Math.Abs(outline.MaximumPoint.Y - outline.MinimumPoint.Y);

            double safePaddingX = Math.Min(ViewportOutlinePaddingFeet, width / 2.0);
            double safePaddingY = Math.Min(ViewportOutlinePaddingFeet, height / 2.0);

            double minX = outline.MinimumPoint.X + safePaddingX;
            double maxX = outline.MaximumPoint.X - safePaddingX;
            double minY = outline.MinimumPoint.Y + safePaddingY;
            double maxY = outline.MaximumPoint.Y - safePaddingY;

            topLeft = new XYZ(minX, maxY, 0.0);
            topRight = new XYZ(maxX, maxY, 0.0);
            bottomLeft = new XYZ(minX, minY, 0.0);
            bottomRight = new XYZ(maxX, minY, 0.0);
        }
    }
}
