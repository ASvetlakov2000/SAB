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
    public partial class PlanCornerMarkAlignmentWindow : Window
    {
        private readonly PlanCornerMarkAlignmentSettings _initialSettings;

        private TextBox _cornerOffsetTextBox;
        private Button _okButton;
        private Button _cancelButton;

        public PlanCornerMarkAlignmentWindow(PlanCornerMarkAlignmentSettings initialSettings)
        {
            _initialSettings = initialSettings;

            InitializeWindowFromXamlFile();
            AttachButtonHandlers();
            ApplyInitialSettings();
        }

        public PlanCornerMarkAlignmentSettings SelectedSettings { get; private set; }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(PlanCornerMarkAlignmentWindow).Assembly.Location);
            string xamlPath = Path.Combine(assemblyDirectory, "Cls_InteriorElevations", "Views", "PlanCornerMarkAlignmentWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл настроек выравнивания не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext parserContext = new ParserContext();
                parserContext.BaseUri = new Uri(xamlPath, UriKind.Absolute);

                Window loadedWindow = XamlReader.Load(stream, parserContext) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось загрузить PlanCornerMarkAlignmentWindow.xaml.");
                }

                _cornerOffsetTextBox = loadedWindow.FindName("CornerOffsetTextBox") as TextBox;
                _okButton = loadedWindow.FindName("OkButton") as Button;
                _cancelButton = loadedWindow.FindName("CancelButton") as Button;

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
                Content = loadedWindow.Content;
                Resources = loadedWindow.Resources;

                WindowSizeSettingsService.Apply(this, "InteriorElevations.PlanCornerMarkAlignmentWindow");
            }
        }

        private void AttachButtonHandlers()
        {
            if (_cornerOffsetTextBox == null)
            {
                _cornerOffsetTextBox = FindElementByName<TextBox>(Content as DependencyObject, "CornerOffsetTextBox");
            }

            if (_okButton == null)
            {
                _okButton = FindElementByName<Button>(Content as DependencyObject, "OkButton");
            }

            if (_cancelButton == null)
            {
                _cancelButton = FindElementByName<Button>(Content as DependencyObject, "CancelButton");
            }

            if (_cornerOffsetTextBox == null || _okButton == null || _cancelButton == null)
            {
                throw new InvalidOperationException("Не удалось привязать элементы окна выравнивания марок.");
            }

            _okButton.Click += OkButton_Click;
            _cancelButton.Click += CancelButton_Click;
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

        private void ApplyInitialSettings()
        {
            if (_cornerOffsetTextBox == null)
            {
                return;
            }

            if (_initialSettings == null || _initialSettings.CornerOffsetMm < 0)
            {
                _cornerOffsetTextBox.Text = "80";
                return;
            }

            _cornerOffsetTextBox.Text = _initialSettings.CornerOffsetMm.ToString("0.###", CultureInfo.CurrentCulture);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            double cornerOffsetMm;
            if (!TryParseMillimeters(_cornerOffsetTextBox.Text, out cornerOffsetMm))
            {
                ToastNotifier.ShowWarning("SAB Развертки", "Введите корректное значение для отступа марки от угла.");
                return;
            }

            if (cornerOffsetMm < 0)
            {
                ToastNotifier.ShowWarning("SAB Развертки", "Отступ марки от угла не может быть отрицательным.");
                return;
            }

            SelectedSettings = new PlanCornerMarkAlignmentSettings();
            SelectedSettings.CornerOffsetMm = cornerOffsetMm;
            SelectedSettings.LeaderBreakAngle = PlanLeaderBreakAngleType.Degrees90;

            DialogResult = true;
            Close();
        }

        private bool TryParseMillimeters(string text, out double value)
        {
            if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out value))
            {
                return true;
            }

            return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
