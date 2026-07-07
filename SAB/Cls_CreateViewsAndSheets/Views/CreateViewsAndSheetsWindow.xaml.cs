using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Helpers.Notifications.ToastNotifications;
using Microsoft.Win32;
using SAB.CreateViewsAndSheets.Models;
using SAB.CreateViewsAndSheets.Services;
using SAB.CreateViewsAndSheets.ViewModels;
using SAB.UI;
using RevitTaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace SAB.CreateViewsAndSheets.Views
{
    public partial class CreateViewsAndSheetsWindow : Window
    {
        private static readonly bool ShowBatchEditDebugDialogs = false;
        private const string ColumnLayoutKeyPrefix = "ManualColumnsV4";

        private readonly CreateViewsAndSheetsViewModel _viewModel;
        private readonly CreateViewsAndSheetsWindowLayoutService _layoutService;
        private readonly bool _openSettingsWindowAfterLoaded;

        private DataGrid _rowsDataGrid;
        private readonly List<DataGridColumn> _sheetBrowserParameterColumns;
        private ToggleButton _settingsDrawerToggle;
        private ButtonBase _sourceSheetPlacementToggle;
        private ButtonBase _pointPlacementToggle;
        private FrameworkElement _settingsDrawerPanel;
        private FrameworkElement _manualViewportPlacementPanel;
        private Border _validationPanelBorder;
        private Button _createButton;
        private Point _dragStartPoint;
        private SheetCreationRowViewModel _draggedRow;
        private List<SheetCreationRowViewModel> _draggedRows;
        private SheetCreationRowViewModel _selectionAnchorRow;
        private SheetCreationRowViewModel _batchEditSourceRow;
        private List<SheetCreationRowViewModel> _batchEditRowsSnapshot;
        private List<SheetCreationRowViewModel> _lastMultiSelectedRowsSnapshot;
        private string _batchEditPropertyPath;
        private DataGridColumn _resizedColumn;
        private bool _gridHandlersAttached;
        private bool _layoutRestored;
        private bool _columnResizeStarted;
        private bool _normalizingColumnWidths;
        private bool _isApplyingBatchCellValue;

        public CreateViewsAndSheetsWindow(CreateViewsAndSheetsViewModel viewModel)
            : this(viewModel, false)
        {
        }

        public CreateViewsAndSheetsWindow(CreateViewsAndSheetsViewModel viewModel, bool openSettingsWindowAfterLoaded)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _layoutService = new CreateViewsAndSheetsWindowLayoutService();
            _openSettingsWindowAfterLoaded = openSettingsWindowAfterLoaded;
            _sheetBrowserParameterColumns = new List<DataGridColumn>();

            InitializeWindowFromXamlFile();
            DataContext = _viewModel;

            _viewModel.RequestClose += ViewModel_RequestClose;
            _viewModel.RequestSettingsWindow += ViewModel_RequestSettingsWindow;
            _viewModel.RequestSheetTableImport += ViewModel_RequestSheetTableImport;
            _viewModel.RequestSettingsExport += ViewModel_RequestSettingsExport;
            _viewModel.RequestSettingsImport += ViewModel_RequestSettingsImport;
            _viewModel.RequestPointSelection += ViewModel_RequestPointSelection;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            Loaded += CreateViewsAndSheetsWindow_Loaded;
            Closing += CreateViewsAndSheetsWindow_Closing;
            Closed += CreateViewsAndSheetsWindow_Closed;
        }

        public PlacementPointSelectionRequestEventArgs PendingPointSelectionRequest { get; private set; }

        public bool OpenSettingsAfterPointSelection { get; private set; }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(CreateViewsAndSheetsWindow).Assembly.Location);
            string xamlPath = Path.Combine(assemblyDirectory, "Cls_CreateViewsAndSheets", "Views", "CreateViewsAndSheetsWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл окна создания видов и листов не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext parserContext = new ParserContext();
                parserContext.BaseUri = new Uri(xamlPath, UriKind.Absolute);

                Window loadedWindow = XamlReader.Load(stream, parserContext) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось загрузить CreateViewsAndSheetsWindow.xaml.");
                }

                Title = loadedWindow.Title;
                Width = loadedWindow.Width;
                Height = loadedWindow.Height;
                MinWidth = loadedWindow.MinWidth;
                MinHeight = loadedWindow.MinHeight;
                WindowStartupLocation = loadedWindow.WindowStartupLocation;
                ResizeMode = loadedWindow.ResizeMode;
                Style = loadedWindow.Style;
                Background = loadedWindow.Background;
                FontFamily = loadedWindow.FontFamily;
                FontSize = loadedWindow.FontSize;
                FontWeight = loadedWindow.FontWeight;
                Resources = loadedWindow.Resources;
                Content = loadedWindow.Content;
            }
        }

        private void CreateViewsAndSheetsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AttachSettingsDrawerHandlers();
            AttachRowsDataGridHandlers();
            RestoreWindowLayout();
            AttachAnimatedFeedbackTargets();
            SabWindowBehaviorService.ApplyLoadedBehavior(this);
            OpenSettingsWindowAfterLoadedIfNeeded();
        }

        private void ViewModel_RequestClose(object sender, EventArgs e)
        {
            DialogResult = _viewModel.IsAccepted;
            Close();
        }

        private void ViewModel_RequestSettingsWindow(object sender, EventArgs e)
        {
            CreateViewsAndSheetsSettingsWindow settingsWindow = new CreateViewsAndSheetsSettingsWindow(_viewModel);
            settingsWindow.Owner = this;
            _viewModel.RequestPointSelection -= ViewModel_RequestPointSelection;
            try
            {
                settingsWindow.ShowDialog();
            }
            finally
            {
                _viewModel.RequestPointSelection += ViewModel_RequestPointSelection;
            }

            if (settingsWindow.PendingPointSelectionRequest != null)
            {
                PendingPointSelectionRequest = settingsWindow.PendingPointSelectionRequest;
                OpenSettingsAfterPointSelection = true;
                DialogResult = false;
                Close();
                return;
            }

            ScheduleNormalizeRowsDataGridColumnWidths();
        }

        private void ViewModel_RequestSheetTableImport(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Выберите таблицу листов";
            dialog.Filter = "Excel (*.xlsx)|*.xlsx";
            dialog.CheckFileExists = true;
            dialog.Multiselect = false;

            bool? result = dialog.ShowDialog(this);
            if (result != true)
            {
                return;
            }

            try
            {
                SheetTableImportService importService = new SheetTableImportService();
                IList<SheetTableImportRow> importedRows = importService.ReadRows(dialog.FileName);
                _viewModel.ImportSheetTableRows(importedRows);
                ScheduleNormalizeRowsDataGridColumnWidths();
            }
            catch (Exception exception)
            {
                RevitTaskDialog.Show(
                    "Создание видов и листов",
                    "Не удалось загрузить таблицу Excel.\n\n" + exception.Message);
            }
        }

        private void ViewModel_RequestSettingsExport(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Title = "Сохранить настройки создания видов и листов";
            dialog.Filter = "SAB настройки (*.json)|*.json|Все файлы (*.*)|*.*";
            dialog.FileName = "SAB_CreateViewsAndSheets_Settings.json";
            dialog.OverwritePrompt = true;

            bool? result = dialog.ShowDialog(this);
            if (result != true)
            {
                return;
            }

            try
            {
                SettingsService settingsService = new SettingsService();
                settingsService.ExportSettingsToFile(_viewModel.BuildSessionSettings(), dialog.FileName);
                ShowSettingsFolderSuccessNotification(
                    "Экспорт настроек",
                    "Файл настроек сохранен:\n" + Path.GetFileName(dialog.FileName) + "\nПапка:",
                    Path.GetDirectoryName(dialog.FileName));
            }
            catch (Exception exception)
            {
                ShowSettingsErrorNotification(
                    "Экспорт настроек",
                    "Не удалось сохранить настройки в файл.\n\n" + exception.Message);
            }
        }

        private void ViewModel_RequestSettingsImport(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Выберите файл настроек создания видов и листов";
            dialog.Filter = "SAB настройки (*.json)|*.json|Все файлы (*.*)|*.*";
            dialog.CheckFileExists = true;
            dialog.Multiselect = false;

            bool? result = dialog.ShowDialog(this);
            if (result != true)
            {
                return;
            }

            try
            {
                List<string> warnings = new List<string>();
                SettingsService settingsService = new SettingsService();
                CreateViewsAndSheetsSettings importedSettings = settingsService.ImportSettingsFromFile(dialog.FileName, warnings);
                _viewModel.ApplyImportedSettings(importedSettings);
                ScheduleNormalizeRowsDataGridColumnWidths();

                string message = "Настройки загружены из файла.";
                if (warnings.Count > 0)
                {
                    message += "\n\nПредупреждения:\n" + string.Join("\n", warnings);
                    ShowSettingsWarningNotification("Импорт настроек", message);
                }
                else
                {
                    ShowSettingsSuccessNotification("Импорт настроек", message);
                }
            }
            catch (Exception exception)
            {
                ShowSettingsErrorNotification(
                    "Импорт настроек",
                    "Не удалось загрузить настройки из файла.\n\n" + exception.Message);
            }
        }

        private static void ShowSettingsSuccessNotification(string title, string message)
        {
            try
            {
                ToastNotifier.ShowSuccess(title, message, 8);
            }
            catch
            {
                RevitTaskDialog.Show(title, message);
            }
        }

        private static void ShowSettingsWarningNotification(string title, string message)
        {
            try
            {
                ToastNotifier.ShowWarning(title, message, 12);
            }
            catch
            {
                RevitTaskDialog.Show(title, message);
            }
        }

        private static void ShowSettingsErrorNotification(string title, string message)
        {
            try
            {
                ToastNotifier.ShowError(title, message, 12);
            }
            catch
            {
                RevitTaskDialog.Show(title, message);
            }
        }

        private static void ShowSettingsFolderSuccessNotification(string title, string message, string folderPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath))
                {
                    ToastNotifier.ShowSuccess(title, message, 8);
                }
                else
                {
                    ToastNotifier.ShowFolderLinkSuccess(title, message, folderPath, 10);
                }
            }
            catch
            {
                RevitTaskDialog.Show(title, string.IsNullOrWhiteSpace(folderPath) ? message : message + "\n" + folderPath);
            }
        }

        private void OpenSettingsWindowAfterLoadedIfNeeded()
        {
            if (!_openSettingsWindowAfterLoaded)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(OpenSettingsWindowAfterLoaded), DispatcherPriority.ContextIdle);
        }

        private void OpenSettingsWindowAfterLoaded()
        {
            if (!IsLoaded || PendingPointSelectionRequest != null || _viewModel == null)
            {
                return;
            }

            if (_viewModel.OpenSettingsWindowCommand == null ||
                !_viewModel.OpenSettingsWindowCommand.CanExecute(null))
            {
                return;
            }

            _viewModel.OpenSettingsWindowCommand.Execute(null);
        }

        private void ViewModel_RequestPointSelection(object sender, PlacementPointSelectionRequestEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            PendingPointSelectionRequest = e;
            DialogResult = false;
            Close();
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (string.Equals(e.PropertyName, "IsSingleStoryStructure", StringComparison.Ordinal) ||
                string.Equals(e.PropertyName, "IsMultiStoryStructure", StringComparison.Ordinal) ||
                string.Equals(e.PropertyName, "IsMultiViewStructure", StringComparison.Ordinal) ||
                string.Equals(e.PropertyName, "IsDeletionMode", StringComparison.Ordinal) ||
                string.Equals(e.PropertyName, "ActiveRows", StringComparison.Ordinal))
            {
                ConfigureRowsGrouping();
                ResetRowsGridTransientState();
                UpdateFloorColumnVisibility();
            }

            if (string.Equals(e.PropertyName, "UseSourceSheetViewportPlacement", StringComparison.Ordinal))
            {
                ApplyPlacementModeAnimation(true);
            }

            if (string.Equals(e.PropertyName, "ValidationSummary", StringComparison.Ordinal))
            {
                SabWindowAnimationService.PulseElement(_validationPanelBorder);
            }

            if (string.Equals(e.PropertyName, "StatusText", StringComparison.Ordinal))
            {
                SabWindowAnimationService.PulseElement(_validationPanelBorder);
            }

            if (string.Equals(e.PropertyName, "CanCreate", StringComparison.Ordinal))
            {
                ScheduleCreateButtonPulse();
            }
        }

        private void CreateViewsAndSheetsWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowLayout();
        }

        private void CreateViewsAndSheetsWindow_Closed(object sender, EventArgs e)
        {
            DetachRowsDataGridHandlers();
            DetachSettingsDrawerHandlers();
            _validationPanelBorder = null;
            _createButton = null;
            _manualViewportPlacementPanel = null;
            _sourceSheetPlacementToggle = null;
            _pointPlacementToggle = null;
            _viewModel.RequestClose -= ViewModel_RequestClose;
            _viewModel.RequestSettingsWindow -= ViewModel_RequestSettingsWindow;
            _viewModel.RequestSheetTableImport -= ViewModel_RequestSheetTableImport;
            _viewModel.RequestSettingsExport -= ViewModel_RequestSettingsExport;
            _viewModel.RequestSettingsImport -= ViewModel_RequestSettingsImport;
            _viewModel.RequestPointSelection -= ViewModel_RequestPointSelection;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            Loaded -= CreateViewsAndSheetsWindow_Loaded;
            Closing -= CreateViewsAndSheetsWindow_Closing;
            Closed -= CreateViewsAndSheetsWindow_Closed;
        }

        private void AttachAnimatedFeedbackTargets()
        {
            _validationPanelBorder = FindVisualChildByName<Border>(this, "ValidationPanelBorder");
            _createButton = FindVisualChildByName<Button>(this, "CreateButton");
            _manualViewportPlacementPanel = FindVisualChildByName<FrameworkElement>(this, "ManualViewportPlacementPanel");
            _sourceSheetPlacementToggle = FindVisualChildByName<ButtonBase>(this, "SourceSheetPlacementToggle");
            _pointPlacementToggle = FindVisualChildByName<ButtonBase>(this, "PointPlacementToggle");
            ApplyPlacementModeAnimation(false);
        }

        private void ApplyPlacementModeAnimation(bool animate)
        {
            if (_viewModel == null)
            {
                return;
            }

            SabWindowAnimationService.AnimatePlacementModePanel(
                _manualViewportPlacementPanel,
                _viewModel.UseSourceSheetViewportPlacement,
                animate);

            if (animate)
            {
                ButtonBase activePlacementToggle = _viewModel.UseSourceSheetViewportPlacement
                    ? _sourceSheetPlacementToggle
                    : _pointPlacementToggle;
                SabWindowAnimationService.PulseButton(activePlacementToggle);
            }
        }

        private void ScheduleCreateButtonPulse()
        {
            if (_createButton == null || _viewModel == null || !_viewModel.CanCreate)
            {
                return;
            }

            Dispatcher.BeginInvoke(
                new Action(delegate
                {
                    SabWindowAnimationService.PulseButton(_createButton);
                }),
                DispatcherPriority.Background);
        }

        private void AttachSettingsDrawerHandlers()
        {
            _settingsDrawerToggle = FindVisualChildByName<ToggleButton>(this, "SettingsDrawerToggle");
            _settingsDrawerPanel = FindVisualChildByName<FrameworkElement>(this, "SettingsDrawerPanel");

            if (_settingsDrawerToggle == null)
            {
                return;
            }

            _settingsDrawerToggle.Checked += SettingsDrawerToggle_CheckedChanged;
            _settingsDrawerToggle.Unchecked += SettingsDrawerToggle_CheckedChanged;
            ApplySettingsDrawerState(_settingsDrawerToggle.IsChecked != false);
        }

        private void DetachSettingsDrawerHandlers()
        {
            if (_settingsDrawerToggle == null)
            {
                return;
            }

            _settingsDrawerToggle.Checked -= SettingsDrawerToggle_CheckedChanged;
            _settingsDrawerToggle.Unchecked -= SettingsDrawerToggle_CheckedChanged;
            _settingsDrawerToggle = null;
            _settingsDrawerPanel = null;
        }

        private void SettingsDrawerToggle_CheckedChanged(object sender, RoutedEventArgs e)
        {
            ApplySettingsDrawerState(_settingsDrawerToggle == null || _settingsDrawerToggle.IsChecked != false);
        }

        private void ApplySettingsDrawerState(bool isOpen)
        {
            if (_settingsDrawerPanel != null)
            {
                _settingsDrawerPanel.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
            }

            if (_settingsDrawerToggle != null)
            {
                _settingsDrawerToggle.ToolTip = isOpen ? "Скрыть настройки" : "Показать настройки";
            }

            ScheduleNormalizeRowsDataGridColumnWidths();
        }

        private void AttachRowsDataGridHandlers()
        {
            if (_gridHandlersAttached)
            {
                return;
            }

            _rowsDataGrid = FindVisualChildByName<DataGrid>(this, "CreationRowsDataGrid");
            if (_rowsDataGrid == null)
            {
                return;
            }

            _rowsDataGrid.AllowDrop = true;
            _rowsDataGrid.PreviewMouseLeftButtonDown += RowsDataGrid_PreviewMouseLeftButtonDown;
            _rowsDataGrid.PreviewMouseLeftButtonUp += RowsDataGrid_PreviewMouseLeftButtonUp;
            _rowsDataGrid.PreviewKeyDown += RowsDataGrid_PreviewKeyDown;
            _rowsDataGrid.MouseMove += RowsDataGrid_MouseMove;
            _rowsDataGrid.SizeChanged += RowsDataGrid_SizeChanged;
            _rowsDataGrid.DragOver += RowsDataGrid_DragOver;
            _rowsDataGrid.Drop += RowsDataGrid_Drop;
            _rowsDataGrid.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(RowsDataGrid_EditorTextChanged), true);
            _rowsDataGrid.AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(RowsDataGrid_EditorSelectionChanged), true);
            ConfigureRowsGrouping();
            RebuildSheetBrowserParameterColumns();
            UpdateFloorColumnVisibility();
            _gridHandlersAttached = true;
        }

        private void DetachRowsDataGridHandlers()
        {
            if (!_gridHandlersAttached || _rowsDataGrid == null)
            {
                return;
            }

            _rowsDataGrid.PreviewMouseLeftButtonDown -= RowsDataGrid_PreviewMouseLeftButtonDown;
            _rowsDataGrid.PreviewMouseLeftButtonUp -= RowsDataGrid_PreviewMouseLeftButtonUp;
            _rowsDataGrid.PreviewKeyDown -= RowsDataGrid_PreviewKeyDown;
            _rowsDataGrid.MouseMove -= RowsDataGrid_MouseMove;
            _rowsDataGrid.SizeChanged -= RowsDataGrid_SizeChanged;
            _rowsDataGrid.DragOver -= RowsDataGrid_DragOver;
            _rowsDataGrid.Drop -= RowsDataGrid_Drop;
            _rowsDataGrid.RemoveHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(RowsDataGrid_EditorTextChanged));
            _rowsDataGrid.RemoveHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(RowsDataGrid_EditorSelectionChanged));
            _batchEditRowsSnapshot = null;
            _batchEditSourceRow = null;
            _lastMultiSelectedRowsSnapshot = null;
            _batchEditPropertyPath = null;
            _selectionAnchorRow = null;
            _draggedRows = null;
            _draggedRow = null;
            _gridHandlersAttached = false;
        }

        private void ConfigureRowsGrouping()
        {
            if (_rowsDataGrid == null || _viewModel == null)
            {
                return;
            }

            IList<SheetCreationRowViewModel> activeRows = GetActiveRows();
            if (activeRows == null)
            {
                return;
            }

            ICollectionView rowsView = CollectionViewSource.GetDefaultView(activeRows);
            if (rowsView == null)
            {
                return;
            }

            rowsView.GroupDescriptions.Clear();
            rowsView.GroupDescriptions.Add(new PropertyGroupDescription("ProjectSectionGroupName"));

            ICollectionViewLiveShaping liveShapingView = rowsView as ICollectionViewLiveShaping;
            if (liveShapingView != null && liveShapingView.CanChangeLiveGrouping)
            {
                liveShapingView.LiveGroupingProperties.Clear();
                liveShapingView.LiveGroupingProperties.Add("ProjectSectionGroupName");
                liveShapingView.IsLiveGrouping = true;
            }
        }

        private IList<SheetCreationRowViewModel> GetActiveRows()
        {
            if (_viewModel == null || _viewModel.ActiveRows == null)
            {
                return null;
            }

            return _viewModel.ActiveRows;
        }

        private void ResetRowsGridTransientState()
        {
            _batchEditRowsSnapshot = null;
            _batchEditSourceRow = null;
            _lastMultiSelectedRowsSnapshot = null;
            _batchEditPropertyPath = null;
            _selectionAnchorRow = null;
            _draggedRows = null;
            _draggedRow = null;

            if (_rowsDataGrid != null)
            {
                _rowsDataGrid.SelectedItems.Clear();
            }
        }

        private void RowsDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _draggedRow = null;
            _draggedRows = null;
            _resizedColumn = null;
            _columnResizeStarted = false;

            DependencyObject source = e.OriginalSource as DependencyObject;
            DataGridRow clickedRow = FindParent<DataGridRow>(source);
            SheetCreationRowViewModel clickedRowViewModel = clickedRow != null ? clickedRow.Item as SheetCreationRowViewModel : null;
            if (TrySelectRowsByShift(clickedRowViewModel))
            {
                e.Handled = true;
                return;
            }

            if (_viewModel != null && _viewModel.IsDeletionMode)
            {
                return;
            }

            if (!IsBatchEditableGridChild(source))
            {
                _lastMultiSelectedRowsSnapshot = null;
            }

            RememberBatchEditSelection(clickedRowViewModel, source);
            RememberSelectionAnchor(clickedRowViewModel);

            _resizedColumn = GetColumnResizeTarget(source);
            if (_resizedColumn != null)
            {
                _columnResizeStarted = true;
                return;
            }

            if (IsRowDragHandle(source))
            {
                DataGridRow row = clickedRow;
                if (row != null)
                {
                    _draggedRow = row.Item as SheetCreationRowViewModel;
                    _draggedRows = BuildDraggedRows(_draggedRow);
                }

                return;
            }

            if (IsInteractiveGridChild(source))
            {
                return;
            }

            DataGridCell cell = FindParent<DataGridCell>(source);
            if (TryBeginEditOnSingleClick(cell))
            {
                e.Handled = true;
                return;
            }
        }

        private void RowsDataGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_columnResizeStarted)
            {
                return;
            }

            _columnResizeStarted = false;
            SetColumnWidthToActualPixel(_resizedColumn);
            StretchRowsDataGridRightSideIfNeeded();
            _resizedColumn = null;
        }

        private void RowsDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e == null || e.Key != Key.Enter)
            {
                return;
            }

            DependencyObject source = e.OriginalSource as DependencyObject;
            if (source == null)
            {
                return;
            }

            DataGridCell cell = FindParent<DataGridCell>(source);
            if (cell == null || cell.Column == null)
            {
                return;
            }

            string propertyPath = cell.Column.SortMemberPath;
            if (string.IsNullOrWhiteSpace(propertyPath) ||
                !propertyPath.StartsWith("SheetBrowserParameterValues[", StringComparison.Ordinal))
            {
                return;
            }

            UpdateEditorBindingSource(source);
            _rowsDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            Keyboard.ClearFocus();
            e.Handled = true;
        }

        private bool TrySelectRowsByShift(SheetCreationRowViewModel clickedRow)
        {
            IList<SheetCreationRowViewModel> activeRows = GetActiveRows();
            if (_rowsDataGrid == null || activeRows == null || clickedRow == null)
            {
                return false;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift)
            {
                return false;
            }

            SheetCreationRowViewModel anchorRow = _selectionAnchorRow;
            if (anchorRow == null || !activeRows.Contains(anchorRow))
            {
                anchorRow = _rowsDataGrid.SelectedItem as SheetCreationRowViewModel;
            }

            if (anchorRow == null || !activeRows.Contains(anchorRow))
            {
                anchorRow = clickedRow;
            }

            int anchorIndex = activeRows.IndexOf(anchorRow);
            int clickedIndex = activeRows.IndexOf(clickedRow);
            if (anchorIndex < 0 || clickedIndex < 0)
            {
                return false;
            }

            int startIndex = Math.Min(anchorIndex, clickedIndex);
            int endIndex = Math.Max(anchorIndex, clickedIndex);

            _rowsDataGrid.SelectedItems.Clear();
            for (int i = startIndex; i <= endIndex; i++)
            {
                SheetCreationRowViewModel row = activeRows[i];
                if (row != null)
                {
                    _rowsDataGrid.SelectedItems.Add(row);
                }
            }

            _rowsDataGrid.CurrentItem = clickedRow;
            _selectionAnchorRow = anchorRow;
            _batchEditSourceRow = null;
            _batchEditRowsSnapshot = null;
            _batchEditPropertyPath = null;
            _lastMultiSelectedRowsSnapshot = GetSelectedRowsSnapshot();
            return true;
        }

        private void RememberSelectionAnchor(SheetCreationRowViewModel clickedRow)
        {
            if (clickedRow == null)
            {
                return;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                return;
            }

            _selectionAnchorRow = clickedRow;
        }

        private void RememberBatchEditSelection(SheetCreationRowViewModel clickedRow, DependencyObject source)
        {
            _batchEditSourceRow = null;
            _batchEditRowsSnapshot = null;
            _batchEditPropertyPath = null;

            if (_viewModel != null && _viewModel.IsDeletionMode)
            {
                return;
            }

            if (_rowsDataGrid == null || clickedRow == null || !IsBatchEditableGridChild(source))
            {
                return;
            }

            string propertyPath = GetBatchEditablePropertyPath(source);
            if (string.IsNullOrWhiteSpace(propertyPath))
            {
                return;
            }

            List<SheetCreationRowViewModel> selectedRows = GetSelectedRowsSnapshot();
            if (selectedRows.Count > 1)
            {
                _lastMultiSelectedRowsSnapshot = selectedRows;
            }

            if ((selectedRows.Count <= 1 || !selectedRows.Contains(clickedRow)) &&
                _lastMultiSelectedRowsSnapshot != null &&
                _lastMultiSelectedRowsSnapshot.Count > 1 &&
                _lastMultiSelectedRowsSnapshot.Contains(clickedRow))
            {
                selectedRows = new List<SheetCreationRowViewModel>(_lastMultiSelectedRowsSnapshot);
            }

            if (selectedRows.Count <= 1 || !selectedRows.Contains(clickedRow))
            {
                return;
            }

            _batchEditSourceRow = clickedRow;
            _batchEditRowsSnapshot = selectedRows;
            _batchEditPropertyPath = propertyPath;
        }

        private List<SheetCreationRowViewModel> GetSelectedRowsSnapshot()
        {
            List<SheetCreationRowViewModel> result = new List<SheetCreationRowViewModel>();
            if (_rowsDataGrid == null)
            {
                return result;
            }

            for (int i = 0; i < _rowsDataGrid.SelectedItems.Count; i++)
            {
                SheetCreationRowViewModel row = _rowsDataGrid.SelectedItems[i] as SheetCreationRowViewModel;
                if (row != null && !result.Contains(row))
                {
                    result.Add(row);
                }
            }

            return result;
        }

        private List<SheetCreationRowViewModel> BuildDraggedRows(SheetCreationRowViewModel clickedRow)
        {
            List<SheetCreationRowViewModel> result = new List<SheetCreationRowViewModel>();
            IList<SheetCreationRowViewModel> activeRows = GetActiveRows();
            if (_rowsDataGrid == null ||
                _viewModel == null ||
                _viewModel.IsDeletionMode ||
                activeRows == null ||
                clickedRow == null)
            {
                return result;
            }

            List<SheetCreationRowViewModel> selectedRows = GetSelectedRowsSnapshot();
            if (!selectedRows.Contains(clickedRow))
            {
                _rowsDataGrid.SelectedItems.Clear();
                _rowsDataGrid.SelectedItems.Add(clickedRow);
                result.Add(clickedRow);
                return result;
            }

            // Перенос блока строк сохраняет порядок строк из таблицы, а не порядок кликов по выделению.
            for (int i = 0; i < activeRows.Count; i++)
            {
                SheetCreationRowViewModel row = activeRows[i];
                if (row != null && selectedRows.Contains(row))
                {
                    result.Add(row);
                }
            }

            if (result.Count == 0)
            {
                result.Add(clickedRow);
            }

            return result;
        }

        private void RestoreDraggedRowsSelection(IList<SheetCreationRowViewModel> draggedRows)
        {
            if (_rowsDataGrid == null || draggedRows == null)
            {
                return;
            }

            _rowsDataGrid.SelectedItems.Clear();
            for (int i = 0; i < draggedRows.Count; i++)
            {
                SheetCreationRowViewModel row = draggedRows[i];
                if (row != null)
                {
                    _rowsDataGrid.SelectedItems.Add(row);
                }
            }

            if (draggedRows.Count > 0)
            {
                _rowsDataGrid.CurrentItem = draggedRows[0];
            }
        }

        private bool IsBatchEditableGridChild(DependencyObject source)
        {
            DependencyObject current = source;
            while (current != null)
            {
                if (current is DataGridRow)
                {
                    return false;
                }

                if (current is TextBoxBase || current is ComboBox)
                {
                    return true;
                }

                current = GetParentObject(current);
            }

            return false;
        }

        private void RowsDataGrid_EditorTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isApplyingBatchCellValue)
            {
                return;
            }

            TextBoxBase textBox = e.OriginalSource as TextBoxBase;
            if (textBox == null)
            {
                return;
            }

            string propertyPath = GetBatchEditablePropertyPath(textBox);
            if (!string.IsNullOrWhiteSpace(propertyPath) &&
                propertyPath.StartsWith("SheetBrowserParameterValues[", StringComparison.Ordinal))
            {
                return;
            }

            ApplyBatchValueFromEditor(textBox);
        }

        private void RowsDataGrid_EditorSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isApplyingBatchCellValue)
            {
                return;
            }

            if (ReferenceEquals(e.OriginalSource, _rowsDataGrid))
            {
                RememberLastMultiRowSelection();
                return;
            }

            ComboBox comboBox = e.OriginalSource as ComboBox;
            if (comboBox == null)
            {
                return;
            }

            ApplyBatchValueFromEditor(comboBox);
        }

        private void ApplyBatchValueFromEditor(DependencyObject editor)
        {
            if (_viewModel == null || _rowsDataGrid == null)
            {
                return;
            }

            if (_viewModel.IsDeletionMode)
            {
                return;
            }

            if (!IsEditorKeyboardFocusWithin(editor))
            {
                return;
            }

            if (_batchEditSourceRow == null || _batchEditRowsSnapshot == null)
            {
                TryRestoreBatchEditSelectionFromEditor(editor);
            }

            if (_batchEditSourceRow == null || _batchEditRowsSnapshot == null)
            {
                return;
            }

            if (_batchEditRowsSnapshot.Count <= 1 || !_batchEditRowsSnapshot.Contains(_batchEditSourceRow))
            {
                return;
            }

            DataGridCell cell = FindParent<DataGridCell>(editor);
            if (cell == null || cell.Column == null)
            {
                return;
            }

            string propertyPath = cell.Column.SortMemberPath;
            if (string.IsNullOrWhiteSpace(propertyPath))
            {
                return;
            }

            // Блок пакетного редактирования должен работать только с той колонкой,
            // в которой пользователь начал изменение. Это защищает соседние поля
            // от случайных TextChanged/SelectionChanged событий WPF.
            if (!string.IsNullOrWhiteSpace(_batchEditPropertyPath) &&
                !string.Equals(_batchEditPropertyPath, propertyPath, StringComparison.Ordinal))
            {
                ShowBatchEditDebugMessage(
                    "Пропущено событие другой колонки",
                    propertyPath,
                    _batchEditSourceRow,
                    _batchEditRowsSnapshot.Count);
                return;
            }

            _batchEditPropertyPath = propertyPath;
            UpdateEditorBindingSource(editor);
            ShowBatchEditDebugMessage(
                "Перед пакетным применением",
                propertyPath,
                _batchEditSourceRow,
                _batchEditRowsSnapshot.Count);

            try
            {
                _isApplyingBatchCellValue = true;
                _viewModel.ApplyBatchCellValue(_batchEditSourceRow, _batchEditRowsSnapshot, propertyPath);
            }
            finally
            {
                _isApplyingBatchCellValue = false;
            }
        }

        private void RememberLastMultiRowSelection()
        {
            List<SheetCreationRowViewModel> selectedRows = GetSelectedRowsSnapshot();
            if (selectedRows.Count > 1)
            {
                _lastMultiSelectedRowsSnapshot = selectedRows;
            }
        }

        private void TryRestoreBatchEditSelectionFromEditor(DependencyObject editor)
        {
            SheetCreationRowViewModel sourceRow = GetRowViewModelFromEditor(editor);
            if (sourceRow == null)
            {
                return;
            }

            string propertyPath = GetBatchEditablePropertyPath(editor);
            if (string.IsNullOrWhiteSpace(propertyPath))
            {
                return;
            }

            List<SheetCreationRowViewModel> selectedRows = GetSelectedRowsSnapshot();
            if ((selectedRows.Count <= 1 || !selectedRows.Contains(sourceRow)) &&
                _lastMultiSelectedRowsSnapshot != null &&
                _lastMultiSelectedRowsSnapshot.Count > 1 &&
                _lastMultiSelectedRowsSnapshot.Contains(sourceRow))
            {
                selectedRows = new List<SheetCreationRowViewModel>(_lastMultiSelectedRowsSnapshot);
            }

            if (selectedRows.Count <= 1 || !selectedRows.Contains(sourceRow))
            {
                return;
            }

            _batchEditSourceRow = sourceRow;
            _batchEditRowsSnapshot = selectedRows;
            _batchEditPropertyPath = propertyPath;
        }

        private string GetBatchEditablePropertyPath(DependencyObject source)
        {
            DataGridCell cell = FindParent<DataGridCell>(source);
            if (cell == null || cell.Column == null)
            {
                return string.Empty;
            }

            return cell.Column.SortMemberPath ?? string.Empty;
        }

        private bool IsEditorKeyboardFocusWithin(DependencyObject editor)
        {
            FrameworkElement frameworkElement = editor as FrameworkElement;
            if (frameworkElement == null)
            {
                return true;
            }

            if (frameworkElement.IsKeyboardFocusWithin)
            {
                return true;
            }

            ComboBox comboBox = editor as ComboBox;
            if (comboBox == null)
            {
                comboBox = FindParent<ComboBox>(editor);
            }

            return comboBox != null && comboBox.IsKeyboardFocusWithin;
        }

        private SheetCreationRowViewModel GetRowViewModelFromEditor(DependencyObject editor)
        {
            DataGridRow row = FindParent<DataGridRow>(editor);
            if (row != null)
            {
                return row.Item as SheetCreationRowViewModel;
            }

            FrameworkElement frameworkElement = editor as FrameworkElement;
            if (frameworkElement != null)
            {
                return frameworkElement.DataContext as SheetCreationRowViewModel;
            }

            return null;
        }

        private void ShowBatchEditDebugMessage(
            string step,
            string propertyPath,
            SheetCreationRowViewModel sourceRow,
            int selectedRowsCount)
        {
            if (!ShowBatchEditDebugDialogs)
            {
                return;
            }

            string message =
                "Шаг: " + (step ?? string.Empty) + "\n" +
                "Поле: " + (propertyPath ?? string.Empty) + "\n" +
                "Исходная строка: " + (sourceRow != null ? sourceRow.RowNumber.ToString() : "нет") + "\n" +
                "Выбранных строк: " + selectedRowsCount + "\n" +
                "Масштаб исходной строки: " + (sourceRow != null ? sourceRow.ViewScaleText : string.Empty);

            RevitTaskDialog.Show("Отладка пакетного редактирования", message);
        }

        private void UpdateEditorBindingSource(DependencyObject editor)
        {
            TextBox textBox = editor as TextBox;
            if (textBox != null)
            {
                ComboBox ownerComboBox = FindParent<ComboBox>(textBox);
                if (ownerComboBox == null)
                {
                    BindingExpression bindingExpression = textBox.GetBindingExpression(TextBox.TextProperty);
                    if (bindingExpression != null)
                    {
                        bindingExpression.UpdateSource();
                        return;
                    }
                }
            }

            ComboBox comboBox = editor as ComboBox;
            if (comboBox == null)
            {
                comboBox = FindParent<ComboBox>(editor);
            }

            if (comboBox == null)
            {
                return;
            }

            BindingExpression textBinding = comboBox.GetBindingExpression(ComboBox.TextProperty);
            if (textBinding != null)
            {
                textBinding.UpdateSource();
            }

            BindingExpression selectedValueBinding = comboBox.GetBindingExpression(Selector.SelectedValueProperty);
            if (selectedValueBinding != null)
            {
                selectedValueBinding.UpdateSource();
            }

            BindingExpression selectedItemBinding = comboBox.GetBindingExpression(Selector.SelectedItemProperty);
            if (selectedItemBinding != null)
            {
                selectedItemBinding.UpdateSource();
            }
        }

        private void RowsDataGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ScheduleStretchRowsDataGridRightSide();
        }

        private void RowsDataGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (_columnResizeStarted)
            {
                return;
            }

            if (_viewModel != null && _viewModel.IsDeletionMode)
            {
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed || _draggedRows == null || _draggedRows.Count == 0 || _rowsDataGrid == null)
            {
                return;
            }

            Point currentPosition = e.GetPosition(null);
            double offsetX = Math.Abs(currentPosition.X - _dragStartPoint.X);
            double offsetY = Math.Abs(currentPosition.Y - _dragStartPoint.Y);
            if (offsetX < SystemParameters.MinimumHorizontalDragDistance &&
                offsetY < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            DragDrop.DoDragDrop(_rowsDataGrid, _draggedRows, DragDropEffects.Move);
            _draggedRow = null;
            _draggedRows = null;
        }

        private void RowsDataGrid_DragOver(object sender, DragEventArgs e)
        {
            if (_viewModel != null && _viewModel.IsDeletionMode)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            if (!e.Data.GetDataPresent(typeof(List<SheetCreationRowViewModel>)) &&
                !e.Data.GetDataPresent(typeof(SheetCreationRowViewModel)))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void RowsDataGrid_Drop(object sender, DragEventArgs e)
        {
            if (_viewModel != null && _viewModel.IsDeletionMode)
            {
                return;
            }

            List<SheetCreationRowViewModel> draggedRows = e.Data.GetData(typeof(List<SheetCreationRowViewModel>)) as List<SheetCreationRowViewModel>;
            if ((draggedRows == null || draggedRows.Count == 0) && e.Data.GetDataPresent(typeof(SheetCreationRowViewModel)))
            {
                SheetCreationRowViewModel singleDraggedRow = e.Data.GetData(typeof(SheetCreationRowViewModel)) as SheetCreationRowViewModel;
                if (singleDraggedRow != null)
                {
                    draggedRows = new List<SheetCreationRowViewModel> { singleDraggedRow };
                }
            }

            if (draggedRows == null || draggedRows.Count == 0 || _rowsDataGrid == null)
            {
                return;
            }

            int targetIndex = CalculateDropTargetIndex(e.OriginalSource as DependencyObject);
            _viewModel.MoveRowsToIndex(draggedRows, targetIndex);
            RestoreDraggedRowsSelection(draggedRows);
            e.Handled = true;
        }

        private int CalculateDropTargetIndex(DependencyObject dropSource)
        {
            if (_rowsDataGrid == null || _viewModel.Rows == null || _viewModel.Rows.Count == 0)
            {
                return 0;
            }

            DataGridRow targetRow = FindParent<DataGridRow>(dropSource);
            if (targetRow == null)
            {
                return _viewModel.Rows.Count;
            }

            SheetCreationRowViewModel targetRowViewModel = targetRow.Item as SheetCreationRowViewModel;
            int targetIndex = _viewModel.Rows.IndexOf(targetRowViewModel);
            if (targetIndex < 0)
            {
                return _viewModel.Rows.Count;
            }

            Point pointInTargetRow = Mouse.GetPosition(targetRow);
            int insertionIndex = pointInTargetRow.Y > targetRow.ActualHeight / 2.0
                ? targetIndex + 1
                : targetIndex;

            if (insertionIndex < 0)
            {
                insertionIndex = 0;
            }

            if (insertionIndex > _viewModel.Rows.Count)
            {
                insertionIndex = _viewModel.Rows.Count;
            }

            return insertionIndex;
        }

        private bool IsInteractiveGridChild(DependencyObject source)
        {
            DependencyObject current = source;
            while (current != null)
            {
                if (current is DataGridRow)
                {
                    return false;
                }

                if (current is ButtonBase ||
                    current is TextBoxBase ||
                    current is ComboBox ||
                    current is Thumb ||
                    current is ScrollBar ||
                    current is DataGridColumnHeader)
                {
                    return true;
                }

                current = GetParentObject(current);
            }

            return false;
        }

        private bool IsRowDragHandle(DependencyObject source)
        {
            DependencyObject current = source;
            while (current != null)
            {
                FrameworkElement element = current as FrameworkElement;
                if (element != null && string.Equals(element.Tag as string, "RowDragHandle", StringComparison.Ordinal))
                {
                    return true;
                }

                if (current is DataGridRow)
                {
                    return false;
                }

                current = GetParentObject(current);
            }

            return false;
        }

        private DataGridColumn GetColumnResizeTarget(DependencyObject source)
        {
            Thumb thumb = FindParent<Thumb>(source);
            if (thumb == null)
            {
                return null;
            }

            DataGridColumnHeader header = FindParent<DataGridColumnHeader>(thumb);
            return header != null ? header.Column : null;
        }

        private bool TryBeginEditOnSingleClick(DataGridCell cell)
        {
            return false;
        }

        private void FocusCellEditor(DataGridCell cell)
        {
            if (cell == null)
            {
                return;
            }

            TextBox textBox = FindVisualChild<TextBox>(cell);
            if (textBox != null)
            {
                textBox.Focus();
                textBox.Select(0, textBox.Text != null ? textBox.Text.Length : 0);
                textBox.ScrollToHome();
                textBox.Dispatcher.BeginInvoke(
                    new Action(textBox.ScrollToHome),
                    DispatcherPriority.Background);
                return;
            }

            ComboBox comboBox = FindVisualChild<ComboBox>(cell);
            if (comboBox != null)
            {
                comboBox.Focus();
            }
        }

        private bool IsLastGridColumn(DataGridColumn column)
        {
            if (_rowsDataGrid == null || column == null || _rowsDataGrid.Columns.Count == 0)
            {
                return false;
            }

            return _rowsDataGrid.Columns.IndexOf(column) == _rowsDataGrid.Columns.Count - 1;
        }

        private void UpdateFloorColumnVisibility()
        {
            if (_rowsDataGrid == null || _viewModel == null)
            {
                return;
            }

            bool isDeletionMode = _viewModel.IsDeletionMode;
            bool isCreationMode = !isDeletionMode;
            bool isMultiViewStructure = _viewModel.IsMultiViewStructure;

            SetColumnVisibility(GetDragColumn(), isCreationMode ? Visibility.Visible : Visibility.Collapsed);

            DataGridColumn floorColumn = GetFloorColumn();
            DataGridColumn planKindColumn = GetPlanKindColumn();
            DataGridColumn viewNameColumn = GetColumnBySortMemberPath("ViewName");
            DataGridColumn scaleColumn = GetColumnBySortMemberPath("ViewScaleText");
            DataGridColumn viewTemplateColumn = GetColumnBySortMemberPath("SelectedViewTemplate.Name");
            DataGridColumn placedViewsColumn = GetColumnBySortMemberPath("PlacedViewsText");
            DataGridColumn actionsColumn = GetActionsColumn();

            if (floorColumn != null)
            {
                floorColumn.Header = isMultiViewStructure ? "Зона" : "Этаж";
                SetColumnVisibility(
                    floorColumn,
                    isCreationMode && (_viewModel.IsMultiStoryStructure || isMultiViewStructure)
                        ? Visibility.Visible
                        : Visibility.Collapsed);
            }

            SetColumnVisibility(planKindColumn, isCreationMode && !isMultiViewStructure ? Visibility.Visible : Visibility.Collapsed);
            if (viewNameColumn != null)
            {
                viewNameColumn.Header = isMultiViewStructure ? "Часть имени вида" : "Имя вида";
            }

            SetColumnVisibility(viewNameColumn, isCreationMode ? Visibility.Visible : Visibility.Collapsed);
            SetColumnVisibility(scaleColumn, isCreationMode ? Visibility.Visible : Visibility.Collapsed);
            SetColumnVisibility(viewTemplateColumn, isCreationMode ? Visibility.Visible : Visibility.Collapsed);
            SetColumnVisibility(placedViewsColumn, isDeletionMode ? Visibility.Visible : Visibility.Collapsed);

            if (actionsColumn != null)
            {
                actionsColumn.Header = isDeletionMode ? "Удалить" : "Действия";
            }

            ScheduleFreezeRowsDataGridColumnWidths();
        }

        private void SetColumnVisibility(DataGridColumn column, Visibility visibility)
        {
            if (column == null || column.Visibility == visibility)
            {
                return;
            }

            column.Visibility = visibility;
        }

        private DataGridColumn GetDragColumn()
        {
            if (_rowsDataGrid == null || _rowsDataGrid.Columns.Count == 0)
            {
                return null;
            }

            return _rowsDataGrid.Columns[0];
        }

        private DataGridColumn GetFloorColumn()
        {
            return GetColumnBySortMemberPath("FloorName");
        }

        private DataGridColumn GetPlanKindColumn()
        {
            return GetColumnBySortMemberPath("PlanKind");
        }

        private DataGridColumn GetColumnBySortMemberPath(string sortMemberPath)
        {
            if (_rowsDataGrid == null)
            {
                return null;
            }

            for (int i = 0; i < _rowsDataGrid.Columns.Count; i++)
            {
                DataGridColumn column = _rowsDataGrid.Columns[i];
                if (column != null && string.Equals(column.SortMemberPath, sortMemberPath, StringComparison.Ordinal))
                {
                    return column;
                }
            }

            return null;
        }

        private DataGridColumn GetActionsColumn()
        {
            if (_rowsDataGrid == null || _rowsDataGrid.Columns.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < _rowsDataGrid.Columns.Count; i++)
            {
                DataGridColumn column = _rowsDataGrid.Columns[i];
                string header = column != null && column.Header != null ? column.Header.ToString() : string.Empty;
                if (string.Equals(header, "Действия", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(header, "Удалить", StringComparison.OrdinalIgnoreCase))
                {
                    return column;
                }
            }

            return null;
        }

        private void RebuildSheetBrowserParameterColumns()
        {
            if (_rowsDataGrid == null || _viewModel == null)
            {
                return;
            }

            RemoveSheetBrowserParameterColumns();

            int insertIndex = GetActionsColumnIndex();
            for (int i = 0; i < _viewModel.SheetBrowserParameterLevels.Count; i++)
            {
                SheetBrowserParameterLevelViewModel level = _viewModel.SheetBrowserParameterLevels[i];
                if (level == null)
                {
                    continue;
                }

                DataGridTemplateColumn column = CreateSheetBrowserParameterColumn(i, level);
                _rowsDataGrid.Columns.Insert(insertIndex, column);
                _sheetBrowserParameterColumns.Add(column);
                insertIndex++;
            }
        }

        private void RemoveSheetBrowserParameterColumns()
        {
            if (_rowsDataGrid == null || _sheetBrowserParameterColumns.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _sheetBrowserParameterColumns.Count; i++)
            {
                DataGridColumn column = _sheetBrowserParameterColumns[i];
                if (column != null && _rowsDataGrid.Columns.Contains(column))
                {
                    _rowsDataGrid.Columns.Remove(column);
                }
            }

            _sheetBrowserParameterColumns.Clear();
        }

        private int GetActionsColumnIndex()
        {
            if (_rowsDataGrid == null || _rowsDataGrid.Columns.Count == 0)
            {
                return 0;
            }

            for (int i = 0; i < _rowsDataGrid.Columns.Count; i++)
            {
                DataGridColumn column = _rowsDataGrid.Columns[i];
                string header = column != null && column.Header != null ? column.Header.ToString() : string.Empty;
                if (string.Equals(header, "Действия", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(header, "Удалить", StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return _rowsDataGrid.Columns.Count;
        }

        private DataGridTemplateColumn CreateSheetBrowserParameterColumn(int parameterIndex, SheetBrowserParameterLevelViewModel level)
        {
            DataGridTemplateColumn column = new DataGridTemplateColumn();
            column.Header = level.ParameterName;
            column.SortMemberPath = "SheetBrowserParameterValues[" + parameterIndex + "].Value";
            column.Width = new DataGridLength(170.0, DataGridLengthUnitType.Pixel);
            column.MinWidth = 130.0;
            column.CellTemplate = CreateSheetBrowserParameterCellTemplate(parameterIndex);
            return column;
        }

        private DataTemplate CreateSheetBrowserParameterCellTemplate(int parameterIndex)
        {
            FrameworkElementFactory comboBoxFactory = new FrameworkElementFactory(typeof(ComboBox));

            Binding itemsBinding = new Binding("DataContext.SheetBrowserParameterLevels[" + parameterIndex + "].Values");
            itemsBinding.RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGrid), 1);
            comboBoxFactory.SetBinding(ItemsControl.ItemsSourceProperty, itemsBinding);

            Binding textBinding = new Binding("SheetBrowserParameterValues[" + parameterIndex + "].Value");
            textBinding.Mode = BindingMode.TwoWay;
            textBinding.UpdateSourceTrigger = UpdateSourceTrigger.LostFocus;
            comboBoxFactory.SetBinding(ComboBox.TextProperty, textBinding);

            comboBoxFactory.SetValue(ComboBox.IsEditableProperty, true);
            comboBoxFactory.SetValue(ComboBox.IsTextSearchEnabledProperty, false);

            Style comboBoxStyle = TryFindResource("SabDataGridComboBoxStyle") as Style;
            if (comboBoxStyle != null)
            {
                comboBoxFactory.SetValue(FrameworkElement.StyleProperty, comboBoxStyle);
            }

            DataTemplate template = new DataTemplate();
            template.VisualTree = comboBoxFactory;
            return template;
        }

        private void ScheduleNormalizeRowsDataGridColumnWidths()
        {
            ScheduleFreezeRowsDataGridColumnWidths();
        }

        private void ScheduleFreezeRowsDataGridColumnWidths()
        {
            if (_rowsDataGrid == null)
            {
                return;
            }

            _rowsDataGrid.Dispatcher.BeginInvoke(
                new Action(FreezeRowsDataGridColumnWidthsToPixels),
                DispatcherPriority.Background);
        }

        private void ScheduleStretchRowsDataGridRightSide()
        {
            if (_rowsDataGrid == null)
            {
                return;
            }

            _rowsDataGrid.Dispatcher.BeginInvoke(
                new Action(StretchRowsDataGridRightSideIfNeeded),
                DispatcherPriority.Background);
        }

        private void NormalizeRowsDataGridColumnWidths()
        {
            FreezeRowsDataGridColumnWidthsToPixels();
        }

        private void FreezeRowsDataGridColumnWidthsToPixels()
        {
            if (_normalizingColumnWidths || _columnResizeStarted || _rowsDataGrid == null || _rowsDataGrid.Columns.Count == 0)
            {
                return;
            }

            try
            {
                _normalizingColumnWidths = true;

                // Block responsible for switching the table from star widths to manual pixel widths.
                for (int i = 0; i < _rowsDataGrid.Columns.Count; i++)
                {
                    SetColumnWidthToActualPixel(_rowsDataGrid.Columns[i]);
                }
            }
            finally
            {
                _normalizingColumnWidths = false;
            }

            StretchRowsDataGridRightSideIfNeeded();
        }

        private void SetColumnWidthToActualPixel(DataGridColumn column)
        {
            if (column == null || column.Visibility != Visibility.Visible)
            {
                return;
            }

            if (column.Width.UnitType == DataGridLengthUnitType.Pixel &&
                !double.IsNaN(column.Width.Value) &&
                column.Width.Value > 0)
            {
                return;
            }

            double width = column.ActualWidth;
            if (width <= 0 && !double.IsNaN(column.Width.DisplayValue))
            {
                width = column.Width.DisplayValue;
            }

            if (width <= 20.0)
            {
                return;
            }

            double minimumWidth = column.MinWidth > 0 ? column.MinWidth : 20.0;
            if (width < minimumWidth)
            {
                width = minimumWidth;
            }

            if (!double.IsInfinity(column.MaxWidth) && width > column.MaxWidth)
            {
                width = column.MaxWidth;
            }

            if (width > 0)
            {
                column.Width = new DataGridLength(width, DataGridLengthUnitType.Pixel);
            }
        }

        private void StretchRowsDataGridRightSideIfNeeded()
        {
            if (_normalizingColumnWidths || _columnResizeStarted || _rowsDataGrid == null || _rowsDataGrid.Columns.Count == 0)
            {
                return;
            }

            double availableWidth = GetRowsDataGridViewportWidth();
            if (availableWidth <= 0)
            {
                return;
            }

            double visibleColumnsWidth = 0.0;
            for (int i = 0; i < _rowsDataGrid.Columns.Count; i++)
            {
                DataGridColumn column = _rowsDataGrid.Columns[i];
                if (column == null || column.Visibility != Visibility.Visible)
                {
                    continue;
                }

                visibleColumnsWidth += GetColumnWidthForStretch(column);
            }

            double missingWidth = availableWidth - visibleColumnsWidth;
            if (missingWidth <= 0.5)
            {
                return;
            }

            DataGridColumn stretchColumn = GetRowsDataGridRightStretchColumn();
            if (stretchColumn == null)
            {
                return;
            }

            double currentWidth = GetColumnWidthForStretch(stretchColumn);
            double targetWidth = currentWidth + missingWidth;
            if (!double.IsInfinity(stretchColumn.MaxWidth) && targetWidth > stretchColumn.MaxWidth)
            {
                targetWidth = stretchColumn.MaxWidth;
            }

            if (targetWidth > currentWidth + 0.5)
            {
                stretchColumn.Width = new DataGridLength(targetWidth, DataGridLengthUnitType.Pixel);
            }
        }

        private double GetRowsDataGridViewportWidth()
        {
            if (_rowsDataGrid == null || _rowsDataGrid.ActualWidth <= 0)
            {
                return 0.0;
            }

            double width = _rowsDataGrid.ActualWidth - 2.0;
            ScrollViewer scrollViewer = FindVisualChild<ScrollViewer>(_rowsDataGrid);
            if (scrollViewer != null && scrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible)
            {
                width -= SystemParameters.VerticalScrollBarWidth;
            }

            return width > 0 ? width : 0.0;
        }

        private DataGridColumn GetRowsDataGridRightStretchColumn()
        {
            DataGridColumn sheetNameColumn = GetColumnBySortMemberPath("SheetName");
            if (sheetNameColumn != null && sheetNameColumn.Visibility == Visibility.Visible)
            {
                return sheetNameColumn;
            }

            DataGridColumn viewNameColumn = GetColumnBySortMemberPath("ViewName");
            if (viewNameColumn != null && viewNameColumn.Visibility == Visibility.Visible)
            {
                return viewNameColumn;
            }

            return null;
        }

        private double GetColumnWidthForStretch(DataGridColumn column)
        {
            if (column == null)
            {
                return 0.0;
            }

            if (column.Width.UnitType == DataGridLengthUnitType.Pixel &&
                !double.IsNaN(column.Width.Value) &&
                column.Width.Value > 0)
            {
                return column.Width.Value;
            }

            if (column.ActualWidth > 0)
            {
                return column.ActualWidth;
            }

            if (!double.IsNaN(column.Width.DisplayValue) && column.Width.DisplayValue > 0)
            {
                return column.Width.DisplayValue;
            }

            return column.MinWidth > 0 ? column.MinWidth : 0.0;
        }

        private void RestoreWindowLayout()
        {
            if (_layoutRestored)
            {
                return;
            }

            CreateViewsAndSheetsWindowLayoutSettings settings = _layoutService.Load();
            if (settings == null)
            {
                ApplySettingsDrawerState(_settingsDrawerToggle == null || _settingsDrawerToggle.IsChecked != false);
                _layoutRestored = true;
                ScheduleNormalizeRowsDataGridColumnWidths();
                return;
            }

            if (settings.WindowWidth >= MinWidth)
            {
                Width = settings.WindowWidth;
            }

            if (settings.WindowHeight >= MinHeight)
            {
                Height = settings.WindowHeight;
            }

            if (_settingsDrawerToggle != null)
            {
                _settingsDrawerToggle.IsChecked = settings.IsSettingsPanelOpen;
                ApplySettingsDrawerState(settings.IsSettingsPanelOpen);
            }

            if (_rowsDataGrid != null && settings.ColumnWidths != null)
            {
                for (int i = 0; i < _rowsDataGrid.Columns.Count; i++)
                {
                    DataGridColumn column = _rowsDataGrid.Columns[i];
                    string key = BuildColumnLayoutKey(column, i);
                    double width;
                    if (settings.ColumnWidths.TryGetValue(key, out width) && width > 20.0)
                    {
                        column.Width = new DataGridLength(width, DataGridLengthUnitType.Pixel);
                    }
                }
            }

            _layoutRestored = true;
            ScheduleNormalizeRowsDataGridColumnWidths();
        }

        private void SaveWindowLayout()
        {
            CreateViewsAndSheetsWindowLayoutSettings settings = new CreateViewsAndSheetsWindowLayoutSettings();
            settings.WindowWidth = ActualWidth > 0 ? ActualWidth : Width;
            settings.WindowHeight = ActualHeight > 0 ? ActualHeight : Height;
            settings.IsSettingsPanelOpen = _settingsDrawerToggle == null || _settingsDrawerToggle.IsChecked != false;

            if (_rowsDataGrid != null)
            {
                for (int i = 0; i < _rowsDataGrid.Columns.Count; i++)
                {
                    DataGridColumn column = _rowsDataGrid.Columns[i];
                    double width = column.ActualWidth > 0 ? column.ActualWidth : column.Width.DisplayValue;
                    if (width > 20.0)
                    {
                        settings.ColumnWidths[BuildColumnLayoutKey(column, i)] = width;
                    }
                }
            }

            _layoutService.Save(settings);
        }

        private string BuildColumnLayoutKey(DataGridColumn column, int index)
        {
            string header = column != null && column.Header != null ? column.Header.ToString() : string.Empty;
            // Key version resets old saved column widths after changing the default presets.
            return ColumnLayoutKeyPrefix + "|" + index + "|" + header;
        }

        private T FindVisualChildByName<T>(DependencyObject parent, string name)
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

        private T FindVisualChild<T>(DependencyObject parent)
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

        private T FindParent<T>(DependencyObject child)
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

        private DependencyObject GetParentObject(DependencyObject child)
        {
            if (child == null)
            {
                return null;
            }

            ContentElement contentElement = child as ContentElement;
            if (contentElement != null)
            {
                DependencyObject parent = ContentOperations.GetParent(contentElement);
                if (parent != null)
                {
                    return parent;
                }

                FrameworkContentElement frameworkContentElement = contentElement as FrameworkContentElement;
                return frameworkContentElement != null ? frameworkContentElement.Parent : null;
            }

            return VisualTreeHelper.GetParent(child);
        }
    }
}
