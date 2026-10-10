using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using SAB.UI;

internal static class Program
{
    private static readonly List<string> Results = new List<string>();
    private static string Output;
    [STAThread]
    private static int Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        Output = Path.Combine(root, "outputs", "ui-theme"); Directory.CreateDirectory(Output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        SabWindowBehaviorService.Initialize();
        try
        {
            CheckFields(root);
            CheckFields(root, "SABRedesignStyles.xaml");
            foreach (string file in Directory.GetFiles(Path.Combine(root, "SAB"), "*.xaml", SearchOption.AllDirectories)
                .Where(p => !p.Contains("\\bin\\") && !p.Contains("\\obj\\") && !p.Contains("\\Archive\\")))
            {
                XDocument xml = XDocument.Load(file);
                if (xml.Root.Name.LocalName != "Window") continue;
                Window window = null;
                try
                {
                    foreach (var node in xml.Descendants())
                        foreach (var attr in node.Attributes().ToList())
                            if (attr.Name.LocalName == "Class" || Events.Contains(attr.Name.LocalName)) attr.Remove();
                    using (var reader = System.Xml.XmlReader.Create(new StringReader(xml.ToString()),
                        new System.Xml.XmlReaderSettings(), new Uri(file).AbsoluteUri))
                        window = (Window)XamlReader.Load(reader);
                    window.ShowInTaskbar = false;
                    window.Show(); WaitLoaded(window); Drain(); window.UpdateLayout();
                    string name = Path.GetFileNameWithoutExtension(file);
                    Capture(window, name + "-default");
                    window.Width = Math.Max(window.MinWidth, Math.Min(window.ActualWidth, 740));
                    window.Height = Math.Max(window.MinHeight, Math.Min(window.ActualHeight, 540));
                    Drain(); window.UpdateLayout();
                    var tabs = Children<TabControl>(window).ToList();
                    foreach (var tab in tabs)
                        for (int i = 0; i < tab.Items.Count; i++)
                        {
                            tab.SelectedIndex = i; Drain(); window.UpdateLayout(); CheckGutters(window);
                            Capture(window, name + "-min-tab" + i);
                        }
                    if (tabs.Count == 0) { CheckGutters(window); Capture(window, name + "-min"); }
                    foreach (var field in Children<TextBox>(window).Where(t => t.Name != "PART_EditableTextBox"))
                        Require(field.FocusVisualStyle == null, name + ": extra text focus adorner");
                    Results.Add("PASS " + name);
                }
                catch (Exception e) { Results.Add("FAIL " + Path.GetFileName(file) + ": " + e); }
                finally { if (window != null) window.Close(); }
            }
        }
        catch (Exception e) { Results.Add("FAIL behavior: " + e); }
        File.WriteAllLines(Path.Combine(Output, "results.txt"), Results);
        foreach (string result in Results) Console.WriteLine(result);
        app.Shutdown();
        return Results.Any(s => s.StartsWith("FAIL")) ? 1 : 0;
    }

    private static readonly HashSet<string> Events = new HashSet<string>
    { "Loaded", "Click", "Checked", "Unchecked", "SelectionChanged", "TextChanged", "Closing", "Closed",
      "PreviewMouseLeftButtonDown", "PreviewMouseMove", "PreviewMouseLeftButtonUp", "MouseLeftButtonDown",
      "MouseDoubleClick", "PreviewKeyDown", "KeyDown", "SizeChanged", "Expanded", "Collapsed" };

