using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using SAB.ViewTemplateGraphics.Models;

namespace SAB.ViewTemplateGraphics.Services
{
    public class ViewTemplateGraphicsApplyService
    {
        public ApplyViewTemplateGraphicsResult Apply(
            Document document,
            ViewTemplateGraphicsData data,
            IList<int> targetTemplateIdValues)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            if (data == null)
            {
                throw new ArgumentNullException("data");
            }

            if (targetTemplateIdValues == null || targetTemplateIdValues.Count == 0)
            {
                throw new InvalidOperationException("Не выбраны шаблоны видов для применения настроек.");
            }

            if (!data.IsDirty)
            {
                throw new InvalidOperationException("Нет изменённых настроек для применения.");
            }

            List<View> targetTemplates = ResolveTargetTemplates(document, targetTemplateIdValues);
            ApplyViewTemplateGraphicsResult result = new ApplyViewTemplateGraphicsResult();

            TransactionGroup transactionGroup = new TransactionGroup(document, "SAB Пакетное редактирование шаблонов видов");
            transactionGroup.Start();

            try
            {
                for (int i = 0; i < targetTemplates.Count; i++)
                {
                    View targetTemplate = targetTemplates[i];
                    Transaction transaction = new Transaction(document, "Настройки шаблона: " + targetTemplate.Name);
                    transaction.Start();

                    try
                    {
                        int changedSettingCount = ApplyToTemplate(targetTemplate, data, result.Warnings);
                        transaction.Commit();
                        result.ProcessedTemplateCount++;
                        result.ChangedSettingCount += changedSettingCount;
                    }
                    catch (Exception exception)
                    {
                        if (transaction.GetStatus() == TransactionStatus.Started)
                        {
                            transaction.RollBack();
                        }

                        throw new InvalidOperationException(
                            "Не удалось применить настройки к шаблону «" + targetTemplate.Name + "».\n\n" + exception.Message,
                            exception);
                    }
                }

                transactionGroup.Assimilate();
                return result;
            }
            catch
            {
                if (transactionGroup.GetStatus() == TransactionStatus.Started)
                {
                    transactionGroup.RollBack();
                }

                throw;
            }
        }

        private static List<View> ResolveTargetTemplates(Document document, IList<int> targetTemplateIdValues)
        {
            List<View> result = new List<View>();
            HashSet<int> uniqueIds = new HashSet<int>();

            for (int i = 0; i < targetTemplateIdValues.Count; i++)
            {
                int idValue = targetTemplateIdValues[i];
                if (!uniqueIds.Add(idValue))
                {
                    continue;
                }

                View view = document.GetElement(new ElementId(idValue)) as View;
                if (view == null || !view.IsTemplate)
                {
                    throw new InvalidOperationException("Шаблон вида с Id " + idValue + " не найден в документе.");
                }

                result.Add(view);
            }

            return result;
        }

        private int ApplyToTemplate(View targetTemplate, ViewTemplateGraphicsData data, IList<string> warnings)
        {
            int changedSettingCount = 0;

            if (!ViewTemplateGraphicsDataService.AreGraphicsOverridesAllowed(targetTemplate))
            {
                AddUniqueWarning(
                    warnings,
                    "Шаблон «" + targetTemplate.Name + "» (" + targetTemplate.ViewType + ") пропущен: этот тип вида не поддерживает «Переопределение видимости/графики».");
                return 0;
            }

            changedSettingCount += ApplyTemplateSectionStates(targetTemplate, data);
            changedSettingCount += ApplyCategoryTab(targetTemplate, data.ModelCategories, warnings);
            changedSettingCount += ApplyCategoryTab(targetTemplate, data.AnnotationCategories, warnings);
            changedSettingCount += ApplyCategoryTab(targetTemplate, data.AnalyticalCategories, warnings);
            changedSettingCount += ApplyCategoryTab(targetTemplate, data.ImportedCategories, warnings);
            changedSettingCount += ApplyFilters(targetTemplate, data, warnings);
            changedSettingCount += ApplyWorksets(targetTemplate, data.Worksets);
            changedSettingCount += ApplyRevitLinks(targetTemplate, data.RevitLinks, warnings);

            return changedSettingCount;
        }

