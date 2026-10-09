using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using SAB.DoorWindowExplanations.Models;
using SAB.UI;

namespace SAB.DoorWindowExplanations.Views
{
    public enum DoorWindowSelectionListAction
    {
        Cancel = 0,
        AddElements = 1,
        AddElementsByRectangle = 2,
        Continue = 3
    }

    public partial class DoorWindowSelectionListWindow : Window
    {
        private readonly IList<DoorWindowSelectionData> _items;
        private DataGrid _selectionDataGrid;
        private TextBlock _selectionStatusTextBlock;
        private Button _addButton;
        private Button _addByRectangleButton;
        private Button _removeButton;
        private Button _clearButton;
        private Button _cancelButton;
        private Button _continueButton;

        public DoorWindowSelectionListWindow(IList<DoorWindowSelectionData> items)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            InitializeWindowFromXamlFile();
            BindControls();
            _selectionDataGrid.ItemsSource = _items;
            AttachHandlers();
            UpdateStatus();
            Loaded += DoorWindowSelectionListWindow_Loaded;
        }

        public DoorWindowSelectionListAction RequestedAction { get; private set; }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(DoorWindowSelectionListWindow).Assembly.Location);
            string xamlPath = Path.Combine(
                assemblyDirectory,
                "Cls_DoorWindowExplanations",
                "Views",
                "DoorWindowSelectionListWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл окна списка не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext context = new ParserContext();
                context.BaseUri = new Uri(xamlPath, UriKind.Absolute);
                Window loadedWindow = XamlReader.Load(stream, context) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось загрузить DoorWindowSelectionListWindow.xaml.");
                }

                _selectionDataGrid = loadedWindow.FindName("SelectionDataGrid") as DataGrid;
                _selectionStatusTextBlock = loadedWindow.FindName("SelectionStatusTextBlock") as TextBlock;
                _addButton = loadedWindow.FindName("AddButton") as Button;
                _addByRectangleButton = loadedWindow.FindName("AddByRectangleButton") as Button;
                _removeButton = loadedWindow.FindName("RemoveButton") as Button;
                _clearButton = loadedWindow.FindName("ClearButton") as Button;
                _cancelButton = loadedWindow.FindName("CancelButton") as Button;
                _continueButton = loadedWindow.FindName("ContinueButton") as Button;

                Title = loadedWindow.Title;
                Width = loadedWindow.Width;
                Height = loadedWindow.Height;
                MinWidth = loadedWindow.MinWidth;
                MinHeight = loadedWindow.MinHeight;
                WindowStartupLocation = loadedWindow.WindowStartupLocation;
                ResizeMode = loadedWindow.ResizeMode;
                Background = loadedWindow.Background;
                FontFamily = loadedWindow.FontFamily;
                FontSize = loadedWindow.FontSize;
                Resources = loadedWindow.Resources;
                Content = loadedWindow.Content;
            }
        }

        private void BindControls()
        {
            if (_selectionDataGrid == null || _selectionStatusTextBlock == null ||
                _addButton == null || _addByRectangleButton == null ||
                _removeButton == null || _clearButton == null ||
                _cancelButton == null || _continueButton == null)
            {
                throw new InvalidOperationException("Не удалось привязать элементы окна списка.");
            }
        }

        private void AttachHandlers()
        {
            _addButton.Click += AddButton_Click;
            _addByRectangleButton.Click += AddByRectangleButton_Click;
            _removeButton.Click += RemoveButton_Click;
            _clearButton.Click += ClearButton_Click;
            _cancelButton.Click += CancelButton_Click;
            _continueButton.Click += ContinueButton_Click;
        }

        private void DoorWindowSelectionListWindow_Loaded(object sender, RoutedEventArgs e)
        {
            WindowSizeSettingsService.Apply(this, "DoorWindowExplanations.SelectionListWindow");
            SabWindowBehaviorService.ApplyLoadedBehavior(this);
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            RequestedAction = DoorWindowSelectionListAction.AddElements;
            Close();
        }

        private void AddByRectangleButton_Click(object sender, RoutedEventArgs e)
        {
            RequestedAction = DoorWindowSelectionListAction.AddElementsByRectangle;
            Close();
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            IList selectedItems = _selectionDataGrid.SelectedItems;
            List<DoorWindowSelectionData> itemsToRemove = new List<DoorWindowSelectionData>();
            for (int i = 0; i < selectedItems.Count; i++)
            {
                DoorWindowSelectionData item = selectedItems[i] as DoorWindowSelectionData;
                if (item != null)
                {
                    itemsToRemove.Add(item);
                }
            }

            for (int i = 0; i < itemsToRemove.Count; i++)
            {
                _items.Remove(itemsToRemove[i]);
            }

            RefreshGrid();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _items.Clear();
            RefreshGrid();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            RequestedAction = DoorWindowSelectionListAction.Cancel;
            Close();
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0)
            {
                _selectionStatusTextBlock.Text = "Добавьте хотя бы одну дверь, окно или витраж.";
                return;
            }

            RequestedAction = DoorWindowSelectionListAction.Continue;
            Close();
        }

        private void RefreshGrid()
        {
            ICollectionView view = CollectionViewSource.GetDefaultView(_selectionDataGrid.ItemsSource);
            if (view != null)
            {
                view.Refresh();
            }

            UpdateStatus();
        }

        private void UpdateStatus()
        {
            int linkedCount = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] != null && _items[i].IsLinked)
                {
                    linkedCount++;
                }
            }

            _selectionStatusTextBlock.Text = _items.Count == 0
                ? "Список пуст. Добавьте элементы кликами или выделите их рамкой."
                : "Элементов: " + _items.Count + " · из связей: " + linkedCount +
                  " · количество видов задаётся на следующем шаге";
        }
    }
}
