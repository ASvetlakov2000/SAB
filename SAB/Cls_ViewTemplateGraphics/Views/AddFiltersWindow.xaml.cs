using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using SAB.UI;
using SAB.ViewTemplateGraphics.Models;
using RevitTaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace SAB.ViewTemplateGraphics.Views
{
    public partial class AddFiltersWindow : Window
    {
        private const string WindowTitle = "SAB Добавить фильтры";

        private TextBox _searchTextBox;
        private DataGrid _filtersDataGrid;

        public AddFiltersWindow(IList<FilterOverrideRow> filters)
        {
            Items = new ObservableCollection<FilterSelectionDialogItem>();
            SelectedFilterIdValues = new List<int>();

            if (filters != null)
            {
                List<FilterSelectionDialogItem> sortedItems = new List<FilterSelectionDialogItem>();
                for (int i = 0; i < filters.Count; i++)
                {
                    FilterOverrideRow filter = filters[i];
                    FilterSelectionDialogItem item = new FilterSelectionDialogItem();
                    item.FilterIdValue = filter.FilterIdValue;
                    item.Name = filter.Name;
                    item.IsAlreadyInTemplate = filter.IncludedState == true;
                    item.IsPartiallyIncluded = filter.IncludedState == null;
                    sortedItems.Add(item);
                }

                sortedItems.Sort(delegate(FilterSelectionDialogItem first, FilterSelectionDialogItem second)
                {
                    return string.Compare(first.Name, second.Name, StringComparison.CurrentCultureIgnoreCase);
                });

                for (int i = 0; i < sortedItems.Count; i++)
                {
                    Items.Add(sortedItems[i]);
                }
            }

            InitializeWindowFromXamlFile();
            DataContext = this;

            AddHandler(Button.ClickEvent, new RoutedEventHandler(Button_Click));
            Loaded += AddFiltersWindow_Loaded;
            Closed += AddFiltersWindow_Closed;
            SourceInitialized += Window_SourceInitialized;
        }

        public ObservableCollection<FilterSelectionDialogItem> Items { get; private set; }

        public List<int> SelectedFilterIdValues { get; private set; }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(AddFiltersWindow).Assembly.Location);
            string xamlPath = Path.Combine(
                assemblyDirectory,
                "Cls_ViewTemplateGraphics",
                "Views",
                "AddFiltersWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл окна выбора фильтров не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext parserContext = new ParserContext();
                parserContext.BaseUri = new Uri(xamlPath, UriKind.Absolute);

                Window loadedWindow = XamlReader.Load(stream, parserContext) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось загрузить AddFiltersWindow.xaml.");
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

        private void AddFiltersWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _searchTextBox = FindVisualChildByName<TextBox>(this, "FilterSearchTextBox");
            _filtersDataGrid = FindVisualChildByName<DataGrid>(this, "FiltersDataGrid");
            WindowSizeSettingsService.Apply(this, "ViewTemplateGraphics.AddFiltersWindow");
        }

        private void AddFiltersWindow_Closed(object sender, EventArgs e)
        {
            Loaded -= AddFiltersWindow_Loaded;
            Closed -= AddFiltersWindow_Closed;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button button = FindParent<Button>(e.OriginalSource as DependencyObject);
            if (button == null)
            {
                return;
            }

            if (string.Equals(button.Name, "FindButton", StringComparison.Ordinal))
            {
                ApplySearch();
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

            if (string.Equals(button.Name, "AddButton", StringComparison.Ordinal))
            {
                AcceptSelection();
                e.Handled = true;
            }
        }

        private void ApplySearch()
        {
            string searchText = _searchTextBox != null
                ? (_searchTextBox.Text ?? string.Empty).Trim()
                : string.Empty;

            for (int i = 0; i < Items.Count; i++)
            {
                Items[i].IsVisibleInList = searchText.Length == 0 ||
                    (Items[i].Name ?? string.Empty).IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase) >= 0;
            }

            if (_filtersDataGrid == null)
            {
                return;
            }

            ICollectionView collectionView = CollectionViewSource.GetDefaultView(_filtersDataGrid.ItemsSource);
            if (collectionView != null)
            {
                collectionView.Filter = delegate(object item)
                {
                    FilterSelectionDialogItem filter = item as FilterSelectionDialogItem;
                    return filter == null || filter.IsVisibleInList;
                };
                collectionView.Refresh();
            }

            _filtersDataGrid.UpdateLayout();
            ScrollViewer scrollViewer = FindVisualChild<ScrollViewer>(_filtersDataGrid);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollToTop();
            }

            if (_filtersDataGrid.Items.Count > 0)
            {
                _filtersDataGrid.ScrollIntoView(_filtersDataGrid.Items[0]);
            }
        }

        private void AcceptSelection()
        {
            SelectedFilterIdValues.Clear();
            for (int i = 0; i < Items.Count; i++)
            {
                FilterSelectionDialogItem item = Items[i];
                if (item.IsSelected && !item.IsAlreadyInTemplate)
                {
                    SelectedFilterIdValues.Add(item.FilterIdValue);
                }
            }

            if (SelectedFilterIdValues.Count == 0)
            {
                RevitTaskDialog.Show(WindowTitle, "Отметьте галочками один или несколько фильтров.");
                return;
            }

            DialogResult = true;
            Close();
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
