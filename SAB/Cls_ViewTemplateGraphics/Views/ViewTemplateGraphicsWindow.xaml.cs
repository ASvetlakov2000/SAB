using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using SAB.UI;
using SAB.ViewTemplateGraphics.Models;
using SAB.ViewTemplateGraphics.ViewModels;
using RevitTaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace SAB.ViewTemplateGraphics.Views
{
    public partial class ViewTemplateGraphicsWindow : Window
    {
        private const string WindowTitle = "SAB Пакетное редактирование шаблонов видов";

        private readonly ViewTemplateGraphicsViewModel _viewModel;
        private TextBox _templateSearchTextBox;
        private TextBox _filtersSearchTextBox;
        private TextBox _worksetsSearchTextBox;
        private TextBox _revitLinksSearchTextBox;
        private ListBox _templatesListBox;
        private DataGrid _filtersDataGrid;
        private bool _isApplyingBatchEdit;
        private ItemsControl _selectionSnapshotOwner;
        private readonly List<object> _selectionSnapshot = new List<object>();

        public ViewTemplateGraphicsWindow(ViewTemplateGraphicsViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException("viewModel");

            InitializeWindowFromXamlFile();
            DataContext = _viewModel;

            AddHandler(Button.ClickEvent, new RoutedEventHandler(Button_Click));
            AddHandler(ComboBox.SelectionChangedEvent, new SelectionChangedEventHandler(ComboBox_SelectionChanged));
            AddHandler(TabControl.SelectionChangedEvent, new SelectionChangedEventHandler(TabControl_SelectionChanged));
            AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(Window_PreviewMouseLeftButtonDown));
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            Loaded += ViewTemplateGraphicsWindow_Loaded;
            Closed += ViewTemplateGraphicsWindow_Closed;
            SourceInitialized += Window_SourceInitialized;
        }

        public ViewTemplateGraphicsData GraphicsData
        {
            get { return _viewModel.GraphicsData; }
        }

        public System.Collections.Generic.List<int> TargetTemplateIdValues
        {
            get { return _viewModel.GetTargetTemplateIdValues(); }
        }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(ViewTemplateGraphicsWindow).Assembly.Location);
            string xamlPath = Path.Combine(
                assemblyDirectory,
                "Cls_ViewTemplateGraphics",
                "Views",
                "ViewTemplateGraphicsWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл окна пакетного редактора не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext parserContext = new ParserContext();
                parserContext.BaseUri = new Uri(xamlPath, UriKind.Absolute);

                Window loadedWindow = XamlReader.Load(stream, parserContext) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось загрузить ViewTemplateGraphicsWindow.xaml.");
                }

                CopyWindowProperties(loadedWindow);
            }
        }

        private void CopyWindowProperties(Window loadedWindow)
        {
            Title = loadedWindow.Title;
            Width = loadedWindow.Width;
            Height = loadedWindow.Height;
            MinWidth = loadedWindow.MinWidth;
            MinHeight = loadedWindow.MinHeight;
            WindowStartupLocation = loadedWindow.WindowStartupLocation;
            ResizeMode = loadedWindow.ResizeMode;
            ShowInTaskbar = loadedWindow.ShowInTaskbar;
            Background = loadedWindow.Background;
            FontFamily = loadedWindow.FontFamily;
            FontSize = loadedWindow.FontSize;
            Resources = loadedWindow.Resources;
            Content = loadedWindow.Content;
        }

        private void ViewTemplateGraphicsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _templateSearchTextBox = FindVisualChildByName<TextBox>(this, "TemplateSearchTextBox");
            _filtersSearchTextBox = FindVisualChildByName<TextBox>(this, "FiltersSearchTextBox");
            _worksetsSearchTextBox = FindVisualChildByName<TextBox>(this, "WorksetsSearchTextBox");
            _revitLinksSearchTextBox = FindVisualChildByName<TextBox>(this, "RevitLinksSearchTextBox");
            _templatesListBox = FindVisualChildByName<ListBox>(this, "TemplatesListBox");

            if (_templatesListBox != null)
            {
                _templatesListBox.SelectionMode = SelectionMode.Extended;
            }

            WindowSizeSettingsService.Apply(this, "ViewTemplateGraphics.MainWindow");
            Dispatcher.BeginInvoke(new Action(RefreshVisibleDataGrids), DispatcherPriority.Background);
        }

        private void ViewTemplateGraphicsWindow_Closed(object sender, EventArgs e)
        {
            if (_templatesListBox != null)
            {
                _templatesListBox = null;
            }

            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;

            Loaded -= ViewTemplateGraphicsWindow_Loaded;
            Closed -= ViewTemplateGraphicsWindow_Closed;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = FindParent<CheckBox>(e.OriginalSource as DependencyObject);
            if (checkBox != null && checkBox.Tag is string)
            {
                HandleBatchCheckBox(checkBox);
                e.Handled = true;
                return;
            }

            Button button = FindParent<Button>(e.OriginalSource as DependencyObject);
            if (button == null)
            {
                return;
            }

            if (string.Equals(button.Name, "SelectAllTemplatesButton", StringComparison.Ordinal))
            {
                _viewModel.SetAllVisibleTargets(true);
                e.Handled = true;
                return;
            }

            if (string.Equals(button.Name, "ClearTemplatesButton", StringComparison.Ordinal))
            {
                _viewModel.SetAllVisibleTargets(false);
                e.Handled = true;
                return;
            }

            if (string.Equals(button.Name, "FindTemplatesButton", StringComparison.Ordinal))
            {
                _viewModel.FilterTemplates(_templateSearchTextBox != null ? _templateSearchTextBox.Text : string.Empty);
                e.Handled = true;
                return;
            }

            if (string.Equals(button.Name, "AddSelectedFiltersButton", StringComparison.Ordinal))
            {
                OpenAddFiltersWindow();
                e.Handled = true;
                return;
            }

            if (string.Equals(button.Name, "RemoveSelectedFiltersButton", StringComparison.Ordinal))
            {
                SetSelectedFiltersIncluded(false);
                e.Handled = true;
                return;
            }

            if (string.Equals(button.Name, "MoveFilterUpButton", StringComparison.Ordinal))
            {
                MoveSelectedFilter(-1);
                e.Handled = true;
                return;
            }

            if (string.Equals(button.Name, "MoveFilterDownButton", StringComparison.Ordinal))
            {
                MoveSelectedFilter(1);
                e.Handled = true;
                return;
            }

            if (string.Equals(button.Name, "CancelButton", StringComparison.Ordinal))
            {
                DialogResult = false;
                Close();
                e.Handled = true;
                return;
            }

            if (string.Equals(button.Name, "ApplyButton", StringComparison.Ordinal))
            {
                if (!_viewModel.CanApply)
                {
                    RevitTaskDialog.Show(WindowTitle, "Выберите хотя бы один шаблон и измените минимум одну настройку.");
                    return;
                }

                DialogResult = true;
                Close();
                e.Handled = true;
                return;
            }

            string action = button.Tag as string;
            if (string.Equals(action, "FindCategories", StringComparison.Ordinal) ||
                string.Equals(action, "FindFilters", StringComparison.Ordinal) ||
                string.Equals(action, "FindWorksets", StringComparison.Ordinal) ||
                string.Equals(action, "FindRevitLinks", StringComparison.Ordinal))
            {
                ExecuteSearch(button, action);
                e.Handled = true;
            }
            else if (string.Equals(action, "ToggleCategory", StringComparison.Ordinal))
            {
                CategoryOverrideRow categoryRow = button.CommandParameter as CategoryOverrideRow;
                DataGrid dataGrid = FindParent<DataGrid>(button);
                CategoryTabData tab = dataGrid != null ? dataGrid.DataContext as CategoryTabData : null;
                if (tab != null)
                {
                    tab.ToggleCategoryExpansion(categoryRow);
                    RefreshDataGridFilter(dataGrid, false);
                }

                e.Handled = true;
            }
            else if (!string.IsNullOrEmpty(action) && action.StartsWith("EditCategory", StringComparison.Ordinal))
            {
                CategoryOverrideRow categoryRow = button.CommandParameter as CategoryOverrideRow;
                if (categoryRow != null)
                {
                    List<CategoryOverrideRow> selectedRows = GetSelectedRows(button, categoryRow);
                    List<GraphicOverrideData> graphics = new List<GraphicOverrideData>();
                    bool supportsCut = true;
                    bool supportsSurfacePatterns = true;
                    bool supportsTransparency = true;
                    bool supportsDetailLevel = true;
                    for (int i = 0; i < selectedRows.Count; i++)
                    {
                        graphics.Add(selectedRows[i].Graphics);
                        supportsCut = supportsCut && selectedRows[i].SupportsCut;
                        supportsSurfacePatterns = supportsSurfacePatterns && selectedRows[i].SupportsSurfacePatterns;
                        supportsTransparency = supportsTransparency && selectedRows[i].SupportsTransparency;
                        supportsDetailLevel = supportsDetailLevel && selectedRows[i].SupportsDetailLevel;
                    }

                    OpenGraphicsEditor(
                        graphics,
                        GetEditorSection(action),
                        supportsCut,
                        supportsSurfacePatterns,
                        supportsTransparency,
                        supportsDetailLevel);
                }

                e.Handled = true;
            }
            else if (!string.IsNullOrEmpty(action) && action.StartsWith("EditFilter", StringComparison.Ordinal))
            {
                FilterOverrideRow filterRow = button.CommandParameter as FilterOverrideRow;
                if (filterRow != null && filterRow.CanEditGraphics)
                {
                    List<FilterOverrideRow> selectedRows = GetSelectedRows(button, filterRow);
                    List<GraphicOverrideData> graphics = new List<GraphicOverrideData>();
                    for (int i = 0; i < selectedRows.Count; i++)
                    {
                        if (selectedRows[i].CanEditGraphics)
                        {
                            graphics.Add(selectedRows[i].Graphics);
                        }
                    }

                    OpenGraphicsEditor(
                        graphics,
                        GetEditorSection(action),
                        true,
                        true,
                        true,
                        false);
                }

                e.Handled = true;
            }
        }

        private void MoveSelectedFilter(int direction)
        {
            if (_filtersDataGrid == null)
            {
                _filtersDataGrid = FindVisualChildByName<DataGrid>(this, "FiltersDataGrid");
            }

            FilterOverrideRow selectedFilter = _filtersDataGrid != null
                ? _filtersDataGrid.SelectedItem as FilterOverrideRow
                : null;

            if (selectedFilter == null)
            {
                RevitTaskDialog.Show(WindowTitle, "Выберите фильтр в таблице.");
                return;
            }

            if (!selectedFilter.IsIncluded)
            {
                RevitTaskDialog.Show(WindowTitle, "Сначала добавьте фильтр в шаблон флажком «В шаблоне».");
                return;
            }

            _viewModel.MoveFilter(selectedFilter, direction);
        }

        private void SetSelectedFiltersIncluded(bool isIncluded)
        {
            if (_filtersDataGrid == null)
            {
                _filtersDataGrid = FindVisualChildByName<DataGrid>(this, "FiltersDataGrid");
            }

            if (_filtersDataGrid == null || _filtersDataGrid.SelectedItems.Count == 0)
            {
                RevitTaskDialog.Show(WindowTitle, "Выберите один или несколько фильтров в таблице.");
                return;
            }

            List<FilterOverrideRow> selectedFilters = new List<FilterOverrideRow>();
            for (int i = 0; i < _filtersDataGrid.SelectedItems.Count; i++)
            {
                FilterOverrideRow row = _filtersDataGrid.SelectedItems[i] as FilterOverrideRow;
                if (row != null)
                {
                    selectedFilters.Add(row);
                }
            }

            if (selectedFilters.Count == 0)
            {
                RevitTaskDialog.Show(WindowTitle, "Не удалось определить выбранные фильтры.");
                return;
            }

            for (int i = 0; i < selectedFilters.Count; i++)
            {
                selectedFilters[i].IncludedState = isIncluded;
                selectedFilters[i].IsPresentInTable = isIncluded;
            }

            _viewModel.NotifyDataChanged();
            RefreshFilterDataGrid(false);
        }

        private void OpenAddFiltersWindow()
        {
            if (_viewModel.GraphicsData == null)
            {
                return;
            }

            AddFiltersWindow window = new AddFiltersWindow(_viewModel.GraphicsData.Filters);
            window.Owner = this;
            bool? result = window.ShowDialog();
            if (result != true)
            {
                return;
            }

            if (_filtersSearchTextBox != null)
            {
                _filtersSearchTextBox.Text = string.Empty;
            }

            _viewModel.FilterFilters(string.Empty);
            for (int selectedIndex = 0; selectedIndex < window.SelectedFilterIdValues.Count; selectedIndex++)
            {
                int filterIdValue = window.SelectedFilterIdValues[selectedIndex];
                for (int rowIndex = 0; rowIndex < _viewModel.GraphicsData.Filters.Count; rowIndex++)
                {
                    FilterOverrideRow row = _viewModel.GraphicsData.Filters[rowIndex];
                    if (row.FilterIdValue == filterIdValue)
                    {
                        row.IncludedState = true;
                        row.IsPresentInTable = true;
                        break;
                    }
                }
            }

            _viewModel.NotifyDataChanged();
            RefreshFilterDataGrid(false);
        }

        private void ExecuteSearch(Button button, string action)
        {
            if (button == null)
            {
                return;
            }

            System.Windows.Controls.Grid searchGrid = FindParent<System.Windows.Controls.Grid>(button);
            if (string.Equals(action, "FindCategories", StringComparison.Ordinal))
            {
                TextBox input = FindVisualChildByTag<TextBox>(searchGrid, "CategorySearchInput");
                CategoryTabData categoryTab = button.CommandParameter as CategoryTabData;
                if (categoryTab != null)
                {
                    categoryTab.SearchText = input != null ? input.Text : string.Empty;
                }

                DataGrid categoryDataGrid = FindVisualChild<DataGrid>(GetParentObject(searchGrid));
                RefreshDataGridFilter(categoryDataGrid, true);

                return;
            }

            if (string.Equals(action, "FindFilters", StringComparison.Ordinal))
            {
                _filtersSearchTextBox = FindVisualChildByTag<TextBox>(searchGrid, "FilterSearchInput");
                _viewModel.FilterFilters(_filtersSearchTextBox != null ? _filtersSearchTextBox.Text : string.Empty);
                RefreshDataGridFilter(FindVisualChild<DataGrid>(GetParentObject(searchGrid)), true);
            }
            else if (string.Equals(action, "FindWorksets", StringComparison.Ordinal))
            {
                _worksetsSearchTextBox = FindVisualChildByTag<TextBox>(searchGrid, "WorksetSearchInput");
                _viewModel.FilterWorksets(_worksetsSearchTextBox != null ? _worksetsSearchTextBox.Text : string.Empty);
                RefreshDataGridFilter(FindVisualChild<DataGrid>(GetParentObject(searchGrid)), true);
            }
            else if (string.Equals(action, "FindRevitLinks", StringComparison.Ordinal))
            {
                _revitLinksSearchTextBox = FindVisualChildByTag<TextBox>(searchGrid, "RevitLinkSearchInput");
                _viewModel.FilterRevitLinks(_revitLinksSearchTextBox != null ? _revitLinksSearchTextBox.Text : string.Empty);
                RefreshDataGridFilter(FindVisualChild<DataGrid>(GetParentObject(searchGrid)), true);
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(e.OriginalSource is TabControl))
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(RefreshVisibleDataGrids), DispatcherPriority.Background);
        }

        // Block responsible for applying search and hierarchy filtering to the DataGrid collection view.
        // Removing hidden items from the view prevents WPF virtualization from leaving a blank scroll area.
        private void RefreshDataGridFilter(DataGrid dataGrid, bool resetScrollPosition)
        {
            if (dataGrid == null || dataGrid.ItemsSource == null)
            {
                return;
            }

            ICollectionView collectionView = CollectionViewSource.GetDefaultView(dataGrid.ItemsSource);
            if (collectionView == null)
            {
                return;
            }

            collectionView.Filter = IsDataGridItemVisible;
            collectionView.Refresh();

            if (!resetScrollPosition)
            {
                return;
            }

            Dispatcher.BeginInvoke(
                new Action(delegate
                {
                    dataGrid.UpdateLayout();
                    ScrollViewer currentScrollViewer = FindVisualChild<ScrollViewer>(dataGrid);
                    if (currentScrollViewer != null)
                    {
                        currentScrollViewer.ScrollToTop();
                    }

                    if (dataGrid.Items.Count > 0)
                    {
                        dataGrid.ScrollIntoView(dataGrid.Items[0]);
                    }
                }),
                DispatcherPriority.Background);
        }

        private static bool IsDataGridItemVisible(object item)
        {
            CategoryOverrideRow categoryRow = item as CategoryOverrideRow;
            if (categoryRow != null)
            {
                return categoryRow.IsVisibleInTree;
            }

            FilterOverrideRow filterRow = item as FilterOverrideRow;
            if (filterRow != null)
            {
                return filterRow.IsVisibleInList;
            }

            WorksetOverrideRow worksetRow = item as WorksetOverrideRow;
            if (worksetRow != null)
            {
                return worksetRow.IsVisibleInList;
            }

            RevitLinkInfo revitLinkRow = item as RevitLinkInfo;
            if (revitLinkRow != null)
            {
                return revitLinkRow.IsVisibleInList;
            }

            return true;
        }

        private void RefreshVisibleDataGrids()
        {
            List<DataGrid> dataGrids = FindVisualChildren<DataGrid>(this);
            for (int i = 0; i < dataGrids.Count; i++)
            {
                RefreshDataGridFilter(dataGrids[i], false);
            }
        }

        private void RefreshFilterDataGrid(bool resetScrollPosition)
        {
            if (_filtersDataGrid == null)
            {
                _filtersDataGrid = FindVisualChildByName<DataGrid>(this, "FiltersDataGrid");
            }

            RefreshDataGridFilter(_filtersDataGrid, resetScrollPosition);
        }

        private void OpenGraphicsEditor(
            IList<GraphicOverrideData> sourceData,
            GraphicOverrideEditorSection section,
            bool supportsCut,
            bool supportsSurfacePatterns,
            bool supportsTransparency,
            bool supportsDetailLevel)
        {
            if (sourceData == null || sourceData.Count == 0 || _viewModel.GraphicsData == null)
            {
                return;
            }

            GraphicOverrideData editedData = sourceData[0].CloneForEditing();
            for (int i = 1; i < sourceData.Count; i++)
            {
                editedData.MergeValues(sourceData[i]);
            }

            editedData.StartTrackingChanges();
            GraphicOverrideEditorViewModel editorViewModel = new GraphicOverrideEditorViewModel(
                editedData,
                section,
                _viewModel.GraphicsData.LineWeights,
                _viewModel.GraphicsData.LinePatterns,
                _viewModel.GraphicsData.FillPatterns,
                _viewModel.GraphicsData.DetailLevels,
                supportsCut,
                supportsSurfacePatterns,
                supportsTransparency,
                supportsDetailLevel);

            GraphicOverrideEditorWindow editorWindow = new GraphicOverrideEditorWindow(editorViewModel);
            editorWindow.Owner = this;
            bool? result = editorWindow.ShowDialog();
            if (result == true)
            {
                for (int i = 0; i < sourceData.Count; i++)
                {
                    sourceData[i].ApplyEditedValues(editorWindow.EditedData);
                }

                _viewModel.NotifyDataChanged();
            }
        }

        private static GraphicOverrideEditorSection GetEditorSection(string action)
        {
            if (action.IndexOf("SurfacePatterns", StringComparison.Ordinal) >= 0)
            {
                return GraphicOverrideEditorSection.SurfacePatterns;
            }

            if (action.IndexOf("Transparency", StringComparison.Ordinal) >= 0)
            {
                return GraphicOverrideEditorSection.Transparency;
            }

            if (action.IndexOf("CutLines", StringComparison.Ordinal) >= 0)
            {
                return GraphicOverrideEditorSection.CutLines;
            }

            if (action.IndexOf("CutPatterns", StringComparison.Ordinal) >= 0)
            {
                return GraphicOverrideEditorSection.CutPatterns;
            }

            return GraphicOverrideEditorSection.ProjectionLines;
        }

        private void HandleBatchCheckBox(CheckBox checkBox, bool? forcedValue = null)
        {
            if (_isApplyingBatchEdit || checkBox == null)
            {
                return;
            }

            string action = checkBox.Tag as string;
            bool value = forcedValue.HasValue ? forcedValue.Value : checkBox.IsChecked == true;
            try
            {
                _isApplyingBatchEdit = true;
                if (string.Equals(action, "BatchTemplateTarget", StringComparison.Ordinal))
                {
                    if (_viewModel.GraphicsData != null && _viewModel.GraphicsData.IsDirty)
                    {
                        return;
                    }

                    TemplateSelectionItem current = checkBox.DataContext as TemplateSelectionItem;
                    List<TemplateSelectionItem> selected = GetSelectedTemplates(current);
                    _viewModel.SetTargetSelection(selected, value);
                }
                else if (string.Equals(action, "BatchCategoryVisibility", StringComparison.Ordinal))
                {
                    CategoryOverrideRow current = checkBox.CommandParameter as CategoryOverrideRow;
                    List<CategoryOverrideRow> selected = GetSelectedRows(checkBox, current);
                    for (int i = 0; i < selected.Count; i++)
                    {
                        if (selected[i].AllowsVisibilityControl)
                        {
                            selected[i].VisibilityState = value;
                        }
                    }
                }
                else if (string.Equals(action, "BatchCategoryHalftone", StringComparison.Ordinal))
                {
                    CategoryOverrideRow current = checkBox.CommandParameter as CategoryOverrideRow;
                    List<CategoryOverrideRow> selected = GetSelectedRows(checkBox, current);
                    for (int i = 0; i < selected.Count; i++)
                    {
                        selected[i].Graphics.HalftoneState = value;
                    }
                }
                else if (string.Equals(action, "BatchFilterIncluded", StringComparison.Ordinal) ||
                         string.Equals(action, "BatchFilterEnabled", StringComparison.Ordinal) ||
                         string.Equals(action, "BatchFilterVisibility", StringComparison.Ordinal) ||
                         string.Equals(action, "BatchFilterHalftone", StringComparison.Ordinal))
                {
                    FilterOverrideRow current = checkBox.CommandParameter as FilterOverrideRow;
                    List<FilterOverrideRow> selected = GetSelectedRows(checkBox, current);
                    for (int i = 0; i < selected.Count; i++)
                    {
                        if (string.Equals(action, "BatchFilterIncluded", StringComparison.Ordinal))
                        {
                            selected[i].IncludedState = value;
                        }
                        else if (string.Equals(action, "BatchFilterEnabled", StringComparison.Ordinal))
                        {
                            selected[i].EnabledState = value;
                        }
                        else if (string.Equals(action, "BatchFilterHalftone", StringComparison.Ordinal))
                        {
                            selected[i].Graphics.HalftoneState = value;
                        }
                        else
                        {
                            selected[i].VisibilityState = value;
                        }
                    }
                }
            }
            finally
            {
                _isApplyingBatchEdit = false;
                _viewModel.NotifyDataChanged();
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox comboBox = e.OriginalSource as ComboBox;
            if (_isApplyingBatchEdit || comboBox == null || !comboBox.IsKeyboardFocusWithin)
            {
                return;
            }

            string action = comboBox.Tag as string;
            if (string.IsNullOrEmpty(action))
            {
                return;
            }

            try
            {
                _isApplyingBatchEdit = true;
                if (string.Equals(action, "BatchWorksetVisibility", StringComparison.Ordinal))
                {
                    WorksetOverrideRow current = comboBox.DataContext as WorksetOverrideRow;
                    List<WorksetOverrideRow> selected = GetSelectedRows(comboBox, current);
                    int value = comboBox.SelectedValue != null
                        ? Convert.ToInt32(comboBox.SelectedValue)
                        : WorksetOverrideRow.MixedVisibilityValue;
                    if (value != WorksetOverrideRow.MixedVisibilityValue)
                    {
                        for (int i = 0; i < selected.Count; i++)
                        {
                            selected[i].VisibilityValue = value;
                        }
                    }
                }
                else if (string.Equals(action, "BatchCategoryDetailLevel", StringComparison.Ordinal))
                {
                    CategoryOverrideRow current = comboBox.DataContext as CategoryOverrideRow;
                    List<CategoryOverrideRow> selected = GetSelectedRows(comboBox, current);
                    if (comboBox.SelectedValue is ViewDetailLevel)
                    {
                        ViewDetailLevel value = (ViewDetailLevel)comboBox.SelectedValue;
                        if (value != GraphicOverrideData.MixedDetailLevelValue)
                        {
                            for (int i = 0; i < selected.Count; i++)
                            {
                                if (selected[i].SupportsDetailLevel)
                                {
                                    selected[i].Graphics.DetailLevelState = value;
                                }
                            }
                        }
                    }
                }
                else if (string.Equals(action, "BatchLinkVisibilityType", StringComparison.Ordinal))
                {
                    RevitLinkInfo current = comboBox.DataContext as RevitLinkInfo;
                    List<RevitLinkInfo> selected = GetSelectedRows(comboBox, current);
                    string value = comboBox.SelectedValue as string;
                    if (!string.Equals(value, RevitLinkInfo.MixedVisibilityTypeName, StringComparison.Ordinal))
                    {
                        for (int i = 0; i < selected.Count; i++)
                        {
                            if (selected[i].IsApiSupported)
                            {
                                selected[i].VisibilityTypeName = value;
                            }
                        }
                    }
                }
                else if (string.Equals(action, "BatchLinkView", StringComparison.Ordinal))
                {
                    RevitLinkInfo current = comboBox.DataContext as RevitLinkInfo;
                    List<RevitLinkInfo> selected = GetSelectedRows(comboBox, current);
                    int value = comboBox.SelectedValue != null
                        ? Convert.ToInt32(comboBox.SelectedValue)
                        : ElementId.InvalidElementId.IntegerValue;
                    if (value != RevitLinkInfo.MixedLinkedViewIdValue)
                    {
                        for (int i = 0; i < selected.Count; i++)
                        {
                            if (selected[i].CanSelectLinkedView)
                            {
                                selected[i].LinkedViewIdValue = value;
                            }
                        }
                    }
                }
            }
            finally
            {
                _isApplyingBatchEdit = false;
                _viewModel.NotifyDataChanged();
            }
        }

        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject source = e.OriginalSource as DependencyObject;
            DataGrid dataGrid = FindParent<DataGrid>(source);
            if (dataGrid != null)
            {
                CaptureSelection(dataGrid, dataGrid.SelectedItems);
            }
            else
            {
                ListBox listBox = FindParent<ListBox>(source);
                if (listBox != null)
                {
                    CaptureSelection(listBox, listBox.SelectedItems);
                }
            }

            CheckBox checkBox = FindParent<CheckBox>(source);
            if (checkBox != null && checkBox.IsThreeState)
            {
                bool? previousValue = checkBox.IsChecked;
                bool nextValue = previousValue != true;
                string action = checkBox.Tag as string;

                if (!string.IsNullOrEmpty(action) && action.StartsWith("Batch", StringComparison.Ordinal))
                {
                    HandleBatchCheckBox(checkBox, nextValue);
                }
                else
                {
                    checkBox.SetCurrentValue(ToggleButton.IsCheckedProperty, (bool?)nextValue);
                    BindingExpression bindingExpression = checkBox.GetBindingExpression(ToggleButton.IsCheckedProperty);
                    if (bindingExpression != null)
                    {
                        bindingExpression.UpdateSource();
                    }

                    _viewModel.NotifyDataChanged();
                }

                e.Handled = true;
            }
        }

        private void CaptureSelection(ItemsControl owner, System.Collections.IList selectedItems)
        {
            _selectionSnapshotOwner = owner;
            _selectionSnapshot.Clear();
            if (selectedItems == null)
            {
                return;
            }

            for (int i = 0; i < selectedItems.Count; i++)
            {
                _selectionSnapshot.Add(selectedItems[i]);
            }
        }

        private List<T> GetSelectedRows<T>(DependencyObject source, T current)
            where T : class
        {
            List<T> result = new List<T>();
            DataGrid dataGrid = FindParent<DataGrid>(source);
            if (dataGrid != null)
            {
                AddSelectionItems(result, dataGrid, dataGrid.SelectedItems, current);
            }

            if (result.Count == 0 && current != null)
            {
                result.Add(current);
            }

            return result;
        }

        private List<TemplateSelectionItem> GetSelectedTemplates(TemplateSelectionItem current)
        {
            List<TemplateSelectionItem> result = new List<TemplateSelectionItem>();
            if (_templatesListBox != null)
            {
                AddSelectionItems(result, _templatesListBox, _templatesListBox.SelectedItems, current);
            }

            if (result.Count == 0 && current != null)
            {
                result.Add(current);
            }

            return result;
        }

        private void AddSelectionItems<T>(
            List<T> result,
            ItemsControl owner,
            System.Collections.IList currentSelection,
            T current)
            where T : class
        {
            System.Collections.IList source = currentSelection;
            if (_selectionSnapshotOwner == owner && _selectionSnapshot.Count > 1 && _selectionSnapshot.Contains(current))
            {
                source = _selectionSnapshot;
            }

            bool containsCurrent = false;
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    T item = source[i] as T;
                    if (item != null)
                    {
                        result.Add(item);
                        containsCurrent = containsCurrent || ReferenceEquals(item, current);
                    }
                }
            }

            if (!containsCurrent)
            {
                result.Clear();
            }
        }

        private void ApplyRightSearchFilters()
        {
            if (_filtersSearchTextBox != null)
            {
                _viewModel.FilterFilters(_filtersSearchTextBox.Text);
            }

            if (_worksetsSearchTextBox != null)
            {
                _viewModel.FilterWorksets(_worksetsSearchTextBox.Text);
            }

            if (_revitLinksSearchTextBox != null)
            {
                _viewModel.FilterRevitLinks(_revitLinksSearchTextBox.Text);
            }

            RefreshVisibleDataGrids();
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, "GraphicsData", StringComparison.Ordinal))
            {
                Dispatcher.BeginInvoke(new Action(ApplyRightSearchFilters), DispatcherPriority.Background);
            }
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            if (source != null && source.CompositionTarget != null)
            {
                source.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
            }

            SourceInitialized -= Window_SourceInitialized;
        }

        private static T FindVisualChildByName<T>(DependencyObject parent, string name)
            where T : FrameworkElement
        {
            if (parent == null)
            {
                return null;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                T typedChild = child as T;
                if (typedChild != null && string.Equals(typedChild.Name, name, StringComparison.Ordinal))
                {
                    return typedChild;
                }

                T nestedChild = FindVisualChildByName<T>(child, name);
                if (nestedChild != null)
                {
                    return nestedChild;
                }
            }

            return null;
        }

        private static T FindVisualChildByTag<T>(DependencyObject parent, string tag)
            where T : FrameworkElement
        {
            if (parent == null)
            {
                return null;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                T typedChild = child as T;
                if (typedChild != null && string.Equals(typedChild.Tag as string, tag, StringComparison.Ordinal))
                {
                    return typedChild;
                }

                T nestedChild = FindVisualChildByTag<T>(child, tag);
                if (nestedChild != null)
                {
                    return nestedChild;
                }
            }

            return null;
        }

        private static T FindVisualChild<T>(DependencyObject parent)
            where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                T typedChild = child as T;
                if (typedChild != null)
                {
                    return typedChild;
                }

                T nestedChild = FindVisualChild<T>(child);
                if (nestedChild != null)
                {
                    return nestedChild;
                }
            }

            return null;
        }

        private static List<T> FindVisualChildren<T>(DependencyObject parent)
            where T : DependencyObject
        {
            List<T> result = new List<T>();
            AddVisualChildren(parent, result);
            return result;
        }

        private static void AddVisualChildren<T>(DependencyObject parent, List<T> result)
            where T : DependencyObject
        {
            if (parent == null || result == null)
            {
                return;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                T typedChild = child as T;
                if (typedChild != null)
                {
                    result.Add(typedChild);
                }

                AddVisualChildren(child, result);
            }
        }

        private static T FindParent<T>(DependencyObject child)
            where T : DependencyObject
        {
            DependencyObject current = child;
            while (current != null)
            {
                T typedParent = current as T;
                if (typedParent != null)
                {
                    return typedParent;
                }

                current = GetParentObject(current);
            }

            return null;
        }

        private static DependencyObject GetParentObject(DependencyObject child)
        {
            ContentElement contentElement = child as ContentElement;
            if (contentElement != null)
            {
                DependencyObject contentParent = ContentOperations.GetParent(contentElement);
                if (contentParent != null)
                {
                    return contentParent;
                }

                FrameworkContentElement frameworkContentElement = contentElement as FrameworkContentElement;
                return frameworkContentElement != null ? frameworkContentElement.Parent : null;
            }

            return child != null ? VisualTreeHelper.GetParent(child) : null;
        }
    }
}
