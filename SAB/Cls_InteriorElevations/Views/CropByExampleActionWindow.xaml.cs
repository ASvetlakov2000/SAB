using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using SAB.UI;

namespace SAB.InteriorElevations.Views
{
    public enum CropByExampleAction
    {
        None = 0,
        PickLine = 1,
        CreateLine = 2
    }

    public partial class CropByExampleActionWindow : Window
    {
        private Button _closeButton;
        private Button _pickLineButton;
        private Button _createLineButton;

        public CropByExampleActionWindow()
        {
            InitializeWindowFromXamlFile();
            AttachButtonHandlers();
        }

        public CropByExampleAction RequestedAction { get; private set; }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(CropByExampleActionWindow).Assembly.Location);
            string xamlPath = Path.Combine(assemblyDirectory, "Cls_InteriorElevations", "Views", "CropByExampleActionWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл окна обрезки по примеру не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext parserContext = new ParserContext();
                parserContext.BaseUri = new Uri(xamlPath, UriKind.Absolute);

                Window loadedWindow = XamlReader.Load(stream, parserContext) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось распарсить CropByExampleActionWindow.xaml.");
                }

                _closeButton = loadedWindow.FindName("CloseButton") as Button;
                _pickLineButton = loadedWindow.FindName("PickLineButton") as Button;
                _createLineButton = loadedWindow.FindName("CreateLineButton") as Button;

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

                WindowSizeSettingsService.Apply(this, "InteriorElevations.CropByExampleActionWindow.V4");
            }
        }

        private void AttachButtonHandlers()
        {
            if (_closeButton == null || _pickLineButton == null || _createLineButton == null)
            {
                throw new InvalidOperationException("Не удалось привязать кнопки окна обрезки по примеру.");
            }

            _closeButton.Click += CloseButton_Click;
            _pickLineButton.Click += PickLineButton_Click;
            _createLineButton.Click += CreateLineButton_Click;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            RequestedAction = CropByExampleAction.None;
            DialogResult = false;
            Close();
        }

        private void PickLineButton_Click(object sender, RoutedEventArgs e)
        {
            RequestedAction = CropByExampleAction.PickLine;
            DialogResult = true;
            Close();
        }

        private void CreateLineButton_Click(object sender, RoutedEventArgs e)
        {
            RequestedAction = CropByExampleAction.CreateLine;
            DialogResult = true;
            Close();
        }
    }
}
