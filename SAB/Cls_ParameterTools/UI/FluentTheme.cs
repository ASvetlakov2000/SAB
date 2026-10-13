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
    internal static class FluentTheme
    {
        internal static readonly Brush Background = new SolidColorBrush(Color.FromRgb(247, 248, 250));
        internal static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(15, 108, 189));
        internal static void Window(Window w)
        {

            var resources = new ResourceDictionary { Source = new Uri(Path.Combine(Path.GetDirectoryName(typeof(FluentTheme).Assembly.Location), "UI", "Styles", "SABFluentLight.xaml"), UriKind.Absolute) };
            w.Resources.MergedDictionaries.Add(resources);
            w.FontFamily = new FontFamily("Segoe UI"); w.FontSize = 13;
            w.Foreground = (Brush)resources["SabBrush.Text"]; w.Background = (Brush)resources["SabBrush.WindowBackground"];
            w.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Preserve the incumbent keyboard contract with the native Fluent window chrome.
            w.PreviewKeyDown += (sender, e) => {
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
            var button = new Button { Content = label, Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(0,0,8,0), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
            button.Click += click; return button;
        }
        internal static TextBlock Text(string text, bool bold = false)
        {
            var block = new TextBlock { Text = text, Margin = new Thickness(0, 8, 0, 8), VerticalAlignment = VerticalAlignment.Center, Tag = !bold && text.Length >= 65 ? "SabExplanation" : null, TextWrapping = TextWrapping.Wrap, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal };
            block.SetResourceReference(TextBlock.ForegroundProperty, "SabBrush.Text"); SAB.UI.FluentLight.Presentation.PrepareText(block, bold); return block;
        }
        internal static void Frame(Window window, string title, string subtitle, UIElement body, UIElement footer = null)
        {

            var root = new Grid { Background = (Brush)window.FindResource("SabBrush.WindowBackground") };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var brand = new StackPanel { Orientation = Orientation.Horizontal };
            brand.Children.Add(new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(5), Background = Accent, Child = new TextBlock { Text = "S", Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
            brand.Children.Add(new TextBlock { Text = "SAB", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10,0,24,0), FontWeight = FontWeights.SemiBold });
            brand.Children.Add(new TextBlock { Text = "Параметризация", VerticalAlignment = VerticalAlignment.Center });
            var bar = new Border { Background = Brushes.White, BorderBrush = (Brush)window.FindResource("SabBrush.Border"), BorderThickness = new Thickness(0,0,0,1), Padding = new Thickness(24,14,24,14), Child = brand }; root.Children.Add(bar);
            var header = new StackPanel { Margin = new Thickness(24,18,24,12) };
            header.Children.Add(new TextBlock { Text = title, Style = (Style)window.FindResource("SabWindowTitleTextStyle") });
            
            foreach(var info in header.Children.OfType<Border>()) info.Margin = new Thickness(0,6,0,0); Grid.SetRow(header,1);root.Children.Add(header);
            var content = new Border { Padding = new Thickness(24,0,24,18), Child = body };Grid.SetRow(content,2);root.Children.Add(content);
            if(footer!=null) { var bottom = new Border { BorderBrush = (Brush)window.FindResource("SabBrush.BorderWeak"), BorderThickness = new Thickness(0,1,0,0), Background = Brushes.White, Padding = new Thickness(24,12,24,12), Child = footer };Grid.SetRow(bottom,3);root.Children.Add(bottom); }
            SAB.UI.FluentLight.Presentation.Decorate(body); if (footer != null) SAB.UI.FluentLight.Presentation.Decorate(footer); SAB.UI.FluentLight.Instructions.EnsureFooter(window, root, footer); window.Content = root; window.AddHandler(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent, new SelectionChangedEventHandler((sender,args) => { if(args.OriginalSource is TabControl) window.Dispatcher.BeginInvoke(new Action(() => SAB.UI.FluentLight.Presentation.Decorate(body)), System.Windows.Threading.DispatcherPriority.Loaded); }));

        }
        internal static GroupBox Card(Window window, string title, UIElement body)
        { SAB.UI.FluentLight.Presentation.Decorate(body); return new GroupBox { Header = title, Style = (Style)window.FindResource("SabLayeredSettingsGroupStyle"), Content = body }; }
        internal static void Primary(Window window, Button button)
        { button.Style = (Style)window.FindResource("SabPrimaryButtonStyle"); }
        internal static void Tabs(TabControl tabs)
        {
            tabs.Background = Brushes.Transparent; tabs.BorderThickness = new Thickness(0);
        }
        internal static DataTemplate ActionIcon(bool plus)
        {
            var path = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
            path.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse(plus ? "M1 6 H11 M6 1 V11" : "M2 3 H10 M4 3 V1 H8 V3 M3 4 V11 H9 V4 M5 5 V9 M7 5 V9"));
            path.SetValue(System.Windows.Shapes.Path.StrokeThicknessProperty, 1.4);path.SetResourceReference(System.Windows.Shapes.Path.StrokeProperty,"SabBrush.TextSecondary");
            path.SetValue(FrameworkElement.WidthProperty,12.0);path.SetValue(FrameworkElement.HeightProperty,12.0);path.SetValue(System.Windows.Shapes.Path.StretchProperty,Stretch.Uniform);
            return new DataTemplate { VisualTree = path };
        }
        internal static ToggleButton Toggle()
        {
            return new ToggleButton { Template = (ControlTemplate)SAB.UI.FluentLight.FluentResources.Find("SabSwitchTemplate"), Width = 38, Height = 22 };
        }
    }
}
