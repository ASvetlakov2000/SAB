using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using SAB.DoorWindowExplanations.Models;

namespace SAB.DoorWindowExplanations.Services
{
    public class DoorWindowViewCreationService
    {
        private const double MinimumDepthFeet = 1.0 / 304.8;

        public DoorWindowViewCreationResult CreateViews(
            Document document,
            DoorWindowOrientedBounds bounds,
            DoorWindowViewSettings settings,
            IList<DoorWindowViewTemplateItem> templates,
            IList<string> viewNames)
        {
            if (document == null || bounds == null || settings == null)
            {
                throw new ArgumentNullException("Не переданы данные для создания видов.");
            }

            if (viewNames == null || viewNames.Count != 3)
            {
                throw new InvalidOperationException("Не удалось подготовить три имени видов.");
            }

            DoorWindowRevitDataService dataService = new DoorWindowRevitDataService();
            ElementId sectionTypeId = dataService.GetSectionViewFamilyTypeId(document);

            double widthOffset = ToFeet(settings.WidthCropOffsetMm);
            double planDepthOffset = ToFeet(settings.PlanDepthCropOffsetMm);
            double frontVerticalOffset = ToFeet(settings.FrontVerticalCropOffsetMm);
            double sectionOffset = ToFeet(settings.SectionCropOffsetMm);
            double topProjectionDepth = Math.Max(ToFeet(settings.TopProjectionDepthMm), MinimumDepthFeet);
            double frontProjectionDepth = Math.Max(ToFeet(settings.FrontProjectionDepthMm), MinimumDepthFeet);
            double sectionProjectionDepth = Math.Max(ToFeet(settings.SectionProjectionDepthMm), MinimumDepthFeet);
            double sectionLineOverhang = ToFeet(settings.SectionMarkerExtensionMm);

            double commonMinWidth = bounds.MinWidth - widthOffset;
            double commonMaxWidth = bounds.MaxWidth + widthOffset;
            double frontMinHeight = bounds.MinHeight - frontVerticalOffset;
            double frontMaxHeight = bounds.MaxHeight + frontVerticalOffset;

            BoundingBoxXYZ topBox = CreateTopBox(
                bounds,
                commonMinWidth,
                commonMaxWidth,
                planDepthOffset,
                topProjectionDepth,
                settings.TopCutHeightPercent);
            BoundingBoxXYZ frontBox = CreateFrontBox(
                bounds,
                commonMinWidth,
                commonMaxWidth,
                frontMinHeight,
                frontMaxHeight,
                planDepthOffset,
                frontProjectionDepth,
                settings.ElevationSide);
            BoundingBoxXYZ sectionBox = CreateCenterSectionBox(
                bounds,
                frontMinHeight,
                frontMaxHeight,
                sectionOffset,
                sectionLineOverhang,
                sectionProjectionDepth);

            DoorWindowViewCreationResult result = new DoorWindowViewCreationResult();
            result.ModelAlignmentPoint = bounds.Origin +
                                         XYZ.BasisZ * ((bounds.MinHeight + bounds.MaxHeight) / 2.0);
            bool createAll = settings.WorkflowMode == DoorWindowWorkflowMode.FullExplication;
            if (createAll || settings.SingleViewKind == DoorWindowSingleViewKind.Top)
            {
                result.TopView = ViewSection.CreateSection(document, sectionTypeId, topBox);
                result.TopView.Name = viewNames[0];
                ApplyViewSettings(
                    document,
                    result.TopView,
                    settings.TopViewScale,
                    FindTemplateId(templates, settings.TopViewTemplateName),
                    settings.IsolateSelectedElement);
            }

            if (createAll || settings.SingleViewKind == DoorWindowSingleViewKind.Front)
            {
                result.FrontView = ViewSection.CreateSection(document, sectionTypeId, frontBox);
                result.FrontView.Name = viewNames[1];
                ApplyViewSettings(
                    document,
                    result.FrontView,
                    settings.FrontViewScale,
                    FindTemplateId(templates, settings.FrontViewTemplateName),
                    settings.IsolateSelectedElement);
                DisableAnnotationCrop(result.FrontView);
            }

            if (createAll || settings.SingleViewKind == DoorWindowSingleViewKind.Section)
            {
                result.SectionView = ViewSection.CreateSection(document, sectionTypeId, sectionBox);
                result.SectionView.Name = viewNames[2];
                ApplyViewSettings(
                    document,
                    result.SectionView,
                    settings.SectionViewScale,
                    FindTemplateId(templates, settings.SectionViewTemplateName),
                    settings.IsolateSelectedElement);
            }

            return result;
        }

