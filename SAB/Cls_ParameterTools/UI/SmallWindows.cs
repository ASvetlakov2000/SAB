using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Shell;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.IO;
using System.Text;
using Autodesk.Revit.UI;
using ToggleButton = System.Windows.Controls.Primitives.ToggleButton;
using TextBox = System.Windows.Controls.TextBox;

namespace SAB.ParameterTools.UI
{
    internal static class Theme
    {
        internal static readonly Brush Background = new SolidColorBrush(Color.FromRgb(247, 248, 250));
        internal static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(15, 108, 189));
        internal static void Window(Window w)
        {
            var resources = new ResourceDictionary
            { Source = new Uri("pack://application:,,,/SAB;component/UI/Styles/SABWindowStyles.xaml", UriKind.Absolute) };
            w.Resources.MergedDictionaries.Add(resources);
            AddStyle(w, resources, typeof(Button), "SabNeutralButtonStyle");
            AddStyle(w, resources, typeof(System.Windows.Controls.TextBox), "SabTextBoxStyle");
            AddStyle(w, resources, typeof(System.Windows.Controls.ComboBox), "SabComboBoxStyle");
            var centeredCombo = new Style(typeof(System.Windows.Controls.ComboBox), (Style)resources["SabComboBoxStyle"]);
            centeredCombo.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            centeredCombo.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Normal));
            w.Resources[typeof(System.Windows.Controls.ComboBox)] = centeredCombo;
            AddStyle(w, resources, typeof(CheckBox), "SabCheckBoxStyle");
            AddStyle(w, resources, typeof(DataGrid), "SabDataGridStyle");
            w.FontFamily = new FontFamily("Segoe UI"); w.FontSize = 13;
            w.Foreground = (Brush)resources["SabBrush.Text"];
            w.WindowStyle = WindowStyle.None; w.AllowsTransparency = true; w.Background = Brushes.Transparent;
            w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowChrome.SetWindowChrome(w, new WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(7),
                CornerRadius = new CornerRadius(8), GlassFrameThickness = new Thickness(0), UseAeroCaptionButtons = false });
            w.PreviewKeyDown += (s, e) => {
                if (e.Key != Key.Escape) return;
                var origin = e.OriginalSource as DependencyObject;
                while (origin is Visual) {
                    if (origin is System.Windows.Controls.ComboBox combo && combo.IsDropDownOpen) return;
                    origin = VisualTreeHelper.GetParent(origin);
                }
                w.Close(); e.Handled = true;
            };
        }
        private static void AddStyle(Window window, ResourceDictionary resources, Type type, string key)
        { window.Resources[type] = new Style(type, (Style)resources[key]); }
        internal static Button Button(string label, RoutedEventHandler click)
        {
            var button = new Button { Content = label, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(4) };
            button.Click += click; return button;
        }
        internal static TextBlock Text(string text, bool bold = false)
        {
            var block = new TextBlock { Text = text, Margin = new Thickness(4, 6, 4, 6), TextWrapping = TextWrapping.Wrap, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal };
            block.SetResourceReference(TextBlock.ForegroundProperty, "SabBrush.Text"); return block;
        }
        internal static void Frame(Window window, string title, string subtitle, UIElement body, UIElement footer = null)
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var bar = new Grid { Background = (Brush)window.FindResource("SabBrush.PanelBackground") };
            bar.ColumnDefinitions.Add(new ColumnDefinition()); bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            brand.Children.Add(new Border { Width = 21, Height = 21, CornerRadius = new CornerRadius(4), Background = Accent,
                Child = new TextBlock { Text = "S", FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
            brand.Children.Add(new TextBlock { Text = "SAB  ПАРАМЕТРЫ", Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontSize = 12, FontWeight = FontWeights.SemiBold });
            bar.Children.Add(brand);
            var chrome = new StackPanel { Orientation = Orientation.Horizontal };
            var min = new Button { Content = "─", ToolTip = "Свернуть", Style = (Style)window.FindResource("SabWindowChromeButtonStyle") };
            min.Click += (s, e) => window.WindowState = WindowState.Minimized;
            var close = new Button { Content = "✕", ToolTip = "Закрыть", Style = (Style)window.FindResource("SabWindowCloseButtonStyle") };
            close.Click += (s, e) => window.Close(); chrome.Children.Add(min); chrome.Children.Add(close);
            Grid.SetColumn(chrome, 1); bar.Children.Add(chrome); root.Children.Add(bar);
            bar.MouseLeftButtonDown += (s, e) => { if (e.OriginalSource is TextBlock || ReferenceEquals(e.OriginalSource, bar)) window.DragMove(); };
            var header = new StackPanel { Margin = new Thickness(18, 14, 18, 10) };
            header.Children.Add(new TextBlock { Text = title, Style = (Style)window.FindResource("SabWindowTitleTextStyle") });
            if (!string.IsNullOrEmpty(subtitle)) header.Children.Add(new TextBlock { Text = subtitle, Margin = new Thickness(0, 4, 0, 0), Style = (Style)window.FindResource("SabWindowSubtitleTextStyle") });
            Grid.SetRow(header, 1); root.Children.Add(header);
            var content = new Border { Padding = new Thickness(18, 0, 18, 12), Child = body };
            Grid.SetRow(content, 2); root.Children.Add(content);
            if (footer != null)
            {
                var bottom = new Border { BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = (Brush)window.FindResource("SabBrush.BorderWeak"),
                    Background = (Brush)window.FindResource("SabBrush.PanelBackground"), Padding = new Thickness(14, 8, 14, 8), Child = footer };
                Grid.SetRow(bottom, 3); root.Children.Add(bottom);
            }
            window.Content = new Border { Style = (Style)window.FindResource("SabWindowFrameStyle"), Child = root };
        }
        internal static GroupBox Card(Window window, string title, UIElement body)
        { return new GroupBox { Header = title, Style = (Style)window.FindResource("SabLayeredSettingsGroupStyle"), Content = body }; }
        internal static void Primary(Window window, Button button)
        { button.Style = (Style)window.FindResource("SabPrimaryButtonStyle"); }
        internal static void Tabs(TabControl tabs)
        {
            // Match the underline tabs used by the elevation settings window.
            tabs.Background = Brushes.Transparent; tabs.BorderThickness = new Thickness(0);
            tabs.ItemContainerStyle = (Style)XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TabItem'>
                <Setter Property='Foreground' Value='{DynamicResource SabBrush.TextSecondary}'/><Setter Property='Padding' Value='12,9'/><Setter Property='Margin' Value='0,0,4,0'/>
                <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TabItem'><Grid Background='Transparent' Cursor='Hand'>
                <Border x:Name='Hover' Margin='2,3' Background='{DynamicResource SabBrush.AccentLight}' CornerRadius='4' Opacity='0'/>
                <ContentPresenter ContentSource='Header' Margin='{TemplateBinding Padding}' HorizontalAlignment='Center'/>
                <Border x:Name='Line' Height='3' Width='0' CornerRadius='2,2,0,0' Background='{DynamicResource SabBrush.Accent}' VerticalAlignment='Bottom'/>
                </Grid><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Hover' Property='Opacity' Value='0.72'/></Trigger>
                <Trigger Property='IsSelected' Value='True'><Setter Property='Foreground' Value='{DynamicResource SabBrush.Accent}'/><Setter Property='FontWeight' Value='SemiBold'/><Setter TargetName='Line' Property='Width' Value='54'/></Trigger>
                </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
        }
        internal static ToggleButton Toggle()
        {
            var toggle = new ToggleButton { Width = 38, Height = 22, Margin = new Thickness(0, 0, 10, 0), Cursor = Cursors.Hand };
            toggle.Template = (ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ToggleButton'>
                <Border x:Name='Track' Background='#CDD4DE' CornerRadius='11'><Ellipse x:Name='Knob' Width='16' Height='16' Fill='White' Margin='3' HorizontalAlignment='Left'/></Border>
                <ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='Track' Property='Background' Value='{DynamicResource SabBrush.Accent}'/><Setter TargetName='Knob' Property='HorizontalAlignment' Value='Right'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            return toggle;
        }
    }
    internal static class Toast
    {
        internal static void Show(IntPtr owner, string text, int seconds = 3)
        {
            var resources = new ResourceDictionary { Source = new Uri("pack://application:,,,/SAB;component/UI/Styles/SABWindowStyles.xaml", UriKind.Absolute) };
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = "SAB  ПАРАМЕТРЫ", Foreground = (Brush)resources["SabBrush.Accent"], FontWeight = FontWeights.SemiBold,
                FontSize = 12, Margin = new Thickness(0, 0, 0, 8) });
            content.Children.Add(new TextBlock { Text = text, Foreground = (Brush)resources["SabBrush.Text"], FontSize = 13, TextWrapping = TextWrapping.Wrap });
            var w = new Window { Width = 540, MaxHeight = 420, SizeToContent = SizeToContent.Height, WindowStyle = WindowStyle.None,
                AllowsTransparency = true, Background = Brushes.Transparent, FontFamily = new FontFamily("Segoe UI"),
                ShowInTaskbar = false, ShowActivated = false, Topmost = true, ResizeMode = ResizeMode.NoResize,
                Content = new Border { Background = (Brush)resources["SabBrush.PanelBackground"], BorderBrush = (Brush)resources["SabBrush.Border"],
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(16),
                    Child = new ScrollViewer { MaxHeight = 380, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Content = content } } };
            new WindowInteropHelper(w).Owner = owner;
            w.Left = SystemParameters.WorkArea.Right - w.Width - 24;
            w.Show();
            w.Top = Math.Max(SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Bottom - w.ActualHeight - 24);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
            timer.Tick += (s, e) => { timer.Stop(); w.Close(); }; timer.Start();
        }
    }
    public sealed class ModelSourceChoice
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
    internal sealed class ModelSourceWindow : Window
    {
        internal string SelectedId { get; private set; }
        internal ModelSourceWindow(string title, string hint, IList<ModelSourceChoice> choices, string selectedId = null)
        {
            Title = title; Width = 530; Height = 420; MinWidth = 430; MinHeight = 320; Theme.Window(this);
            var body = new DockPanel(); var search = new TextBox { Margin = new Thickness(4), ToolTip = "Поиск по названию или номеру" };
            DockPanel.SetDock(search, Dock.Top); body.Children.Add(search);
            var list = new ListBox { ItemsSource = choices, DisplayMemberPath = "Name", Margin = new Thickness(4), BorderThickness = new Thickness(1) }; body.Children.Add(list);
            list.SelectedItem = choices.FirstOrDefault(c => c.Id == selectedId);
            var status = Theme.Text("Выберите строку и нажмите «Применить». Можно искать по номеру или названию.");
            search.TextChanged += (s, e) => list.ItemsSource = choices.Where(c => c.Name.IndexOf(search.Text.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            var footer = new DockPanel(); var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            buttons.Children.Add(Theme.Button("Отмена", (s, e) => DialogResult = false));
            var apply = Theme.Button("Применить", (s, e) => { if (!(list.SelectedItem is ModelSourceChoice selected)) { status.Text = "Сначала выберите строку в списке."; return; } SelectedId = selected.Id; DialogResult = true; });
            Theme.Primary(this, apply); buttons.Children.Add(apply); DockPanel.SetDock(buttons, Dock.Right); footer.Children.Add(buttons); footer.Children.Add(status);
            Theme.Frame(this, title, hint, body, footer);
        }
    }
    internal sealed class CorpusChoiceWindow : Window
    {
        internal string Corpus { get; private set; }
        internal CorpusChoiceWindow(IList<string> corpora, string current = null)
        {
            Title = "Рабочий корпус"; Width = 400; Height = Math.Min(650, 185 + corpora.Count * 48); MinHeight = 230; ResizeMode = ResizeMode.NoResize;
            Theme.Window(this); var root = new StackPanel();
            foreach (var value in corpora)
            {
                string corpus = value;
                var button = Theme.Button(corpus, (s, e) => { Corpus = corpus; DialogResult = true; });
                if (corpus == current) { Theme.Primary(this, button); button.ToolTip = "Последний выбранный корпус этой модели"; }
                root.Children.Add(button);
            }
            Theme.Frame(this, "Выбрать корпус", "Назначить корпус выделенным элементам", new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
                Theme.Button("Отмена", (s, e) => { DialogResult = false; }));
        }
    }
    internal sealed class ReportWindow : Window
    {
        private ReportWindow(UIDocument ui, IList<Issue> issues, string title)
        {
            Title = title; Width = 1120; Height = 680; MinWidth = 980; MinHeight = 540; Theme.Window(this);
            var root = new DockPanel { Margin = new Thickness(16) };
            string reportText = Format(issues, title, ui);
            string savedPath = null;
            if (ui != null)
            {
                try
                {
                    string folder = RuntimeHost.LogFolder; Directory.CreateDirectory(folder);
                    savedPath = Path.Combine(folder, "report-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".txt");
                    File.WriteAllText(savedPath, reportText, Encoding.UTF8);
                }
                catch (Exception ex) { ParameterToolsModule.Host?.Log(ex); }
            }
            var header = Theme.Text("Строк отчёта: " + issues.Count + ". Сводки по группам приведены по параметрам. Выберите строку для подробностей.", true);
            DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
            var grid = new DataGrid { ItemsSource = issues, AutoGenerateColumns = false, IsReadOnly = true,
                CanUserAddRows = false, RowHeight = double.NaN, MinRowHeight = 72, SelectionMode = DataGridSelectionMode.Extended, HeadersVisibility = DataGridHeadersVisibility.Column };
            Add(grid, "Элемент", "Element", 130); Add(grid, "Параметр", "Parameter", 290);
            Add(grid, "Сейчас", "Actual", 110); Add(grid, "Ожидается", "Expected", 110); Add(grid, "Причина", "Reason", 360);
            var details = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 140, MaxHeight = 180, Margin = new Thickness(4),
                Text = "Выберите строку, чтобы прочитать полную причину и технические подробности." };
            var bottom = new StackPanel(); bottom.Children.Add(details);
            var path = Theme.Text(savedPath == null ? "Отчёт можно скопировать кнопкой «Копировать отчёт»." : "Файл отчёта: " + savedPath); path.FontSize = 11; bottom.Children.Add(path);
            DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
            grid.SelectionChanged += (s, e) => { var row = grid.SelectedItem as Issue; if (row != null) details.Text = row.Reason + "\n\n" + Format(new[] { row }, "Подробности", null); };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(Theme.Button("Копировать отчёт", (s, e) => {
                try { Clipboard.SetText(reportText); path.Text = "Отчёт скопирован." + (savedPath == null ? "" : " Файл: " + savedPath); }
                catch (Exception ex) { path.Text = "Не удалось скопировать отчёт: " + ex.Message; }
            }));
            buttons.Children.Add(Theme.Button("Выделить", (s, e) => {
                var ids = grid.SelectedItems.Cast<Issue>().Select(i => i.ElementId).Where(id => id != null && id != Autodesk.Revit.DB.ElementId.InvalidElementId).Distinct().ToList();
                if (ui != null && ids.Count > 0) ui.Selection.SetElementIds(ids);
            }));
            buttons.Children.Add(Theme.Button("Показать", (s, e) => {
                var ids = grid.SelectedItems.Cast<Issue>().Select(i => i.ElementId).Where(id => id != null && id != Autodesk.Revit.DB.ElementId.InvalidElementId).Distinct().ToList();
                if (ui != null && ids.Count > 0) { ui.Selection.SetElementIds(ids); ui.ShowElements(ids); }
            }));
            buttons.Children.Add(Theme.Button("Снять подсветку", (s, e) => {
                try { ParameterToolsModule.Host.Highlight.Clear(ui); }
                catch (Exception ex) { ParameterToolsModule.Host.Log(ex); MessageBox.Show(this, ex.Message, "Снятие подсветки"); }
            }));
            buttons.Children.Add(Theme.Button("Закрыть", (s, e) => Close()));
            root.Children.Add(grid); Theme.Frame(this, title, "Причины отказов, пропуски в группах и диагностика Revit", root, buttons);
            if (issues.Count > 0) grid.SelectedIndex = 0;
        }
        internal static string Format(IEnumerable<Issue> issues, string title, UIDocument ui)
        {
            var text = new StringBuilder(title);
            if (ui != null) text.Append("\nДата: ").Append(DateTime.Now.ToString("O")).Append("\nМодель: ").Append(ui.Document.Title)
                .Append("\nRevit: ").Append(ui.Application.Application.VersionNumber).Append(" · ").Append(ui.Application.Application.VersionBuild);
            foreach (var issue in issues)
            {
                text.Append("\n\nЭлемент / область: ").Append(issue.Element).Append("\nПараметр: ").Append(issue.Parameter)
                    .Append("\nСейчас: ").Append(issue.Actual).Append("\nОжидается: ").Append(issue.Expected).Append("\nПричина: ").Append(issue.Reason);
                if (!string.IsNullOrWhiteSpace(issue.Technical)) text.Append("\nТехнические подробности:\n").Append(issue.Technical);
            }
            return text.ToString();
        }
        private static void Add(DataGrid grid, string title, string path, int width)
        {
            var style = new Style(typeof(TextBlock)); style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(3, 6, 3, 6)));
            grid.Columns.Add(new DataGridTextColumn { Header = title, Binding = new Binding(path), Width = width, ElementStyle = style });
        }
        internal static void Show(UIDocument ui, IList<Issue> issues, string title)
        {
            var w = new ReportWindow(ui, issues, title); new WindowInteropHelper(w).Owner = ui.Application.MainWindowHandle; w.ShowDialog();
        }
    }
}
