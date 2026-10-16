using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SAB.UI.FluentLight;

namespace SAB.InteriorElevations.Views
{
    // UI stores strings/actions only. Revit operations are dispatched by the controller.
    public sealed class ElevationDirectionPreviewWindow : Window
    {
        private readonly Button _flip, _create, _cancel;
        private readonly TextBlock _status;
        private bool _allowClose;
        public bool IsBusy { get; private set; }
        public event Action FlipRequested;
        public event Action CreateRequested;
        public event Action CancelRequested;

        public ElevationDirectionPreviewWindow(string summary)
        {
            Title = "SAB · Предпросмотр направлений";
            Width = 610; MinWidth = 590; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Resources.MergedDictionaries.Add(FluentResources.CreateDictionary());
            SetResourceReference(BackgroundProperty, "SabBrush.WindowBackground");
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"); FontSize = 13;
            var body = new StackPanel();
            body.Children.Add(new TextBlock { Text = "Предпросмотр направлений", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,16) });
            var summaryText = new TextBlock { Name = "PreviewSummary", Text = summary, TextWrapping = TextWrapping.Wrap };
            var summaryFrame = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(12), Margin = new Thickness(0,0,0,12), Child = summaryText };
            summaryFrame.SetResourceReference(Border.BorderBrushProperty, "SabBrush.Border");
            summaryFrame.SetResourceReference(Border.BackgroundProperty, "SabBrush.PanelBackground"); body.Children.Add(summaryFrame);
            _status = new TextBlock { Name = "PreviewStatus", Text = "", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,12) };
            _status.SetResourceReference(TextBlock.ForegroundProperty, "SabBrush.TextSecondary");
            var statusScroll = new ScrollViewer { MaxHeight = 180, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _status };
            statusScroll.SetResourceReference(StyleProperty, "SabFormScrollViewerStyle"); body.Children.Add(statusScroll);
            _flip = Button("Сменить сторону", "FlipPreviewButton", false); _flip.HorizontalAlignment = HorizontalAlignment.Left;
            _flip.Margin = new Thickness(0,0,0,20); _flip.Click += (s,e) => FlipRequested?.Invoke(); body.Children.Add(_flip);
            var footer = new Grid(); footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var help = Button("Инструкция", "PreviewHelpButton", false); help.HorizontalAlignment = HorizontalAlignment.Left;
            SAB.UI.FluentLight.Instructions.SetFile(help, "SAB_HTML_Sozdanie_i_korrektirovka_razvertok.html");
            footer.Children.Add(help);
            var actions = new StackPanel { Orientation = Orientation.Horizontal }; Grid.SetColumn(actions,1);
            _cancel = Button("Отмена", "CancelPreviewButton", false); _cancel.Margin = new Thickness(0,0,8,0);
            _cancel.Click += (s,e) => CancelRequested?.Invoke(); actions.Children.Add(_cancel);
            _create = Button("Создать развёртки", "CreatePreviewButton", true); _create.Click += (s,e) => CreateRequested?.Invoke(); actions.Children.Add(_create);
            footer.Children.Add(actions); body.Children.Add(footer);
            var frame = new Border { Padding = new Thickness(24), Child = body };
            frame.SetResourceReference(Border.BackgroundProperty, "SabBrush.WindowBackground"); Content = frame;
            Closing += OnClosing;
            PreviewKeyDown += (s,e) => { if (e.Key == Key.Escape && !_allowClose) { e.Handled = true; if (!IsBusy) CancelRequested?.Invoke(); } };
        }

        private Button Button(string label, string name, bool primary)
        {
            var button = new Button { Content = label, Name = name, MinHeight = 34 };
            button.SetResourceReference(StyleProperty, primary ? "SabPrimaryButtonStyle" : "SabOutlineButtonStyle");
            return button;
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (_allowClose) return;
            e.Cancel = true;
            if (!IsBusy) CancelRequested?.Invoke();
        }

        public void SetStatus(string text, bool busy = false)
        {
            IsBusy = busy; _status.Text = text ?? string.Empty;
            _flip.IsEnabled = _create.IsEnabled = _cancel.IsEnabled = !busy;
        }

        public void CloseAfterCleanup() { _allowClose = true; Close(); }
    }
}
