using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Helpers.Notifications.ToastNotifications;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.ViewModels;
using SAB.UI;

namespace SAB.InteriorElevations.Views
{
    public enum MultiRoomSelectionWindowAction
    {
        Cancel = 0,
        PickSelection = 1,
        Transfer = 2
    }

    public partial class MultiRoomSelectionWindow : Window
    {
        private readonly MultiRoomSelectionViewModel _viewModel;
        private DataGrid _roomsDataGrid;
        private Button _cancelButton;
        private Button _transferButton;

        public MultiRoomSelectionWindow(ObservableCollection<MultiRoomSelectionItem> rows)
        {
            _viewModel = new MultiRoomSelectionViewModel(rows);
            InitializeWindowFromXamlFile();
            DataContext = _viewModel;
            AttachHandlers();
        }

        public MultiRoomSelectionWindowAction RequestedAction { get; private set; }

        public MultiRoomSelectionItem RequestedRow { get; private set; }

        public ObservableCollection<MultiRoomSelectionItem> Rows
        {
            get { return _viewModel.Rows; }
        }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(MultiRoomSelectionWindow).Assembly.Location);
            string xamlPath = Path.Combine(
                assemblyDirectory,
                "Cls_InteriorElevations",
                "Views",
                "MultiRoomSelectionWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл списка помещений не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext parserContext = new ParserContext();
                parserContext.BaseUri = new Uri(xamlPath, UriKind.Absolute);

                Window loadedWindow = XamlReader.Load(stream, parserContext) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось распарсить MultiRoomSelectionWindow.xaml.");
                }

                _roomsDataGrid = loadedWindow.FindName("RoomsDataGrid") as DataGrid;
                _cancelButton = loadedWindow.FindName("CancelButton") as Button;
                _transferButton = loadedWindow.FindName("TransferButton") as Button;

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

                WindowSizeSettingsService.Apply(this, "InteriorElevations.MultiRoomSelectionWindow");
            }
        }

        private void AttachHandlers()
        {
            if (_roomsDataGrid == null || _cancelButton == null || _transferButton == null)
            {
                throw new InvalidOperationException("Не удалось привязать элементы окна списка помещений.");
            }

            _roomsDataGrid.AddHandler(
                Button.ClickEvent,
                new RoutedEventHandler(RoomActionButton_Click),
                true);
            _cancelButton.Click += CancelButton_Click;
            _transferButton.Click += TransferButton_Click;
        }

        private void RoomActionButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = e.OriginalSource as Button;
            if (button == null)
            {
                return;
            }

            MultiRoomSelectionItem row = button.Tag as MultiRoomSelectionItem;
            if (row == null)
            {
                return;
            }

            if (string.Equals(button.Name, "SelectRoomButton", StringComparison.Ordinal))
            {
                RequestedRow = row;
                RequestedAction = MultiRoomSelectionWindowAction.PickSelection;
                DialogResult = true;
                Close();
                return;
            }

            if (string.Equals(button.Name, "AddRoomButton", StringComparison.Ordinal))
            {
                _viewModel.AddRowAfter(row);
                e.Handled = true;
                return;
            }

            if (string.Equals(button.Name, "DeleteRoomButton", StringComparison.Ordinal))
            {
                _viewModel.DeleteRow(row);
                e.Handled = true;
            }
        }

        private void TransferButton_Click(object sender, RoutedEventArgs e)
        {
            string validationMessage;
            if (!_viewModel.TryValidateTransfer(out validationMessage))
            {
                _viewModel.RefreshSummary();
                ToastNotifier.ShowWarning("SAB Развертки", validationMessage);
                return;
            }

            RequestedAction = MultiRoomSelectionWindowAction.Transfer;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            RequestedAction = MultiRoomSelectionWindowAction.Cancel;
            DialogResult = false;
            Close();
        }
    }
}
