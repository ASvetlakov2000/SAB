using System;
using System.Windows;
using System.Windows.Controls;

namespace SAB.UI
{
    /// <summary>Presentation helpers for the three approved settings surfaces.</summary>
    public static class SabRedesignLayout
    {
        private static readonly DependencyProperty PreviewWatchedProperty = DependencyProperty.RegisterAttached(
            "PreviewWatched", typeof(bool), typeof(SabRedesignLayout), new PropertyMetadata(false));
        public static Border Panel(Window window, UIElement content, Thickness? padding = null)
        {
            return new Border { Style = (Style)window.FindResource("SabReviewPanelStyle"),
                Padding = padding ?? new Thickness(16), Child = content };
        }

        public static void Initialize(Window window)
        {
            SabWindowAnimationService.AttachWindowAnimations(window);
            SabWindowAnimationService.AnimateWindowEntrance(window);
        }

        public static void WatchPreview(DataGrid grid, FrameworkElement preview)
        {
            if (grid == null || preview == null || (bool)grid.GetValue(PreviewWatchedProperty)) return;
            grid.SetValue(PreviewWatchedProperty, true);
            grid.SelectionChanged += (sender, args) =>
            {
                if (ReferenceEquals(args.OriginalSource, grid)) SabWindowAnimationService.PulseElement(preview);
            };
        }
    }
}
