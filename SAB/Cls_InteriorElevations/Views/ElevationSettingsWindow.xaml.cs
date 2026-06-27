using System;
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
    public enum ElevationSettingsWindowAction
    {
        Cancel = 0,
        PickSelection = 1,
        Create = 2
    }

    public partial class ElevationSettingsWindow : Window
    {
        private readonly ElevationSettingsViewModel _viewModel;
        private readonly bool _initialMultipleGroupsMode;
        private readonly string _initialSelectionStatusText;

        private Button _okButton;
        private Button _cancelButton;
        private Button _pickLinesButton;
        private RadioButton _singleGroupRadioButton;
        private RadioButton _multipleGroupsRadioButton;
        private TextBlock _selectionStatusTextBlock;

        public ElevationSettingsWindow(ElevationSettingsViewModel viewModel)
            : this(viewModel, false, "Линии и помещение не выбраны.")
        {
        }

        public ElevationSettingsWindow(
            ElevationSettingsViewModel viewModel,
            bool initialMultipleGroupsMode,
            string initialSelectionStatusText)
        {
            _viewModel = viewModel;
            _initialMultipleGroupsMode = initialMultipleGroupsMode;
            _initialSelectionStatusText = string.IsNullOrWhiteSpace(initialSelectionStatusText)
                ? "Линии и помещение не выбраны."
                : initialSelectionStatusText;

            // Основной блок инициализации окна: загружаем XAML, назначаем DataContext и подключаем кнопки.
            InitializeWindowFromXamlFile();
            DataContext = _viewModel;
            ApplyInitialSelectionUiState();
            AttachButtonHandlers();
        }

        public ElevationSettings SelectedSettings { get; private set; }

        public ElevationSettingsWindowAction RequestedAction { get; private set; }

        public bool IsMultipleGroupsMode
        {
            get
            {
                return _multipleGroupsRadioButton != null && _multipleGroupsRadioButton.IsChecked == true;
            }
        }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(ElevationSettingsWindow).Assembly.Location);
            string xamlPath = Path.Combine(assemblyDirectory, "Cls_InteriorElevations", "Views", "ElevationSettingsWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл настроек не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext parserContext = new ParserContext();
                parserContext.BaseUri = new Uri(xamlPath, UriKind.Absolute);

                Window loadedWindow = XamlReader.Load(stream, parserContext) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось распарсить ElevationSettingsWindow.xaml.");
                }

                _okButton = loadedWindow.FindName("OkButton") as Button;
                _cancelButton = loadedWindow.FindName("CancelButton") as Button;
                _pickLinesButton = loadedWindow.FindName("PickLinesButton") as Button;
                _singleGroupRadioButton = loadedWindow.FindName("SingleGroupRadioButton") as RadioButton;
                _multipleGroupsRadioButton = loadedWindow.FindName("MultipleGroupsRadioButton") as RadioButton;
                _selectionStatusTextBlock = loadedWindow.FindName("SelectionStatusTextBlock") as TextBlock;

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

                WindowSizeSettingsService.Apply(this, "InteriorElevations.ElevationSettingsWindow");
            }
        }

        private void AttachButtonHandlers()
        {
            if (_okButton == null)
            {
                _okButton = FindElementByName<Button>(Content as DependencyObject, "OkButton");
            }

            if (_cancelButton == null)
            {
                _cancelButton = FindElementByName<Button>(Content as DependencyObject, "CancelButton");
            }

            if (_pickLinesButton == null)
            {
                _pickLinesButton = FindElementByName<Button>(Content as DependencyObject, "PickLinesButton");
            }

            if (_singleGroupRadioButton == null)
            {
                _singleGroupRadioButton = FindElementByName<RadioButton>(Content as DependencyObject, "SingleGroupRadioButton");
            }

            if (_multipleGroupsRadioButton == null)
            {
                _multipleGroupsRadioButton = FindElementByName<RadioButton>(Content as DependencyObject, "MultipleGroupsRadioButton");
            }

            if (_selectionStatusTextBlock == null)
            {
                _selectionStatusTextBlock = FindElementByName<TextBlock>(Content as DependencyObject, "SelectionStatusTextBlock");
            }

            if (_okButton == null || _cancelButton == null || _pickLinesButton == null)
            {
                throw new InvalidOperationException("Не удалось привязать кнопки окна настроек.");
            }

            _okButton.Click += OkButton_Click;
            _cancelButton.Click += CancelButton_Click;
            _pickLinesButton.Click += PickLinesButton_Click;
        }

        private void ApplyInitialSelectionUiState()
        {
            if (_singleGroupRadioButton == null)
            {
                _singleGroupRadioButton = FindElementByName<RadioButton>(Content as DependencyObject, "SingleGroupRadioButton");
            }

            if (_multipleGroupsRadioButton == null)
            {
                _multipleGroupsRadioButton = FindElementByName<RadioButton>(Content as DependencyObject, "MultipleGroupsRadioButton");
            }

            if (_selectionStatusTextBlock == null)
            {
                _selectionStatusTextBlock = FindElementByName<TextBlock>(Content as DependencyObject, "SelectionStatusTextBlock");
            }

            if (_singleGroupRadioButton != null)
            {
                _singleGroupRadioButton.IsChecked = !_initialMultipleGroupsMode;
            }

            if (_multipleGroupsRadioButton != null)
            {
                _multipleGroupsRadioButton.IsChecked = _initialMultipleGroupsMode;
            }

            if (_selectionStatusTextBlock != null)
            {
                _selectionStatusTextBlock.Text = _initialSelectionStatusText;
            }
        }

        private T FindElementByName<T>(DependencyObject root, string name) where T : FrameworkElement
        {
            if (root == null)
            {
                return null;
            }

            FrameworkElement frameworkElement = root as FrameworkElement;
            if (frameworkElement != null && string.Equals(frameworkElement.Name, name, StringComparison.Ordinal))
            {
                return frameworkElement as T;
            }

            foreach (object childObject in LogicalTreeHelper.GetChildren(root))
            {
                DependencyObject childDependencyObject = childObject as DependencyObject;
                if (childDependencyObject == null)
                {
                    continue;
                }

                T nestedChild = FindElementByName<T>(childDependencyObject, name);
                if (nestedChild != null)
                {
                    return nestedChild;
                }
            }

            return null;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ElevationSettings settings;
            string validationMessage;

            if (!_viewModel.TryBuildSettings(out settings, out validationMessage))
            {
                ToastNotifier.ShowWarning("SAB Развертки", validationMessage);
                return;
            }

            SelectedSettings = settings;
            RequestedAction = ElevationSettingsWindowAction.Create;
            DialogResult = true;
            Close();
        }

        private void PickLinesButton_Click(object sender, RoutedEventArgs e)
        {
            RequestedAction = ElevationSettingsWindowAction.PickSelection;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            RequestedAction = ElevationSettingsWindowAction.Cancel;
            DialogResult = false;
            Close();
        }
    }
}
