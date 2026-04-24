using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Autodesk.Revit.UI;
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

            // Important block: load window structure from XAML file to keep layout maintainable.
            InitializeWindowFromXamlFile();
            DataContext = _viewModel;

            // After runtime XAML load, hook action buttons explicitly.
            AttachButtonHandlers();
        }

        public ElevationSettings SelectedSettings { get; private set; }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(ElevationSettingsWindow).Assembly.Location);
            string xamlPath = Path.Combine(assemblyDirectory, "Cls_InteriorElevations", "Views", "ElevationSettingsWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Elevation settings XAML file was not found: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                Window loadedWindow = XamlReader.Load(stream) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Failed to parse ElevationSettingsWindow.xaml.");
                }

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
            _okButton = FindElementByName<Button>(Content as DependencyObject, "OkButton");
            _cancelButton = FindElementByName<Button>(Content as DependencyObject, "CancelButton");

            if (_okButton == null || _cancelButton == null)
            {
                throw new InvalidOperationException("Failed to bind OK/Cancel buttons in the settings window.");
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

            int childrenCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childrenCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                T typedChild = child as T;

                if (typedChild != null && string.Equals(typedChild.Name, name, StringComparison.Ordinal))
                {
                    return typedChild;
                }

                T nestedChild = FindElementByName<T>(child, name);
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
                TaskDialog.Show("SAB Interior Elevations", validationMessage);
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
