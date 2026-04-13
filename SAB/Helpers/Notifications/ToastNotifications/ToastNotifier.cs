using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Helpers.Notifications.ToastNotifications
{
    public static class ToastNotifier
    {
        private static Window _dummyWindow;
        private static ToastHost _host;

        static ToastNotifier()
        {
            _dummyWindow = new Window
            {
                Width = 0,
                Height = 0,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Opacity = 0
            };
            _dummyWindow.Show();

            _host = new ToastHost
            {
                Owner = _dummyWindow,
                Left = SystemParameters.WorkArea.Right - 544 - 10, // ширина + 20%
                Top = SystemParameters.WorkArea.Bottom - (SystemParameters.WorkArea.Height * 2 / 3) - 10
            };
            _host.Show();
        }

        public static void ShowInfo(string title, string message, int durationSeconds = 5) =>
            _host.ShowToast(title, message, ToastType.Info, durationSeconds);

        public static void ShowSuccess(string title, string message, int durationSeconds = 5) =>
            _host.ShowToast(title, message, ToastType.Success, durationSeconds);

        public static void ShowWarning(string title, string message, int durationSeconds = 5) =>
            _host.ShowToast(title, message, ToastType.Warning, durationSeconds);

        public static void ShowError(string title, string message, int durationSeconds = 5) =>
            _host.ShowToast(title, message, ToastType.Error, durationSeconds);

        public static void ShowFolderLinkInfo(string title, string message, string folderLink, int durationSeconds = 10) =>
            _host.ShowToastWithLink(title, message, folderLink, ToastType.Info, durationSeconds);

        public static void ShowFolderLinkSuccess(string title, string message, string folderLink, int durationSeconds = 10) =>
            _host.ShowToastWithLink(title, message, folderLink, ToastType.Success, durationSeconds);

        public static void ShowFolderLinkWarning(string title, string message, string folderLink, int durationSeconds = 10) =>
            _host.ShowToastWithLink(title, message, folderLink, ToastType.Warning, durationSeconds);

        public static void ShowFolderLinkError(string title, string message, string folderLink, int durationSeconds = 10) =>
            _host.ShowToastWithLink(title, message, folderLink, ToastType.Error, durationSeconds);
    }

    public enum ToastType { Info, Success, Warning, Error }

    public class ToastHost : Window
    {
        private StackPanel _stackPanel;
        private ScrollViewer _scrollViewer;
        private const double TextMaxWidth = 400; // немного увеличили, чтобы текст не обрезался
        private const double IconSize = 26;

        public ToastHost()
        {
            Width = 544; // увеличено на 20%
            Topmost = true;
            AllowsTransparency = true;
            WindowStyle = WindowStyle.None;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            MaxHeight = SystemParameters.WorkArea.Height * 2 / 3;

            _stackPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };

            _scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _stackPanel,
                Background = Brushes.Transparent
            };

            Content = _scrollViewer;
        }

        public void ShowToast(string title, string message, ToastType type, int durationSeconds)
        {
            var border = CreateToastBorder(type);
            var grid = CreateToastGrid(border, title, message, type, link: null);
            border.Child = grid;
            AddToast(border, durationSeconds);
        }

        public void ShowToastWithLink(string title, string message, string link, ToastType type, int durationSeconds)
        {
            var border = CreateToastBorder(type);
            var grid = CreateToastGrid(border, title, message, type, link);
            border.Child = grid;
            AddToast(border, durationSeconds);
        }

        private Border CreateToastBorder(ToastType type)
        {
            return new Border
            {
                Background = GetBackground(type),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 5),
                Padding = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Right
            };
        }

        private Grid CreateToastGrid(Border border, string title, string message, ToastType type, string link)
        {
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Right };

            // Расстановка колонок
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(IconSize) });   // значок
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });       // отступ между значком и левым разделителем
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5) });      // левый разделитель
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });       // отступ от разделителя до текста
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // текст
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) });      // правый разделитель
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });           // кнопка закрытия

            // Иконка
            var icon = new TextBlock
            {
                Text = GetIcon(type),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            Grid.SetColumn(icon, 0);

            // Левый разделитель
            var separator = new Rectangle
            {
                Width = 1.5,
                Fill = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetColumn(separator, 2);

            // Текстовая часть
            var textStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var titleBlock = new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap
            };
            textStack.Children.Add(titleBlock);

            var messageBlock = new TextBlock
            {
                Text = message,
                FontSize = 14,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = TextMaxWidth,
                Margin = new Thickness(0, 2, 0, 0)
            };
            textStack.Children.Add(messageBlock);

            if (!string.IsNullOrEmpty(link))
            {
                var linkBlock = new TextBlock
                {
                    Text = link,
                    FontSize = 14,
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = TextMaxWidth,
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0, 2, 0, 0),
                    TextDecorations = TextDecorations.Underline
                };
                linkBlock.MouseLeftButtonUp += (_, __) =>
                {
                    try { Process.Start(new ProcessStartInfo(link) { UseShellExecute = true }); }
                    catch { }
                };
                textStack.Children.Add(linkBlock);
            }

            Grid.SetColumn(textStack, 4);

            // Правый разделитель
            var separatorRight = new Rectangle
            {
                Width = 1.5,
                Fill = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetColumn(separatorRight, 5);

            // Кнопка закрытия
            var closeButton = new Button
            {
                Content = "✖",
                Width = 24,
                Height = 24,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                Cursor = Cursors.Hand,
                Padding = new Thickness(0),
                Margin = new Thickness(0)
            };
            closeButton.Click += (_, __) => _stackPanel.Children.Remove(border);
            Grid.SetColumn(closeButton, 6);

            grid.Children.Add(icon);
            grid.Children.Add(separator);
            grid.Children.Add(textStack);
            grid.Children.Add(separatorRight);
            grid.Children.Add(closeButton);

            return grid;
        }

        private void AddToast(Border border, int durationSeconds)
        {
            _stackPanel.Children.Insert(0, border);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            border.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(durationSeconds) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                fadeOut.Completed += (s2, e2) => _stackPanel.Children.Remove(border);
                border.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            };
            timer.Start();
        }

        private Brush GetBackground(ToastType type) => type switch
        {
            ToastType.Info => new SolidColorBrush(Color.FromRgb(116, 155, 184)),
            ToastType.Success => new SolidColorBrush(Color.FromRgb(116, 155, 184)),
            ToastType.Warning => new SolidColorBrush(Color.FromRgb(255, 193, 94)),
            ToastType.Error => new SolidColorBrush(Color.FromRgb(255, 128, 128)),
            _ => Brushes.Gray
        };

        private string GetIcon(ToastType type) => type switch
        {
            ToastType.Info => "ℹ",
            ToastType.Success => "✔",
            ToastType.Warning => "❗",
            ToastType.Error => "✖",
            _ => "?"
        };
    }
}