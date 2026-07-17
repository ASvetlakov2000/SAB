using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Autodesk.Revit.DB;
using SAB.ViewTemplateGraphics.Models;

namespace SAB.ViewTemplateGraphics.Services
{
    public class ViewTemplateGraphicsDataService
    {
        public List<TemplateSelectionItem> GetViewTemplates(Document document)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            List<View> views = new List<View>();
            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(View));
            foreach (Element element in collector)
            {
                View view = element as View;
                if (view != null && view.IsTemplate)
                {
                    views.Add(view);
                }
            }

            views.Sort(delegate(View first, View second)
            {
                return string.Compare(first.Name, second.Name, StringComparison.CurrentCultureIgnoreCase);
            });

            List<TemplateSelectionItem> result = new List<TemplateSelectionItem>();
            for (int i = 0; i < views.Count; i++)
            {
                View view = views[i];
                TemplateSelectionItem item = new TemplateSelectionItem();
                item.TemplateIdValue = view.Id.IntegerValue;
                item.Name = view.Name;
                item.ViewTypeName = GetViewTypeDisplayName(view.ViewType);
                result.Add(item);
            }

            return result;
        }

        public ViewTemplateGraphicsData Collect(Document document, View sourceTemplate)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            if (sourceTemplate == null || !sourceTemplate.IsTemplate)
            {
                throw new ArgumentException("Для чтения настроек требуется шаблон вида.", "sourceTemplate");
            }

            ViewTemplateGraphicsData data = new ViewTemplateGraphicsData();
            data.SourceTemplateIdValue = sourceTemplate.Id.IntegerValue;
            data.SourceTemplateName = sourceTemplate.Name;

            FillTemplateSectionStates(sourceTemplate, data);
            FillEditorOptions(document, data);
            if (AreGraphicsOverridesAllowed(sourceTemplate))
            {
                FillCategoryTabs(document, sourceTemplate, data);
                FillFilters(document, sourceTemplate, data.Filters);
                FillWorksets(document, sourceTemplate, data.Worksets);
                FillRevitLinks(document, sourceTemplate, data);
            }
            else
            {
                data.SupportsRevitLinkOverrides = false;
            }

            data.StartTrackingChanges();
            return data;
        }

        public ViewTemplateGraphicsData Collect(Document document, IList<View> sourceTemplates)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            if (sourceTemplates == null || sourceTemplates.Count == 0)
            {
                throw new ArgumentException("Не выбран ни один шаблон вида.", "sourceTemplates");
            }

            View baselineTemplate = null;
            for (int i = 0; i < sourceTemplates.Count; i++)
            {
                View candidate = sourceTemplates[i];
                if (candidate != null && candidate.IsTemplate && AreGraphicsOverridesAllowed(candidate))
                {
                    baselineTemplate = candidate;
                    break;
                }
            }

            if (baselineTemplate == null)
            {
                baselineTemplate = sourceTemplates[0];
            }

            ViewTemplateGraphicsData aggregate = Collect(document, baselineTemplate);
            int mergedTemplateCount = 1;
            for (int i = 0; i < sourceTemplates.Count; i++)
            {
                View sourceTemplate = sourceTemplates[i];
                if (sourceTemplate == null || sourceTemplate.Id == baselineTemplate.Id)
                {
                    continue;
                }

                MergeTemplateIntoAggregate(sourceTemplate, aggregate);
                mergedTemplateCount++;
            }

            if (!AreGraphicsOverridesAllowed(baselineTemplate))
            {
                aggregate.SourceTemplateName = "Выбранный тип шаблона не поддерживает переопределение видимости/графики.";
            }
            else
            {
                aggregate.SourceTemplateName = mergedTemplateCount == 1
                    ? baselineTemplate.Name
                    : "Выбрано шаблонов: " + mergedTemplateCount;
            }
            AddMixedValueOptions(aggregate);
            InitializeFilterTableVisibility(aggregate.Filters);
            SetCategoryComparisonState(aggregate, mergedTemplateCount > 1);
            // После объединения нескольких шаблонов фиксируем именно агрегированные значения
            // как исходное состояние для обратимого отображения статуса изменений.
            aggregate.StartTrackingChanges();
            return aggregate;
        }

        private static void SetCategoryComparisonState(ViewTemplateGraphicsData data, bool isComparisonActive)
        {
            CategoryTabData[] tabs =
            {
                data.ModelCategories,
                data.AnnotationCategories,
                data.AnalyticalCategories,
                data.ImportedCategories
            };

            for (int tabIndex = 0; tabIndex < tabs.Length; tabIndex++)
            {
                tabs[tabIndex].IsComparisonActive = isComparisonActive;
                for (int rowIndex = 0; rowIndex < tabs[tabIndex].Rows.Count; rowIndex++)
                {
                    tabs[tabIndex].Rows[rowIndex].IsComparisonActive = isComparisonActive;
                }
            }
        }

        private static void InitializeFilterTableVisibility(ObservableCollection<FilterOverrideRow> filters)
        {
            for (int i = 0; i < filters.Count; i++)
            {
                filters[i].IsPresentInTable = filters[i].IncludedState != false;
            }
        }

        public static bool AreGraphicsOverridesAllowed(View view)
        {
            if (view == null || !view.IsTemplate)
            {
                return false;
            }

            try
            {
                return view.AreGraphicsOverridesAllowed();
            }
            catch
            {
                return false;
            }
        }

        private void MergeTemplateIntoAggregate(View sourceTemplate, ViewTemplateGraphicsData aggregate)
        {
            MergeTemplateSectionStates(sourceTemplate, aggregate);

            if (!AreGraphicsOverridesAllowed(sourceTemplate))
            {
                MarkAllGraphicsAsMixed(aggregate);
                return;
            }

            try
            {
                aggregate.ModelCategories.MergeGroupVisibility(!sourceTemplate.AreModelCategoriesHidden);
                aggregate.AnnotationCategories.MergeGroupVisibility(!sourceTemplate.AreAnnotationCategoriesHidden);
                aggregate.AnalyticalCategories.MergeGroupVisibility(!sourceTemplate.AreAnalyticalModelCategoriesHidden);
                aggregate.ImportedCategories.MergeGroupVisibility(!sourceTemplate.AreImportCategoriesHidden);
            }
            catch
            {
                MarkCategoryGroupsAsMixed(aggregate);
            }

            MergeCategoryRows(sourceTemplate, aggregate.ModelCategories);
            MergeCategoryRows(sourceTemplate, aggregate.AnnotationCategories);
            MergeCategoryRows(sourceTemplate, aggregate.AnalyticalCategories);
            MergeCategoryRows(sourceTemplate, aggregate.ImportedCategories);
            MergeFilterRows(sourceTemplate, aggregate.Filters);
            MergeWorksetRows(sourceTemplate, aggregate.Worksets);
            MergeRevitLinkRows(sourceTemplate, aggregate.RevitLinks);
        }

        private static void MergeTemplateSectionStates(View sourceTemplate, ViewTemplateGraphicsData aggregate)
        {
            HashSet<int> nonControlledParameterIds = new HashSet<int>();
            try
            {
                ICollection<ElementId> ids = sourceTemplate.GetNonControlledTemplateParameterIds();
                if (ids != null)
                {
                    foreach (ElementId id in ids)
                    {
                        if (id != null)
                        {
                            nonControlledParameterIds.Add(id.IntegerValue);
                        }
                    }
                }
            }
            catch
            {
                // Все секции ниже станут смешанными, если набор параметров прочитать нельзя.
            }

            TemplateSectionState[] sections =
            {
                aggregate.ModelCategories.Section,
                aggregate.AnnotationCategories.Section,
                aggregate.AnalyticalCategories.Section,
                aggregate.ImportedCategories.Section,
                aggregate.FiltersSection,
                aggregate.WorksetsSection,
                aggregate.RevitLinksSection
            };

            for (int i = 0; i < sections.Length; i++)
            {
                sections[i].MergeValue(!nonControlledParameterIds.Contains(sections[i].ParameterIdValue));
            }
        }

        private static void MergeCategoryRows(View sourceTemplate, CategoryTabData tab)
        {
            for (int i = 0; i < tab.Rows.Count; i++)
            {
                CategoryOverrideRow row = tab.Rows[i];
                ElementId categoryId = new ElementId(row.CategoryIdValue);
                try
                {
                    row.MergeVisibility(!sourceTemplate.GetCategoryHidden(categoryId));
                    OverrideGraphicSettings overrides = sourceTemplate.GetCategoryOverrides(categoryId);
                    GraphicOverrideData otherGraphics = new GraphicOverrideData();
                    LoadGraphicOverrides(overrides, otherGraphics);
                    row.Graphics.MergeValues(otherGraphics);
                    if (overrides != null)
                    {
                        overrides.Dispose();
                    }
                }
                catch
                {
                    row.MergeVisibility(!row.IsVisible);
                    row.Graphics.MergeValues(null);
                }
            }
        }

        private static void MergeFilterRows(View sourceTemplate, ObservableCollection<FilterOverrideRow> rows)
        {
            HashSet<int> includedIds = new HashSet<int>();
            try
            {
                IList<ElementId> filterIds = sourceTemplate.GetOrderedFilters();
                for (int i = 0; i < filterIds.Count; i++)
                {
                    includedIds.Add(filterIds[i].IntegerValue);
                }
            }
            catch
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    rows[i].MergeValues(!rows[i].IsIncluded, !rows[i].IsEnabled, !rows[i].IsVisible, null);
                }

                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                FilterOverrideRow row = rows[i];
                bool isIncluded = includedIds.Contains(row.FilterIdValue);
                bool isEnabled = true;
                bool isVisible = true;
                GraphicOverrideData graphics = new GraphicOverrideData();
                if (isIncluded)
                {
                    ElementId filterId = new ElementId(row.FilterIdValue);
                    try
                    {
                        isEnabled = sourceTemplate.GetIsFilterEnabled(filterId);
                        isVisible = sourceTemplate.GetFilterVisibility(filterId);
                        OverrideGraphicSettings overrides = sourceTemplate.GetFilterOverrides(filterId);
                        LoadGraphicOverrides(overrides, graphics);
                        if (overrides != null)
                        {
                            overrides.Dispose();
                        }
                    }
                    catch
                    {
                        graphics = null;
                    }
                }

                row.MergeValues(isIncluded, isEnabled, isVisible, graphics);
            }
        }

        private static void MergeWorksetRows(View sourceTemplate, ObservableCollection<WorksetOverrideRow> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                try
                {
                    WorksetVisibility visibility = sourceTemplate.GetWorksetVisibility(new WorksetId(rows[i].WorksetIdValue));
                    rows[i].MergeVisibility(visibility);
                }
                catch
                {
                    WorksetVisibility different = rows[i].Visibility == WorksetVisibility.Hidden
                        ? WorksetVisibility.Visible
                        : WorksetVisibility.Hidden;
                    rows[i].MergeVisibility(different);
                }
            }
        }

        private static void MergeRevitLinkRows(View sourceTemplate, ObservableCollection<RevitLinkInfo> rows)
        {
            RevitLinkGraphicsReflectionService service = new RevitLinkGraphicsReflectionService();
            for (int i = 0; i < rows.Count; i++)
            {
                RevitLinkInfo other = new RevitLinkInfo();
                other.LinkElementIdValue = rows[i].LinkElementIdValue;
                other.IsApiSupported = service.IsSupported;
                try
                {
                    service.ReadOverrides(sourceTemplate, new ElementId(rows[i].LinkElementIdValue), other);
                    rows[i].MergeValues(other.VisibilityTypeName, other.LinkedViewIdValue);
                }
                catch
                {
                    rows[i].MergeValues(
                        RevitLinkInfo.MixedVisibilityTypeName,
                        RevitLinkInfo.MixedLinkedViewIdValue);
                }
            }
        }

        private static void MarkAllGraphicsAsMixed(ViewTemplateGraphicsData aggregate)
        {
            MarkCategoryGroupsAsMixed(aggregate);
            CategoryTabData[] categoryTabs =
            {
                aggregate.ModelCategories,
                aggregate.AnnotationCategories,
                aggregate.AnalyticalCategories,
                aggregate.ImportedCategories
            };

            for (int tabIndex = 0; tabIndex < categoryTabs.Length; tabIndex++)
            {
                for (int rowIndex = 0; rowIndex < categoryTabs[tabIndex].Rows.Count; rowIndex++)
                {
                    CategoryOverrideRow row = categoryTabs[tabIndex].Rows[rowIndex];
                    row.MergeVisibility(!row.IsVisible);
                    row.Graphics.MergeValues(null);
                }
            }

            for (int i = 0; i < aggregate.Filters.Count; i++)
            {
                FilterOverrideRow row = aggregate.Filters[i];
                row.MergeValues(!row.IsIncluded, !row.IsEnabled, !row.IsVisible, null);
            }

            for (int i = 0; i < aggregate.Worksets.Count; i++)
            {
                WorksetOverrideRow row = aggregate.Worksets[i];
                row.MergeVisibility(row.Visibility == WorksetVisibility.Hidden
                    ? WorksetVisibility.Visible
                    : WorksetVisibility.Hidden);
            }

            for (int i = 0; i < aggregate.RevitLinks.Count; i++)
            {
                RevitLinkInfo row = aggregate.RevitLinks[i];
                row.MergeValues(RevitLinkInfo.MixedVisibilityTypeName, RevitLinkInfo.MixedLinkedViewIdValue);
            }
        }

        private static void MarkCategoryGroupsAsMixed(ViewTemplateGraphicsData aggregate)
        {
            aggregate.ModelCategories.MergeGroupVisibility(!aggregate.ModelCategories.IsGroupVisible);
            aggregate.AnnotationCategories.MergeGroupVisibility(!aggregate.AnnotationCategories.IsGroupVisible);
            aggregate.AnalyticalCategories.MergeGroupVisibility(!aggregate.AnalyticalCategories.IsGroupVisible);
            aggregate.ImportedCategories.MergeGroupVisibility(!aggregate.ImportedCategories.IsGroupVisible);
        }

        private static void AddMixedValueOptions(ViewTemplateGraphicsData aggregate)
        {
            bool hasMixedWorksets = false;
            for (int i = 0; i < aggregate.Worksets.Count; i++)
            {
                if (aggregate.Worksets[i].IsMixed)
                {
                    hasMixedWorksets = true;
                    break;
                }
            }

            if (hasMixedWorksets)
            {
                aggregate.WorksetVisibilities.Insert(
                    0,
                    new NamedIntegerOption(WorksetOverrideRow.MixedVisibilityValue, "Разные значения"));
            }

            bool hasMixedLinkTypes = false;
            for (int i = 0; i < aggregate.RevitLinks.Count; i++)
            {
                RevitLinkInfo row = aggregate.RevitLinks[i];
                if (row.IsVisibilityTypeMixed)
                {
                    hasMixedLinkTypes = true;
                }

                if (row.IsLinkedViewMixed)
                {
                    row.LinkedViews.Insert(
                        0,
                        new NamedElementOption(RevitLinkInfo.MixedLinkedViewIdValue, "Разные значения"));
                }
            }

            if (hasMixedLinkTypes)
            {
                aggregate.RevitLinkVisibilityTypes.Insert(
                    0,
                    new NamedStringOption(RevitLinkInfo.MixedVisibilityTypeName, "Разные значения"));
            }
        }

        private static void FillTemplateSectionStates(View sourceTemplate, ViewTemplateGraphicsData data)
        {
            HashSet<int> nonControlledParameterIds = new HashSet<int>();
            ICollection<ElementId> nonControlledParameters = sourceTemplate.GetNonControlledTemplateParameterIds();
            if (nonControlledParameters != null)
            {
                foreach (ElementId parameterId in nonControlledParameters)
                {
                    if (parameterId != null)
                    {
                        nonControlledParameterIds.Add(parameterId.IntegerValue);
                    }
                }
            }

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

            for (int i = 0; i < sections.Length; i++)
            {
                sections[i].IsIncluded = !nonControlledParameterIds.Contains(sections[i].ParameterIdValue);
            }
        }

        private void FillEditorOptions(Document document, ViewTemplateGraphicsData data)
        {
            // Блок вариантов толщины линий. Revit поддерживает номера перьев 1-16.
            data.LineWeights.Add(new NamedIntegerOption(OverrideGraphicSettings.InvalidPenNumber, "Без переопределения"));
            for (int lineWeight = 1; lineWeight <= 16; lineWeight++)
            {
                data.LineWeights.Add(new NamedIntegerOption(lineWeight, lineWeight.ToString()));
            }

            data.LinePatterns.Add(new NamedElementOption(ElementId.InvalidElementId.IntegerValue, "Без переопределения"));
            try
            {
                ElementId solidPatternId = LinePatternElement.GetSolidPatternId();
                if (solidPatternId != null && solidPatternId != ElementId.InvalidElementId)
                {
                    data.LinePatterns.Add(new NamedElementOption(solidPatternId.IntegerValue, "Сплошная"));
                }
            }
            catch
            {
                // Отсутствие специального варианта не мешает выбрать обычные образцы линий.
            }

            List<LinePatternElement> linePatterns = new List<LinePatternElement>();
            FilteredElementCollector linePatternCollector = new FilteredElementCollector(document).OfClass(typeof(LinePatternElement));
            foreach (Element element in linePatternCollector)
            {
                LinePatternElement linePattern = element as LinePatternElement;
                if (linePattern != null)
                {
                    linePatterns.Add(linePattern);
                }
            }

            linePatterns.Sort(delegate(LinePatternElement first, LinePatternElement second)
            {
                return string.Compare(first.Name, second.Name, StringComparison.CurrentCultureIgnoreCase);
            });

            for (int i = 0; i < linePatterns.Count; i++)
            {
                if (!ContainsElementOption(data.LinePatterns, linePatterns[i].Id.IntegerValue))
                {
                    data.LinePatterns.Add(new NamedElementOption(linePatterns[i].Id.IntegerValue, linePatterns[i].Name));
                }
            }

            data.FillPatterns.Add(new NamedElementOption(ElementId.InvalidElementId.IntegerValue, "Без переопределения"));
            List<FillPatternElement> fillPatterns = new List<FillPatternElement>();
            FilteredElementCollector fillPatternCollector = new FilteredElementCollector(document).OfClass(typeof(FillPatternElement));
            foreach (Element element in fillPatternCollector)
            {
                FillPatternElement fillPatternElement = element as FillPatternElement;
                if (fillPatternElement == null)
                {
                    continue;
                }

                FillPattern fillPattern = fillPatternElement.GetFillPattern();
                if (fillPattern != null && fillPattern.Target == FillPatternTarget.Drafting)
                {
                    fillPatterns.Add(fillPatternElement);
                }
            }

            fillPatterns.Sort(delegate(FillPatternElement first, FillPatternElement second)
            {
                FillPattern firstPattern = first.GetFillPattern();
                FillPattern secondPattern = second.GetFillPattern();
                if (firstPattern != null && firstPattern.IsSolidFill && (secondPattern == null || !secondPattern.IsSolidFill))
                {
                    return -1;
                }

                if (secondPattern != null && secondPattern.IsSolidFill && (firstPattern == null || !firstPattern.IsSolidFill))
                {
                    return 1;
                }

                return string.Compare(first.Name, second.Name, StringComparison.CurrentCultureIgnoreCase);
            });

            for (int i = 0; i < fillPatterns.Count; i++)
            {
                data.FillPatterns.Add(new NamedElementOption(fillPatterns[i].Id.IntegerValue, fillPatterns[i].Name));
            }

            data.DetailLevels.Add(new NamedDetailLevelOption(ViewDetailLevel.Undefined, "По виду"));
            data.DetailLevels.Add(new NamedDetailLevelOption(ViewDetailLevel.Coarse, "Низкий"));
            data.DetailLevels.Add(new NamedDetailLevelOption(ViewDetailLevel.Medium, "Средний"));
            data.DetailLevels.Add(new NamedDetailLevelOption(ViewDetailLevel.Fine, "Высокий"));

            data.WorksetVisibilities.Add(new NamedIntegerOption((int)WorksetVisibility.UseGlobalSetting, "По умолчанию"));
            data.WorksetVisibilities.Add(new NamedIntegerOption((int)WorksetVisibility.Visible, "Видимый"));
            data.WorksetVisibilities.Add(new NamedIntegerOption((int)WorksetVisibility.Hidden, "Скрытый"));
        }

        private void FillCategoryTabs(Document document, View sourceTemplate, ViewTemplateGraphicsData data)
        {
            HashSet<int> importedRootCategoryIds = CollectImportedRootCategoryIds(document);

            data.ModelCategories.IsGroupVisible = !sourceTemplate.AreModelCategoriesHidden;
            data.AnnotationCategories.IsGroupVisible = !sourceTemplate.AreAnnotationCategoriesHidden;
            data.AnalyticalCategories.IsGroupVisible = !sourceTemplate.AreAnalyticalModelCategoriesHidden;
            data.ImportedCategories.IsGroupVisible = !sourceTemplate.AreImportCategoriesHidden;

            List<Category> rootCategories = new List<Category>();
            foreach (Category category in document.Settings.Categories)
            {
                if (category != null && category.Parent == null)
                {
                    rootCategories.Add(category);
                }
            }

            rootCategories.Sort(CompareCategoriesByName);
            for (int i = 0; i < rootCategories.Count; i++)
            {
                Category rootCategory = rootCategories[i];
                CategoryTabData categoryTab = GetCategoryTab(rootCategory, importedRootCategoryIds, data);
                if (categoryTab == null)
                {
                    continue;
                }

                AddCategoryAndSubcategories(
                    sourceTemplate,
                    rootCategory,
                    categoryTab,
                    0,
                    ElementId.InvalidElementId.IntegerValue);
            }

            data.ModelCategories.RefreshRowVisibility();
            data.AnnotationCategories.RefreshRowVisibility();
            data.AnalyticalCategories.RefreshRowVisibility();
            data.ImportedCategories.RefreshRowVisibility();
        }

        private void AddCategoryAndSubcategories(
            View sourceTemplate,
            Category category,
            CategoryTabData categoryTab,
            int indentLevel,
            int parentCategoryIdValue)
        {
            List<Category> subcategories = new List<Category>();
            if (category.SubCategories != null)
            {
                foreach (Category subcategory in category.SubCategories)
                {
                    if (subcategory != null)
                    {
                        subcategories.Add(subcategory);
                    }
                }
            }

            subcategories.Sort(CompareCategoriesByName);
            CategoryOverrideRow row = TryCreateCategoryRow(sourceTemplate, category, categoryTab.Group, indentLevel);
            int childParentCategoryIdValue = parentCategoryIdValue;
            if (row != null)
            {
                row.ParentCategoryIdValue = parentCategoryIdValue;
                row.HasChildren = subcategories.Count > 0;
                categoryTab.Rows.Add(row);
                childParentCategoryIdValue = row.CategoryIdValue;
            }

            for (int i = 0; i < subcategories.Count; i++)
            {
                AddCategoryAndSubcategories(
                    sourceTemplate,
                    subcategories[i],
                    categoryTab,
                    row != null ? indentLevel + 1 : indentLevel,
                    childParentCategoryIdValue);
            }
        }

        private CategoryOverrideRow TryCreateCategoryRow(
            View sourceTemplate,
            Category category,
            CategoryGraphicsGroup group,
            int indentLevel)
        {
            if (category == null || category.Id == null || category.Id == ElementId.InvalidElementId)
            {
                return null;
            }

            bool isVisibleInUi;
            try
            {
                isVisibleInUi = category.IsVisibleInUI;
            }
            catch
            {
                isVisibleInUi = true;
            }

            if (!isVisibleInUi)
            {
                return null;
            }

            bool allowsVisibilityControl = false;
            try
            {
                allowsVisibilityControl = category.get_AllowsVisibilityControl(sourceTemplate) &&
                                          sourceTemplate.CanCategoryBeHidden(category.Id);
            }
            catch
            {
                allowsVisibilityControl = false;
            }

            OverrideGraphicSettings overrides;
            try
            {
                overrides = sourceTemplate.GetCategoryOverrides(category.Id);
            }
            catch
            {
                if (!allowsVisibilityControl)
                {
                    return null;
                }

                overrides = new OverrideGraphicSettings();
            }

            CategoryOverrideRow row = new CategoryOverrideRow();
            row.CategoryIdValue = category.Id.IntegerValue;
            row.Name = category.Name;
            row.IndentLevel = indentLevel;
            row.AllowsVisibilityControl = allowsVisibilityControl;
            row.SupportsCut = group == CategoryGraphicsGroup.Model || group == CategoryGraphicsGroup.AnalyticalModel;
            row.SupportsSurfacePatterns = group != CategoryGraphicsGroup.Annotation;
            row.SupportsTransparency = group == CategoryGraphicsGroup.Model || group == CategoryGraphicsGroup.AnalyticalModel;
            row.SupportsDetailLevel = group == CategoryGraphicsGroup.Model || group == CategoryGraphicsGroup.AnalyticalModel;

            try
            {
                row.IsVisible = !sourceTemplate.GetCategoryHidden(category.Id);
            }
            catch
            {
                row.IsVisible = true;
                row.AllowsVisibilityControl = false;
            }

            LoadGraphicOverrides(overrides, row.Graphics);
            if (overrides != null)
            {
                overrides.Dispose();
            }

            return row;
        }

        private void FillFilters(Document document, View sourceTemplate, ObservableCollection<FilterOverrideRow> rows)
        {
            IList<ElementId> orderedFilterIds = sourceTemplate.GetOrderedFilters();
            HashSet<int> includedFilterIds = new HashSet<int>();
            for (int i = 0; i < orderedFilterIds.Count; i++)
            {
                includedFilterIds.Add(orderedFilterIds[i].IntegerValue);
                FilterElement filter = document.GetElement(orderedFilterIds[i]) as FilterElement;
                if (filter != null)
                {
                    rows.Add(CreateFilterRow(sourceTemplate, filter, true));
                }
            }

            List<FilterElement> remainingFilters = CollectAllFilterElements(document);
            for (int filterIndex = remainingFilters.Count - 1; filterIndex >= 0; filterIndex--)
            {
                FilterElement filter = remainingFilters[filterIndex];
                if (filter == null || includedFilterIds.Contains(filter.Id.IntegerValue))
                {
                    remainingFilters.RemoveAt(filterIndex);
                }
            }

            remainingFilters.Sort(delegate(FilterElement first, FilterElement second)
            {
                return string.Compare(first.Name, second.Name, StringComparison.CurrentCultureIgnoreCase);
            });

            for (int i = 0; i < remainingFilters.Count; i++)
            {
                rows.Add(CreateFilterRow(sourceTemplate, remainingFilters[i], false));
            }
        }

        private static List<FilterElement> CollectAllFilterElements(Document document)
        {
            List<FilterElement> result = new List<FilterElement>();
            HashSet<int> collectedIds = new HashSet<int>();

            FilteredElementCollector parameterFilterCollector =
                new FilteredElementCollector(document).OfClass(typeof(ParameterFilterElement));
            foreach (Element element in parameterFilterCollector)
            {
                FilterElement filter = element as FilterElement;
                if (filter != null && collectedIds.Add(filter.Id.IntegerValue))
                {
                    result.Add(filter);
                }
            }

            FilteredElementCollector selectionFilterCollector =
                new FilteredElementCollector(document).OfClass(typeof(SelectionFilterElement));
            foreach (Element element in selectionFilterCollector)
            {
                FilterElement filter = element as FilterElement;
                if (filter != null && collectedIds.Add(filter.Id.IntegerValue))
                {
                    result.Add(filter);
                }
            }

            return result;
        }

        private FilterOverrideRow CreateFilterRow(View sourceTemplate, FilterElement filter, bool isIncluded)
        {
            FilterOverrideRow row = new FilterOverrideRow();
            row.FilterIdValue = filter.Id.IntegerValue;
            row.Name = filter.Name;
            row.IsIncluded = isIncluded;
            row.IsEnabled = true;
            row.IsVisible = true;

            if (isIncluded)
            {
                row.IsEnabled = sourceTemplate.GetIsFilterEnabled(filter.Id);
                row.IsVisible = sourceTemplate.GetFilterVisibility(filter.Id);
                OverrideGraphicSettings overrides = sourceTemplate.GetFilterOverrides(filter.Id);
                LoadGraphicOverrides(overrides, row.Graphics);
                if (overrides != null)
                {
                    overrides.Dispose();
                }
            }

            return row;
        }

        private void FillWorksets(Document document, View sourceTemplate, ObservableCollection<WorksetOverrideRow> rows)
        {
            if (!document.IsWorkshared)
            {
                return;
            }

            List<Workset> worksets = new List<Workset>();
            FilteredWorksetCollector collector = new FilteredWorksetCollector(document).OfKind(WorksetKind.UserWorkset);
            foreach (Workset workset in collector)
            {
                if (workset != null)
                {
                    worksets.Add(workset);
                }
            }

            worksets.Sort(delegate(Workset first, Workset second)
            {
                return string.Compare(first.Name, second.Name, StringComparison.CurrentCultureIgnoreCase);
            });

            for (int i = 0; i < worksets.Count; i++)
            {
                WorksetOverrideRow row = new WorksetOverrideRow();
                row.WorksetIdValue = worksets[i].Id.IntegerValue;
                row.Name = worksets[i].Name;
                row.Visibility = sourceTemplate.GetWorksetVisibility(worksets[i].Id);
                rows.Add(row);
            }
        }

        private void FillRevitLinks(Document document, View sourceTemplate, ViewTemplateGraphicsData data)
        {
            RevitLinkGraphicsReflectionService linkGraphicsService = new RevitLinkGraphicsReflectionService();
            data.SupportsRevitLinkOverrides = linkGraphicsService.IsSupported;
            List<NamedStringOption> visibilityTypeOptions = linkGraphicsService.GetVisibilityTypeOptions();
            for (int optionIndex = 0; optionIndex < visibilityTypeOptions.Count; optionIndex++)
            {
                data.RevitLinkVisibilityTypes.Add(visibilityTypeOptions[optionIndex]);
            }

            Dictionary<int, List<RevitLinkInstance>> instancesByTypeId =
                new Dictionary<int, List<RevitLinkInstance>>();
            FilteredElementCollector instanceCollector = new FilteredElementCollector(document).OfClass(typeof(RevitLinkInstance));
            foreach (Element element in instanceCollector)
            {
                RevitLinkInstance instance = element as RevitLinkInstance;
                if (instance == null)
                {
                    continue;
                }

                int typeIdValue = instance.GetTypeId().IntegerValue;
                List<RevitLinkInstance> typeInstances;
                if (!instancesByTypeId.TryGetValue(typeIdValue, out typeInstances))
                {
                    typeInstances = new List<RevitLinkInstance>();
                    instancesByTypeId[typeIdValue] = typeInstances;
                }

                typeInstances.Add(instance);
            }

            List<RevitLinkType> linkTypes = new List<RevitLinkType>();
            FilteredElementCollector linkTypeCollector = new FilteredElementCollector(document).OfClass(typeof(RevitLinkType));
            foreach (Element element in linkTypeCollector)
            {
                RevitLinkType linkType = element as RevitLinkType;
                if (linkType != null)
                {
                    linkTypes.Add(linkType);
                }
            }

            linkTypes.Sort(delegate(RevitLinkType first, RevitLinkType second)
            {
                return string.Compare(first.Name, second.Name, StringComparison.CurrentCultureIgnoreCase);
            });

            for (int i = 0; i < linkTypes.Count; i++)
            {
                RevitLinkType linkType = linkTypes[i];
                List<RevitLinkInstance> typeInstances;
                if (!instancesByTypeId.TryGetValue(linkType.Id.IntegerValue, out typeInstances))
                {
                    typeInstances = new List<RevitLinkInstance>();
                }

                typeInstances.Sort(delegate(RevitLinkInstance first, RevitLinkInstance second)
                {
                    return string.Compare(first.Name, second.Name, StringComparison.CurrentCultureIgnoreCase);
                });

                RevitLinkInfo info = new RevitLinkInfo();
                info.LinkElementIdValue = linkType.Id.IntegerValue;
                info.Name = linkType.Name;
                info.Status = GetLinkStatusDisplayName(linkType.GetLinkedFileStatus());
                info.InstanceCount = typeInstances.Count;
                info.IsInstance = false;
                info.IndentLevel = 0;
                info.IsApiSupported = linkGraphicsService.IsSupported;

                Document linkedDocument = typeInstances.Count > 0 ? typeInstances[0].GetLinkDocument() : null;
                FillLinkedViewOptions(info, linkedDocument);
                TryReadLinkOverrides(linkGraphicsService, sourceTemplate, linkType.Id, info);
                data.RevitLinks.Add(info);

                for (int instanceIndex = 0; instanceIndex < typeInstances.Count; instanceIndex++)
                {
                    RevitLinkInstance instance = typeInstances[instanceIndex];
                    RevitLinkInfo instanceInfo = new RevitLinkInfo();
                    instanceInfo.LinkElementIdValue = instance.Id.IntegerValue;
                    instanceInfo.Name = "Экземпляр: " + instance.Name;
                    instanceInfo.Status = info.Status;
                    instanceInfo.InstanceCount = 1;
                    instanceInfo.IsInstance = true;
                    instanceInfo.IndentLevel = 1;
                    instanceInfo.IsApiSupported = linkGraphicsService.IsSupported;
                    FillLinkedViewOptions(instanceInfo, instance.GetLinkDocument());
                    TryReadLinkOverrides(linkGraphicsService, sourceTemplate, instance.Id, instanceInfo);
                    data.RevitLinks.Add(instanceInfo);
                }
            }
        }

        private static void FillLinkedViewOptions(RevitLinkInfo linkInfo, Document linkedDocument)
        {
            linkInfo.LinkedViews.Add(
                new NamedElementOption(ElementId.InvalidElementId.IntegerValue, "Не задан"));

            if (linkedDocument == null)
            {
                return;
            }

            List<View> linkedViews = new List<View>();
            FilteredElementCollector collector = new FilteredElementCollector(linkedDocument).OfClass(typeof(View));
            foreach (Element element in collector)
            {
                View linkedView = element as View;
                if (linkedView == null || linkedView.IsTemplate || !linkedView.CanBePrinted)
                {
                    continue;
                }

                if (linkedView.ViewType == ViewType.DrawingSheet ||
                    linkedView.ViewType == ViewType.Schedule ||
                    linkedView.ViewType == ViewType.ProjectBrowser ||
                    linkedView.ViewType == ViewType.SystemBrowser)
                {
                    continue;
                }

                linkedViews.Add(linkedView);
            }

            linkedViews.Sort(delegate(View first, View second)
            {
                return string.Compare(first.Name, second.Name, StringComparison.CurrentCultureIgnoreCase);
            });

            for (int i = 0; i < linkedViews.Count; i++)
            {
                linkInfo.LinkedViews.Add(new NamedElementOption(linkedViews[i].Id.IntegerValue, linkedViews[i].Name));
            }
        }

        private static void TryReadLinkOverrides(
            RevitLinkGraphicsReflectionService service,
            View sourceTemplate,
            ElementId linkElementId,
            RevitLinkInfo row)
        {
            try
            {
                service.ReadOverrides(sourceTemplate, linkElementId, row);
            }
            catch
            {
                row.IsApiSupported = false;
            }
        }

        private static void LoadGraphicOverrides(OverrideGraphicSettings source, GraphicOverrideData target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.ProjectionLineWeight = source.ProjectionLineWeight;
            target.ProjectionLineColorValue = PackColor(source.ProjectionLineColor);
            target.ProjectionLinePatternId = GetIdValue(source.ProjectionLinePatternId);
            target.SurfaceForegroundPatternVisible = source.IsSurfaceForegroundPatternVisible;
            target.SurfaceForegroundPatternId = GetIdValue(source.SurfaceForegroundPatternId);
            target.SurfaceForegroundPatternColorValue = PackColor(source.SurfaceForegroundPatternColor);
            target.SurfaceBackgroundPatternVisible = source.IsSurfaceBackgroundPatternVisible;
            target.SurfaceBackgroundPatternId = GetIdValue(source.SurfaceBackgroundPatternId);
            target.SurfaceBackgroundPatternColorValue = PackColor(source.SurfaceBackgroundPatternColor);
            target.Transparency = source.Transparency;
            target.CutLineWeight = source.CutLineWeight;
            target.CutLineColorValue = PackColor(source.CutLineColor);
            target.CutLinePatternId = GetIdValue(source.CutLinePatternId);
            target.CutForegroundPatternVisible = source.IsCutForegroundPatternVisible;
            target.CutForegroundPatternId = GetIdValue(source.CutForegroundPatternId);
            target.CutForegroundPatternColorValue = PackColor(source.CutForegroundPatternColor);
            target.CutBackgroundPatternVisible = source.IsCutBackgroundPatternVisible;
            target.CutBackgroundPatternId = GetIdValue(source.CutBackgroundPatternId);
            target.CutBackgroundPatternColorValue = PackColor(source.CutBackgroundPatternColor);
            target.Halftone = source.Halftone;
            target.DetailLevel = source.DetailLevel;
        }

        private static int PackColor(Color color)
        {
            if (color == null || !color.IsValid)
            {
                return GraphicOverrideData.NoColorValue;
            }

            return (color.Red << 16) | (color.Green << 8) | color.Blue;
        }

        private static int GetIdValue(ElementId elementId)
        {
            return elementId != null ? elementId.IntegerValue : ElementId.InvalidElementId.IntegerValue;
        }

        private static bool ContainsElementOption(ObservableCollection<NamedElementOption> options, int idValue)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].IdValue == idValue)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareCategoriesByName(Category first, Category second)
        {
            string firstName = first != null ? first.Name : string.Empty;
            string secondName = second != null ? second.Name : string.Empty;
            return string.Compare(firstName, secondName, StringComparison.CurrentCultureIgnoreCase);
        }

        private static HashSet<int> CollectImportedRootCategoryIds(Document document)
        {
            HashSet<int> result = new HashSet<int>();
            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(ImportInstance));
            foreach (Element element in collector)
            {
                ImportInstance importInstance = element as ImportInstance;
                Category category = importInstance != null ? importInstance.Category : null;
                if (category == null)
                {
                    continue;
                }

                while (category.Parent != null)
                {
                    category = category.Parent;
                }

                if (category.Id != null && category.Id != ElementId.InvalidElementId)
                {
                    result.Add(category.Id.IntegerValue);
                }
            }

            return result;
        }

        private static CategoryTabData GetCategoryTab(
            Category category,
            HashSet<int> importedRootCategoryIds,
            ViewTemplateGraphicsData data)
        {
            if (category == null || category.Id == null)
            {
                return null;
            }

            if (importedRootCategoryIds.Contains(category.Id.IntegerValue))
            {
                return data.ImportedCategories;
            }

            if (category.CategoryType == CategoryType.Model)
            {
                return data.ModelCategories;
            }

            if (category.CategoryType == CategoryType.Annotation)
            {
                return data.AnnotationCategories;
            }

            if (category.CategoryType == CategoryType.AnalyticalModel)
            {
                return data.AnalyticalCategories;
            }

            return null;
        }

        private static string GetViewTypeDisplayName(ViewType viewType)
        {
            switch (viewType)
            {
                case ViewType.FloorPlan:
                    return "План этажа";
                case ViewType.CeilingPlan:
                    return "План потолка";
                case ViewType.Elevation:
                    return "Фасад";
                case ViewType.Section:
                    return "Разрез";
                case ViewType.ThreeD:
                    return "3D вид";
                case ViewType.DraftingView:
                    return "Чертёжный вид";
                case ViewType.Detail:
                    return "Фрагмент";
                case ViewType.EngineeringPlan:
                    return "Инженерный план";
                case ViewType.AreaPlan:
                    return "План зонирования";
                default:
                    return viewType.ToString();
            }
        }

        private static string GetLinkStatusDisplayName(LinkedFileStatus status)
        {
            switch (status)
            {
                case LinkedFileStatus.Loaded:
                    return "Загружен";
                case LinkedFileStatus.Unloaded:
                    return "Выгружен";
                case LinkedFileStatus.NotFound:
                    return "Не найден";
                case LinkedFileStatus.InClosedWorkset:
                    return "В закрытом рабочем наборе";
                case LinkedFileStatus.LocallyUnloaded:
                    return "Выгружен локально";
                default:
                    return status.ToString();
            }
        }
    }
}
