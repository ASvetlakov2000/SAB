using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SAB.UI.FluentLight
{
    public static class CollectionChrome
    {
        public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(CollectionChrome),new PropertyMetadata(false,Changed));
        public static bool GetEnabled(DependencyObject d)=>(bool)d.GetValue(EnabledProperty);
        public static void SetEnabled(DependencyObject d,bool value)=>d.SetValue(EnabledProperty,value);
        static void Changed(DependencyObject d,DependencyPropertyChangedEventArgs e)
        {
            if(!(d is Border border)||!(bool)e.NewValue)return;
            // Decorator.Child is not a DP: a Style binding to Child before XAML attaches it stays null.
            // Keep zero-height children measurable so a later item can restore the outline naturally.
            Action update=()=> {
                if(!(border.Child is ItemsControl items))return;
                bool visible=items.HasItems&&items.ActualHeight>0;
                var thickness=new Thickness(visible?1:0);var padding=new Thickness(visible?8:0);
                if(border.BorderThickness!=thickness)border.SetCurrentValue(Border.BorderThicknessProperty,thickness);
                if(border.Padding!=padding)border.SetCurrentValue(Border.PaddingProperty,padding);
            };
            border.Loaded+=(s,a)=>update();border.LayoutUpdated+=(s,a)=>update();
        }
    }
    public static class TableScroll
    {
        public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(TableScroll),new PropertyMetadata(false,Changed));
        public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);
        public static void SetEnabled(DependencyObject d,bool value) => d.SetValue(EnabledProperty,value);
        static readonly DependencyProperty HandlerProperty=DependencyProperty.RegisterAttached("Handler",typeof(EventHandler),typeof(TableScroll));
        static void Changed(DependencyObject d,DependencyPropertyChangedEventArgs e)
        {
            if(!(d is DataGrid grid)) return;
            var previous=grid.GetValue(HandlerProperty) as EventHandler;
            if(previous!=null)grid.LayoutUpdated-=previous;
            if((bool)e.NewValue) { EventHandler handler=(s,args)=>Update(grid,args);grid.SetValue(HandlerProperty,handler);grid.LayoutUpdated+=handler; }
        }
        static IEnumerable<DependencyObject> Visuals(DependencyObject root)
        {
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
            { var child=VisualTreeHelper.GetChild(root,i); yield return child; foreach(var next in Visuals(child))yield return next; }
        }
        static void Update(object sender,EventArgs e)
        {
            if(!(sender is DataGrid grid)||!grid.IsVisible) return;
            var viewer=Visuals(grid).OfType<ScrollViewer>().FirstOrDefault(s=>s.Name=="DG_ScrollViewer");
            if(viewer==null) return;
            var bar=viewer.Template.FindName("PART_VerticalScrollBar",viewer) as ScrollBar;
            var body=viewer.Template.FindName("PART_ScrollContentPresenter",viewer) as ScrollContentPresenter;
            if(bar==null||body==null||!(bar.Parent is Grid layout))return;
            var headers=Visuals(layout).OfType<DataGridColumnHeadersPresenter>().FirstOrDefault();
            if(headers!=null&&headers.Parent==layout)
            {
                var span=Grid.GetColumn(bar)+1-Grid.GetColumn(headers);
                if(span>0&&Grid.GetColumnSpan(headers)!=span)Grid.SetColumnSpan(headers,span);
            }
            double top=body.TranslatePoint(new Point(),layout).Y;
            double prefix=Visuals(body).OfType<ToggleButton>().Where(b=>b.Name=="GroupToggle"&&b.IsVisible)
                .Select(b=>new { Y=b.TranslatePoint(new Point(),layout).Y,H=b.ActualHeight })
                .Where(b=>b.Y<=top+1&&b.Y+b.H>top).Select(b=>b.Y+b.H-top).DefaultIfEmpty(0).Max();
            prefix=Math.Min(prefix,body.ActualHeight);
            var margin=new Thickness(0,prefix,0,0);
            if(bar.Margin!=margin)bar.Margin=margin;
            var cap=layout.Children.OfType<Border>().FirstOrDefault(b=>Equals(b.Tag,"GroupScrollCap"));
            if(cap==null)
            {
                cap=new Border { Tag="GroupScrollCap",VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false,BorderThickness=new Thickness(0,0,0,1) };
                cap.SetResourceReference(Border.BackgroundProperty,"SabBrush.TableHeaderBackground");
                cap.SetResourceReference(Border.BorderBrushProperty,"SabBrush.Border");
                Grid.SetColumn(cap,Grid.GetColumn(bar));Grid.SetRow(cap,Grid.GetRow(bar));layout.Children.Add(cap);
            }
            if(Math.Abs(cap.Height-prefix)>0.1||double.IsNaN(cap.Height))cap.Height=prefix;
            var visibility=bar.IsVisible&&prefix>0?Visibility.Visible:Visibility.Collapsed;
            if(cap.Visibility!=visibility)cap.Visibility=visibility;
        }
    }
    public static class Instructions
    {
        public const string ViewsFile = "SAB_HTML_Sozdat_vidy_i_listy.html";
        public static readonly DependencyProperty FileProperty = DependencyProperty.RegisterAttached("File", typeof(string), typeof(Instructions), new PropertyMetadata(null, Changed));
        public static string GetFile(DependencyObject d) => (string)d.GetValue(FileProperty);
        public static void SetFile(DependencyObject d, string value) => d.SetValue(FileProperty, value);
        static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is Button button)) return;
            button.Click -= Open; if (e.NewValue != null) button.Click += Open;
        }
        public static string Resolve(string file)
        {
            if (string.IsNullOrEmpty(file) || System.IO.Path.GetFileName(file) != file || !file.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Неверное имя файла инструкции.");
            var index = SAB.Instructions.InstructionLauncher.Resolve(System.IO.Path.GetDirectoryName(typeof(Instructions).Assembly.Location), SAB.Instructions.InstructionLauncher.IndexFile);
            var path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(index), file);
            if (!System.IO.File.Exists(path)) throw new System.IO.FileNotFoundException("Файл инструкции не найден.", path);
            return path;
        }
        static void Open(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = Resolve(GetFile(button)), UseShellExecute = true }); }
            catch (Exception error) { MessageBox.Show(Window.GetWindow(button), error.Message, "Инструкция", MessageBoxButton.OK, MessageBoxImage.Information); }
        }
        public static void EnsureFooter(Window window, Grid root, UIElement footer)
        {
            // Preserve the parameter window's original instruction handler; other shared dialogs get the catalog.
            if (footer != null && HasHelp(footer)) return;
            var help = new Button { Content = "Инструкция", Height = 34, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,16,0) };
            SetFile(help, SAB.Instructions.InstructionLauncher.IndexFile);
            var panel = new DockPanel(); DockPanel.SetDock(help, Dock.Left); panel.Children.Add(help);
            var bottom = root.Children.OfType<Border>().FirstOrDefault(x => Grid.GetRow(x) == 3);
            if (bottom == null) { bottom = new Border { Padding = new Thickness(24,12,24,12), Background = Brushes.White }; Grid.SetRow(bottom,3); root.Children.Add(bottom); }
            else bottom.Child = null;
            if (footer != null) panel.Children.Add(footer); bottom.Child = panel;
        }
        static bool HasHelp(DependencyObject root)
        {
            if (root is Button button && Equals(button.Content,"Инструкция")) return true;
            return LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>().Any(HasHelp);
        }
    }

    sealed class StatusText : IValueConverter
    {
        public object Convert(object value, Type type, object parameter, System.Globalization.CultureInfo culture)
        { var text = value as string; return text != null && text.StartsWith("Настройки готовы.", StringComparison.Ordinal) ? "Настройки готовы." : value; }
        public object ConvertBack(object value, Type type, object parameter, System.Globalization.CultureInfo culture) { throw new NotSupportedException(); }
    }

    // Presentation only: original radio bindings, events and automation peers remain native.
    public static class Capsule
    {
        public static readonly DependencyProperty IsGroupProperty = DependencyProperty.RegisterAttached("IsGroup", typeof(bool), typeof(Capsule), new PropertyMetadata(false, Changed));
        public static bool GetIsGroup(DependencyObject d) { return (bool)d.GetValue(IsGroupProperty); }
        public static void SetIsGroup(DependencyObject d, bool value) { d.SetValue(IsGroupProperty, value); }
        private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = d as Grid; if (grid == null || !(bool)e.NewValue) return;
            grid.Loaded += (s, a) => { Update(grid, false); DependencyObject parent = grid; while(parent != null && !(parent is TabControl)) parent = VisualTreeHelper.GetParent(parent); if(parent is TabControl tabs) tabs.SelectionChanged += (sender,args) => { if(ReferenceEquals(args.Source,tabs)) Update(grid,true); }; };
            grid.SizeChanged += (s, a) => Update(grid, false);
            grid.AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler((s, a) => Update(grid, true)));
            grid.LayoutUpdated += (s, a) => Update(grid, false);
        }
        private static void Update(Grid grid, bool animate)
        {
            var indicator = grid.Children.OfType<Border>().FirstOrDefault(b => Equals(b.Tag, "CapsuleIndicator"));
            var choices = grid.Children.OfType<Panel>().SelectMany(p => p.Children.OfType<FrameworkElement>()).ToList();
            var active = choices.FirstOrDefault(r => r.IsVisible && ((r is RadioButton radio && radio.IsChecked == true) || (r is TabItem tab && tab.IsSelected)));
            if (indicator == null || active == null || active.ActualWidth <= 0) return;
            var pos = active.TranslatePoint(new Point(0, 0), grid);
            var transform = indicator.RenderTransform as TranslateTransform;
            if (transform == null) { transform = new TranslateTransform(); indicator.RenderTransform = transform; }
            var width = Math.Max(0, active.ActualWidth);
            var target = pos.X;
            indicator.VerticalAlignment = VerticalAlignment.Top;
            indicator.Height = Math.Max(30, active.ActualHeight);
            indicator.CornerRadius = new CornerRadius(indicator.Height / 2);
            var track = grid.Children.OfType<Border>().FirstOrDefault(b => !Equals(b.Tag,"CapsuleIndicator"));
            if (track != null) track.CornerRadius = new CornerRadius(grid.ActualHeight > active.ActualHeight + 5 ? 22 : grid.ActualHeight / 2);
            if (Math.Abs(indicator.Width - width) < .1 && Math.Abs((double)indicator.GetValue(TargetProperty) - target) < .1 && Math.Abs((double)indicator.GetValue(TargetYProperty) - pos.Y) < .1) return;
            indicator.Width = width;
            indicator.SetValue(TargetProperty, target);
            indicator.SetValue(TargetYProperty, pos.Y);
            if (animate && SystemParameters.ClientAreaAnimation) transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            else { transform.BeginAnimation(TranslateTransform.XProperty, null); transform.X = target; }
            if (animate && SystemParameters.ClientAreaAnimation) transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(pos.Y, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            else { transform.BeginAnimation(TranslateTransform.YProperty, null); transform.Y = pos.Y; }
        }
        private static readonly DependencyProperty TargetYProperty = DependencyProperty.RegisterAttached("TargetY", typeof(double), typeof(Capsule), new PropertyMetadata(double.NaN));
        private static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached("Target", typeof(double), typeof(Capsule), new PropertyMetadata(double.NaN));
    }

    public static class RoundedClip
    {
        public static readonly DependencyProperty RadiusProperty = DependencyProperty.RegisterAttached("Radius",typeof(double),typeof(RoundedClip),new PropertyMetadata(0.0,Changed));
        public static double GetRadius(DependencyObject d) => (double)d.GetValue(RadiusProperty);
        public static void SetRadius(DependencyObject d,double value) => d.SetValue(RadiusProperty,value);
        static void Changed(DependencyObject d,DependencyPropertyChangedEventArgs e)
        {
            if(!(d is Border border)) return;
            Action update = () => { if(border.Child is FrameworkElement child && child.ActualWidth > 0) child.Clip = new RectangleGeometry(new Rect(0,0,child.ActualWidth,child.ActualHeight),Math.Max(0,GetRadius(border)-1),Math.Max(0,GetRadius(border)-1)); };
            border.Loaded += (s,a) => update(); border.SizeChanged += (s,a) => update(); border.LayoutUpdated += (s,a) => { if(border.Child is FrameworkElement child && (!(child.Clip is RectangleGeometry rect) || rect.Rect.Width != child.ActualWidth || rect.Rect.Height != child.ActualHeight)) update(); };
        }
    }

    public static class ScrollEdges
    {
        public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(ScrollEdges),new PropertyMetadata(false,Changed));
        public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);
        public static void SetEnabled(DependencyObject d,bool value) => d.SetValue(EnabledProperty,value);
        static void Changed(DependencyObject d,DependencyPropertyChangedEventArgs e)
        {
            if(!(d is ScrollViewer scroll) || !(bool)e.NewValue) return;
            Action update = () => {
                var fade = Math.Min(.06,12/Math.Max(1,scroll.ActualHeight));
                var brush = new LinearGradientBrush { StartPoint = new Point(0,0), EndPoint = new Point(0,1) };
                brush.GradientStops.Add(new GradientStop(scroll.VerticalOffset > 0 ? Colors.Transparent : Colors.White,0));
                brush.GradientStops.Add(new GradientStop(Colors.White,fade)); brush.GradientStops.Add(new GradientStop(Colors.White,1-fade));
                brush.GradientStops.Add(new GradientStop(scroll.VerticalOffset < scroll.ScrollableHeight ? Colors.Transparent : Colors.White,1)); scroll.OpacityMask = brush;
            };
            scroll.Loaded += (s,a) => update(); scroll.SizeChanged += (s,a) => update(); scroll.ScrollChanged += (s,a) => update();
        }
    }

    public sealed class EmptyRowsVisibility : IValueConverter
    {
        public object Convert(object value, Type type, object parameter, System.Globalization.CultureInfo culture) { return System.Convert.ToInt32(value) == 0 ? Visibility.Visible : Visibility.Collapsed; }
        public object ConvertBack(object value, Type type, object parameter, System.Globalization.CultureInfo culture) { throw new NotSupportedException(); }
    }

    public static class Presentation
    {
        public static void PrepareText(TextBlock block, bool bold)
        {
            var text = block.Text;
            if (text.StartsWith("Существующая матрица сохранена. Получатели: ", StringComparison.Ordinal))
            { block.Text = text.Replace("Существующая матрица сохранена. ", "").Split(new[] { ". Привязка хранится" }, StringSplitOptions.None)[0]; block.Tag = "SabDataExplanation"; return; }
            if (text.StartsWith("Задайте допустимые значения.", StringComparison.Ordinal))
            { var marker = "Последнее выбранное значение: "; var start = text.IndexOf(marker, StringComparison.Ordinal); block.Text = start < 0 ? "" : text.Substring(start).Split(new[] { ". Для записи" }, StringSplitOptions.None)[0]; block.Tag = "SabDataExplanation"; return; }
            if (text.StartsWith("Во вкладке «Правила» добавьте", StringComparison.Ordinal))
            { block.Text = "Нет включённых правил."; block.Tag = null; return; }
            if (text == "Выберите правило, для которого настраиваете источник") { block.Text = "Правило"; return; }
            var guidance = Equals(block.Tag,"SabExplanation") || new[] { "Одна константа для этого правила", "Shift — диапазон", "Список значений задаётся в настройках", "Исходное значение преобразуется по таблице" }.Any(prefix => text.StartsWith(prefix,StringComparison.Ordinal));
            if (!bold && guidance) { block.Visibility = Visibility.Collapsed; block.Margin = new Thickness(0); block.Tag = "SabGuidanceRemoved"; }
        }
        public static Border Status(TextBlock source)
        {
            // Original UpdateStatus and validation methods still write the source; only displayed copy is shortened.
            var display = new TextBlock { FontSize = source.FontSize, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            display.SetBinding(TextBlock.TextProperty,new Binding("Text") { Source = source, Converter = new StatusText() });
            display.SetBinding(TextBlock.ForegroundProperty,new Binding("Foreground") { Source = source });
            display.SetBinding(FrameworkElement.ToolTipProperty,new Binding("ToolTip") { Source = source, Converter = new StatusText() });
            return Information(display);
        }
        public static FrameworkElement TableCard(Window window,string title,DockPanel source)
        {
            var frame = new Border { Style = (Style)window.FindResource("SabDataGridContainerBorderStyle") };
            var layout = new Grid(); frame.Child = layout;
            for(int i=0;i<5;i++) layout.RowDefinitions.Add(new RowDefinition { Height = i==3 ? new GridLength(1,GridUnitType.Star) : GridLength.Auto });
            var heading = new TextBlock { Text = title, Style = (Style)window.FindResource("SabPanelHeaderTextStyle"), Margin = new Thickness(16,16,16,12) }; layout.Children.Add(heading);
            var children = source.Children.Cast<UIElement>().ToList(); source.Children.Clear();
            foreach(var child in children) {
                var row = child is DataGrid ? 3 : child is TextBlock || child is Border ? 1 : child is WrapPanel ? 2 : 4;
                if(child is FrameworkElement element) element.Margin = new Thickness(16,0,16,12);
                Grid.SetRow(child,row); layout.Children.Add(child);
            }
            Decorate(layout);
            return frame;
        }
        private static void OutlineCollection(Control collection)
        {
            var parent = collection.Parent;
            var existing = parent as Border;
            // Category list and empty-state overlay share their existing frame.
            if (collection is ListBox && parent is Grid overlay) existing = overlay.Parent as Border;
            if (existing != null && existing.BorderThickness.Left > 0)
            {
                existing.Tag = "CollectionFrame";
                existing.SetResourceReference(Border.BorderBrushProperty,"SabBrush.Border");
                RoundedClip.SetRadius(existing,6);
                return;
            }
            if (!(parent is Panel panel)) return;
            var index = panel.Children.IndexOf(collection);
            var frame = new Border { Style = (Style)FluentResources.Find("SabCollectionBorderStyle"), Tag = "CollectionFrame", Margin = collection.Margin };
            Grid.SetRow(frame,Grid.GetRow(collection)); Grid.SetColumn(frame,Grid.GetColumn(collection));
            Grid.SetRowSpan(frame,Grid.GetRowSpan(collection)); Grid.SetColumnSpan(frame,Grid.GetColumnSpan(collection)); DockPanel.SetDock(frame,DockPanel.GetDock(collection));
            frame.SetBinding(UIElement.VisibilityProperty,new Binding("Visibility") { Source = collection });
            panel.Children.RemoveAt(index); collection.Margin = new Thickness(0); frame.Child = collection; panel.Children.Insert(index,frame);
        }
        public static FrameworkElement ActionGroup(string title, FrameworkElement content)
        {
            var group = new StackPanel { Margin = new Thickness(0,0,16,8) };
            group.Children.Add(new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,6) }); group.Children.Add(content); return group;
        }
        public static Border Information(FrameworkElement content)
        {
            var frame = new Border { Style = (Style)FluentResources.Find("SabInformationBorderStyle"), Child = content, Margin = new Thickness(0, 8, 0, 12) };
            frame.SetBinding(UIElement.VisibilityProperty, new Binding("Visibility") { Source = content });
            content.Margin = new Thickness(0); return frame;
        }
        public static void Decorate(DependencyObject root)
        {
            if (root is TabControl tabs) { foreach(var tab in tabs.Items.OfType<TabItem>()) if(tab.Content is DependencyObject content) Decorate(content); return; }
            if(root is Border host && host.Child is ScrollViewer viewport && !ScrollEdges.GetEnabled(viewport))
            { host.Style = (Style)FluentResources.Find("SabPanelBorderStyle"); host.Padding = new Thickness(12); ScrollEdges.SetEnabled(viewport,true); }
            if(root is DataGrid dataGrid) { dataGrid.RowHeight = 44; OutlineCollection(dataGrid); return; }
            if(root is ListBox list) { OutlineCollection(list); return; }
            if (root is FrameworkElement field && (root is ComboBox || root is TextBox || root is CheckBox || root is SAB.ParameterTools.UI.ParameterPicker))
            {
                if (field is SAB.ParameterTools.UI.ParameterPicker || (field.Parent is StackPanel vertical && vertical.Orientation == Orientation.Vertical))
                    field.Margin = new Thickness(0,field.Margin.Top,0,field.Margin.Bottom);
                if (root is ComboBox || root is TextBox || root is CheckBox) return;
            }
            var panel = root as Panel;
            if (panel != null)
            {
                foreach (var child in panel.Children.Cast<UIElement>().ToList())
                {
                    var text = child as TextBlock;
                    if (text != null && Equals(text.Tag, "SabDataExplanation"))
                    {
                        var index = panel.Children.IndexOf(child); var dock = DockPanel.GetDock(child);
                        var row = Grid.GetRow(child); var column = Grid.GetColumn(child); var span = Grid.GetColumnSpan(child); var margin = text.Margin;
                        panel.Children.RemoveAt(index); var frame = Information(text); if(panel is DockPanel && text.MaxHeight == 64) frame.Margin = new Thickness(0,8,8,12); if(panel is Grid) { Grid.SetRow(frame,row);Grid.SetColumn(frame,column);Grid.SetColumnSpan(frame,span);frame.Margin=margin; } DockPanel.SetDock(frame, dock); panel.Children.Insert(index, frame);
                    }
                    else Decorate(child);
                }
                if(panel is WrapPanel) foreach(var card in panel.Children.OfType<GroupBox>()) card.Margin = new Thickness(0,0,16,16);
                var table = Descendants(panel).OfType<DataGrid>().FirstOrDefault();
                if (table != null)
                {
                    foreach (var button in panel.Children.OfType<Button>().Where(b => new[] { "Добавить соответствие", "Добавить значение" }.Contains(b.Content as string)))
                    {
                        var label = (string)button.Content;
                        button.ToolTip = label; System.Windows.Automation.AutomationProperties.SetName(button, label);
                        button.Content = null; button.ContentTemplate = SAB.ParameterTools.UI.FluentTheme.ActionIcon(true);
                        button.Width = 28; button.Height = 28; button.Padding = new Thickness(0);
                        button.SetBinding(UIElement.VisibilityProperty, new Binding("Items.Count") { Source = table, Converter = new EmptyRowsVisibility() });
                    }
                }
            }
            else if (root is Decorator decorator) { if (decorator.Child != null) Decorate(decorator.Child); }
            else if (root is ContentControl control && !(root is ButtonBase) && !(root is ListBoxItem)) { if (control.Content is DependencyObject body) Decorate(body); }
        }
        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            { yield return child; foreach (var next in Descendants(child)) yield return next; }
        }
    }
}