        private BoundingBoxXYZ CreateTopBox(
            DoorWindowOrientedBounds bounds,
            double minWidth,
            double maxWidth,
            double depthOffset,
            double projectionDepth,
            double cutHeightPercent)
        {
            double normalizedPercent = Math.Max(0.0, Math.Min(100.0, cutHeightPercent)) / 100.0;
            double cutHeight = bounds.MinHeight +
                               (bounds.MaxHeight - bounds.MinHeight) * normalizedPercent;
            XYZ viewOrigin = bounds.Origin + XYZ.BasisZ * cutHeight;

            Transform transform = Transform.Identity;
            transform.Origin = viewOrigin;
            transform.BasisX = bounds.WidthDirection;
            transform.BasisY = bounds.FacingDirection.Negate();
            transform.BasisZ = XYZ.BasisZ.Negate();

            double minScreenDepth = -bounds.MaxDepth - depthOffset;
            double maxScreenDepth = -bounds.MinDepth + depthOffset;
            return CreateSectionBox(
                transform,
                minWidth,
                maxWidth,
                minScreenDepth,
                maxScreenDepth,
                0.0,
                projectionDepth);
        }

        private BoundingBoxXYZ CreateFrontBox(
            DoorWindowOrientedBounds bounds,
            double minWidth,
            double maxWidth,
            double minHeight,
            double maxHeight,
            double projectionOffset,
            double projectionDepth,
            DoorWindowElevationSide elevationSide)
        {
            bool isBackView = elevationSide == DoorWindowElevationSide.Back;
            XYZ viewOrigin = isBackView
                ? bounds.Origin + bounds.FacingDirection * (bounds.MinDepth - projectionOffset)
                : bounds.Origin + bounds.FacingDirection * (bounds.MaxDepth + projectionOffset);
            Transform transform = Transform.Identity;
            transform.Origin = viewOrigin;
            transform.BasisX = isBackView
                ? bounds.WidthDirection.Negate()
                : bounds.WidthDirection;
            transform.BasisY = XYZ.BasisZ;
            transform.BasisZ = isBackView
                ? bounds.FacingDirection
                : bounds.FacingDirection.Negate();

            double screenMinWidth = isBackView ? -maxWidth : minWidth;
            double screenMaxWidth = isBackView ? -minWidth : maxWidth;

            return CreateSectionBox(
                transform,
                screenMinWidth,
                screenMaxWidth,
                minHeight,
                maxHeight,
                0.0,
                projectionDepth);
        }

        private BoundingBoxXYZ CreateCenterSectionBox(
            DoorWindowOrientedBounds bounds,
            double frontMinHeight,
            double frontMaxHeight,
            double sectionOffset,
            double sectionLineOverhang,
            double projectionDepth)
        {
            double widthCenter = (bounds.MinWidth + bounds.MaxWidth) / 2.0;
            Transform transform = Transform.Identity;
            transform.Origin = bounds.Origin + bounds.WidthDirection * widthCenter;
            transform.BasisX = bounds.FacingDirection;
            transform.BasisY = XYZ.BasisZ;
            transform.BasisZ = bounds.WidthDirection;

            return CreateSectionBox(
                transform,
                bounds.MinDepth - sectionOffset,
                bounds.MaxDepth + sectionOffset,
                frontMinHeight - sectionLineOverhang,
                frontMaxHeight + sectionLineOverhang,
                0.0,
                projectionDepth);
        }

