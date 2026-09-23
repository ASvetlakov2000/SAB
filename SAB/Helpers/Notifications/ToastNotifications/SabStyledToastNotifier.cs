using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Helpers.Notifications.ToastNotifications
{
    public static class SabStyledToastNotifier
    {
        private const int DefaultDurationSeconds = 10;

        private static readonly object SyncRoot = new object();
        private static Window _dummyWindow;
        private static SabStyledToastHost _host;

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

                    _host = new SabStyledToastHost
                    {
                        Owner = _dummyWindow,
                        Left = SystemParameters.WorkArea.Right - 474,
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

    public class SabStyledToastHost : Window
    {
        private static readonly Color WindowBackgroundColor = Color.FromRgb(248, 250, 252);
        private static readonly Color PanelBackgroundColor = Color.FromRgb(255, 255, 255);
        private static readonly Color BorderColor = Color.FromRgb(214, 226, 241);
        private static readonly Color PrimaryColor = Color.FromRgb(20, 115, 215);
        private static readonly Color SuccessColor = Color.FromRgb(13, 148, 136);
        private static readonly Color WarningColor = Color.FromRgb(245, 158, 11);
        private static readonly Color ErrorColor = Color.FromRgb(220, 38, 38);
        private static readonly Color TextColor = Color.FromRgb(15, 23, 42);
        private static readonly Color MutedTextColor = Color.FromRgb(71, 85, 105);

        private const double TextMaxWidth = 340;
        private const double IconSize = 28;

        private readonly StackPanel _stackPanel;

        public SabStyledToastHost()
        {
            Width = 464;
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

            ScrollViewer scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _stackPanel,
                Background = Brushes.Transparent
            };

            Content = scrollViewer;
        }

        public void ShowToast(string title, string message, ToastType type, string folderLink, int durationSeconds)
        {
            Border border = CreateToastBorder(type);
            border.Child = CreateToastGrid(border, title, message, type, folderLink);
            AddToast(border, durationSeconds);
        }

        public void ShowDetailedToast(string title, string message, ToastType type, FrameworkElement details, int durationSeconds)
        {
            Border border = CreateDetailedToast(title, message, type, details);
            while (_stackPanel.Children.Count >= 3) _stackPanel.Children.RemoveAt(_stackPanel.Children.Count - 1);
            AddToast(border, Math.Max(3, Math.Min(60, durationSeconds)));
        }
        public static Border CreateDetailedToast(string title, string message, ToastType type, FrameworkElement details)
        {
            var foreground = new SolidColorBrush(Color.FromRgb(230, 237, 243));
            var root = new StackPanel { Margin = new Thickness(12, 10, 12, 10) };
            var header = new DockPanel();
            var border = new Border { Width = 414, CornerRadius = new CornerRadius(7), Background = new SolidColorBrush(Color.FromRgb(37, 40, 47)),
                BorderBrush = new SolidColorBrush(GetAccentColor(type)), BorderThickness = new Thickness(3, 1, 1, 1), Margin = new Thickness(0, 0, 0, 8), Child = root };
            TextElement.SetForeground(border, foreground);
            TextElement.SetFontFamily(border, new FontFamily("Segoe UI"));
            var close = new Button { Content = "\u00D7", Width = 24, Height = 24, Padding = new Thickness(0), FontSize = 17 };
            DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            close.Click += (s,e) => { var parent = border.Parent as Panel; parent?.Children.Remove(border); };
            header.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            root.Children.Add(header);
            var full = new TextBox { Text = message ?? "", IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 160,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.Transparent, Foreground = foreground,
                BorderThickness = new Thickness(0), FontSize = 12, Padding = new Thickness(0,6,0,6) };
            var preview = new TextBlock { Text = (message ?? "").Replace("\r", " ").Replace("\n", " "), MaxWidth = 344,
                MaxHeight = 34, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 12 };
            root.Children.Add(new Expander { Header = preview, Content = full, IsExpanded = false, Foreground = foreground,
                Margin = new Thickness(0,4,0,4), ToolTip = "Нажмите, чтобы раскрыть полный текст" });
            root.Children.Add(details);
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.ForegroundProperty,foreground));
            style.Setters.Add(new Setter(Control.BackgroundProperty,new SolidColorBrush(Color.FromRgb(55,60,70))));
            style.Setters.Add(new Setter(Control.BorderBrushProperty,new SolidColorBrush(Color.FromRgb(77,85,98))));
            style.Setters.Add(new Setter(Control.FontSizeProperty,12.0));
            var frame = new FrameworkElementFactory(typeof(Border));
            frame.SetValue(Border.CornerRadiusProperty,new CornerRadius(4));
            frame.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));
            frame.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Control.PaddingProperty));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);
            content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
            frame.AppendChild(content);
            style.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(Button)) { VisualTree=frame }));
            var disabled = new Trigger { Property=UIElement.IsEnabledProperty, Value=false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty,0.45)); style.Triggers.Add(disabled);
            border.Resources.Add(typeof(Button),style);
            return border;
        }
        private Border CreateToastBorder(ToastType type)
        {
            return new Border
            {
                Background = new SolidColorBrush(PanelBackgroundColor),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Effect = null,
                Child = null,
                Tag = GetAccentColor(type)
            };
        }

        private Grid CreateToastGrid(Border border, string title, string message, ToastType type, string folderLink)
        {
            Grid grid = new Grid
            {
                MinHeight = 86,
                Background = new SolidColorBrush(WindowBackgroundColor)
            };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(IconSize + 20) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border accentBar = new Border
            {
                Background = new SolidColorBrush(GetAccentColor(type)),
                CornerRadius = new CornerRadius(8, 0, 0, 8)
            };
            Grid.SetColumn(accentBar, 0);

            Border iconBorder = new Border
            {
                Width = IconSize,
                Height = IconSize,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(GetSoftAccentColor(type)),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(14, 16, 10, 0)
            };

            TextBlock icon = new TextBlock
            {
                Text = GetIcon(type),
                FontFamily = new FontFamily("Segoe UI Emoji"),
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(GetAccentColor(type)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            iconBorder.Child = icon;
            Grid.SetColumn(iconBorder, 1);

            StackPanel textStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 14, 12, 14)
            };

            TextBlock titleBlock = new TextBlock
            {
                Text = title ?? string.Empty,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = new SolidColorBrush(TextColor),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = TextMaxWidth
            };
            textStack.Children.Add(titleBlock);

            TextBlock messageBlock = new TextBlock
            {
                Text = message ?? string.Empty,
                FontSize = 13,
                Foreground = new SolidColorBrush(MutedTextColor),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = TextMaxWidth,
                Margin = new Thickness(0, 4, 0, 0)
            };
            textStack.Children.Add(messageBlock);

            if (!string.IsNullOrWhiteSpace(folderLink))
            {
                TextBlock linkBlock = CreateFolderLink(folderLink);
                textStack.Children.Add(linkBlock);
            }

            Grid.SetColumn(textStack, 2);

            Button closeButton = CreateCloseButton(border);
            Grid.SetColumn(closeButton, 3);

            grid.Children.Add(accentBar);
            grid.Children.Add(iconBorder);
            grid.Children.Add(textStack);
            grid.Children.Add(closeButton);

            return grid;
        }

        private TextBlock CreateFolderLink(string folderLink)
        {
            TextBlock linkBlock = new TextBlock
            {
                Text = folderLink,
                FontSize = 13,
                Foreground = new SolidColorBrush(PrimaryColor),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = TextMaxWidth,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 6, 0, 0),
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
                }
            };

            return linkBlock;
        }

        private Button CreateCloseButton(Border border)
        {
            Button closeButton = new Button
            {
                Content = "\u00D7",
                Width = 28,
                Height = 28,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(MutedTextColor),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 12, 12, 0),
                VerticalAlignment = VerticalAlignment.Top,
                FontSize = 18,
                FontWeight = FontWeights.Normal,
                Template = CreateFlatButtonTemplate()
            };

            closeButton.Click += (sender, args) => _stackPanel.Children.Remove(border);
            return closeButton;
        }

        private void AddToast(Border border, int durationSeconds)
        {
            _stackPanel.Children.Insert(0, border);

            DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
            border.BeginAnimation(OpacityProperty, fadeIn);

            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(durationSeconds)
            };

            timer.Tick += (sender, args) =>
            {
                timer.Stop();
                border.BeginAnimation(OpacityProperty, null);
                _stackPanel.Children.Remove(border);
            };

            timer.Start();
        }

        private static ControlTemplate CreateFlatButtonTemplate()
        {
            FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            borderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);

            FrameworkElementFactory contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            borderFactory.AppendChild(contentFactory);

            ControlTemplate template = new ControlTemplate(typeof(Button));
            template.VisualTree = borderFactory;
            return template;
        }

        private static Color GetAccentColor(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success:
                    return SuccessColor;
                case ToastType.Warning:
                    return WarningColor;
                case ToastType.Error:
                    return ErrorColor;
                case ToastType.Info:
                default:
                    return PrimaryColor;
            }
        }

        private static Color GetSoftAccentColor(ToastType type)
        {
            Color accent = GetAccentColor(type);
            return Color.FromArgb(32, accent.R, accent.G, accent.B);
        }

        private static string GetIcon(ToastType type)
        {
            switch (type)
            {
                case ToastType.Info:
                    return "ℹ️";
                case ToastType.Success:
                    return "✅";
                case ToastType.Warning:
                    return "⚠️";
                case ToastType.Error:
                    return "❌";
                default:
                    return "❔";
            }
        }
    }
}