        private static int ApplyTemplateSectionStates(View targetTemplate, ViewTemplateGraphicsData data)
        {
            TemplateSectionState[] sections =
            {
                data.ModelCategories.Section,
                data.AnnotationCategories.Section,
                data.AnalyticalCategories.Section,
                data.ImportedCategories.Section,
                data.FiltersSection,
                data.WorksetsSection,
                data.RevitLinksSection
            };

            bool hasChanges = false;
            for (int i = 0; i < sections.Length; i++)
            {
                if (sections[i].IsModified)
                {
                    hasChanges = true;
                    break;
                }
            }

            if (!hasChanges)
            {
                return 0;
            }

            ICollection<ElementId> existingNonControlledIds = targetTemplate.GetNonControlledTemplateParameterIds();
            Dictionary<int, ElementId> nonControlledIdsByValue = new Dictionary<int, ElementId>();
            if (existingNonControlledIds != null)
            {
                foreach (ElementId parameterId in existingNonControlledIds)
                {
                    if (parameterId != null)
                    {
                        nonControlledIdsByValue[parameterId.IntegerValue] = parameterId;
                    }
                }
            }

            int changedSettingCount = 0;
            for (int i = 0; i < sections.Length; i++)
            {
                TemplateSectionState section = sections[i];
                if (!section.IsModified)
                {
                    continue;
                }

                if (section.IsIncluded)
                {
                    nonControlledIdsByValue.Remove(section.ParameterIdValue);
                }
                else
                {
                    nonControlledIdsByValue[section.ParameterIdValue] = new ElementId(section.ParameterIdValue);
                }

                changedSettingCount++;
            }

            List<ElementId> updatedNonControlledIds = new List<ElementId>(nonControlledIdsByValue.Values);
            targetTemplate.SetNonControlledTemplateParameterIds(updatedNonControlledIds);
            return changedSettingCount;
        }

        private int ApplyCategoryTab(View targetTemplate, CategoryTabData categoryTab, IList<string> warnings)
        {
            int changedSettingCount = 0;

            if (categoryTab.IsGroupVisibilityModified)
            {
                SetCategoryGroupVisibility(targetTemplate, categoryTab.Group, categoryTab.IsGroupVisible);
                changedSettingCount++;
            }

            for (int i = 0; i < categoryTab.Rows.Count; i++)
            {
                CategoryOverrideRow row = categoryTab.Rows[i];
                if (!row.IsModified)
                {
                    continue;
                }

                ElementId categoryId = new ElementId(row.CategoryIdValue);

                if (row.IsVisibilityModified)
                {
                    bool canHideCategory = false;
                    try
                    {
                        canHideCategory = targetTemplate.CanCategoryBeHidden(categoryId);
                    }
                    catch
                    {
                        canHideCategory = false;
                    }

                    if (canHideCategory)
                    {
                        targetTemplate.SetCategoryHidden(categoryId, !row.IsVisible);
                        changedSettingCount++;
                    }
                    else
                    {
                        AddUniqueWarning(
                            warnings,
                            "Шаблон «" + targetTemplate.Name + "»: категория «" + row.Name + "» не поддерживает управление видимостью для этого типа вида.");
                    }
                }

                if (row.Graphics.IsModified)
                {
                    OverrideGraphicSettings targetOverrides = null;
                    try
                    {
                        targetOverrides = targetTemplate.GetCategoryOverrides(categoryId);
                        changedSettingCount += ApplyGraphicChanges(
                            targetOverrides,
                            row.Graphics,
                            row.SupportsCut,
                            row.SupportsSurfacePatterns,
                            row.SupportsTransparency,
                            row.SupportsDetailLevel);
                        targetTemplate.SetCategoryOverrides(categoryId, targetOverrides);
                    }
                    catch (Autodesk.Revit.Exceptions.ArgumentException)
                    {
                        AddUniqueWarning(
                            warnings,
                            "Шаблон «" + targetTemplate.Name + "»: графические переопределения категории «" + row.Name + "» недоступны для этого типа вида.");
                    }
                    finally
                    {
                        if (targetOverrides != null)
                        {
                            targetOverrides.Dispose();
                        }
                    }
                }
            }

            return changedSettingCount;
        }

