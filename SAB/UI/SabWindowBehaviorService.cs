using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace SAB.UI
{
    public static class SabWindowBehaviorService
    {
        private static bool _registered;
        private static readonly DependencyProperty LoadedBehaviorAppliedProperty =
            DependencyProperty.RegisterAttached("LoadedBehaviorApplied", typeof(bool),
                typeof(SabWindowBehaviorService), new PropertyMetadata(false));

        // Scope the class handlers to SAB. Revit and other add-ins keep their own UI.
        public static void Initialize()
        {
            if (_registered) return;
            GC.KeepAlive(typeof(Wpf.Ui.Controls.SymbolIcon));
            _registered = true;
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
                new RoutedEventHandler(GlobalWindow_Loaded), true);
            EventManager.RegisterClassHandler(typeof(ScrollViewer), FrameworkElement.LoadedEvent,
                new RoutedEventHandler(FormScrollViewer_Loaded), true);
            EventManager.RegisterClassHandler(typeof(ButtonBase), FrameworkElement.LoadedEvent,
                new RoutedEventHandler(Control_Loaded), true);
            EventManager.RegisterClassHandler(typeof(Expander), FrameworkElement.LoadedEvent,
                new RoutedEventHandler(Control_Loaded), true);
        }

        private static void Control_Loaded(object sender, RoutedEventArgs e)
        {
            FrameworkElement control = sender as FrameworkElement;
            if (control != null && IsSabWindow(Window.GetWindow(control)))
                SabWindowAnimationService.AttachControlAnimations(control);
        }

        private static bool IsSabWindow(Window window)
        {
            if (window == null) return false;
            string typeName = window.GetType().FullName ?? string.Empty;
            if (typeName.StartsWith("SAB.Helpers.Notifications.") || typeName.StartsWith("SAB.Notifications.") ||
                typeName.EndsWith("DuckReminderWindow") || typeName.EndsWith("PeekingAnimalReminderWindow") ||
                typeName.EndsWith("PoopDropWindow")) return false;
            return
                (window.GetType().Assembly == typeof(SabWindowBehaviorService).Assembly ||
                 window.TryFindResource("SabTextBoxStyle") != null);
        }

        private static void GlobalWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Window window = sender as Window;
            if (IsSabWindow(window)) ApplyLoadedBehavior(window);
        }

        private static void FormScrollViewer_Loaded(object sender, RoutedEventArgs e)
        {
            ScrollViewer viewer = sender as ScrollViewer;
            if (viewer != null && IsSabWindow(Window.GetWindow(viewer))) ConfigureFormScrollViewer(viewer);
        }

        private static void ConfigureFormScrollViewer(ScrollViewer viewer)
        {
            if (viewer.TemplatedParent != null) return;
            // Never replace the text editor, combo popup, or virtualized table scroller.
            for (DependencyObject parent = VisualTreeHelper.GetParent(viewer); parent != null;
                 parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent is TextBoxBase || parent is ComboBox || parent is DataGrid) return;
            }
            if (viewer.ReadLocalValue(FrameworkElement.StyleProperty) == DependencyProperty.UnsetValue)
                viewer.SetResourceReference(FrameworkElement.StyleProperty, "SabFormScrollViewerStyle");
        }

        private static void ConfigureScrollViewers(DependencyObject root)
        {
            ScrollViewer viewer = root as ScrollViewer;
            if (viewer != null) ConfigureFormScrollViewer(viewer);
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                ConfigureScrollViewers(VisualTreeHelper.GetChild(root, i));
        }

        private static void ApplyTheme(Window window)
        {
            if (window.TryFindResource("SabTextBoxStyle") == null)
            {
                string path = Path.Combine(Path.GetDirectoryName(typeof(SabWindowBehaviorService).Assembly.Location),
                    "UI", "Styles", "SABWindowStyles.xaml");
                window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(path) });
            }
            window.SetResourceReference(Control.ForegroundProperty, "SabBrush.Text");
            if (!window.AllowsTransparency)
                window.SetResourceReference(Control.BackgroundProperty, "SabBrush.WindowBackground");
            window.FontFamily = new FontFamily("Segoe UI");
            window.UseLayoutRounding = true;
            window.SnapsToDevicePixels = true;
        }
        private static readonly DependencyProperty BehaviorAttachedProperty =
            DependencyProperty.RegisterAttached(
                "BehaviorAttached",
                typeof(bool),
                typeof(SabWindowBehaviorService),
                new PropertyMetadata(false));

        public static void Apply(Window window)
        {
            Initialize();
            if (window == null || GetBehaviorAttached(window))
            {
                return;
            }

            SetBehaviorAttached(window, true);

            if (window.IsLoaded)
            {
                ApplyLoadedBehavior(window);
                return;
            }

            window.Opacity = 0.0;
            window.Loaded += Window_Loaded;
            window.Closed += Window_Closed;
        }

        public static void ApplyLoadedBehavior(Window window)
        {
            Initialize();
            if (window == null || (bool)window.GetValue(LoadedBehaviorAppliedProperty))
            {
                return;
            }

            window.SetValue(LoadedBehaviorAppliedProperty, true);
            ApplyTheme(window);
            ConfigureScrollViewers(window);
            SabWindowPlacementService.CenterOnCurrentScreen(window);
            window.Opacity = 1.0;
            SabWindowAnimationService.AttachWindowAnimations(window);
            SabWindowAnimationService.AnimateWindowEntrance(window);
        }

        private static void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyLoadedBehavior(sender as Window);
        }

        private static void Window_Closed(object sender, System.EventArgs e)
        {
            Window window = sender as Window;
            if (window == null)
            {
                return;
            }

            window.Loaded -= Window_Loaded;
            window.Closed -= Window_Closed;
            SetBehaviorAttached(window, false);
        }

        private static bool GetBehaviorAttached(DependencyObject element)
        {
            return element != null && (bool)element.GetValue(BehaviorAttachedProperty);
        }

        private static void SetBehaviorAttached(DependencyObject element, bool value)
        {
            if (element != null)
            {
                element.SetValue(BehaviorAttachedProperty, value);
            }
        }
    }
}
