using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace SAB.UI.Notifications
{
    /// <summary>One owner-bound stack. No Revit API is used from timers or input callbacks.</summary>
    public sealed class NotificationStack : Window
    {
        private static readonly Dictionary<IntPtr, NotificationStack> stacks = new Dictionary<IntPtr, NotificationStack>();
        private static readonly Stopwatch clock = Stopwatch.StartNew();
        internal static double Now => clock.Elapsed.TotalSeconds;
        private readonly IntPtr ownerHandle;
        private readonly StackPanel items = new StackPanel();
        private readonly ScrollViewer viewport;
        private readonly DispatcherTimer timer;
        private double nextPlacement;
        public IReadOnlyList<NotificationCard> Cards => items.Children.Cast<NotificationCard>().ToList();

        private NotificationStack(IntPtr owner)
        {
            ownerHandle = owner;
            Title = "SAB · Уведомления"; Width = 416; SizeToContent = SizeToContent.Height;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; ShowActivated = false; Topmost = true;
            viewport = new ScrollViewer { Content = items, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, CanContentScroll = false, Background = Brushes.Transparent };
            Content = viewport;
            if (owner != IntPtr.Zero) new WindowInteropHelper(this).Owner = owner;
            timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(50) };
            timer.Tick += Pulse; viewport.ScrollChanged += Scrolled;
            SizeChanged += Resized; Closed += Disposed;
            Place();
        }

        public static NotificationStack Show(IntPtr owner, NotificationMessage message)
        {
            NotificationStack stack;
            if (!stacks.TryGetValue(owner, out stack))
            {
                stack = new NotificationStack(owner); stacks.Add(owner, stack);
                stack.Add(message); stack.Show(); stack.Place(); stack.timer.Start();
            }
            else stack.Add(message);
            return stack;
        }

        private void Add(NotificationMessage message)
        {
            bool reading = Cards.Any(c => c.Reading);
            double offset = viewport.VerticalOffset;
            var card = new NotificationCard(message); card.BoundBody(MaxHeight);
            card.CloseRequested += RemoveRequested; card.InteractionChanged += Interaction;
            items.Children.Add(card); Spacing();
            UpdateLayout();
            if (reading) viewport.ScrollToVerticalOffset(offset);
            else viewport.ScrollToBottom();
            UpdateLifetimes();
        }
        private void Spacing()
        { for (int i = 0; i < items.Children.Count; i++) ((NotificationCard)items.Children[i]).Margin = new Thickness(0, 0, 0, i == items.Children.Count - 1 ? 0 : 12); }
        private void RemoveRequested(object sender, EventArgs e) => Remove((NotificationCard)sender);
        private void Remove(NotificationCard card)
        {
            card.CloseRequested -= RemoveRequested; card.InteractionChanged -= Interaction;
            items.Children.Remove(card); Spacing();
            if (items.Children.Count == 0) Close();
        }
        private void Interaction(object sender, EventArgs e) => UpdateLifetimes();
        private void Scrolled(object sender, ScrollChangedEventArgs e) => UpdateLifetimes();
        private void Resized(object sender, SizeChangedEventArgs e) { if (IsVisible) Place(); }
        private void Pulse(object sender, EventArgs e)
        {
            if (ownerHandle != IntPtr.Zero && !IsWindow(ownerHandle)) { Close(); return; }
            if (Now >= nextPlacement) { Place(); nextPlacement = Now + .25; }
            UpdateLifetimes();
            foreach (var card in Cards.Where(c => c.Lifetime.RemainingSeconds <= 0).ToList()) Remove(card);
        }
        private void UpdateLifetimes()
        {
            bool unavailable = !IsVisible || (ownerHandle != IntPtr.Zero && (!IsWindowEnabled(ownerHandle) || IsIconic(ownerHandle)));
            foreach (var card in Cards)
            {
                bool visible = false;
                if (card.IsArrangeValid && viewport.ActualHeight > 0)
                {
                    Point p = card.TranslatePoint(new Point(), viewport);
                    visible = p.Y >= -.5 && p.Y + card.ActualHeight <= viewport.ActualHeight + .5;
                }
                card.Tick(Now, unavailable || !visible);
            }
        }
        private void Place()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            var monitor = MonitorFromWindow(ownerHandle != IntPtr.Zero ? ownerHandle : hwnd, 2);
            var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            if (!GetMonitorInfo(monitor, ref info)) return;
            double scale = 1;
            if (hwnd != IntPtr.Zero)
            {
                try { uint dpi = GetDpiForWindow(hwnd); if (dpi > 0) scale = dpi / 96.0; }
                catch (EntryPointNotFoundException) { var source = PresentationSource.FromVisual(this); if (source?.CompositionTarget != null) scale = source.CompositionTarget.TransformToDevice.M11; }
            }
            double width = Math.Min(416, (info.Work.Right - info.Work.Left) / scale - 32);
            double height = (info.Work.Bottom - info.Work.Top) / scale - 32;
            if (Math.Abs(Width - width) > .5) Width = Math.Max(180, width);
            if (Math.Abs(MaxHeight - height) > .5 || double.IsInfinity(MaxHeight)) { MaxHeight = Math.Max(180, height); viewport.MaxHeight = MaxHeight; foreach (var card in Cards) card.BoundBody(MaxHeight); }
            if (hwnd != IntPtr.Zero) SetWindowPos(hwnd, IntPtr.Zero, info.Work.Right - (int)Math.Round(ActualWidth * scale) - (int)(16 * scale), info.Work.Bottom - (int)Math.Round(ActualHeight * scale) - (int)(16 * scale), 0, 0, 0x0015);
        }
        private void Disposed(object sender, EventArgs e)
        {
            timer.Stop(); timer.Tick -= Pulse; viewport.ScrollChanged -= Scrolled;
            SizeChanged -= Resized; Closed -= Disposed;
            foreach (var card in Cards) { card.CloseRequested -= RemoveRequested; card.InteractionChanged -= Interaction; }
            items.Children.Clear(); stacks.Remove(ownerHandle);
        }
        public static void CloseAll() { foreach (var stack in stacks.Values.ToList()) stack.Close(); }

        [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    }
}
