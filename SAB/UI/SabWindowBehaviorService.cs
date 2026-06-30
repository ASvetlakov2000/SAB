using System.Windows;

namespace SAB.UI
{
    public static class SabWindowBehaviorService
    {
        private static readonly DependencyProperty BehaviorAttachedProperty =
            DependencyProperty.RegisterAttached(
                "BehaviorAttached",
                typeof(bool),
                typeof(SabWindowBehaviorService),
                new PropertyMetadata(false));

        public static void Apply(Window window)
        {
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
            if (window == null)
            {
                return;
            }

            SabWindowPlacementService.CenterOnCurrentScreen(window);
            window.Opacity = 1.0;
            SabWindowAnimationService.AttachWindowAnimations(window);
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
