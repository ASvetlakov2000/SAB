using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using SAB.DoorWindowExplanations.Models;

namespace SAB.DoorWindowExplanations.Services
{
    public class DoorWindowSheetPlacementService
    {
        private const double SheetMarginMm = 15.0;

        public ViewSheet CreateSheetAndPlaceViews(
            Document document,
            DoorWindowViewSettings settings,
            IList<DoorWindowNamedElementItem> titleBlockTypes,
            IList<DoorWindowNamedElementItem> viewportTypes,
            IList<DoorWindowViewCreationResult> viewGroups)
        {
            if (document == null || settings == null)
            {
                throw new ArgumentNullException("Не переданы данные для создания листа.");
            }

            if (viewGroups == null || viewGroups.Count == 0)
            {
                throw new InvalidOperationException("Нет видов для размещения на листе.");
            }

            DoorWindowNamedElementItem titleBlock = FindItem(titleBlockTypes, settings.TitleBlockTypeName);
            if (titleBlock == null || titleBlock.Id == null || titleBlock.Id == ElementId.InvalidElementId)
            {
                throw new InvalidOperationException("Выберите тип основной надписи для общего листа.");
            }

            ViewSheet sheet = ViewSheet.Create(document, titleBlock.Id);
            sheet.SheetNumber = GetUniqueSheetNumber(document, settings.SheetNumber, sheet.Id);
            sheet.Name = string.IsNullOrWhiteSpace(settings.SheetName)
                ? "Экспликации дверей и окон"
                : settings.SheetName.Trim();

            document.Regenerate();
            BoundingBoxUV outline = sheet.Outline;
            double margin = UnitUtils.ConvertToInternalUnits(SheetMarginMm, UnitTypeId.Millimeters);
            double startX = outline != null ? outline.Min.U + margin : margin;
            double startY = outline != null ? outline.Max.V - margin : -margin;
            double viewGap = UnitUtils.ConvertToInternalUnits(
                settings.ViewHorizontalStepMm,
                UnitTypeId.Millimeters);
            double elementGap = UnitUtils.ConvertToInternalUnits(
                settings.ElementVerticalStepMm,
                UnitTypeId.Millimeters);

            DoorWindowNamedElementItem viewportType = FindItem(
                viewportTypes,
                settings.ViewportTypeIdValue,
                settings.ViewportTypeName);
            ElementId viewportTypeId = viewportType != null ? viewportType.Id : ElementId.InvalidElementId;

            double rowTopY = startY;
            for (int row = 0; row < viewGroups.Count; row++)
            {
                DoorWindowViewCreationResult group = viewGroups[row];
                if (group == null || group.TopView == null ||
                    group.FrontView == null || group.SectionView == null)
                {
                    throw new InvalidOperationException("Одна из групп не содержит три вида.");
                }

                ViewportLayoutData top = CreateViewportLayoutData(
                    document, sheet, group.TopView, viewportTypeId, startX, rowTopY);
                ViewportLayoutData front = CreateViewportLayoutData(
                    document, sheet, group.FrontView, viewportTypeId, startX, rowTopY);
                ViewportLayoutData section = CreateViewportLayoutData(
                    document, sheet, group.SectionView, viewportTypeId, startX, rowTopY);

                // План стоит сверху, фасад под ним, а поперечный разрез — справа.
                double elevationAxisX = startX + Math.Max(top.Width, front.Width) / 2.0;
                double lowerViewsTopY = rowTopY - top.Height - viewGap;
                double lowerViewsCenterY = lowerViewsTopY - Math.Max(front.Height, section.Height) / 2.0;

                MoveViewport(
                    document,
                    top.Viewport,
                    new XYZ(elevationAxisX, rowTopY - top.Height / 2.0, 0.0));
                MoveViewport(
                    document,
                    front.Viewport,
                    new XYZ(elevationAxisX, lowerViewsCenterY, 0.0));
                MoveViewport(
                    document,
                    section.Viewport,
                    new XYZ(
                        elevationAxisX + front.Width / 2.0 + viewGap + section.Width / 2.0,
                        lowerViewsCenterY,
                        0.0));

                document.Regenerate();
                AlignSectionToFrontByModelPoint(
                    document,
                    group.FrontView,
                    front.Viewport,
                    group.SectionView,
                    section.Viewport,
                    group.ModelAlignmentPoint);

                document.Regenerate();
                ShiftLowerViewsBelowTop(
                    document,
                    front.Viewport,
                    section.Viewport,
                    lowerViewsTopY);

                document.Regenerate();
                PlaceViewportTitle(top.Viewport, settings);
                PlaceViewportTitle(front.Viewport, settings);
                PlaceViewportTitle(section.Viewport, settings);

                Outline topOutline = GetViewportOutline(top.Viewport);
                Outline frontOutline = GetViewportOutline(front.Viewport);
                Outline sectionOutline = GetViewportOutline(section.Viewport);
                rowTopY = Math.Min(
                    topOutline.MinimumPoint.Y,
                    Math.Min(frontOutline.MinimumPoint.Y, sectionOutline.MinimumPoint.Y)) - elementGap;
            }

            return sheet;
        }