        private int ApplyFilters(
            View targetTemplate,
            ViewTemplateGraphicsData data,
            IList<string> warnings)
        {
            int changedSettingCount = 0;

            HashSet<int> currentFilterIds = GetCurrentFilterIds(targetTemplate);
            for (int i = 0; i < data.Filters.Count; i++)
            {
                FilterOverrideRow row = data.Filters[i];
                ElementId filterId = new ElementId(row.FilterIdValue);
                bool isCurrentlyIncluded = currentFilterIds.Contains(row.FilterIdValue);

                if (row.IsIncludedModified)
                {
                    if (row.IsIncluded && !isCurrentlyIncluded)
                    {
                        try
                        {
                            targetTemplate.AddFilter(filterId);
                            currentFilterIds.Add(row.FilterIdValue);
                            isCurrentlyIncluded = true;
                            changedSettingCount++;
                        }
                        catch (Autodesk.Revit.Exceptions.ArgumentException exception)
                        {
                            AddUniqueWarning(
                                warnings,
                                "Шаблон «" + targetTemplate.Name + "»: фильтр «" + row.Name + "» не удалось добавить. " + exception.Message);
                        }
                    }
                    else if (!row.IsIncluded && isCurrentlyIncluded)
                    {
                        try
                        {
                            targetTemplate.RemoveFilter(filterId);
                            currentFilterIds.Remove(row.FilterIdValue);
                            isCurrentlyIncluded = false;
                            changedSettingCount++;
                        }
                        catch (Autodesk.Revit.Exceptions.ArgumentException exception)
                        {
                            AddUniqueWarning(
                                warnings,
                                "Шаблон «" + targetTemplate.Name + "»: фильтр «" + row.Name + "» не удалось удалить. " + exception.Message);
                        }
                    }
                }

                if (row.IsIncludedModified && !row.IsIncluded)
                {
                    continue;
                }

                if (!isCurrentlyIncluded)
                {
                    // Наличие фильтра не меняем косвенно. Для добавления пользователь должен
                    // явно установить флажок «В шаблоне» из смешанного или выключенного состояния.
                    continue;
                }

                if (row.IsEnabledModified)
                {
                    targetTemplate.SetIsFilterEnabled(filterId, row.IsEnabled);
                    changedSettingCount++;
                }

                if (row.IsVisibilityModified)
                {
                    targetTemplate.SetFilterVisibility(filterId, row.IsVisible);
                    changedSettingCount++;
                }

                if (row.Graphics.IsModified)
                {
                    OverrideGraphicSettings targetOverrides = targetTemplate.GetFilterOverrides(filterId);
                    try
                    {
                        changedSettingCount += ApplyGraphicChanges(
                            targetOverrides,
                            row.Graphics,
                            true,
                            true,
                            true,
                            false);
                        targetTemplate.SetFilterOverrides(filterId, targetOverrides);
                    }
                    finally
                    {
                        if (targetOverrides != null)
                        {
                            targetOverrides.Dispose();
                        }
                    }
                }
            }

            if (data.IsFilterOrderModified)
            {
                List<ElementId> desiredFilters = new List<ElementId>();
                for (int i = 0; i < data.Filters.Count; i++)
                {
                    FilterOverrideRow row = data.Filters[i];
                    if (currentFilterIds.Contains(row.FilterIdValue))
                    {
                        desiredFilters.Add(new ElementId(row.FilterIdValue));
                    }
                }

                SetOrderedFilters(targetTemplate, desiredFilters);
                changedSettingCount++;
            }

            return changedSettingCount;
        }

