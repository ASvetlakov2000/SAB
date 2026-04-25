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
            IList<string> warnings)
        {
            ViewportPlacementResult result = new ViewportPlacementResult();

            if (document == null || sheet == null || createdViews == null || layoutSettings == null)
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

            while (currentIndex < createdViews.Count)
            {
                double cursorX = startXFeet;
                double rowMaxHeight = 0.0;
                int rowPlacedCount = 0;

                for (int column = 0; column < columnsCount && currentIndex < createdViews.Count; column++)
                {
                    ElevationViewData elevationViewData = createdViews[currentIndex];
                    currentIndex++;

                    if (elevationViewData == null || elevationViewData.ViewSection == null)
                    {
                        continue;
                    }

                    try
                    {
                        if (!Viewport.CanAddViewToSheet(document, sheet.Id, elevationViewData.ViewSection.Id))
                        {
                            if (warnings != null)
                            {
                                warnings.Add("Вид " + elevationViewData.ViewName + " нельзя разместить на листе.");
                            }

                            continue;
                        }

                        // Временное размещение, чтобы получить реальный размер прямоугольника viewport.
                        Viewport viewport = Viewport.Create(document, sheet.Id, elevationViewData.ViewSection.Id, new XYZ(startXFeet, startYFeet, 0.0));
                        if (viewport == null)
                        {
                            if (warnings != null)
                            {
                                warnings.Add("Не удалось создать viewport для вида " + elevationViewData.ViewName + ".");
                            }

                            continue;
                        }

                        double viewportWidth;
                        double viewportHeight;
                        Outline initialOutline;
                        if (!TryGetViewportOutline(viewport, out initialOutline, out viewportWidth, out viewportHeight))
                        {
                            if (warnings != null)
                            {
                                warnings.Add("Не удалось определить размер viewport для вида " + elevationViewData.ViewName + ".");
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
                                warnings.Add("Не удалось определить итоговые границы viewport для вида " + elevationViewData.ViewName + ".");
                            }

                            continue;
                        }

                        PlacedViewportData placedViewportData = new PlacedViewportData();
                        placedViewportData.ViewportId = viewport.Id;
                        placedViewportData.ViewId = elevationViewData.ViewSection.Id;
                        placedViewportData.Center = viewport.GetBoxCenter();
                        XYZ topLeft;
                        XYZ topRight;
                        BuildTrueTopCorners(finalOutline, out topLeft, out topRight);
                        placedViewportData.TopLeft = topLeft;
                        placedViewportData.TopRight = topRight;
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
                            warnings.Add("Не удалось разместить вид " + elevationViewData.ViewName + " на листе: " + exception.Message);
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

        private void BuildTrueTopCorners(Outline outline, out XYZ topLeft, out XYZ topRight)
        {
            topLeft = XYZ.Zero;
            topRight = XYZ.Zero;

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
            double maxY = outline.MaximumPoint.Y - safePaddingY;

            topLeft = new XYZ(minX, maxY, 0.0);
            topRight = new XYZ(maxX, maxY, 0.0);
        }
    }
}
