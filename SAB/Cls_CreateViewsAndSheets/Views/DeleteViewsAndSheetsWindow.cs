using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using SAB.CreateViewsAndSheets.ViewModels;
using SAB.UI;

namespace SAB.CreateViewsAndSheets.Views
{
    public class DeleteViewsAndSheetsWindow : Window
    {
        private readonly DeleteViewsAndSheetsViewModel _viewModel;
        private DataGrid _rowsDataGrid;
        private DeleteViewsAndSheetsRowViewModel _selectionAnchorRow;
        private bool _isApplyingSelectionValue;

        public DeleteViewsAndSheetsWindow(DeleteViewsAndSheetsViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            InitializeWindowFromXamlFile();
            DataContext = _viewModel;

            _viewModel.RequestClose += ViewModel_RequestClose;
            Loaded += DeleteViewsAndSheetsWindow_Loaded;
            Closed += DeleteViewsAndSheetsWindow_Closed;
        }

        private void ViewModel_RequestClose(object sender, EventArgs e)
        {
            DialogResult = _viewModel.IsAccepted;
            Close();
        }

        private void DeleteViewsAndSheetsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AttachRowsDataGridHandlers();
            SabWindowBehaviorService.ApplyLoadedBehavior(this);
        }

        private void DeleteViewsAndSheetsWindow_Closed(object sender, EventArgs e)
        {
            DetachRowsDataGridHandlers();
            _viewModel.RequestClose -= ViewModel_RequestClose;
            Loaded -= DeleteViewsAndSheetsWindow_Loaded;
            Closed -= DeleteViewsAndSheetsWindow_Closed;
        }