        private int ApplyWorksets(View targetTemplate, IList<WorksetOverrideRow> worksets)
        {
            int changedSettingCount = 0;
            for (int i = 0; i < worksets.Count; i++)
            {
                WorksetOverrideRow row = worksets[i];
                if (!row.IsModified)
                {
                    continue;
                }

                targetTemplate.SetWorksetVisibility(new WorksetId(row.WorksetIdValue), row.Visibility);
                changedSettingCount++;
            }

            return changedSettingCount;
        }

        private static int ApplyRevitLinks(
            View targetTemplate,
            IList<RevitLinkInfo> links,
            IList<string> warnings)
        {
            RevitLinkGraphicsReflectionService reflectionService = new RevitLinkGraphicsReflectionService();
            int changedSettingCount = 0;

            for (int i = 0; i < links.Count; i++)
            {
                if (!links[i].IsModified)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(links[i].VisibilityTypeName) &&
                    links[i].VisibilityTypeName.IndexOf("Custom", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AddUniqueWarning(
                        warnings,
                        "Шаблон «" + targetTemplate.Name + "», связь «" + links[i].Name + "»: режим «Пользовательские» доступен для чтения, но Autodesk Revit API 2024 запрещает его программную установку. Строка пропущена.");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(links[i].VisibilityTypeName) &&
                    links[i].VisibilityTypeName.IndexOf("Linked", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    links[i].LinkedViewIdValue == ElementId.InvalidElementId.IntegerValue)
                {
                    AddUniqueWarning(
                        warnings,
                        "Шаблон «" + targetTemplate.Name + "», связь «" + links[i].Name + "»: для режима «По связанному виду» не выбран связанный вид. Строка пропущена.");
                    continue;
                }

                changedSettingCount += reflectionService.ApplyOverrides(targetTemplate, links[i]);
            }

            return changedSettingCount;
        }

        private static int ApplyGraphicChanges(
            OverrideGraphicSettings target,
            GraphicOverrideData patch,
            bool supportsCut,
            bool supportsSurfacePatterns,
            bool supportsTransparency,
            bool supportsDetailLevel)
        {
            if (target == null || patch == null)
            {
                return 0;
            }

            int changedSettingCount = 0;

            if (patch.IsPropertyModified(GraphicOverrideData.ProjectionLineWeightProperty))
            {
                target.SetProjectionLineWeight(patch.ProjectionLineWeight);
                changedSettingCount++;
            }

            if (patch.IsPropertyModified(GraphicOverrideData.ProjectionLineColorValueProperty))
            {
                target.SetProjectionLineColor(CreateColor(patch.ProjectionLineColorValue));
                changedSettingCount++;
            }

            if (patch.IsPropertyModified(GraphicOverrideData.ProjectionLinePatternIdProperty))
            {
                target.SetProjectionLinePatternId(new ElementId(patch.ProjectionLinePatternId));
                changedSettingCount++;
            }

            if (supportsSurfacePatterns)
            {
                if (patch.IsPropertyModified(GraphicOverrideData.SurfaceForegroundPatternVisibleProperty))
                {
                    target.SetSurfaceForegroundPatternVisible(patch.SurfaceForegroundPatternVisible);
                    changedSettingCount++;
                }

                if (patch.IsPropertyModified(GraphicOverrideData.SurfaceForegroundPatternIdProperty))
                {
                    target.SetSurfaceForegroundPatternId(new ElementId(patch.SurfaceForegroundPatternId));
                    changedSettingCount++;
                }

                if (patch.IsPropertyModified(GraphicOverrideData.SurfaceForegroundPatternColorValueProperty))
                {
                    target.SetSurfaceForegroundPatternColor(CreateColor(patch.SurfaceForegroundPatternColorValue));
                    changedSettingCount++;
                }

                if (patch.IsPropertyModified(GraphicOverrideData.SurfaceBackgroundPatternVisibleProperty))
                {
                    target.SetSurfaceBackgroundPatternVisible(patch.SurfaceBackgroundPatternVisible);
                    changedSettingCount++;
                }

                if (patch.IsPropertyModified(GraphicOverrideData.SurfaceBackgroundPatternIdProperty))
                {
                    target.SetSurfaceBackgroundPatternId(new ElementId(patch.SurfaceBackgroundPatternId));
                    changedSettingCount++;
                }

                if (patch.IsPropertyModified(GraphicOverrideData.SurfaceBackgroundPatternColorValueProperty))
                {
                    target.SetSurfaceBackgroundPatternColor(CreateColor(patch.SurfaceBackgroundPatternColorValue));
                    changedSettingCount++;
                }
            }

            if (supportsTransparency && patch.IsPropertyModified(GraphicOverrideData.TransparencyProperty))
            {
                target.SetSurfaceTransparency(patch.Transparency);
                changedSettingCount++;
            }

            if (supportsCut)
            {
                if (patch.IsPropertyModified(GraphicOverrideData.CutLineWeightProperty))
                {
                    target.SetCutLineWeight(patch.CutLineWeight);
                    changedSettingCount++;
                }

                if (patch.IsPropertyModified(GraphicOverrideData.CutLineColorValueProperty))
                {
                    target.SetCutLineColor(CreateColor(patch.CutLineColorValue));
                    changedSettingCount++;
                }

                if (patch.IsPropertyModified(GraphicOverrideData.CutLinePatternIdProperty))
                {
                    target.SetCutLinePatternId(new ElementId(patch.CutLinePatternId));
                    changedSettingCount++;
                }

                if (patch.IsPropertyModified(GraphicOverrideData.CutForegroundPatternVisibleProperty))
                {
                    target.SetCutForegroundPatternVisible(patch.CutForegroundPatternVisible);
                    changedSettingCount++;
                }

                if (patch.IsPropertyModified(GraphicOverrideData.CutForegroundPatternIdProperty))
                {
                    target.SetCutForegroundPatternId(new ElementId(patch.CutForegroundPatternId));
                    changedSettingCount++;
                }

                if (patch.IsPropertyModified(GraphicOverrideData.CutForegroundPatternColorValueProperty))
                {
                    target.SetCutForegroundPatternColor(CreateColor(patch.CutForegroundPatternColorValue));
                    changedSettingCount++;
                }

                if (patch.IsPropertyModified(GraphicOverrideData.CutBackgroundPatternVisibleProperty))
                {
                    target.SetCutBackgroundPatternVisible(patch.CutBackgroundPatternVisible);
                    changedSettingCount++;
                }

                if (patch.IsPropertyModified(GraphicOverrideData.CutBackgroundPatternIdProperty))
                {
                    target.SetCutBackgroundPatternId(new ElementId(patch.CutBackgroundPatternId));
                    changedSettingCount++;
                }

                if (patch.IsPropertyModified(GraphicOverrideData.CutBackgroundPatternColorValueProperty))
                {
                    target.SetCutBackgroundPatternColor(CreateColor(patch.CutBackgroundPatternColorValue));
                    changedSettingCount++;
                }
            }

            if (patch.IsPropertyModified(GraphicOverrideData.HalftoneProperty))
            {
                target.SetHalftone(patch.Halftone);
                changedSettingCount++;
            }

            if (supportsDetailLevel && patch.IsPropertyModified(GraphicOverrideData.DetailLevelProperty))
            {
                target.SetDetailLevel(patch.DetailLevel);
                changedSettingCount++;
            }

            return changedSettingCount;
        }

        private static HashSet<int> GetCurrentFilterIds(View view)
        {
            HashSet<int> result = new HashSet<int>();
            ICollection<ElementId> filterIds = view.GetFilters();
            foreach (ElementId filterId in filterIds)
            {
                result.Add(filterId.IntegerValue);
            }

            return result;
        }

        private static void SetOrderedFilters(View view, IList<ElementId> desiredFilterIds)
        {
            // Revit API 2023 читает порядок фильтров, но не предоставляет прямой SetFilters.
            // Поэтому сохраняем все параметры, пересоздаём список в нужном порядке и восстанавливаем настройки.
            Dictionary<int, FilterState> existingStates = new Dictionary<int, FilterState>();
            IList<ElementId> existingFilterIds = view.GetOrderedFilters();
            for (int i = 0; i < existingFilterIds.Count; i++)
            {
                ElementId filterId = existingFilterIds[i];
                FilterState state = new FilterState();
                state.IsEnabled = view.GetIsFilterEnabled(filterId);
                state.IsVisible = view.GetFilterVisibility(filterId);
                state.Overrides = view.GetFilterOverrides(filterId);
                existingStates[filterId.IntegerValue] = state;
            }

            try
            {
                for (int i = existingFilterIds.Count - 1; i >= 0; i--)
                {
                    view.RemoveFilter(existingFilterIds[i]);
                }

                for (int i = 0; i < desiredFilterIds.Count; i++)
                {
                    ElementId filterId = desiredFilterIds[i];
                    view.AddFilter(filterId);

                    FilterState state;
                    if (!existingStates.TryGetValue(filterId.IntegerValue, out state))
                    {
                        continue;
                    }

                    view.SetIsFilterEnabled(filterId, state.IsEnabled);
                    view.SetFilterVisibility(filterId, state.IsVisible);
                    view.SetFilterOverrides(filterId, state.Overrides);
                }
            }
            finally
            {
                foreach (KeyValuePair<int, FilterState> pair in existingStates)
                {
                    if (pair.Value.Overrides != null)
                    {
                        pair.Value.Overrides.Dispose();
                    }
                }
            }
        }

        private class FilterState
        {
            public bool IsEnabled { get; set; }

            public bool IsVisible { get; set; }

            public OverrideGraphicSettings Overrides { get; set; }
        }

        private static void SetCategoryGroupVisibility(View view, CategoryGraphicsGroup group, bool isVisible)
        {
            switch (group)
            {
                case CategoryGraphicsGroup.Model:
                    view.AreModelCategoriesHidden = !isVisible;
                    break;
                case CategoryGraphicsGroup.Annotation:
                    view.AreAnnotationCategoriesHidden = !isVisible;
                    break;
                case CategoryGraphicsGroup.AnalyticalModel:
                    view.AreAnalyticalModelCategoriesHidden = !isVisible;
                    break;
                case CategoryGraphicsGroup.Imported:
                    view.AreImportCategoriesHidden = !isVisible;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("group");
            }
        }

        private static Color CreateColor(int colorValue)
        {
            if (colorValue == GraphicOverrideData.NoColorValue)
            {
                return Color.InvalidColorValue;
            }

            byte red = (byte)((colorValue >> 16) & 255);
            byte green = (byte)((colorValue >> 8) & 255);
            byte blue = (byte)(colorValue & 255);
            return new Color(red, green, blue);
        }

        private static void AddUniqueWarning(IList<string> warnings, string warning)
        {
            if (warnings == null || string.IsNullOrWhiteSpace(warning))
            {
                return;
            }

            for (int i = 0; i < warnings.Count; i++)
            {
                if (string.Equals(warnings[i], warning, StringComparison.Ordinal))
                {
                    return;
                }
            }

            warnings.Add(warning);
        }
    }
}