    private static void CheckFields(string root, string dictionary = "SABWindowStyles.xaml")
    {
        var window = new Window { Title = "SAB — проверка полей", Width = 640, Height = 520 };
        window.Resources.MergedDictionaries.Add(new ResourceDictionary
        { Source = new Uri(Path.Combine(root, "SAB", "UI", "Styles", dictionary)) });
        var fields = new StackPanel { Margin = new Thickness(20) };
        fields.Children.Add(new TextBlock { Text = "Одна рамка поля — обычное / активное / ошибка", Margin = new Thickness(0,0,0,12) });
        var text = new TextBox { Text = "Развёртка помещения", Margin = new Thickness(0,0,0,12) };
        fields.Children.Add(text);
        var combo = new ComboBox { IsEditable = true, ItemsSource = new[] { "Лист А1", "Лист А2" }, SelectedIndex = 0,
            Margin = new Thickness(0,0,0,12) };
        fields.Children.Add(combo);
        var table = new DataGrid { AutoGenerateColumns = false, Height = 120, ItemsSource =
            Enumerable.Range(0,2000).Select(i => new Row { Name = "Помещение " + i }).ToList() };
        table.Columns.Add(new DataGridTextColumn { Header = "Имя вида", Binding = new Binding("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            EditingElementStyle = (Style)window.FindResource("SabDataGridTextBoxEditingStyle") });
        fields.Children.Add(table);
        for (int i = 0; i < 10; i++) fields.Children.Add(new TextBox { Text = "Настройка " + i, Margin = new Thickness(0,8,0,0) });
        var scroll = new ScrollViewer { Content = fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        window.Content = scroll;
        window.Show(); WaitLoaded(window); Drain(); window.UpdateLayout();
        Require(scroll.Style == window.TryFindResource("SabFormScrollViewerStyle"),
            "Form scroller not themed: local=" + scroll.ReadLocalValue(FrameworkElement.StyleProperty) +
            ", templated=" + scroll.TemplatedParent + ", window=" + Window.GetWindow(scroll) +
            ", loaded=" + scroll.IsLoaded + "/" + window.IsLoaded);
        text.Focus(); Drain();
        var border = (Border)text.Template.FindName("Root", text);
        Require(text.IsKeyboardFocusWithin, "Text field was not focused");
        Require(border.CornerRadius.TopLeft == 5 && border.BorderThickness == new Thickness(1), "Text field geometry");
        Require(((SolidColorBrush)border.BorderBrush).Color == Color.FromRgb(15,108,189), "Active text field accent");
        Require(text.FocusVisualStyle == null && Validation.GetErrorTemplate(text) == null, "Extra focus/error frame");
        Capture(window, "fields-focused");
        text.SetBinding(TextBox.TextProperty, new Binding("Name") { Source = new Row { Name = "Ошибочное значение" } });
        var expression = text.GetBindingExpression(TextBox.TextProperty);
        Validation.MarkInvalid(expression, new ValidationError(new ExceptionValidationRule(), expression, "Пример ошибки", null));
        Drain();
        Require(((SolidColorBrush)border.BorderBrush).Color == Color.FromRgb(217,45,32), "Validation uses the same border");
        Capture(window, "fields-error"); Validation.ClearInvalid(expression);
        combo.Focus(); combo.ApplyTemplate();
        var editable = (TextBox)combo.Template.FindName("PART_EditableTextBox", combo); editable.Focus(); Drain();
        Require(editable.BorderThickness == new Thickness(0) && editable.FocusVisualStyle == null, "Editable combo has two frames");
        var symbol = Children<FrameworkElement>(combo).First(c => c.GetType().FullName == "Wpf.Ui.Controls.SymbolIcon");
        var family = (FontFamily)symbol.GetType().GetProperty("FontFamily").GetValue(symbol,null);
        int codepoint = Convert.ToInt32(symbol.GetType().GetProperty("Symbol").GetValue(symbol,null));
        GlyphTypeface glyph;
        Require(family.GetTypefaces().Any(t => t.TryGetGlyphTypeface(out glyph) && glyph.CharacterToGlyphMap.ContainsKey(codepoint)),
            "Fluent symbol font is missing its glyph");
        table.SelectedIndex = 0; table.CurrentCell = new DataGridCellInfo(table.Items[0], table.Columns[0]);
        table.Focus(); table.BeginEdit(); Drain();
        var editor = Children<TextBox>(table).First(); editor.Focus(); Drain();
        var cell = Children<DataGridCell>(table).First();
        var cellFocus = (Border)cell.Template.FindName("CellFocus", cell);
        Require(cellFocus.Visibility == Visibility.Collapsed && editor.IsKeyboardFocusWithin, "Table cell duplicates editor focus");
        Capture(window, "fields-table-edit");
        Require(Children<DataGridRow>(table).Count() < 20, "Table lost row virtualization");
        CheckGutters(window);
        var panel = new Border { Child = new TextBlock { Text = "Размещение" } };
        fields.Children.Add(panel);
        for (int i = 0; i < 20; i++) SabWindowAnimationService.AnimatePlacementModePanel(panel, i % 2 == 0, true);
        Drain();
        Require(!System.Windows.DependencyPropertyHelper.GetValueSource(panel, FrameworkElement.MaxHeightProperty).IsAnimated,
            "Layout height still animated");
        SabWindowAnimationService.Enabled = false;
        SabWindowAnimationService.AnimatePlacementModePanel(panel, true, true);
        SabWindowAnimationService.AnimatePlacementModePanel(panel, false, true);
        Require(panel.MaxHeight == 520 && panel.Opacity == 1 && panel.IsHitTestVisible, "Reduced motion placement state");
        SabWindowAnimationService.Enabled = true;
        window.Close(); Results.Add("PASS focus, validation, editable combo, table editing, 2000-row virtualization, gutters, repeated motion, reduced motion");
    }

    private static void CheckGutters(Window window)
    {
        foreach (var scroll in Children<ScrollViewer>(window))
        {
            if (!scroll.IsVisible || scroll.Style != window.TryFindResource("SabFormScrollViewerStyle")) continue;
            var bar = (ScrollBar)scroll.Template.FindName("PART_VerticalScrollBar", scroll);
            var content = (ScrollContentPresenter)scroll.Template.FindName("PART_ScrollContentPresenter", scroll);
            Require(bar != null && content != null, "Form scroll template parts missing");
            if (bar.Visibility == Visibility.Visible && content.ActualWidth > 0)
            {
                double edge = content.TranslatePoint(new Point(content.ActualWidth,0), scroll).X;
                double start = bar.TranslatePoint(new Point(0,0), scroll).X;
                Require(start - edge >= 11.5, window.Title + ": scrolling overlaps fields");
            }
        }
    }
    private static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i=0; i<VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root,i);
            if (child is T) yield return (T)child;
            foreach (var item in Children<T>(child)) yield return item;
        }
    }
    private static void Drain() { Dispatcher.CurrentDispatcher.Invoke(() => {}, DispatcherPriority.ApplicationIdle); }
    private static void WaitLoaded(Window window)
    {
        var frame = new DispatcherFrame();
        var deadline = DateTime.UtcNow.AddSeconds(3);
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (s,e) => { if ((window.IsLoaded && Children<FrameworkElement>(window).All(c => c.IsLoaded)) || DateTime.UtcNow >= deadline) frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
        Require(window.IsLoaded, "Window did not load");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Capture(Window window, string name)
    {
        var element = window.Content as FrameworkElement;
        if (element == null || element.ActualWidth < 1 || element.ActualHeight < 1) return;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96,96,PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var context = background.RenderOpen())
            context.DrawRectangle((Brush)window.FindResource("SabBrush.WindowBackground"),null,
                new Rect(0,0,element.ActualWidth,element.ActualHeight));
        bitmap.Render(background);
        bitmap.Render(element); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(Output,name+".png"))) encoder.Save(stream);
    }
    public sealed class Row { public string Name { get; set; } }
}