        private BoundingBoxXYZ CreateSectionBox(
            Transform transform,
            double minX,
            double maxX,
            double minY,
            double maxY,
            double minZ,
            double maxZ)
        {
            if (maxX - minX < MinimumDepthFeet ||
                maxY - minY < MinimumDepthFeet ||
                maxZ - minZ < MinimumDepthFeet)
            {
                throw new InvalidOperationException("Одна из границ создаваемого вида получилась вырожденной.");
            }

            BoundingBoxXYZ box = new BoundingBoxXYZ();
            box.Transform = transform;
            box.Min = new XYZ(minX, minY, minZ);
            box.Max = new XYZ(maxX, maxY, maxZ);
            box.Enabled = true;
            return box;
        }

        private void ApplyViewSettings(
            Document document,
            ViewSection view,
            int scale,
            ElementId templateId,
            bool isolateSelectedElement)
        {
            view.Scale = scale;
            view.CropBoxActive = true;
            view.CropBoxVisible = false;

            if (templateId != null && templateId != ElementId.InvalidElementId)
            {
                view.ViewTemplateId = templateId;
                return;
            }

            if (isolateSelectedElement)
            {
                ApplyDoorsAndWindowsOnlyVisibility(document, view);
            }
        }

        private void ApplyDoorsAndWindowsOnlyVisibility(Document document, View view)
        {
            foreach (Category category in document.Settings.Categories)
            {
                if (category == null || category.CategoryType != CategoryType.Model)
                {
                    continue;
                }

                int categoryId = category.Id.IntegerValue;
                bool keepVisible = categoryId == (int)BuiltInCategory.OST_Doors ||
                                   categoryId == (int)BuiltInCategory.OST_Windows ||
                                   categoryId == (int)BuiltInCategory.OST_Walls ||
                                   categoryId == (int)BuiltInCategory.OST_CurtainWallPanels ||
                                   categoryId == (int)BuiltInCategory.OST_CurtainWallMullions ||
                                   categoryId == (int)BuiltInCategory.OST_CurtainGrids ||
                                   categoryId == (int)BuiltInCategory.OST_CurtainGridsWall ||
                                   categoryId == (int)BuiltInCategory.OST_RvtLinks;

                try
                {
                    if (view.CanCategoryBeHidden(category.Id))
                    {
                        view.SetCategoryHidden(category.Id, !keepVisible);
                    }
                }
                catch
                {
                    // Некоторые внутренние категории Revit формально являются модельными,
                    // но не допускают управление видимостью на конкретном виде.
                }
            }
        }

        private void DisableAnnotationCrop(View view)
        {
            try
            {
                Parameter parameter = view.get_Parameter(BuiltInParameter.VIEWER_ANNOTATION_CROP_ACTIVE);
                if (parameter != null && !parameter.IsReadOnly)
                {
                    parameter.Set(0);
                }
            }
            catch
            {
                // Шаблон вида может управлять этой настройкой.
            }
        }

        private ElementId FindTemplateId(IList<DoorWindowViewTemplateItem> templates, string templateName)
        {
            if (templates == null || string.IsNullOrWhiteSpace(templateName))
            {
                return ElementId.InvalidElementId;
            }

            for (int i = 0; i < templates.Count; i++)
            {
                if (string.Equals(templates[i].Name, templateName, StringComparison.CurrentCultureIgnoreCase))
                {
                    return templates[i].Id;
                }
            }

            return ElementId.InvalidElementId;
        }

        private double ToFeet(double millimeters)
        {
            return UnitUtils.ConvertToInternalUnits(millimeters, UnitTypeId.Millimeters);
        }
    }
}