        private void AlignSectionToFrontByModelPoint(
            Document document,
            ViewSection frontView,
            Viewport frontViewport,
            ViewSection sectionView,
            Viewport sectionViewport,
            XYZ modelPoint)
        {
            XYZ frontPointOnSheet;
            XYZ sectionPointOnSheet;
            if (!TryGetModelPointOnSheet(frontView, frontViewport, modelPoint, out frontPointOnSheet) ||
                !TryGetModelPointOnSheet(sectionView, sectionViewport, modelPoint, out sectionPointOnSheet))
            {
                throw new InvalidOperationException(
                    "Не удалось вычислить привязку двери между фасадом и разрезом.");
            }

            double deltaY = frontPointOnSheet.Y - sectionPointOnSheet.Y;
            if (Math.Abs(deltaY) > 1e-9)
            {
                ElementTransformUtils.MoveElement(
                    document,
                    sectionViewport.Id,
                    new XYZ(0.0, deltaY, 0.0));
            }
        }

        private bool TryGetModelPointOnSheet(
            ViewSection view,
            Viewport viewport,
            XYZ modelPoint,
            out XYZ pointOnSheet)
        {
            pointOnSheet = null;
            if (view == null || viewport == null || modelPoint == null)
            {
                return false;
            }

            if (TryGetModelPointOnSheetWithRevit2023Transforms(
                view,
                viewport,
                modelPoint,
                out pointOnSheet))
            {
                return true;
            }

            return TryGetModelPointOnSheetWithLegacyViewCoordinates(
                view,
                viewport,
                modelPoint,
                out pointOnSheet);
        }

