using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Autodesk.Revit.UI;
using SAB.InteriorElevations.Services.Elevations;
using SAB.UI;

namespace SAB.InteriorElevations.Views
{
    public partial class CropByExampleLineCreationWindow : Window
    {
        private readonly CropByExampleExternalEventHandler _externalEventHandler;
        private readonly ExternalEvent _externalEvent;

        private TextBlock _statusHeaderTextBlock;
        private TextBlock _statusDescriptionTextBlock;
        private Button _cancelButton;
        private Button _selectLineButton;
        private Button _createViewButton;
        private Button _acceptCropButton;

        public CropByExampleLineCreationWindow(
            CropByExampleExternalEventHandler externalEventHandler,
            ExternalEvent externalEvent)
        {
            _externalEventHandler = externalEventHandler;
            _externalEvent = externalEvent;

            InitializeWindowFromXamlFile();
            AttachButtonHandlers();
            UpdateStatus(
                "Линия еще не выбрана.",
                "Создайте или выберите одну линию детализации, затем нажмите Выбрать линию.",
                false,
                false);
        }

        public void SetExistingLineMode()
        {
            UpdateStatus(
                "Выберите существующую линию.",
                "Укажите одну существующую линию детализации, затем выберите помещение, когда Revit запросит выбор.",
                false,
                false);
        }

        public void SetCreateLineMode()
        {
            UpdateStatus(
                "Линия еще не выбрана.",
                "Нарисуйте одну линию детализации слева направо, завершите команду Revit, затем нажмите Выбрать линию.",
                false,
                false);
        }

        public void UpdateStatus(
            string header,
            string description,
            bool lineSelected,
            bool sampleViewCreated)
        {
            if (_statusHeaderTextBlock != null)
            {
                _statusHeaderTextBlock.Text = string.IsNullOrWhiteSpace(header) ? "Состояние." : header;
            }

            if (_statusDescriptionTextBlock != null)
            {
                _statusDescriptionTextBlock.Text = string.IsNullOrWhiteSpace(description) ? string.Empty : description;
            }

            if (_selectLineButton != null)
            {
                _selectLineButton.IsEnabled = !sampleViewCreated;
            }

            if (_createViewButton != null)
            {
                _createViewButton.IsEnabled = lineSelected && !sampleViewCreated;
            }

            if (_acceptCropButton != null)
            {
                _acceptCropButton.IsEnabled = sampleViewCreated;
            }

            Activate();
        }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(CropByExampleLineCreationWindow).Assembly.Location);
            string xamlPath = Path.Combine(assemblyDirectory, "Cls_InteriorElevations", "Views", "CropByExampleLineCreationWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл окна создания линии-основы не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext parserContext = new ParserContext();
                parserContext.BaseUri = new Uri(xamlPath, UriKind.Absolute);

                Window loadedWindow = XamlReader.Load(stream, parserContext) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось распарсить CropByExampleLineCreationWindow.xaml.");
                }

                _statusHeaderTextBlock = loadedWindow.FindName("StatusHeaderTextBlock") as TextBlock;
                _statusDescriptionTextBlock = loadedWindow.FindName("StatusDescriptionTextBlock") as TextBlock;
                _cancelButton = loadedWindow.FindName("CancelButton") as Button;
                _selectLineButton = loadedWindow.FindName("SelectLineButton") as Button;
                _createViewButton = loadedWindow.FindName("CreateViewButton") as Button;
                _acceptCropButton = loadedWindow.FindName("AcceptCropButton") as Button;

                Title = loadedWindow.Title;
                Width = loadedWindow.Width;
                Height = loadedWindow.Height;
                MinWidth = loadedWindow.MinWidth;
                MinHeight = loadedWindow.MinHeight;
                WindowStartupLocation = loadedWindow.WindowStartupLocation;
                ResizeMode = loadedWindow.ResizeMode;
                Topmost = loadedWindow.Topmost;
                Style = loadedWindow.Style;
                Background = loadedWindow.Background;
                FontFamily = loadedWindow.FontFamily;
                FontSize = loadedWindow.FontSize;
                FontWeight = loadedWindow.FontWeight;
                Resources = loadedWindow.Resources;
                Content = loadedWindow.Content;

                WindowSizeSettingsService.Apply(this, "InteriorElevations.CropByExampleLineCreationWindow.V4");
            }
        }

        private void AttachButtonHandlers()
        {
            if (_cancelButton == null || _selectLineButton == null || _createViewButton == null || _acceptCropButton == null)
            {
                throw new InvalidOperationException("Не удалось привязать кнопки окна создания линии-основы.");
            }

            _cancelButton.Click += CancelButton_Click;
            _selectLineButton.Click += SelectLineButton_Click;
            _createViewButton.Click += CreateViewButton_Click;
            _acceptCropButton.Click += AcceptCropButton_Click;
        }

        private void SelectLineButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseExternalEvent(CropByExampleOperation.PickLine);
        }

        private void CreateViewButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseExternalEvent(CropByExampleOperation.CreateView);
        }

        private void AcceptCropButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseExternalEvent(CropByExampleOperation.AcceptCrop);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RaiseExternalEvent(CropByExampleOperation operation)
        {
            if (_externalEventHandler == null || _externalEvent == null)
            {
                MessageBox.Show(
                    "Не удалось выполнить действие: сервис Revit API недоступен.",
                    "SAB Развертки",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                _externalEventHandler.RequestOperation(operation);
                _externalEvent.Raise();
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "Не удалось передать действие в Revit: " + exception.Message,
                    "SAB Развертки",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}
