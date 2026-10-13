using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SAB.UI.FluentLight;

namespace SAB.UI.Notifications
{
    public sealed class NotificationCard : Border
    {
        public NotificationMessage Message { get; }
        public NotificationLifetime Lifetime { get; } = new NotificationLifetime();
        public event EventHandler CloseRequested;
        internal event EventHandler InteractionChanged;
        private readonly ScrollViewer body;
        private readonly Grid header;
        private readonly StackPanel footer;
        private double availableHeight = double.PositiveInfinity;
        private readonly TextBlock countdown;
        private readonly ProgressBar progress;
        private readonly Action<string> copy;
        private bool copying;
        private double feedbackUntil;
        private Button feedbackButton;
        private string feedbackLabel;

        public NotificationCard(NotificationMessage message, Action<string> clipboardWriter = null)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
            copy = clipboardWriter ?? Clipboard.SetText;
            Resources.MergedDictionaries.Add(FluentResources.CreateDictionary());
            Background = Brush("SabBrush.PanelBackground"); BorderBrush = Brush("SabBrush.Border");
            BorderThickness = new Thickness(1); CornerRadius = new CornerRadius(6); Padding = new Thickness(16);
            TextElement.SetFontFamily(this, new FontFamily("Segoe UI"));
            TextElement.SetFontSize(this, 13); TextElement.SetForeground(this, Brush("SabBrush.Text"));
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            header = new Grid(); header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var headings = new StackPanel();
            headings.Children.Add(new TextBlock { Text = message.Module, FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            var status = new TextBlock { Text = message.Outcome, FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap,
                Foreground = Brush(message.Tone == NotificationTone.Success ? "SabBrush.Success" : message.Tone == NotificationTone.Warning ? "SabBrush.Warning" : message.Tone == NotificationTone.Error ? "SabBrush.Error" : "SabBrush.TextSecondary") };
            headings.Children.Add(status); header.Children.Add(headings);
            var close = SmallButton(null); close.Width = 28; close.Padding = new Thickness(0); close.BorderThickness = new Thickness(0); close.VerticalAlignment = VerticalAlignment.Top;
            close.Content = new Path { Data = Geometry.Parse("M 1,1 L 9,9 M 9,1 L 1,9"), Width = 10, Height = 10, Stroke = Brush("SabBrush.TextSecondary"), StrokeThickness = 1.2, Stretch = Stretch.Uniform };
            close.ToolTip = "Закрыть уведомление"; AutomationProperties.SetName(close, "Закрыть уведомление");
            close.Click += (s, e) => CloseRequested?.Invoke(this, EventArgs.Empty);
            Grid.SetColumn(close, 1); header.Children.Add(close); root.Children.Add(header);
            var content = new StackPanel();
            if (!string.IsNullOrWhiteSpace(message.Summary)) content.Children.Add(new Border { BorderBrush = Brush("SabBrush.Border"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(10), Background = Brushes.Transparent, Child = Selectable(message.Summary, false) });
            if (!string.IsNullOrWhiteSpace(message.Warnings)) content.Children.Add(CopySection("Предупреждения", "Копировать", message.Warnings, false));
            if (!string.IsNullOrWhiteSpace(message.ElementIds)) content.Children.Add(CopySection("ID элементов", "Копировать ID", message.ElementIds));
            if (!string.IsNullOrWhiteSpace(message.Support)) content.Children.Add(CopySection("Ошибка для поддержки", "Копировать", message.Support));
            body = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 12, 0, 0), CanContentScroll = false };
            Grid.SetRow(body, 1); root.Children.Add(body);
            footer = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            countdown = new TextBlock { FontSize = 12, Foreground = Brush("SabBrush.TextSecondary") };
            progress = new ProgressBar { Minimum = 0, Maximum = NotificationLifetime.DurationSeconds, Value = NotificationLifetime.DurationSeconds, Height = 2, Margin = new Thickness(0, 8, 0, 0), Style = (Style)FindResource("SabNotificationProgressStyle") };
            footer.Children.Add(countdown); footer.Children.Add(progress); Grid.SetRow(footer, 2); root.Children.Add(footer); Child = root;
            MouseEnter += Interaction; MouseLeave += Interaction; IsKeyboardFocusWithinChanged += (s, e) => InteractionChanged?.Invoke(this, EventArgs.Empty);
            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { CloseRequested?.Invoke(this, EventArgs.Empty); e.Handled = true; } };
            RenderTime();
        }

        private Brush Brush(string key) => (Brush)FindResource(key);
        private Button SmallButton(string label) => new Button { Content = label, Height = 28, MinHeight = 28, FontSize = 12, Padding = new Thickness(8, 0, 8, 0), Style = (Style)FindResource(typeof(Button)) };
        private TextBox Selectable(string text, bool mono)
        {
            return new TextBox { Text = text, Style = (Style)FindResource(mono ? "SabNotificationCodeStyle" : "SabNotificationTextStyle"),
                IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
        }
        private Border CopySection(string title, string label, string value, bool mono = true)
        {
            var panel = new StackPanel(); var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = title, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            var button = SmallButton(label); button.Foreground = Brush("SabBrush.Accent");
            button.Click += (s, e) => Copy(button, label, value); Grid.SetColumn(button, 1); row.Children.Add(button); panel.Children.Add(row);
            var details = Selectable(value, mono);
            if (!mono) { details.Background = Brush("SabBrush.DisabledBackground"); details.BorderBrush = Brush("SabBrush.Border"); details.BorderThickness = new Thickness(1); details.Padding = new Thickness(10); }
            panel.Children.Add(details);
            return new Border { BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = Brush("SabBrush.BorderWeak"), Margin = new Thickness(0, 16, 0, 0), Padding = new Thickness(0, 12, 0, 0), Child = panel };
        }
        private void Copy(Button button, string label, string value)
        {
            copying = true; InteractionChanged?.Invoke(this, EventArgs.Empty);
            try { copy(value); button.Content = "Скопировано"; button.ToolTip = null; }
            catch (Exception ex) { button.Content = "Не скопировано"; button.ToolTip = ex.Message; }
            finally
            {
                if (feedbackButton != null && feedbackButton != button) feedbackButton.Content = feedbackLabel;
                feedbackButton = button; feedbackLabel = label; feedbackUntil = NotificationStack.Now + 2;
                copying = false; InteractionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        private void Interaction(object sender, MouseEventArgs e) => InteractionChanged?.Invoke(this, EventArgs.Empty);
        internal bool Reading => IsMouseOver || IsKeyboardFocusWithin || copying;
        internal void BoundBody(double height) { availableHeight = height; InvalidateMeasure(); }
        protected override Size MeasureOverride(Size constraint)
        {
            if (!double.IsInfinity(availableHeight))
            {
                var width = Math.Max(0, constraint.Width - 34);
                header.Measure(new Size(width, double.PositiveInfinity)); footer.Measure(new Size(width, double.PositiveInfinity));
                body.MaxHeight = Math.Max(0, availableHeight - 34 - 12 - header.DesiredSize.Height - footer.DesiredSize.Height);
            }
            return base.MeasureOverride(constraint);
        }
        internal void Tick(double now, bool paused)
        {
            Lifetime.Update(now, paused || Reading);
            if (feedbackButton != null && now >= feedbackUntil) { feedbackButton.Content = feedbackLabel; feedbackButton.ToolTip = null; feedbackButton = null; }
            RenderTime();
        }
        private void RenderTime()
        {
            countdown.Text = "Скрытие через " + Math.Ceiling(Lifetime.RemainingSeconds) + " с";
            progress.Value = Lifetime.RemainingSeconds;
        }
    }
}
