using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Helpers.Notifications.ToastNotifications;
using SAB.InteriorElevations.Models;
using SAB.UI;

namespace SAB.InteriorElevations.Views
{
    public partial class ElevationCropAdjustmentWindow : Window
    {
        private TextBox _leftTextBox;
        private TextBox _rightTextBox;
        private TextBox _topTextBox;
        private TextBox _bottomTextBox;
        private Button _applyButton;
        private Button _cancelButton;

        public ElevationCropAdjustmentWindow()
        {
            InitializeWindowFromXamlFile();
            AttachHandlers();
        }

        public ElevationCropAdjustmentSettings SelectedSettings { get; private set; }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(ElevationCropAdjustmentWindow).Assembly.Location);
            string xamlPath = Path.Combine(
                assemblyDirectory,
                "Cls_InteriorElevations",
                "Views",
                "ElevationCropAdjustmentWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл редактора границ разверток не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext parserContext = new ParserContext();
                parserContext.BaseUri = new Uri(xamlPath, UriKind.Absolute);

                Window loadedWindow = XamlReader.Load(stream, parserContext) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось загрузить ElevationCropAdjustmentWindow.xaml.");
                }

                _leftTextBox = loadedWindow.FindName("LeftTextBox") as TextBox;
                _rightTextBox = loadedWindow.FindName("RightTextBox") as TextBox;
                _topTextBox = loadedWindow.FindName("TopTextBox") as TextBox;
                _bottomTextBox = loadedWindow.FindName("BottomTextBox") as TextBox;
                _applyButton = loadedWindow.FindName("ApplyButton") as Button;
                _cancelButton = loadedWindow.FindName("CancelButton") as Button;

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
                FontWeight = loadedWindow.FontWeight;
                Content = loadedWindow.Content;
                Resources = loadedWindow.Resources;

                WindowSizeSettingsService.Apply(this, "InteriorElevations.ElevationCropAdjustmentWindow");
            }
        }

        private void AttachHandlers()
        {
            _leftTextBox = _leftTextBox ?? FindElementByName<TextBox>(Content as DependencyObject, "LeftTextBox");
            _rightTextBox = _rightTextBox ?? FindElementByName<TextBox>(Content as DependencyObject, "RightTextBox");
            _topTextBox = _topTextBox ?? FindElementByName<TextBox>(Content as DependencyObject, "TopTextBox");
            _bottomTextBox = _bottomTextBox ?? FindElementByName<TextBox>(Content as DependencyObject, "BottomTextBox");
            _applyButton = _applyButton ?? FindElementByName<Button>(Content as DependencyObject, "ApplyButton");
            _cancelButton = _cancelButton ?? FindElementByName<Button>(Content as DependencyObject, "CancelButton");

            if (_leftTextBox == null || _rightTextBox == null || _topTextBox == null ||
                _bottomTextBox == null || _applyButton == null || _cancelButton == null)
            {
                throw new InvalidOperationException("Не удалось привязать элементы редактора границ разверток.");
            }

            _applyButton.Click += ApplyButton_Click;
            _cancelButton.Click += CancelButton_Click;
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            double leftMm;
            double rightMm;
            double topMm;
            double bottomMm;

            if (!TryParseMillimeters(_leftTextBox.Text, out leftMm) ||
                !TryParseMillimeters(_rightTextBox.Text, out rightMm) ||
                !TryParseMillimeters(_topTextBox.Text, out topMm) ||
                !TryParseMillimeters(_bottomTextBox.Text, out bottomMm))
            {
                ToastNotifier.ShowWarning("SAB Развертки", "Введите числовые значения для всех четырех границ.");
                return;
            }

            SelectedSettings = new ElevationCropAdjustmentSettings();
            SelectedSettings.LeftMm = leftMm;
            SelectedSettings.RightMm = rightMm;
            SelectedSettings.TopMm = topMm;
            SelectedSettings.BottomMm = bottomMm;

            DialogResult = true;
            Close();
        }

        private static bool TryParseMillimeters(string text, out double value)
        {
            if (double.TryParse(
                    text,
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.CurrentCulture,
                    out value))
            {
                return !double.IsNaN(value) && !double.IsInfinity(value);
            }

            bool parsed = double.TryParse(
                text,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out value);
            return parsed && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static T FindElementByName<T>(DependencyObject root, string name) where T : FrameworkElement
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
                DependencyObject child = childObject as DependencyObject;
                if (child == null)
                {
                    continue;
                }

                T nested = FindElementByName<T>(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
