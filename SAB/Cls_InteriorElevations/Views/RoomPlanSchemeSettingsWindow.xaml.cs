using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Helpers.Notifications.ToastNotifications;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.ViewModels;

namespace SAB.InteriorElevations.Views
{
    /// <summary>
    /// Окно настроек создания план-схем разверток помещений.
    /// </summary>
    public partial class RoomPlanSchemeSettingsWindow : Window
    {
        private readonly RoomPlanSchemeSettingsViewModel _viewModel;
        private Button _okButton;
        private Button _cancelButton;

        public RoomPlanSchemeSettingsWindow(RoomPlanSchemeSettingsViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            InitializeWindowFromXamlFile();
            DataContext = _viewModel;
            AttachButtonHandlers();
        }

        public RoomPlanSchemeSettings SelectedSettings { get; private set; }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(RoomPlanSchemeSettingsWindow).Assembly.Location);
            string xamlPath = Path.Combine(assemblyDirectory, "Cls_InteriorElevations", "Views", "RoomPlanSchemeSettingsWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл настроек не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                Window loadedWindow = XamlReader.Load(stream) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось распарсить RoomPlanSchemeSettingsWindow.xaml.");
                }

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
                Resources = loadedWindow.Resources;
                Content = loadedWindow.Content;
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

            if (_okButton == null || _cancelButton == null)
            {
                throw new InvalidOperationException("Не удалось привязать кнопки ОК/Отмена в окне настроек.");
            }

            _okButton.Click += OkButton_Click;
            _cancelButton.Click += CancelButton_Click;
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
                DependencyObject childDependencyObject = childObject as DependencyObject;
                if (childDependencyObject == null)
                {
                    continue;
                }

                T nested = FindElementByName<T>(childDependencyObject, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.TryBuildSettings(out RoomPlanSchemeSettings settings, out string validationMessage))
            {
                ToastNotifier.ShowWarning("SAB Развертки", validationMessage);
                return;
            }

            SelectedSettings = settings;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

