using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Sab.UiLab
{
    public static class Motion
    {
        public static bool Enabled { get; set; } = true;
        public static bool CanAnimate => Enabled && SystemParameters.ClientAreaAnimation;
        private static readonly DependencyProperty AttachedProperty = DependencyProperty.RegisterAttached("Attached", typeof(bool), typeof(Motion), new PropertyMetadata(false));
        private static readonly DependencyProperty OwnedProperty = DependencyProperty.RegisterAttached("Owned", typeof(bool), typeof(Motion), new PropertyMetadata(false));
        private static DoubleAnimation Animate(double from, double to, int ms) => new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
        public static void Reveal(FrameworkElement element, int ms = 160, double distance = 0)
        {
            if (element == null) return;
            element.SetValue(OwnedProperty, true);
            element.Opacity = 1;
            element.BeginAnimation(UIElement.OpacityProperty, null);
            var translate = element.RenderTransform as TranslateTransform;
            if (translate == null) { translate = new TranslateTransform(); element.RenderTransform = translate; }
            translate.Y = 0;
            translate.BeginAnimation(TranslateTransform.YProperty, null);
            if (!CanAnimate) return;
            element.BeginAnimation(UIElement.OpacityProperty, Animate(0.65, 1, ms), HandoffBehavior.SnapshotAndReplace);
            if (distance != 0) translate.BeginAnimation(TranslateTransform.YProperty, Animate(distance, 0, ms), HandoffBehavior.SnapshotAndReplace);
        }
        public static void Attach(DependencyObject root)
        {
            bool attached = (bool)root.GetValue(AttachedProperty);
            root.SetValue(AttachedProperty, true);
            if (!attached && root is Wpf.Ui.Controls.Button button)
            {
                button.SetValue(OwnedProperty, true);
                var scale = new ScaleTransform(1, 1); button.RenderTransform = scale; button.RenderTransformOrigin = new Point(0.5, 0.5);
                button.PreviewMouseLeftButtonDown += (s, e) => Press(scale, 0.985, 70);
                button.PreviewMouseLeftButtonUp += (s, e) => Press(scale, 1, 100);
                button.MouseLeave += (s, e) => { Press(scale, 1, 100); Hover(button, 1); };
                button.MouseEnter += (s, e) => Hover(button, 0.86);
                button.LostMouseCapture += (s, e) => Press(scale, 1, 70);
                button.IsEnabledChanged += (s, e) => Press(scale, 1, 70);
            }
            if (!attached && root is Expander expander) expander.Expanded += (s, e) => { if (expander.Content is FrameworkElement content) Reveal(content, 150, 3); };
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) Attach(VisualTreeHelper.GetChild(root, i));
        }
        private static void Hover(UIElement element, double value)
        { double current = element.Opacity; element.Opacity = value; element.BeginAnimation(UIElement.OpacityProperty, CanAnimate ? Animate(current, value, 100) : null, HandoffBehavior.SnapshotAndReplace); }
        private static void Press(ScaleTransform scale, double target, int ms)
        { double x = scale.ScaleX, y = scale.ScaleY; scale.ScaleX = target; scale.ScaleY = target; scale.BeginAnimation(ScaleTransform.ScaleXProperty, CanAnimate ? Animate(x, target, ms) : null, HandoffBehavior.SnapshotAndReplace); scale.BeginAnimation(ScaleTransform.ScaleYProperty, CanAnimate ? Animate(y, target, ms) : null, HandoffBehavior.SnapshotAndReplace); }
        public static void Stop(DependencyObject root)
        {
            if ((bool)root.GetValue(OwnedProperty) && root is UIElement element) { element.BeginAnimation(UIElement.OpacityProperty, null); element.Opacity = 1; if (element.RenderTransform is TranslateTransform t) { t.BeginAnimation(TranslateTransform.YProperty, null); t.Y = 0; } if (element.RenderTransform is ScaleTransform s) { s.BeginAnimation(ScaleTransform.ScaleXProperty, null); s.BeginAnimation(ScaleTransform.ScaleYProperty, null); s.ScaleX = s.ScaleY = 1; } }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) Stop(VisualTreeHelper.GetChild(root, i));
        }
    }
}