        private bool TryGetModelPointOnSheetWithRevit2023Transforms(
            ViewSection view,
            Viewport viewport,
            XYZ modelPoint,
            out XYZ pointOnSheet)
        {
            pointOnSheet = null;

            try
            {
                System.Reflection.MethodInfo hasViewTransformsMethod =
                    view.GetType().GetMethod("HasViewTransforms", Type.EmptyTypes);
                System.Reflection.MethodInfo hasViewportTransformsMethod =
                    viewport.GetType().GetMethod("HasViewportTransforms", Type.EmptyTypes);
                System.Reflection.MethodInfo getViewTransformsMethod =
                    view.GetType().GetMethod("GetModelToProjectionTransforms", Type.EmptyTypes);
                System.Reflection.MethodInfo getProjectionToSheetMethod =
                    viewport.GetType().GetMethod("GetProjectionToSheetTransform", Type.EmptyTypes);

                if (hasViewTransformsMethod == null ||
                    hasViewportTransformsMethod == null ||
                    getViewTransformsMethod == null ||
                    getProjectionToSheetMethod == null ||
                    !(bool)hasViewTransformsMethod.Invoke(view, null) ||
                    !(bool)hasViewportTransformsMethod.Invoke(viewport, null))
                {
                    return false;
                }

                System.Collections.IList viewTransforms =
                    getViewTransformsMethod.Invoke(view, null) as System.Collections.IList;
                if (viewTransforms == null || viewTransforms.Count == 0 || viewTransforms[0] == null)
                {
                    return false;
                }

                object transformWithBoundary = viewTransforms[0];
                System.Reflection.MethodInfo getModelToProjectionMethod =
                    transformWithBoundary.GetType().GetMethod(
                        "GetModelToProjectionTransform",
                        Type.EmptyTypes);
                if (getModelToProjectionMethod == null)
                {
                    return false;
                }

                Transform modelToProjection =
                    getModelToProjectionMethod.Invoke(transformWithBoundary, null) as Transform;
                Transform projectionToSheet =
                    getProjectionToSheetMethod.Invoke(viewport, null) as Transform;
                if (modelToProjection == null || projectionToSheet == null)
                {
                    return false;
                }

                pointOnSheet = projectionToSheet.OfPoint(modelToProjection.OfPoint(modelPoint));
                return pointOnSheet != null;
            }
            catch (System.Reflection.TargetInvocationException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private bool TryGetModelPointOnSheetWithLegacyViewCoordinates(
            ViewSection view,
            Viewport viewport,
            XYZ modelPoint,
            out XYZ pointOnSheet)
        {
            pointOnSheet = null;

            BoundingBoxUV viewOutline = view.Outline;
            XYZ viewOrigin = view.Origin;
            XYZ rightDirection = view.RightDirection;
            XYZ upDirection = view.UpDirection;
            XYZ viewportCenter = viewport.GetBoxCenter();
            if (viewOutline == null ||
                viewOutline.Min == null ||
                viewOutline.Max == null ||
                viewOrigin == null ||
                rightDirection == null ||
                upDirection == null ||
                viewportCenter == null ||
                view.Scale <= 0)
            {
                return false;
            }

            XYZ modelOffset = modelPoint - viewOrigin;
            double pointU = modelOffset.DotProduct(rightDirection) / view.Scale;
            double pointV = modelOffset.DotProduct(upDirection) / view.Scale;
            double outlineCenterU = (viewOutline.Min.U + viewOutline.Max.U) / 2.0;
            double outlineCenterV = (viewOutline.Min.V + viewOutline.Max.V) / 2.0;

            pointOnSheet = new XYZ(
                viewportCenter.X + pointU - outlineCenterU,
                viewportCenter.Y + pointV - outlineCenterV,
                viewportCenter.Z);
            return true;
        }

        private void ShiftLowerViewsBelowTop(
            Document document,
            Viewport frontViewport,
            Viewport sectionViewport,
            double maximumY)
        {
            Outline frontOutline = GetViewportOutline(frontViewport);
            Outline sectionOutline = GetViewportOutline(sectionViewport);
            double overflow = Math.Max(
                frontOutline.MaximumPoint.Y,
                sectionOutline.MaximumPoint.Y) - maximumY;
            if (overflow <= 1e-9)
            {
                return;
            }

            XYZ shift = new XYZ(0.0, -overflow, 0.0);
            ElementTransformUtils.MoveElement(document, frontViewport.Id, shift);
            ElementTransformUtils.MoveElement(document, sectionViewport.Id, shift);
        }

        private void PlaceViewportTitle(Viewport viewport, DoorWindowViewSettings settings)
        {
            Outline outline = GetViewportOutline(viewport);
            double minimumX = outline.MinimumPoint.X;
            double minimumY = outline.MinimumPoint.Y;
            double maximumX = outline.MaximumPoint.X;
            double maximumY = outline.MaximumPoint.Y;

            double anchorX = minimumX;
            double anchorY = minimumY;
            bool isCentered = settings.ViewTitleAnchor == DoorWindowViewTitleAnchor.BottomCenter ||
                              settings.ViewTitleAnchor == DoorWindowViewTitleAnchor.TopCenter;
            if (isCentered)
            {
                anchorX = (minimumX + maximumX) / 2.0;
            }
            else if (settings.ViewTitleAnchor == DoorWindowViewTitleAnchor.BottomRight)
            {
                anchorX = maximumX;
            }

            double offsetX = UnitUtils.ConvertToInternalUnits(
                settings.ViewTitleOffsetXmm,
                UnitTypeId.Millimeters);
            double offsetY = UnitUtils.ConvertToInternalUnits(
                settings.ViewTitleOffsetYmm,
                UnitTypeId.Millimeters);
            if (settings.ViewTitleAnchor == DoorWindowViewTitleAnchor.TopCenter)
            {
                anchorY = maximumY;
                offsetY = -offsetY;
            }

            viewport.LabelOffset = new XYZ(
                anchorX + offsetX - minimumX,
                anchorY + offsetY - minimumY,
                0.0);

            if (!isCentered)
            {
                return;
            }

            Document document = viewport.Document;
            if (document == null)
            {
                return;
            }

            document.Regenerate();
            Outline labelOutline;
            try
            {
                labelOutline = viewport.GetLabelOutline();
            }
            catch
            {
                // Тип без видимого заголовка не требует центрирования.
                return;
            }

            if (labelOutline == null ||
                labelOutline.MinimumPoint == null ||
                labelOutline.MaximumPoint == null)
            {
                return;
            }

            double targetCenterX = anchorX + offsetX;
            double correctionX = CalculateLabelCenterCorrection(labelOutline, targetCenterX);
            if (Math.Abs(correctionX) <= 1e-9)
            {
                return;
            }

            XYZ currentOffset = viewport.LabelOffset;
            viewport.LabelOffset = new XYZ(
                currentOffset.X + correctionX,
                currentOffset.Y,
                currentOffset.Z);
            document.Regenerate();
        }

        private double CalculateLabelCenterCorrection(Outline labelOutline, double targetCenterX)
        {
            if (labelOutline == null ||
                labelOutline.MinimumPoint == null ||
                labelOutline.MaximumPoint == null)
            {
                return 0.0;
            }

            double currentLabelCenterX =
                (labelOutline.MinimumPoint.X + labelOutline.MaximumPoint.X) / 2.0;
            return targetCenterX - currentLabelCenterX;
        }

        private Outline GetViewportOutline(Viewport viewport)
        {
            Outline outline = viewport != null ? viewport.GetBoxOutline() : null;
            if (outline == null || outline.MinimumPoint == null || outline.MaximumPoint == null)
            {
                throw new InvalidOperationException("Не удалось определить границы видового экрана.");
            }

            return outline;
        }

        private ViewportLayoutData CreateViewportLayoutData(
            Document document,
            ViewSheet sheet,
            ViewSection view,
            ElementId viewportTypeId,
            double temporaryX,
            double temporaryY)
        {
            if (!Viewport.CanAddViewToSheet(document, sheet.Id, view.Id))
            {
                throw new InvalidOperationException(
                    "Вид \"" + view.Name + "\" нельзя разместить на создаваемом листе.");
            }

            Viewport viewport = Viewport.Create(
                document,
                sheet.Id,
                view.Id,
                new XYZ(temporaryX, temporaryY, 0.0));
            ApplyViewportType(viewport, viewportTypeId);

            document.Regenerate();

            double width;
            double height;
            if (!TryGetViewportSize(viewport, out width, out height))
            {
                throw new InvalidOperationException(
                    "Не удалось определить размер видового экрана \"" + view.Name + "\".");
            }

            return new ViewportLayoutData(viewport, width, height);
        }

        private void ApplyViewportType(Viewport viewport, ElementId viewportTypeId)
        {
            if (viewport == null || viewportTypeId == null || viewportTypeId.IntegerValue < 0)
            {
                return;
            }

            if (!viewport.CanHaveTypeAssigned() || !viewport.IsValidType(viewportTypeId))
            {
                throw new InvalidOperationException(
                    "Выбранный тип заголовка нельзя назначить видовому экрану.");
            }

            if (viewport.GetTypeId().IntegerValue != viewportTypeId.IntegerValue)
            {
                viewport.ChangeTypeId(viewportTypeId);
            }
        }

        private void MoveViewport(Document document, Viewport viewport, XYZ targetCenter)
        {
            XYZ currentCenter = viewport.GetBoxCenter();
            XYZ target = new XYZ(targetCenter.X, targetCenter.Y, currentCenter.Z);
            XYZ moveVector = target - currentCenter;
            if (moveVector.GetLength() > 1e-9)
            {
                ElementTransformUtils.MoveElement(document, viewport.Id, moveVector);
            }
        }

        private bool TryGetViewportSize(
            Viewport viewport,
            out double width,
            out double height)
        {
            width = 0.0;
            height = 0.0;
            if (viewport == null)
            {
                return false;
            }

            Outline viewportOutline = viewport.GetBoxOutline();
            if (viewportOutline == null ||
                viewportOutline.MinimumPoint == null ||
                viewportOutline.MaximumPoint == null)
            {
                return false;
            }

            width = viewportOutline.MaximumPoint.X - viewportOutline.MinimumPoint.X;
            height = viewportOutline.MaximumPoint.Y - viewportOutline.MinimumPoint.Y;
            return width > 1e-9 && height > 1e-9;
        }

        private DoorWindowNamedElementItem FindItem(
            IList<DoorWindowNamedElementItem> items,
            string name)
        {
            if (items == null || items.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (string.Equals(items[i].Name, name, StringComparison.CurrentCultureIgnoreCase))
                    {
                        return items[i];
                    }
                }
            }

            return items[0];
        }

        private DoorWindowNamedElementItem FindItem(
            IList<DoorWindowNamedElementItem> items,
            int itemIdValue,
            string name)
        {
            if (items == null || items.Count == 0)
            {
                return null;
            }

            if (itemIdValue >= 0)
            {
                for (int index = 0; index < items.Count; index++)
                {
                    DoorWindowNamedElementItem item = items[index];
                    if (item != null && item.Id != null && item.Id.IntegerValue == itemIdValue)
                    {
                        return item;
                    }
                }
            }

            return FindItem(items, name);
        }

        private string GetUniqueSheetNumber(Document document, string requestedNumber, ElementId currentSheetId)
        {
            string baseNumber = string.IsNullOrWhiteSpace(requestedNumber) ? "ЭД-1" : requestedNumber.Trim();
            HashSet<string> existingNumbers = new HashSet<string>(
                new FilteredElementCollector(document)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(sheet => sheet.Id != currentSheetId)
                    .Select(sheet => sheet.SheetNumber),
                StringComparer.CurrentCultureIgnoreCase);

            if (!existingNumbers.Contains(baseNumber))
            {
                return baseNumber;
            }

            int suffix = 2;
            string candidate;
            do
            {
                candidate = baseNumber + "." + suffix;
                suffix++;
            }
            while (existingNumbers.Contains(candidate));

            return candidate;
        }

        private class ViewportLayoutData
        {
            public ViewportLayoutData(Viewport viewport, double width, double height)
            {
                Viewport = viewport;
                Width = width;
                Height = height;
            }

            public Viewport Viewport { get; private set; }

            public double Width { get; private set; }

            public double Height { get; private set; }
        }
    }
}
