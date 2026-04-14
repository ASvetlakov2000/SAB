using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Helpers.Notifications.ToastNotifications
{
    public static class ToastNotifier
    {
        // Блок настройки времени показа уведомления (секунды)
        private const int DefaultDurationSeconds = 10;

        private static readonly object SyncRoot = new object();
        private static Window _dummyWindow;
        private static ToastHost _host;

        // Group 1: ShowToast (without link)
        public static void ShowInfo(string title, string message, int durationSeconds = DefaultDurationSeconds)
        {
            ShowToastInternal(title, message, ToastType.Info, null, durationSeconds);
        }

        public static void ShowSuccess(string title, string message, int durationSeconds = DefaultDurationSeconds)
        {
            ShowToastInternal(title, message, ToastType.Success, null, durationSeconds);
        }

        public static void ShowWarning(string title, string message, int durationSeconds = DefaultDurationSeconds)
        {
            ShowToastInternal(title, message, ToastType.Warning, null, durationSeconds);
        }

        public static void ShowError(string title, string message, int durationSeconds = DefaultDurationSeconds)
        {
            ShowToastInternal(title, message, ToastType.Error, null, durationSeconds);
        }

        // Group 2: ShowToastWithLink (with folder link)
        public static void ShowFolderLinkInfo(string title, string message, string folderLink, int durationSeconds = DefaultDurationSeconds)
        {
            ShowToastInternal(title, message, ToastType.Info, folderLink, durationSeconds);
        }

        public static void ShowFolderLinkSuccess(string title, string message, string folderLink, int durationSeconds = DefaultDurationSeconds)
        {
            ShowToastInternal(title, message, ToastType.Success, folderLink, durationSeconds);
        }

        public static void ShowFolderLinkWarning(string title, string message, string folderLink, int durationSeconds = DefaultDurationSeconds)
        {
            ShowToastInternal(title, message, ToastType.Warning, folderLink, durationSeconds);
        }

        public static void ShowFolderLinkError(string title, string message, string folderLink, int durationSeconds = DefaultDurationSeconds)
        {
            ShowToastInternal(title, message, ToastType.Error, folderLink, durationSeconds);
        }

        // Блок единой точки показа уведомлений (без дублирования логики)
        private static void ShowToastInternal(
            string title,
            string message,
            ToastType toastType,
            string folderLink,
            int durationSeconds)
        {
            try
            {
                if (!EnsureHost())
                {
                    return;
                }

                int safeDuration = durationSeconds > 0 ? durationSeconds : DefaultDurationSeconds;
                _host.ShowToast(title, message, toastType, folderLink, safeDuration);
            }
            catch
            {
                // Защита от падения при проблемах с UI.
            }
        }

        private static bool EnsureHost()
        {
            if (_host != null)
            {
                return true;
            }

            lock (SyncRoot)
            {
                if (_host != null)
                {
                    return true;
                }

                try
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
                        Left = SystemParameters.WorkArea.Right - 554,
                        Top = SystemParameters.WorkArea.Bottom - (SystemParameters.WorkArea.Height * 2 / 3) - 10
                    };
                    _host.Show();
                }
                catch
                {
                    _host = null;
                }
            }

            return _host != null;
        }
    }

    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class ToastHost : Window
    {
        // Блок цветовых настроек уведомлений и кнопок
        private static readonly Color InfoSuccessBackgroundColor = Color.FromRgb(103, 108, 115);
        private static readonly Color WarningBackgroundColor = Color.FromRgb(255, 193, 94);
        private static readonly Color ErrorBackgroundColor = Color.FromRgb(255, 128, 128);
        private static readonly Color ButtonBackgroundColor = Color.FromRgb(103, 108, 115);

        private const double TextMaxWidth = 400;
        private const double IconSize = 26;

        private readonly StackPanel _stackPanel;
        private readonly ScrollViewer _scrollViewer;

        public ToastHost()
        {
            Width = 544;
            Topmost = true;
            AllowsTransparency = true;
            WindowStyle = WindowStyle.None;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            MaxHeight = SystemParameters.WorkArea.Height * 2 / 3;

            // Блок конфигурации базового контейнера уведомлений
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

        public void ShowToast(string title, string message, ToastType type, string folderLink, int durationSeconds)
        {
            Border border = CreateToastBorder(type);
            Grid grid = CreateToastGrid(border, title, message, type, folderLink);
            border.Child = grid;
            AddToast(border, durationSeconds);
        }

        private Border CreateToastBorder(ToastType type)
        {
            // Блок выбора цвета фона по типу уведомления
            Brush backgroundBrush = new SolidColorBrush(GetBackgroundColor(type));

            return new Border
            {
                Background = backgroundBrush,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(3),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 5),
                Padding = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Right
            };
        }

        private Grid CreateToastGrid(Border border, string title, string message, ToastType type, string folderLink)
        {
            Grid grid = new Grid { HorizontalAlignment = HorizontalAlignment.Right };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(IconSize) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock icon = new TextBlock
            {
                Text = GetIcon(type),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            Grid.SetColumn(icon, 0);

            Rectangle separator = new Rectangle
            {
                Width = 1.5,
                Fill = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetColumn(separator, 2);

            StackPanel textStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            TextBlock titleBlock = new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap
            };
            textStack.Children.Add(titleBlock);

            TextBlock messageBlock = new TextBlock
            {
                Text = message,
                FontSize = 14,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = TextMaxWidth,
                Margin = new Thickness(0, 2, 0, 0)
            };
            textStack.Children.Add(messageBlock);

            // Блок обработки уведомлений со ссылкой на папку
            if (!string.IsNullOrWhiteSpace(folderLink))
            {
                TextBlock linkBlock = new TextBlock
                {
                    Text = folderLink,
                    FontSize = 14,
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = TextMaxWidth,
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0, 2, 0, 0),
                    TextDecorations = TextDecorations.Underline
                };

                linkBlock.MouseLeftButtonUp += (sender, args) =>
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(folderLink) { UseShellExecute = true });
                    }
                    catch
                    {
                        // Невалидный путь не должен ломать работу уведомлений.
                    }
                };

                textStack.Children.Add(linkBlock);
            }

            Grid.SetColumn(textStack, 4);

            Rectangle separatorRight = new Rectangle
            {
                Width = 1.5,
                Fill = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetColumn(separatorRight, 5);

            Button closeButton = new Button
            {
                Content = "✖",
                Width = 24,
                Height = 24,
                Background = new SolidColorBrush(ButtonBackgroundColor),
                Foreground = Brushes.White,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(2),
                Cursor = Cursors.Hand,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                Template = CreateRoundedButtonTemplate()
            };

            closeButton.Click += (sender, args) => _stackPanel.Children.Remove(border);
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

            DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            border.BeginAnimation(OpacityProperty, fadeIn);

            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(durationSeconds)
            };

            timer.Tick += (sender, args) =>
            {
                timer.Stop();
                DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                fadeOut.Completed += (sender2, args2) => _stackPanel.Children.Remove(border);
                border.BeginAnimation(OpacityProperty, fadeOut);
            };

            timer.Start();
        }

        private static Color GetBackgroundColor(ToastType type)
        {
            switch (type)
            {
                case ToastType.Warning:
                    return WarningBackgroundColor;
                case ToastType.Error:
                    return ErrorBackgroundColor;
                case ToastType.Info:
                case ToastType.Success:
                default:
                    return InfoSuccessBackgroundColor;
            }
        }

        // Блок конфигурации кнопок уведомления
        private static ControlTemplate CreateRoundedButtonTemplate()
        {
            FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            borderFactory.SetValue(Border.BorderBrushProperty, Brushes.White);
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(2));
            borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(ButtonBackgroundColor));

            FrameworkElementFactory contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            borderFactory.AppendChild(contentFactory);

            ControlTemplate template = new ControlTemplate(typeof(Button));
            template.VisualTree = borderFactory;
            return template;
        }

        // Блок выбора иконки по типу уведомления
        private static string GetIcon(ToastType type)
        {
            switch (type)
            {
                case ToastType.Info:
                    return "ℹ";
                case ToastType.Success:
                    return "✔";
                case ToastType.Warning:
                    return "❗";
                case ToastType.Error:
                    return "✖";
                default:
                    return "?";
            }
        }
    }
}
