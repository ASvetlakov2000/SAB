using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using SAB.CreateViewsAndSheets.Models;
using SAB.CreateViewsAndSheets.Services;
using SAB.CreateViewsAndSheets.ViewModels;

namespace SAB.CreateViewsAndSheets.Views
{
    public partial class CreateViewsAndSheetsWindow : Window
    {
        private readonly CreateViewsAndSheetsViewModel _viewModel;
        private readonly CreateViewsAndSheetsWindowLayoutService _layoutService;

        private DataGrid _rowsDataGrid;
        private readonly List<DataGridColumn> _observedWidthColumns;
        private Point _dragStartPoint;
        private SheetCreationRowViewModel _draggedRow;
        private DataGridColumn _resizedColumn;
        private bool _gridHandlersAttached;
        private bool _layoutRestored;
        private bool _columnResizeStarted;
        private bool _normalizingColumnWidths;

        public CreateViewsAndSheetsWindow(CreateViewsAndSheetsViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _layoutService = new CreateViewsAndSheetsWindowLayoutService();
            _observedWidthColumns = new List<DataGridColumn>();

            InitializeWindowFromXamlFile();
            DataContext = _viewModel;

            _viewModel.RequestClose += ViewModel_RequestClose;
            _viewModel.RequestPointSelection += ViewModel_RequestPointSelection;
            Loaded += CreateViewsAndSheetsWindow_Loaded;
            Closing += CreateViewsAndSheetsWindow_Closing;
            Closed += CreateViewsAndSheetsWindow_Closed;
        }

        public PlacementPointSelectionRequestEventArgs PendingPointSelectionRequest { get; private set; }

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
            AttachRowsDataGridHandlers();
            RestoreWindowLayout();
        }

        private void ViewModel_RequestClose(object sender, EventArgs e)
        {
            DialogResult = _viewModel.IsAccepted;
            Close();
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

        private void CreateViewsAndSheetsWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowLayout();
        }

        private void CreateViewsAndSheetsWindow_Closed(object sender, EventArgs e)
        {
            DetachRowsDataGridHandlers();
            _viewModel.RequestClose -= ViewModel_RequestClose;
            _viewModel.RequestPointSelection -= ViewModel_RequestPointSelection;
            Loaded -= CreateViewsAndSheetsWindow_Loaded;
            Closing -= CreateViewsAndSheetsWindow_Closing;
            Closed -= CreateViewsAndSheetsWindow_Closed;
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
            _rowsDataGrid.MouseMove += RowsDataGrid_MouseMove;
            _rowsDataGrid.SizeChanged += RowsDataGrid_SizeChanged;
            _rowsDataGrid.DragOver += RowsDataGrid_DragOver;
            _rowsDataGrid.Drop += RowsDataGrid_Drop;
            AttachRowsDataGridColumnWidthObservers();
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
            _rowsDataGrid.MouseMove -= RowsDataGrid_MouseMove;
            _rowsDataGrid.SizeChanged -= RowsDataGrid_SizeChanged;
            _rowsDataGrid.DragOver -= RowsDataGrid_DragOver;
            _rowsDataGrid.Drop -= RowsDataGrid_Drop;
            DetachRowsDataGridColumnWidthObservers();
            _gridHandlersAttached = false;
        }

        private void RowsDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _draggedRow = null;
            _resizedColumn = null;
            _columnResizeStarted = false;

            DependencyObject source = e.OriginalSource as DependencyObject;
            _resizedColumn = GetColumnResizeTarget(source);
            if (_resizedColumn != null)
            {
                _columnResizeStarted = true;
                return;
            }

            if (IsRowDragHandle(source))
            {
                DataGridRow row = FindParent<DataGridRow>(source);
                if (row != null)
                {
                    _draggedRow = row.Item as SheetCreationRowViewModel;
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
            NormalizeRowsDataGridColumnWidths();
            _resizedColumn = null;
        }

        private void RowsDataGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ScheduleNormalizeRowsDataGridColumnWidths();
        }

        private void RowsDataGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (_columnResizeStarted)
            {
                NormalizeRowsDataGridColumnWidths();
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed || _draggedRow == null || _rowsDataGrid == null)
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

