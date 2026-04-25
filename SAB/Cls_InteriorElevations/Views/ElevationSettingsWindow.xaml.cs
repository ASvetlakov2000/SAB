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
    public partial class ElevationSettingsWindow : Window
    {
        private readonly ElevationSettingsViewModel _viewModel;
        private Button _okButton;
        private Button _cancelButton;

        public ElevationSettingsWindow(ElevationSettingsViewModel viewModel)
        {
            _viewModel = viewModel;

            // Основной блок инициализации окна: загружаем XAML, назначаем DataContext и подключаем кнопки.
            InitializeWindowFromXamlFile();
            DataContext = _viewModel;
            AttachButtonHandlers();
        }

        public ElevationSettings SelectedSettings { get; private set; }

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
                Window loadedWindow = XamlReader.Load(stream) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось распарсить ElevationSettingsWindow.xaml.");
                }

                _okButton = loadedWindow.FindName("OkButton") as Button;
                _cancelButton = loadedWindow.FindName("CancelButton") as Button;

                Title = loadedWindow.Title;
                Width = loadedWindow.Width;
                Height = loadedWindow.Height;
                WindowStartupLocation = loadedWindow.WindowStartupLocation;
                ResizeMode = loadedWindow.ResizeMode;
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
