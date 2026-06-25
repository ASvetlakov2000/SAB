using System;
using Autodesk.Revit.DB;
using SAB.CreateViewsAndSheets.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.CreateViewsAndSheets.Services
{
    public class ViewportPlacementService
    {
        public Viewport PlaceViewOnSheet(
            Document document,
            ViewSheet sheet,
            View view,
            ElementId viewportTypeId,
            SheetBounds sheetBounds,
            PlacementSettings placementSettings,
            System.Collections.Generic.IList<string> warnings)
        {
            if (document == null)
            {
                throw new InvalidOperationException("Документ Revit недоступен.");
            }

            if (sheet == null)
            {
                throw new InvalidOperationException("Лист для размещения вида недоступен.");
            }

            if (view == null)
            {
                throw new InvalidOperationException("Вид для размещения недоступен.");
            }

            if (sheetBounds == null)
            {
                throw new InvalidOperationException("Не определены габариты листа для расчета координат.");
            }

            if (!Viewport.CanAddViewToSheet(document, sheet.Id, view.Id))
            {
                throw new InvalidOperationException("Вид \"" + view.Name + "\" нельзя разместить на листе " + sheet.SheetNumber + ".");
            }

            ValidatePlacementValues(sheetBounds, placementSettings);

            XYZ viewportCenter = BuildSheetPoint(sheetBounds, placementSettings.ViewCenterXmm, placementSettings.ViewCenterYmm);
            ValidatePoint(viewportCenter, "центр Viewport");

            Viewport viewport = Viewport.Create(document, sheet.Id, view.Id, viewportCenter);
            if (viewport == null)
            {
                throw new InvalidOperationException("Revit API не создал Viewport для вида \"" + view.Name + "\".");
            }

            TryApplyViewportType(viewport, viewportTypeId, warnings);
            document.Regenerate();

            try
            {
                viewport.SetBoxCenter(viewportCenter);
            }
            catch
            {
                XYZ currentCenter = viewport.GetBoxCenter();
                XYZ moveVector = viewportCenter - currentCenter;
                if (moveVector.GetLength() > 1e-9)
                {
                    ElementTransformUtils.MoveElement(document, viewport.Id, moveVector);
                }
            }

            document.Regenerate();
            TryPlaceViewportLabel(viewport, sheetBounds, placementSettings, warnings);
            return viewport;
        }

        private void TryApplyViewportType(Viewport viewport, ElementId viewportTypeId, System.Collections.Generic.IList<string> warnings)
        {
            if (viewport == null || viewportTypeId == null || viewportTypeId == ElementId.InvalidElementId)
            {
                return;
            }

            try
            {
                if (viewport.CanHaveTypeAssigned() && viewport.IsValidType(viewportTypeId))
                {
                    viewport.ChangeTypeId(viewportTypeId);
                }
                else
                {
                    AddWarning(warnings, "Выбранный тип Viewport не подходит для созданного видового экрана.");
                }
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось назначить тип Viewport: " + exception.Message);
            }

            WarnIfViewportTitleIsHidden(viewport, warnings);
        }

        private void TryPlaceViewportLabel(Viewport viewport, SheetBounds sheetBounds, PlacementSettings placementSettings, System.Collections.Generic.IList<string> warnings)
        {
            if (viewport == null || sheetBounds == null || placementSettings == null)
            {
                return;
            }

            try
            {
                if (IsViewportTitleHidden(viewport))
                {
                    AddWarning(warnings, "У выбранного типа Viewport отключено отображение заголовка. Положение заголовка не применялось.");
                    return;
                }

                Outline outline = viewport.GetBoxOutline();
                if (outline == null || outline.MinimumPoint == null)
                {
                    AddWarning(warnings, "Не удалось определить габарит Viewport для расчета положения заголовка.");
                    return;
                }

                XYZ absoluteLabelPoint = BuildSheetPoint(sheetBounds, placementSettings.ViewTitleXmm, placementSettings.ViewTitleYmm);
                ValidatePoint(absoluteLabelPoint, "точка заголовка Viewport");

                XYZ viewportBottomLeft = outline.MinimumPoint;
                XYZ labelOffset = new XYZ(
                    absoluteLabelPoint.X - viewportBottomLeft.X,
                    absoluteLabelPoint.Y - viewportBottomLeft.Y,
                    0.0);

                ValidatePoint(labelOffset, "смещение заголовка Viewport");
                if (IsOffsetTooLarge(labelOffset, sheetBounds))
                {
                    AddWarning(warnings, "Смещение заголовка Viewport слишком большое. Заголовок оставлен в положении Revit по умолчанию.");
                    return;
                }

                // Длину линии заголовка не задаем: в Revit изменение LabelLineLength может сбрасывать LabelOffset.
                // Плагин управляет только положением левого конца линии заголовка через LabelOffset.
                viewport.LabelOffset = labelOffset;
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось разместить заголовок Viewport: " + exception.Message);
            }
        }

        private XYZ BuildSheetPoint(SheetBounds sheetBounds, double xMm, double yMm)
        {
            double xFeet = sheetBounds.MinXFeet + UnitConversionUtils.MillimetersToFeet(xMm);
            double yFeet = sheetBounds.MinYFeet + UnitConversionUtils.MillimetersToFeet(yMm);
            return new XYZ(xFeet, yFeet, 0.0);
        }

        private void ValidatePlacementValues(SheetBounds sheetBounds, PlacementSettings placementSettings)
        {
            if (placementSettings == null)
            {
                throw new InvalidOperationException("Настройки размещения не получены.");
            }

            if (!IsFinite(sheetBounds.MinXFeet) ||
                !IsFinite(sheetBounds.MinYFeet) ||
                !IsFinite(sheetBounds.WidthFeet) ||
                !IsFinite(sheetBounds.HeightFeet) ||
                sheetBounds.WidthFeet <= 1e-9 ||
                sheetBounds.HeightFeet <= 1e-9)
            {
                throw new InvalidOperationException("Габарит листа содержит некорректные значения.");
            }

            if (!IsFinite(placementSettings.ViewCenterXmm) ||
                !IsFinite(placementSettings.ViewCenterYmm) ||
                !IsFinite(placementSettings.ViewTitleXmm) ||
                !IsFinite(placementSettings.ViewTitleYmm) ||
                !IsFinite(placementSettings.TitleLineLengthMm))
            {
                throw new InvalidOperationException("Координаты размещения содержат некорректные значения.");
            }

            if (!sheetBounds.ContainsPointMm(placementSettings.ViewCenterXmm, placementSettings.ViewCenterYmm))
            {
                throw new InvalidOperationException("Координаты центра Viewport выходят за габарит листа.");
            }

            if (!sheetBounds.ContainsPointMm(placementSettings.ViewTitleXmm, placementSettings.ViewTitleYmm))
            {
                throw new InvalidOperationException("Координаты заголовка Viewport выходят за габарит листа.");
            }

            if (placementSettings.TitleLineLengthMm <= 0)
            {
                throw new InvalidOperationException("Длина линии заголовка должна быть больше нуля.");
            }
        }

        private void ValidatePoint(XYZ point, string pointDescription)
        {
            if (point == null ||
                !IsFinite(point.X) ||
                !IsFinite(point.Y) ||
                !IsFinite(point.Z))
            {
                throw new InvalidOperationException("Точка \"" + pointDescription + "\" содержит некорректные координаты.");
            }
        }

        private bool IsOffsetTooLarge(XYZ offset, SheetBounds sheetBounds)
        {
            if (offset == null || sheetBounds == null)
            {
                return true;
            }

            double sheetDiagonal = Math.Sqrt(sheetBounds.WidthFeet * sheetBounds.WidthFeet + sheetBounds.HeightFeet * sheetBounds.HeightFeet);
            if (sheetDiagonal <= 1e-9)
            {
                return true;
            }

            if (Math.Abs(offset.X) > sheetBounds.WidthFeet * 2.0)
            {
                return true;
            }

            if (Math.Abs(offset.Y) > sheetBounds.HeightFeet * 2.0)
            {
                return true;
            }

            return offset.GetLength() > sheetDiagonal * 2.0;
        }

        private bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private void WarnIfViewportTitleIsHidden(Viewport viewport, System.Collections.Generic.IList<string> warnings)
        {
            if (viewport == null)
            {
                return;
            }

            try
            {
                if (IsViewportTitleHidden(viewport))
                {
                    AddWarning(warnings, "У выбранного типа Viewport отключено отображение заголовка вида.");
                }
            }
            catch
            {
                // Проверка отображения заголовка является вспомогательной и не должна прерывать размещение.
            }
        }

        private bool IsViewportTitleHidden(Viewport viewport)
        {
            if (viewport == null)
            {
                return false;
            }

            Document document = viewport.Document;
            if (document == null)
            {
                return false;
            }

            ElementType viewportType = document.GetElement(viewport.GetTypeId()) as ElementType;
            if (viewportType == null)
            {
                return false;
            }

            Parameter showTitleParameter = GetParameterByBuiltInName(viewportType, "VIEWPORT_ATTR_SHOW_LABEL");
            return showTitleParameter != null &&
                   showTitleParameter.StorageType == StorageType.Integer &&
                   showTitleParameter.AsInteger() == 0;
        }

        private Parameter GetParameterByBuiltInName(Element element, string builtInParameterName)
        {
            if (element == null || string.IsNullOrWhiteSpace(builtInParameterName))
            {
                return null;
            }

            try
            {
                BuiltInParameter builtInParameter = (BuiltInParameter)Enum.Parse(typeof(BuiltInParameter), builtInParameterName);
                return element.get_Parameter(builtInParameter);
            }
            catch
            {
                return null;
            }
        }

        private void AddWarning(System.Collections.Generic.IList<string> warnings, string warningText)
        {
            if (warnings == null || string.IsNullOrWhiteSpace(warningText))
            {
                return;
            }

            warnings.Add(warningText);
        }

    }
}
