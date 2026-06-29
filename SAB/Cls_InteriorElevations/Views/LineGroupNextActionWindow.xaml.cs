using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using SAB.UI;

namespace SAB.InteriorElevations.Views
{
    public partial class LineGroupNextActionWindow : Window
    {
        private TextBlock _headerTextBlock;
        private Button _nextGroupButton;
        private Button _finishButton;
        private Button _cancelButton;

        public LineGroupNextActionWindow(int currentGroupNumber)
        {
            InitializeWindowFromXamlFile();
            AttachButtonHandlers();
            SetGroupNumber(currentGroupNumber);
        }

        public bool IsNextGroupAction { get; private set; }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(LineGroupNextActionWindow).Assembly.Location);
            string xamlPath = Path.Combine(assemblyDirectory, "Cls_InteriorElevations", "Views", "LineGroupNextActionWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл окна следующего действия не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext parserContext = new ParserContext();
                parserContext.BaseUri = new Uri(xamlPath, UriKind.Absolute);

                Window loadedWindow = XamlReader.Load(stream, parserContext) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось распарсить LineGroupNextActionWindow.xaml.");
                }

                _headerTextBlock = loadedWindow.FindName("HeaderTextBlock") as TextBlock;
                _nextGroupButton = loadedWindow.FindName("NextGroupButton") as Button;
                _finishButton = loadedWindow.FindName("FinishButton") as Button;
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

                WindowSizeSettingsService.Apply(this, "InteriorElevations.LineGroupNextActionWindow.CompactV3");
            }
        }

        private void AttachButtonHandlers()
        {
            if (_nextGroupButton == null || _finishButton == null || _cancelButton == null)
            {
                throw new InvalidOperationException("Не удалось привязать кнопки в окне следующего действия.");
            }

            _nextGroupButton.Click += NextGroupButton_Click;
            _finishButton.Click += FinishButton_Click;
            _cancelButton.Click += CancelButton_Click;
        }

        private void SetGroupNumber(int currentGroupNumber)
        {
            if (_headerTextBlock != null)
            {
                int safeGroupNumber = currentGroupNumber > 0 ? currentGroupNumber : 1;
                _headerTextBlock.Text = "Группа №" + safeGroupNumber + " добавлена.";
            }
        }

        private void NextGroupButton_Click(object sender, RoutedEventArgs e)
        {
            IsNextGroupAction = true;
            DialogResult = true;
            Close();
        }

        private void FinishButton_Click(object sender, RoutedEventArgs e)
        {
            IsNextGroupAction = false;
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