        private void AttachRowsDataGridHandlers()
        {
            _rowsDataGrid = FindVisualChildByName<DataGrid>(this, "DeletionRowsDataGrid");
            if (_rowsDataGrid == null)
            {
                return;
            }

            _rowsDataGrid.PreviewMouseLeftButtonDown += RowsDataGrid_PreviewMouseLeftButtonDown;
            _rowsDataGrid.AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(RowCheckBox_CheckedChanged), true);
            _rowsDataGrid.AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(RowCheckBox_CheckedChanged), true);
        }

        private void DetachRowsDataGridHandlers()
        {
            if (_rowsDataGrid == null)
            {
                return;
            }

            _rowsDataGrid.PreviewMouseLeftButtonDown -= RowsDataGrid_PreviewMouseLeftButtonDown;
            _rowsDataGrid.RemoveHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(RowCheckBox_CheckedChanged));
            _rowsDataGrid.RemoveHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(RowCheckBox_CheckedChanged));
            _rowsDataGrid = null;
            _selectionAnchorRow = null;
        }

        private void RowsDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject source = e != null ? e.OriginalSource as DependencyObject : null;
            DataGridRow clickedRow = FindParent<DataGridRow>(source);
            DeleteViewsAndSheetsRowViewModel clickedRowViewModel = clickedRow != null ? clickedRow.Item as DeleteViewsAndSheetsRowViewModel : null;
            if (clickedRowViewModel == null)
            {
                return;
            }

            bool isShiftPressed = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            if (isShiftPressed && TrySelectRowsByShift(clickedRowViewModel))
            {
                if (FindParent<CheckBox>(source) == null && !(source is CheckBox))
                {
                    e.Handled = true;
                }

                return;
            }

            if (!isShiftPressed)
            {
                _selectionAnchorRow = clickedRowViewModel;
            }
        }

        private bool TrySelectRowsByShift(DeleteViewsAndSheetsRowViewModel clickedRow)
        {
            if (_rowsDataGrid == null || _viewModel == null || _viewModel.Rows == null || clickedRow == null)
            {
                return false;
            }

            DeleteViewsAndSheetsRowViewModel anchorRow = _selectionAnchorRow;
            if (anchorRow == null || !_viewModel.Rows.Contains(anchorRow))
            {
                anchorRow = _rowsDataGrid.SelectedItem as DeleteViewsAndSheetsRowViewModel;
            }

            if (anchorRow == null || !_viewModel.Rows.Contains(anchorRow))
            {
                anchorRow = clickedRow;
            }

            int anchorIndex = _viewModel.Rows.IndexOf(anchorRow);
            int clickedIndex = _viewModel.Rows.IndexOf(clickedRow);
            if (anchorIndex < 0 || clickedIndex < 0)
            {
                return false;
            }

            int startIndex = Math.Min(anchorIndex, clickedIndex);
            int endIndex = Math.Max(anchorIndex, clickedIndex);

            _rowsDataGrid.SelectedItems.Clear();
            for (int i = startIndex; i <= endIndex; i++)
            {
                DeleteViewsAndSheetsRowViewModel row = _viewModel.Rows[i];
                if (row != null)
                {
                    _rowsDataGrid.SelectedItems.Add(row);
                }
            }

            _rowsDataGrid.CurrentItem = clickedRow;
            _selectionAnchorRow = anchorRow;
            return true;
        }

        private void RowCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_isApplyingSelectionValue)
            {
                return;
            }

            CheckBox checkBox = e != null ? e.OriginalSource as CheckBox : null;
            if (checkBox == null)
            {
                return;
            }

            DeleteViewsAndSheetsRowViewModel sourceRow = GetRowViewModelFromElement(checkBox);
            if (sourceRow == null)
            {
                return;
            }

            bool newValue = checkBox.IsChecked == true;
            List<DeleteViewsAndSheetsRowViewModel> selectedRows = GetSelectedRowsSnapshot();
            if (selectedRows.Count <= 1 || !selectedRows.Contains(sourceRow))
            {
                return;
            }

            try
            {
                _isApplyingSelectionValue = true;
                for (int i = 0; i < selectedRows.Count; i++)
                {
                    if (selectedRows[i] != null)
                    {
                        selectedRows[i].IsSelected = newValue;
                    }
                }
            }
            finally
            {
                _isApplyingSelectionValue = false;
            }
        }

        private List<DeleteViewsAndSheetsRowViewModel> GetSelectedRowsSnapshot()
        {
            List<DeleteViewsAndSheetsRowViewModel> result = new List<DeleteViewsAndSheetsRowViewModel>();
            if (_rowsDataGrid == null)
            {
                return result;
            }

            for (int i = 0; i < _rowsDataGrid.SelectedItems.Count; i++)
            {
                DeleteViewsAndSheetsRowViewModel row = _rowsDataGrid.SelectedItems[i] as DeleteViewsAndSheetsRowViewModel;
                if (row != null && !result.Contains(row))
                {
                    result.Add(row);
                }
            }

            return result;
        }

        private DeleteViewsAndSheetsRowViewModel GetRowViewModelFromElement(DependencyObject source)
        {
            DataGridRow row = FindParent<DataGridRow>(source);
            if (row != null)
            {
                return row.Item as DeleteViewsAndSheetsRowViewModel;
            }

            FrameworkElement frameworkElement = source as FrameworkElement;
            return frameworkElement != null ? frameworkElement.DataContext as DeleteViewsAndSheetsRowViewModel : null;
        }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(DeleteViewsAndSheetsWindow).Assembly.Location);
            string xamlPath = Path.Combine(assemblyDirectory, "Cls_CreateViewsAndSheets", "Views", "DeleteViewsAndSheetsWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл окна удаления видов и листов не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext parserContext = new ParserContext();
                parserContext.BaseUri = new Uri(xamlPath, UriKind.Absolute);

                Window loadedWindow = XamlReader.Load(stream, parserContext) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось загрузить DeleteViewsAndSheetsWindow.xaml.");
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

        private T FindParent<T>(DependencyObject child)
            where T : DependencyObject
        {
            DependencyObject current = child;
            while (current != null)
            {
                T typedCurrent = current as T;
                if (typedCurrent != null)
                {
                    return typedCurrent;
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

            FrameworkElement frameworkElement = child as FrameworkElement;
            if (frameworkElement != null && frameworkElement.Parent != null)
            {
                return frameworkElement.Parent;
            }

            return VisualTreeHelper.GetParent(child);
        }
    }
}
