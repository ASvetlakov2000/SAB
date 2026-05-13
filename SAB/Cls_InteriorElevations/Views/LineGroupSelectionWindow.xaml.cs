using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace SAB.InteriorElevations.Views
{
    public partial class LineGroupSelectionWindow : Window
    {
        private Button _singleGroupButton;
        private Button _multipleGroupsButton;
        private Button _cancelButton;

        public LineGroupSelectionWindow()
        {
            InitializeWindowFromXamlFile();
            AttachButtonHandlers();
        }

        public bool IsMultipleGroups { get; private set; }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(LineGroupSelectionWindow).Assembly.Location);
            string xamlPath = Path.Combine(assemblyDirectory, "Cls_InteriorElevations", "Views", "LineGroupSelectionWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл окна выбора режима линий не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                Window loadedWindow = XamlReader.Load(stream) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось распарсить LineGroupSelectionWindow.xaml.");
                }

                _singleGroupButton = loadedWindow.FindName("SingleGroupButton") as Button;
                _multipleGroupsButton = loadedWindow.FindName("MultipleGroupsButton") as Button;
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
            if (_singleGroupButton == null || _multipleGroupsButton == null || _cancelButton == null)
            {
                throw new InvalidOperationException("Не удалось привязать кнопки в окне выбора режима линий.");
            }

            _singleGroupButton.Click += SingleGroupButton_Click;
            _multipleGroupsButton.Click += MultipleGroupsButton_Click;
            _cancelButton.Click += CancelButton_Click;
        }

        private void SingleGroupButton_Click(object sender, RoutedEventArgs e)
        {
            IsMultipleGroups = false;
            DialogResult = true;
            Close();
        }

        private void MultipleGroupsButton_Click(object sender, RoutedEventArgs e)
        {
            IsMultipleGroups = true;
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
