using System;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using SAB.CreateViewsAndSheets.ViewModels;

namespace SAB.CreateViewsAndSheets.Views
{
    public class CreateViewsAndSheetsSettingsWindow : Window
    {
        private readonly CreateViewsAndSheetsViewModel _viewModel;

        public CreateViewsAndSheetsSettingsWindow(CreateViewsAndSheetsViewModel viewModel)
        {
            if (viewModel == null)
            {
                throw new ArgumentNullException(nameof(viewModel));
            }

            _viewModel = viewModel;
            InitializeWindowFromXamlFile();
            DataContext = viewModel;
            _viewModel.RequestPointSelection += ViewModel_RequestPointSelection;
            Closed += CreateViewsAndSheetsSettingsWindow_Closed;
        }

        public PlacementPointSelectionRequestEventArgs PendingPointSelectionRequest { get; private set; }

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

        private void CreateViewsAndSheetsSettingsWindow_Closed(object sender, EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.RequestPointSelection -= ViewModel_RequestPointSelection;
            }

            Closed -= CreateViewsAndSheetsSettingsWindow_Closed;
        }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(CreateViewsAndSheetsSettingsWindow).Assembly.Location);
            string xamlPath = Path.Combine(assemblyDirectory, "Cls_CreateViewsAndSheets", "Views", "CreateViewsAndSheetsSettingsWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл окна настроек создания видов и листов не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext parserContext = new ParserContext();
                parserContext.BaseUri = new Uri(xamlPath, UriKind.Absolute);

                Window loadedWindow = XamlReader.Load(stream, parserContext) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось загрузить CreateViewsAndSheetsSettingsWindow.xaml.");
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
    }
}