            DragDrop.DoDragDrop(_rowsDataGrid, _draggedRow, DragDropEffects.Move);
            _draggedRow = null;
        }

        private void RowsDataGrid_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(SheetCreationRowViewModel)))
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
            SheetCreationRowViewModel draggedRow = e.Data.GetData(typeof(SheetCreationRowViewModel)) as SheetCreationRowViewModel;
            if (draggedRow == null || _rowsDataGrid == null)
            {
                return;
            }

            int targetIndex = CalculateDropTargetIndex(e.OriginalSource as DependencyObject, e.GetPosition(_rowsDataGrid), draggedRow);
            _viewModel.MoveRowToIndex(draggedRow, targetIndex);
            e.Handled = true;
        }

        private int CalculateDropTargetIndex(DependencyObject dropSource, Point dropPoint, SheetCreationRowViewModel draggedRow)
        {
            if (_rowsDataGrid == null || _viewModel.Rows == null || _viewModel.Rows.Count == 0)
            {
                return 0;
            }

            int sourceIndex = _viewModel.Rows.IndexOf(draggedRow);
            if (sourceIndex < 0)
            {
                return 0;
            }

            DataGridRow targetRow = FindParent<DataGridRow>(dropSource);
            if (targetRow == null)
            {
                int lastIndex = _viewModel.Rows.Count - 1;
                return sourceIndex < _viewModel.Rows.Count ? lastIndex : sourceIndex;
            }

            SheetCreationRowViewModel targetRowViewModel = targetRow.Item as SheetCreationRowViewModel;
            int targetIndex = _viewModel.Rows.IndexOf(targetRowViewModel);
            if (targetIndex < 0)
            {
                return sourceIndex;
            }

            Point pointInTargetRow = Mouse.GetPosition(targetRow);
            int insertionIndex = pointInTargetRow.Y > targetRow.ActualHeight / 2.0
                ? targetIndex + 1
                : targetIndex;

            if (sourceIndex < insertionIndex)
            {
                insertionIndex--;
            }

            if (insertionIndex < 0)
            {
                insertionIndex = 0;
            }

            if (insertionIndex >= _viewModel.Rows.Count)
            {
                insertionIndex = _viewModel.Rows.Count - 1;
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
            if (_rowsDataGrid == null || cell == null || cell.Column == null || cell.Column.IsReadOnly)
            {
                return false;
            }

            if (IsLastGridColumn(cell.Column))
            {
                return false;
            }

            DataGridRow row = FindParent<DataGridRow>(cell);
            if (row == null || !(row.Item is SheetCreationRowViewModel))
            {
                return false;
            }

            _rowsDataGrid.SelectedItem = row.Item;
            _rowsDataGrid.CurrentCell = new DataGridCellInfo(row.Item, cell.Column);
            cell.Focus();

            bool editStarted = _rowsDataGrid.BeginEdit();
            if (editStarted)
            {
                _rowsDataGrid.Dispatcher.BeginInvoke(
                    new Action(() => FocusCellEditor(cell)),
                    DispatcherPriority.Input);
            }

            return editStarted;
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
                textBox.SelectAll();
                return;
            }

            ComboBox comboBox = FindVisualChild<ComboBox>(cell);
            if (comboBox != null)
            {
                comboBox.Focus();
            }
        }

        private void AttachRowsDataGridColumnWidthObservers()
        {
            DetachRowsDataGridColumnWidthObservers();

            if (_rowsDataGrid == null)
            {
                return;
            }

            DependencyPropertyDescriptor descriptor = DependencyPropertyDescriptor.FromProperty(
                DataGridColumn.WidthProperty,
                typeof(DataGridColumn));
            if (descriptor == null)
            {
                return;
            }

            DataGridColumn fillColumn = GetRowsDataGridFillColumn();
            for (int i = 0; i < _rowsDataGrid.Columns.Count; i++)
            {
                DataGridColumn column = _rowsDataGrid.Columns[i];
                if (column == null || column == fillColumn)
                {
                    continue;
                }

                descriptor.AddValueChanged(column, RowsDataGridColumnWidthChanged);
                _observedWidthColumns.Add(column);
            }
        }

        private void DetachRowsDataGridColumnWidthObservers()
        {
            if (_observedWidthColumns.Count == 0)
            {
                return;
            }

            DependencyPropertyDescriptor descriptor = DependencyPropertyDescriptor.FromProperty(
                DataGridColumn.WidthProperty,
                typeof(DataGridColumn));
            if (descriptor != null)
            {
                for (int i = 0; i < _observedWidthColumns.Count; i++)
                {
                    descriptor.RemoveValueChanged(_observedWidthColumns[i], RowsDataGridColumnWidthChanged);
                }
            }

            _observedWidthColumns.Clear();
        }

        private void RowsDataGridColumnWidthChanged(object sender, EventArgs e)
        {
            NormalizeRowsDataGridColumnWidths();
        }

        private bool IsLastGridColumn(DataGridColumn column)
        {
            if (_rowsDataGrid == null || column == null || _rowsDataGrid.Columns.Count == 0)
            {
                return false;
            }

            return _rowsDataGrid.Columns.IndexOf(column) == _rowsDataGrid.Columns.Count - 1;
        }

        private void ScheduleNormalizeRowsDataGridColumnWidths()
        {
            if (_rowsDataGrid == null)
            {
                return;
            }

            _rowsDataGrid.Dispatcher.BeginInvoke(
                new Action(NormalizeRowsDataGridColumnWidths),
                DispatcherPriority.Background);
        }

        private void NormalizeRowsDataGridColumnWidths()
        {
            if (_normalizingColumnWidths || _rowsDataGrid == null || _rowsDataGrid.Columns.Count < 2 || _rowsDataGrid.ActualWidth <= 0)
            {
                return;
            }

            DataGridColumn fillColumn = GetRowsDataGridFillColumn();
            if (fillColumn == null)
            {
                return;
            }

            try
            {
                _normalizingColumnWidths = true;

                double availableWidth = _rowsDataGrid.ActualWidth - 2.0;
                ScrollViewer scrollViewer = FindVisualChild<ScrollViewer>(_rowsDataGrid);
                if (scrollViewer != null && scrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible)
                {
                    availableWidth -= SystemParameters.VerticalScrollBarWidth;
                }

                double fixedWidth = 0.0;
                for (int i = 0; i < _rowsDataGrid.Columns.Count; i++)
                {
                    DataGridColumn column = _rowsDataGrid.Columns[i];
                    if (column == fillColumn || column.Visibility != Visibility.Visible)
                    {
                        continue;
                    }

                    fixedWidth += GetColumnActualWidth(column);
                }

                double minimumWidth = fillColumn.MinWidth > 0 ? fillColumn.MinWidth : 20.0;
                double targetWidth = availableWidth - fixedWidth;
                if (targetWidth < minimumWidth)
                {
                    targetWidth = TryLimitResizedColumnWidth(fillColumn, availableWidth, fixedWidth, minimumWidth);
                }

                if (targetWidth < minimumWidth)
                {
                    targetWidth = minimumWidth;
                }

                if (!double.IsInfinity(fillColumn.MaxWidth) && targetWidth > fillColumn.MaxWidth)
                {
                    targetWidth = fillColumn.MaxWidth;
                }

                if (targetWidth > 0 && Math.Abs(GetColumnActualWidth(fillColumn) - targetWidth) > 0.5)
                {
                    fillColumn.Width = new DataGridLength(targetWidth, DataGridLengthUnitType.Pixel);
                }
            }
            finally
            {
                _normalizingColumnWidths = false;
            }
        }

        private DataGridColumn GetRowsDataGridFillColumn()
        {
            if (_rowsDataGrid == null || _rowsDataGrid.Columns.Count < 2)
            {
                return null;
            }

            return _rowsDataGrid.Columns[_rowsDataGrid.Columns.Count - 2];
        }

        private double TryLimitResizedColumnWidth(DataGridColumn fillColumn, double availableWidth, double fixedWidth, double fillColumnMinimumWidth)
        {
            if (_resizedColumn == null || _resizedColumn == fillColumn || _resizedColumn.Visibility != Visibility.Visible)
            {
                return availableWidth - fixedWidth;
            }

            double deficit = fillColumnMinimumWidth - (availableWidth - fixedWidth);
            if (deficit <= 0.5)
            {
                return availableWidth - fixedWidth;
            }

            double currentWidth = GetColumnActualWidth(_resizedColumn);
            double resizedColumnMinimumWidth = _resizedColumn.MinWidth > 0 ? _resizedColumn.MinWidth : 20.0;
            double limitedWidth = currentWidth - deficit;
            if (limitedWidth < resizedColumnMinimumWidth)
            {
                limitedWidth = resizedColumnMinimumWidth;
            }

            if (limitedWidth < currentWidth)
            {
                _resizedColumn.Width = new DataGridLength(limitedWidth, DataGridLengthUnitType.Pixel);
                fixedWidth -= currentWidth - limitedWidth;
            }

            return availableWidth - fixedWidth;
        }

        private double GetColumnActualWidth(DataGridColumn column)
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
            return index + "|" + header;
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
