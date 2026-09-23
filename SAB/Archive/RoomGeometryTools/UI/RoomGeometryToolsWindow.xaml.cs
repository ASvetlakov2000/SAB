using System;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using SAB.RoomGeometryTools.ViewModels;
using SAB.UI;

namespace SAB.RoomGeometryTools.UI
{
    /// <summary>
    /// Окно инструментов проверки геометрии помещений.
    /// </summary>
    public partial class RoomGeometryToolsWindow : Window
    {
        private readonly RoomGeometryToolsViewModel _viewModel;

        public RoomGeometryToolsWindow(RoomGeometryToolsViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            InitializeWindowFromXamlFile();
            DataContext = _viewModel;
            _viewModel.RequestClose += ViewModel_RequestClose;
            Loaded += RoomGeometryToolsWindow_Loaded;
            Closed += RoomGeometryToolsWindow_Closed;
        }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(RoomGeometryToolsWindow).Assembly.Location);
            string xamlPath = Path.Combine(assemblyDirectory, "Cls_RoomGeometryTools", "UI", "RoomGeometryToolsWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл окна не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext parserContext = new ParserContext();
                parserContext.BaseUri = new Uri(xamlPath, UriKind.Absolute);

                Window loadedWindow = XamlReader.Load(stream, parserContext) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось загрузить RoomGeometryToolsWindow.xaml.");
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

                WindowSizeSettingsService.Apply(this, "RoomGeometryTools.MainWindow");
            }
        }

        private void RoomGeometryToolsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel.RunStartupActionIfNeeded();
        }

        private void ViewModel_RequestClose(object sender, EventArgs e)
        {
            Close();
        }

        private void RoomGeometryToolsWindow_Closed(object sender, EventArgs e)
        {
            _viewModel.RequestClose -= ViewModel_RequestClose;
            Loaded -= RoomGeometryToolsWindow_Loaded;
            Closed -= RoomGeometryToolsWindow_Closed;
        }
    }
}
