using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SAB.UI
{
    // Opt-in presentation fixes for the three audited modules. No Revit API work.
    internal static class SabLayoutAssist
    {
        private static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
            "Enabled", typeof(bool), typeof(SabLayoutAssist), new PropertyMetadata(false));

        internal static void Apply(Window window)
        {
            if ((bool)window.GetValue(EnabledProperty)) return;
            window.SetValue(EnabledProperty, true);
            window.PreviewMouseWheel += ForwardBoundaryWheel;
            ProtectBorders(window, window.Content as Border);
        }

        private static void ProtectBorders(DependencyObject parent, Border frame)
        {
            if (parent is Border border && (ReferenceEquals(border, frame) || border.Child is DataGrid)
                && border.CornerRadius.TopLeft > 0)
            {
                border.SizeChanged += (s, e) => ClipChild(border);
                ClipChild(border);
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                ProtectBorders(VisualTreeHelper.GetChild(parent, i), frame);
        }

        private static void ClipChild(Border border)
        {
            var child = border.Child as FrameworkElement;
            if (child == null || child.ActualWidth <= 0 || child.ActualHeight <= 0) return;
            // Clip only the interior; leave the existing outline and resize frame intact.
            double inset = Math.Max(border.BorderThickness.Left, border.BorderThickness.Top);
            double radius = Math.Max(0, border.CornerRadius.TopLeft - inset);
            var clip = new RectangleGeometry(new Rect(child.RenderSize), radius, radius);
            clip.Freeze();
            child.Clip = clip;
        }

        private static DependencyObject Parent(DependencyObject child)
        {
            if (child is Visual || child is System.Windows.Media.Media3D.Visual3D)
                return VisualTreeHelper.GetParent(child);
            return child is FrameworkContentElement content ? content.Parent : LogicalTreeHelper.GetParent(child);
        }

        private static bool CanScroll(ScrollViewer viewer, int delta)
        {
            return delta < 0 ? viewer.VerticalOffset < viewer.ScrollableHeight - 0.01 : viewer.VerticalOffset > 0.01;
        }

        private static void ForwardBoundaryWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled || e.Delta == 0) return;
            var current = e.OriginalSource as DependencyObject;
            while (current != null && !(current is ScrollViewer)) current = Parent(current);
            var inner = current as ScrollViewer;
            if (inner == null || CanScroll(inner, e.Delta)) return;
            current = Parent(inner);
            while (current != null)
            {
                if (current is ScrollViewer outer && CanScroll(outer, e.Delta))
                {
                    outer.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                    { RoutedEvent = Mouse.MouseWheelEvent, Source = outer });
                    e.Handled = true;
                    return;
                }
                current = Parent(current);
            }
        }
    }
}
