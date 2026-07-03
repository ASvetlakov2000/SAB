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

        public Viewport PlaceViewOnSheetBySourceViewport(
            Document document,
            ViewSheet sourceSheet,
            View sourceView,
            ViewSheet targetSheet,
            View targetView,
            ElementId fallbackViewportTypeId,
            System.Collections.Generic.IList<string> warnings)
        {
            if (document == null)
            {
                throw new InvalidOperationException("Документ Revit недоступен.");
            }

            if (sourceSheet == null)
            {
                throw new InvalidOperationException("Лист-образец для копирования положения вида недоступен.");
            }

            if (sourceView == null)
            {
                throw new InvalidOperationException("Вид-образец для копирования положения недоступен.");
            }

            if (targetSheet == null)
            {
                throw new InvalidOperationException("Лист для размещения вида недоступен.");
            }

            if (targetView == null)
            {
                throw new InvalidOperationException("Вид для размещения недоступен.");
            }

            if (!Viewport.CanAddViewToSheet(document, targetSheet.Id, targetView.Id))
            {
                throw new InvalidOperationException("Вид \"" + targetView.Name + "\" нельзя разместить на листе " + targetSheet.SheetNumber + ".");
            }

            Viewport sourceViewport = FindViewportOnSheet(document, sourceSheet, sourceView);
            if (sourceViewport == null)
            {
                throw new InvalidOperationException(
                    "На листе-образце " + sourceSheet.SheetNumber +
                    " не найден размещенный вид \"" + sourceView.Name + "\".");
            }

            SourceViewportPlacementData sourcePlacement = BuildSourceViewportPlacementData(sourceViewport);
            ElementId viewportTypeId = sourcePlacement.ViewportTypeId != null &&
                                       sourcePlacement.ViewportTypeId != ElementId.InvalidElementId
                ? sourcePlacement.ViewportTypeId
                : fallbackViewportTypeId;

            Viewport targetViewport = Viewport.Create(document, targetSheet.Id, targetView.Id, sourcePlacement.Center);
            if (targetViewport == null)
            {
                throw new InvalidOperationException("Revit API не создал Viewport для вида \"" + targetView.Name + "\".");
            }

            TryApplyViewportType(targetViewport, viewportTypeId, warnings);
            document.Regenerate();

            MoveViewportToCenter(document, targetViewport, sourcePlacement.Center);

            document.Regenerate();
            TryPlaceViewportLabelBySource(targetViewport, sourcePlacement, warnings);
            return targetViewport;
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

        private void TryPlaceViewportLabelBySource(
            Viewport viewport,
            SourceViewportPlacementData sourcePlacement,
            System.Collections.Generic.IList<string> warnings)
        {
            if (viewport == null || sourcePlacement == null)
            {
                return;
            }

            try
            {
                if (!sourcePlacement.HasTitlePoint)
                {
                    AddWarning(warnings, "Не удалось скопировать положение заголовка: на листе-образце не определена точка заголовка Viewport.");
                    return;
                }

                if (IsViewportTitleHidden(viewport))
                {
                    AddWarning(warnings, "У типа Viewport с листа-образца отключено отображение заголовка. Положение заголовка не применялось.");
                    return;
                }

                Outline outline = viewport.GetBoxOutline();
                if (outline == null || outline.MinimumPoint == null)
                {
                    AddWarning(warnings, "Не удалось определить габарит нового Viewport для копирования положения заголовка.");
                    return;
                }

                XYZ targetBottomLeft = outline.MinimumPoint;
                XYZ labelOffset = new XYZ(
                    sourcePlacement.TitlePoint.X - targetBottomLeft.X,
                    sourcePlacement.TitlePoint.Y - targetBottomLeft.Y,
                    0.0);

                ValidatePoint(labelOffset, "смещение заголовка Viewport с листа-образца");

                // Блок копирования положения заголовка с листа-образца.
                // Копируется абсолютная точка заголовка на листе, чтобы заголовок остался на том же месте.
                TryApplySourceLabelLineLength(viewport, sourcePlacement, warnings);
                viewport.LabelOffset = labelOffset;
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось скопировать положение заголовка Viewport с листа-образца: " + exception.Message);
            }
        }

        private SourceViewportPlacementData BuildSourceViewportPlacementData(Viewport sourceViewport)
        {
            if (sourceViewport == null)
            {
                throw new InvalidOperationException("Viewport на листе-образце недоступен.");
            }

            SourceViewportPlacementData data = new SourceViewportPlacementData();
            data.Center = sourceViewport.GetBoxCenter();
            data.ViewportTypeId = sourceViewport.GetTypeId();
            ValidatePoint(data.Center, "центр Viewport на листе-образце");

            if (IsViewportTitleHidden(sourceViewport))
            {
                data.HasTitlePoint = false;
                return data;
            }

            Outline outline = sourceViewport.GetBoxOutline();
            if (outline == null || outline.MinimumPoint == null)
            {
                data.HasTitlePoint = false;
                return data;
            }

            XYZ labelOffset = sourceViewport.LabelOffset;
            ValidatePoint(labelOffset, "смещение заголовка Viewport на листе-образце");
            TryReadSourceLabelLineLength(sourceViewport, data);

            XYZ sourceBottomLeft = outline.MinimumPoint;
            data.TitlePoint = new XYZ(
                sourceBottomLeft.X + labelOffset.X,
                sourceBottomLeft.Y + labelOffset.Y,
                0.0);
            ValidatePoint(data.TitlePoint, "точка заголовка Viewport на листе-образце");
            data.HasTitlePoint = true;
            return data;
        }

        private void TryReadSourceLabelLineLength(Viewport sourceViewport, SourceViewportPlacementData data)
        {
            if (sourceViewport == null || data == null)
            {
                return;
            }

            try
            {
                double labelLineLength = sourceViewport.LabelLineLength;
                if (IsFinite(labelLineLength) && labelLineLength > 0.0)
                {
                    data.LabelLineLength = labelLineLength;
                    data.HasLabelLineLength = true;
                }
            }
            catch
            {
                data.HasLabelLineLength = false;
            }
        }

        private void TryApplySourceLabelLineLength(
            Viewport viewport,
            SourceViewportPlacementData sourcePlacement,
            System.Collections.Generic.IList<string> warnings)
        {
            if (viewport == null || sourcePlacement == null || !sourcePlacement.HasLabelLineLength)
            {
                return;
            }

            try
            {
                // Длину линии задаем до LabelOffset: в Revit это свойство может смещать заголовок.
                viewport.LabelLineLength = sourcePlacement.LabelLineLength;
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось скопировать длину линии заголовка Viewport: " + exception.Message);
            }
        }

        private Viewport FindViewportOnSheet(Document document, ViewSheet sheet, View view)
        {
            if (document == null || sheet == null || view == null)
            {
                return null;
            }

            try
            {
                System.Collections.Generic.ICollection<ElementId> viewportIds = sheet.GetAllViewports();
                if (viewportIds != null)
                {
                    foreach (ElementId viewportId in viewportIds)
                    {
                        Viewport viewport = document.GetElement(viewportId) as Viewport;
                        if (viewport != null && RevitElementIdUtils.AreEqual(viewport.ViewId, view.Id))
                        {
                            return viewport;
                        }
                    }
                }
            }
            catch
            {
                // Если GetAllViewports недоступен для конкретного листа, ниже используется резервный поиск через collector.
            }

            FilteredElementCollector collector = new FilteredElementCollector(document, sheet.Id).OfClass(typeof(Viewport));
            foreach (Element element in collector)
            {
                Viewport viewport = element as Viewport;
                if (viewport != null && RevitElementIdUtils.AreEqual(viewport.ViewId, view.Id))
                {
                    return viewport;
                }
            }

            return null;
        }

        private void MoveViewportToCenter(Document document, Viewport viewport, XYZ targetCenter)
        {
            if (document == null || viewport == null || targetCenter == null)
            {
                return;
            }

            try
            {
                viewport.SetBoxCenter(targetCenter);
            }
            catch
            {
                XYZ currentCenter = viewport.GetBoxCenter();
                XYZ moveVector = targetCenter - currentCenter;
                if (moveVector.GetLength() > 1e-9)
                {
                    ElementTransformUtils.MoveElement(document, viewport.Id, moveVector);
                }
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

        private class SourceViewportPlacementData
        {
            public XYZ Center { get; set; }

            public XYZ TitlePoint { get; set; }

            public ElementId ViewportTypeId { get; set; }

            public bool HasTitlePoint { get; set; }

            public double LabelLineLength { get; set; }

            public bool HasLabelLineLength { get; set; }
        }

    }
}
