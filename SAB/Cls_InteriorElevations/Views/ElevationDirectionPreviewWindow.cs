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
        private readonly Button _flip, _create;
        private readonly TextBlock _status;
        private readonly ScrollViewer _statusScroll;
        private bool _allowClose;
        public bool IsBusy { get; private set; }
        public event Action FlipRequested;
        public event Action CreateRequested;
        public event Action CancelRequested;

        public ElevationDirectionPreviewWindow()
        {
            Title = "SAB · Направления";
            Width = 240; MinWidth = 240; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Resources.MergedDictionaries.Add(FluentResources.CreateDictionary());
            SetResourceReference(BackgroundProperty, "SabBrush.WindowBackground");
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"); FontSize = 13;
            var body = new StackPanel();
            body.Children.Add(new TextBlock { Text = "Направления", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,20) });
            _status = new TextBlock { Name = "PreviewStatus", Text = "", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,12) };
            _status.SetResourceReference(TextBlock.ForegroundProperty, "SabBrush.TextSecondary");
            _statusScroll = new ScrollViewer { MaxHeight = 180, Visibility = Visibility.Collapsed, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _status };
            _statusScroll.SetResourceReference(StyleProperty, "SabFormScrollViewerStyle"); body.Children.Add(_statusScroll);
            _flip = Button("Сменить сторону", "FlipPreviewButton", true);
            _flip.Margin = new Thickness(0,0,0,12); _flip.Click += (s,e) => FlipRequested?.Invoke(); body.Children.Add(_flip);
            var help = Button("Инструкция", "PreviewHelpButton", false); help.Margin = new Thickness(0,0,0,12);
            SAB.UI.FluentLight.Instructions.SetFile(help, "SAB_HTML_Sozdanie_i_korrektirovka_razvertok.html");
            body.Children.Add(help);
            _create = Button("Создать развёртки", "CreatePreviewButton", true); _create.Click += (s,e) => CreateRequested?.Invoke(); body.Children.Add(_create);
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
            _statusScroll.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
            _flip.IsEnabled = _create.IsEnabled = !busy;
        }

        public void CloseAfterCleanup() { _allowClose = true; Close(); }
    }
}
