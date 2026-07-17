using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Autodesk.Revit.DB;
using SAB.ViewTemplateGraphics.Models;
using SAB.ViewTemplateGraphics.Services;
using RevitTaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace SAB.ViewTemplateGraphics.ViewModels
{
    public class ViewTemplateGraphicsViewModel : NotifyPropertyChangedBase
    {
        private readonly Document _document;
        private readonly ViewTemplateGraphicsDataService _dataService;
        private ViewTemplateGraphicsData _graphicsData;
        private TemplateSelectionItem _selectedSourceTemplate;
        private readonly HashSet<int> _lastTargetTemplateIds;
        private bool _suppressTargetSelectionRefresh;
        private string _templateSearchText;
        private string _selectedTemplateTypeValue;

        public ViewTemplateGraphicsViewModel(
            Document document,
            IList<TemplateSelectionItem> templates,
            ViewTemplateGraphicsDataService dataService)
        {
            _document = document ?? throw new ArgumentNullException("document");
            _dataService = dataService ?? throw new ArgumentNullException("dataService");
            _lastTargetTemplateIds = new HashSet<int>();
            Templates = new ObservableCollection<TemplateSelectionItem>();
            TemplateTypeOptions = new ObservableCollection<NamedStringOption>();
            _templateSearchText = string.Empty;
            _selectedTemplateTypeValue = string.Empty;

            if (templates != null)
            {
                for (int i = 0; i < templates.Count; i++)
                {
                    TemplateSelectionItem template = templates[i];
                    template.PropertyChanged += Template_PropertyChanged;
                    Templates.Add(template);
                }
            }

            BuildTemplateTypeOptions();

            if (Templates.Count > 0)
            {
                _suppressTargetSelectionRefresh = true;
                try
                {
                    Templates[0].IsTarget = true;
                }
                finally
                {
                    _suppressTargetSelectionRefresh = false;
                }

                RefreshSelectedTemplatesData();
            }
        }

        public ObservableCollection<TemplateSelectionItem> Templates { get; private set; }

        public ObservableCollection<NamedStringOption> TemplateTypeOptions { get; private set; }

        public string SelectedTemplateTypeValue
        {
            get { return _selectedTemplateTypeValue ?? string.Empty; }
            set
            {
                if (SetField(ref _selectedTemplateTypeValue, value ?? string.Empty, "SelectedTemplateTypeValue"))
                {
                    ApplyTemplateFilter();
                }
            }
        }

        public ViewTemplateGraphicsData GraphicsData
        {
            get { return _graphicsData; }
            private set
            {
                if (SetField(ref _graphicsData, value, "GraphicsData"))
                {
                    AttachGraphicsDataHandlers(value);
                    RaiseStatusPropertiesChanged();
                }
            }
        }

        public TemplateSelectionItem SelectedSourceTemplate
        {
            get { return _selectedSourceTemplate; }
        }

        public int TargetTemplateCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Templates.Count; i++)
                {
                    if (Templates[i].IsTarget)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int ModifiedSettingCount
        {
            get { return GraphicsData != null ? GraphicsData.CountModifiedRows() : 0; }
        }

        public bool CanApply
        {
            get { return TargetTemplateCount > 0 && GraphicsData != null && GraphicsData.IsDirty; }
        }

        public string StatusText
        {
            get
            {
                string targetText = TargetTemplateCount == 1
                    ? "Выбран 1 шаблон"
                    : "Выбрано шаблонов: " + TargetTemplateCount;
                string modifiedText = ModifiedSettingCount == 1
                    ? "изменена 1 строка настроек"
                    : "изменено строк настроек: " + ModifiedSettingCount;
                return targetText + "  •  " + modifiedText;
            }
        }

        public bool TryChangeSourceTemplate(TemplateSelectionItem template, out string validationMessage)
        {
            validationMessage = string.Empty;
            if (template == null)
            {
                validationMessage = "Не выбран шаблон-источник.";
                return false;
            }

            if (_selectedSourceTemplate == template)
            {
                return true;
            }

            if (GraphicsData != null && GraphicsData.IsDirty)
            {
                validationMessage = "Сначала примените или отмените текущие изменения. При смене шаблона-источника несохранённые настройки будут потеряны.";
                return false;
            }

            if (_selectedSourceTemplate != null)
            {
                _selectedSourceTemplate.IsSource = false;
            }

            _selectedSourceTemplate = template;
            _selectedSourceTemplate.IsSource = true;
            _selectedSourceTemplate.IsTarget = true;
            OnPropertyChanged("SelectedSourceTemplate");
            LoadSourceTemplateData(template);
            return true;
        }

        public void SetAllVisibleTargets(bool isSelected)
        {
            List<TemplateSelectionItem> visibleTemplates = new List<TemplateSelectionItem>();
            for (int i = 0; i < Templates.Count; i++)
            {
                if (Templates[i].IsVisibleInList)
                {
                    visibleTemplates.Add(Templates[i]);
                }
            }

            SetTargetSelection(visibleTemplates, isSelected);
        }

        public bool SetTargetSelection(IList<TemplateSelectionItem> templates, bool isSelected)
        {
            if (GraphicsData != null && GraphicsData.IsDirty)
            {
                RevitTaskDialog.Show(
                    "SAB Пакетное редактирование шаблонов видов",
                    "Сначала примените или отмените текущие изменения. Состав выбранных шаблонов нельзя менять, пока есть несохранённые правки.");
                RestoreLastTargetSelection();
                return false;
            }

            _suppressTargetSelectionRefresh = true;
            try
            {
                if (templates != null)
                {
                    for (int i = 0; i < templates.Count; i++)
                    {
                        if (templates[i] != null)
                        {
                            templates[i].IsTarget = isSelected;
                        }
                    }
                }
            }
            finally
            {
                _suppressTargetSelectionRefresh = false;
            }

            RefreshSelectedTemplatesData();
            return true;
        }

        public void FilterTemplates(string searchText)
        {
            _templateSearchText = (searchText ?? string.Empty).Trim();
            ApplyTemplateFilter();
        }

        private void ApplyTemplateFilter()
        {
            for (int i = 0; i < Templates.Count; i++)
            {
                TemplateSelectionItem template = Templates[i];
                bool matchesSearch = _templateSearchText.Length == 0 ||
                    (template.Name ?? string.Empty).IndexOf(_templateSearchText, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    (template.ViewTypeName ?? string.Empty).IndexOf(_templateSearchText, StringComparison.CurrentCultureIgnoreCase) >= 0;
                bool matchesType = string.IsNullOrEmpty(SelectedTemplateTypeValue) ||
                    string.Equals(template.ViewTypeName, SelectedTemplateTypeValue, StringComparison.CurrentCultureIgnoreCase);
                template.IsVisibleInList = matchesSearch && matchesType;
            }
        }

        private void BuildTemplateTypeOptions()
        {
            TemplateTypeOptions.Clear();
            TemplateTypeOptions.Add(new NamedStringOption(string.Empty, "Все типы"));

            HashSet<string> uniqueTypes = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            List<string> viewTypes = new List<string>();
            for (int i = 0; i < Templates.Count; i++)
            {
                string viewTypeName = Templates[i].ViewTypeName ?? string.Empty;
                if (viewTypeName.Length > 0 && uniqueTypes.Add(viewTypeName))
                {
                    viewTypes.Add(viewTypeName);
                }
            }

            viewTypes.Sort(StringComparer.CurrentCultureIgnoreCase);
            for (int i = 0; i < viewTypes.Count; i++)
            {
                TemplateTypeOptions.Add(new NamedStringOption(viewTypes[i], viewTypes[i]));
            }
        }

        public void FilterFilters(string searchText)
        {
            string normalized = (searchText ?? string.Empty).Trim();
            if (GraphicsData == null)
            {
                return;
            }

            for (int i = 0; i < GraphicsData.Filters.Count; i++)
            {
                FilterOverrideRow row = GraphicsData.Filters[i];
                row.IsVisibleInList = normalized.Length == 0 ||
                    (row.Name ?? string.Empty).IndexOf(normalized, StringComparison.CurrentCultureIgnoreCase) >= 0;
            }
        }

        public void FilterWorksets(string searchText)
        {
            string normalized = (searchText ?? string.Empty).Trim();
            if (GraphicsData == null)
            {
                return;
            }

            for (int i = 0; i < GraphicsData.Worksets.Count; i++)
            {
                WorksetOverrideRow row = GraphicsData.Worksets[i];
                row.IsVisibleInList = normalized.Length == 0 ||
                    (row.Name ?? string.Empty).IndexOf(normalized, StringComparison.CurrentCultureIgnoreCase) >= 0;
            }
        }

        public void FilterRevitLinks(string searchText)
        {
            string normalized = (searchText ?? string.Empty).Trim();
            if (GraphicsData == null)
            {
                return;
            }

            for (int i = 0; i < GraphicsData.RevitLinks.Count; i++)
            {
                RevitLinkInfo row = GraphicsData.RevitLinks[i];
                row.IsVisibleInList = normalized.Length == 0 ||
                    (row.Name ?? string.Empty).IndexOf(normalized, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    (row.Status ?? string.Empty).IndexOf(normalized, StringComparison.CurrentCultureIgnoreCase) >= 0;
            }
        }

        public List<int> GetTargetTemplateIdValues()
        {
            List<int> result = new List<int>();
            for (int i = 0; i < Templates.Count; i++)
            {
                if (Templates[i].IsTarget)
                {
                    result.Add(Templates[i].TemplateIdValue);
                }
            }

            return result;
        }

        public bool MoveFilter(FilterOverrideRow filter, int direction)
        {
            if (GraphicsData == null || filter == null || !filter.IsIncluded || direction == 0)
            {
                return false;
            }

            int currentIndex = GraphicsData.Filters.IndexOf(filter);
            if (currentIndex < 0)
            {
                return false;
            }

            int targetIndex = currentIndex + direction;
            while (targetIndex >= 0 && targetIndex < GraphicsData.Filters.Count && !GraphicsData.Filters[targetIndex].IsIncluded)
            {
                targetIndex += direction;
            }

            if (targetIndex < 0 || targetIndex >= GraphicsData.Filters.Count || !GraphicsData.Filters[targetIndex].IsIncluded)
            {
                return false;
            }

            GraphicsData.Filters.Move(currentIndex, targetIndex);
            GraphicsData.UpdateFilterOrderModificationState();
            RaiseStatusPropertiesChanged();
            return true;
        }

        public void NotifyDataChanged()
        {
            RaiseStatusPropertiesChanged();
        }

        private void LoadSourceTemplateData(TemplateSelectionItem sourceTemplate)
        {
            View view = _document.GetElement(new ElementId(sourceTemplate.TemplateIdValue)) as View;
            if (view == null || !view.IsTemplate)
            {
                throw new InvalidOperationException("Не удалось прочитать выбранный шаблон вида «" + sourceTemplate.Name + "».");
            }

            GraphicsData = _dataService.Collect(_document, view);
        }

        private void RefreshSelectedTemplatesData()
        {
            List<View> selectedTemplates = new List<View>();
            _lastTargetTemplateIds.Clear();
            _selectedSourceTemplate = null;

            for (int i = 0; i < Templates.Count; i++)
            {
                TemplateSelectionItem item = Templates[i];
                item.IsSource = false;
                if (!item.IsTarget)
                {
                    continue;
                }

                View view = _document.GetElement(new ElementId(item.TemplateIdValue)) as View;
                if (view != null && view.IsTemplate)
                {
                    selectedTemplates.Add(view);
                    _lastTargetTemplateIds.Add(item.TemplateIdValue);
                }
            }

            GraphicsData = selectedTemplates.Count > 0
                ? _dataService.Collect(_document, selectedTemplates)
                : null;

            if (GraphicsData != null)
            {
                for (int i = 0; i < Templates.Count; i++)
                {
                    if (Templates[i].TemplateIdValue == GraphicsData.SourceTemplateIdValue)
                    {
                        _selectedSourceTemplate = Templates[i];
                        _selectedSourceTemplate.IsSource = true;
                        break;
                    }
                }
            }

            OnPropertyChanged("SelectedSourceTemplate");
            RaiseStatusPropertiesChanged();
        }

        private void RestoreLastTargetSelection()
        {
            _suppressTargetSelectionRefresh = true;
            try
            {
                for (int i = 0; i < Templates.Count; i++)
                {
                    Templates[i].IsTarget = _lastTargetTemplateIds.Contains(Templates[i].TemplateIdValue);
                }
            }
            finally
            {
                _suppressTargetSelectionRefresh = false;
            }

            RaiseStatusPropertiesChanged();
        }

        private void AttachGraphicsDataHandlers(ViewTemplateGraphicsData data)
        {
            if (data == null)
            {
                return;
            }

            CategoryTabData[] categoryTabs =
            {
                data.ModelCategories,
                data.AnnotationCategories,
                data.AnalyticalCategories,
                data.ImportedCategories
            };

            for (int tabIndex = 0; tabIndex < categoryTabs.Length; tabIndex++)
            {
                categoryTabs[tabIndex].PropertyChanged += GraphicsItem_PropertyChanged;
                categoryTabs[tabIndex].Section.PropertyChanged += GraphicsItem_PropertyChanged;
                for (int rowIndex = 0; rowIndex < categoryTabs[tabIndex].Rows.Count; rowIndex++)
                {
                    categoryTabs[tabIndex].Rows[rowIndex].PropertyChanged += GraphicsItem_PropertyChanged;
                    categoryTabs[tabIndex].Rows[rowIndex].Graphics.PropertyChanged += GraphicsItem_PropertyChanged;
                }
            }

            for (int filterIndex = 0; filterIndex < data.Filters.Count; filterIndex++)
            {
                data.Filters[filterIndex].PropertyChanged += Filter_PropertyChanged;
                data.Filters[filterIndex].Graphics.PropertyChanged += GraphicsItem_PropertyChanged;
            }

            for (int worksetIndex = 0; worksetIndex < data.Worksets.Count; worksetIndex++)
            {
                data.Worksets[worksetIndex].PropertyChanged += GraphicsItem_PropertyChanged;
            }

            for (int linkIndex = 0; linkIndex < data.RevitLinks.Count; linkIndex++)
            {
                data.RevitLinks[linkIndex].PropertyChanged += GraphicsItem_PropertyChanged;
            }

            data.FiltersSection.PropertyChanged += GraphicsItem_PropertyChanged;
            data.WorksetsSection.PropertyChanged += GraphicsItem_PropertyChanged;
            data.RevitLinksSection.PropertyChanged += GraphicsItem_PropertyChanged;
        }

        private void Template_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, "IsTarget", StringComparison.Ordinal))
            {
                if (_suppressTargetSelectionRefresh)
                {
                    return;
                }

                if (GraphicsData != null && GraphicsData.IsDirty)
                {
                    RevitTaskDialog.Show(
                        "SAB Пакетное редактирование шаблонов видов",
                        "Сначала примените или отмените текущие изменения. Состав выбранных шаблонов нельзя менять, пока есть несохранённые правки.");
                    RestoreLastTargetSelection();
                    return;
                }

                RefreshSelectedTemplatesData();
            }
        }

        private void Filter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            RaiseStatusPropertiesChanged();
        }

        private void GraphicsItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            RaiseStatusPropertiesChanged();
        }

        private void RaiseStatusPropertiesChanged()
        {
            OnPropertyChanged("TargetTemplateCount");
            OnPropertyChanged("ModifiedSettingCount");
            OnPropertyChanged("CanApply");
            OnPropertyChanged("StatusText");
        }
    }
}
