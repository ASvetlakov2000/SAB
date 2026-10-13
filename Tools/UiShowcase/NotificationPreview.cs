using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SAB.UI.Notifications;

namespace SAB.UiShowcase
{
    // Synthetic result data; actual production controls. Does not run a Revit operation.
    internal static class NotificationPreview
    {
        private static readonly List<string> results = new List<string>();
        private static string directory;
        private static void Check(bool condition, string name) { results.Add((condition ? "PASS " : "FAIL ") + name); File.WriteAllLines(Path.Combine(directory, "verification.txt"), results); if (!condition) throw new InvalidOperationException(name); }
        private static void Pump() { var frame = new DispatcherFrame(); var deadline = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(150) }; deadline.Tick += (s,e) => { deadline.Stop(); frame.Continue = false; }; deadline.Start(); Dispatcher.PushFrame(frame); }
        internal static int Run(string output)
        {
            directory = output; Directory.CreateDirectory(directory);
            try
            {
                results.Add("Synthetic result fixtures; actual SAB.dll WPF classes. Revit operations: NOT RUN.");
                var lifetime = new NotificationLifetime(); lifetime.Update(0, false); lifetime.Update(3, true);
                Check(lifetime.RemainingSeconds == 7, "timer consumes active seconds");
                lifetime.Update(30, true); Check(lifetime.RemainingSeconds == 7, "paused duration does not consume remaining time");
                lifetime.Update(40, false); lifetime.Update(42, true); Check(lifetime.RemainingSeconds == 5, "resume remainder without reset");
                lifetime.Update(60, false); lifetime.Update(65, false); Check(lifetime.RemainingSeconds == 0, "expires after ten active seconds");
                var second = new NotificationLifetime(); second.Update(65, false); Check(second.RemainingSeconds == 10, "independent new notification lifetime");
                Adapter();
                var success = new NotificationMessage("SAB · Параметризация", "Завершено", NotificationTone.Success, "Обработано элементов: 124 из 124.\nЗаписей параметров: 372.");
                var warning = new NotificationMessage("SAB · Параметризация", "Завершено с пропусками", NotificationTone.Warning, "Обработано элементов: 121 из 124.\nУ 3 элементов не удалось записать параметр.", "482015, 482016, 482021", "Модуль: SAB · Параметризация\nПараметр: SAB_Материал_Описание\nПричина: параметр доступен только для чтения.\nElementId: 482015, 482016, 482021");
                var error = new NotificationMessage("SAB · Параметризация", "Операция отменена", NotificationTone.Error, "Revit отменил транзакцию.\nИзменения этой операции не сохранены.", null, "Модуль: SAB · Параметризация\nОперация: заполнение параметров\nTransactionStatus: RolledBack\nПричина: Revit отменил транзакцию.");
                CaptureCard("01-success", new NotificationCard(success));
                var warningCard = new NotificationCard(warning); var window = CardWindow(warningCard); window.Show(); Pump();
                Save(window, "02-warning");
                Check(Descendants<TextBox>(warningCard).All(t => t.IsReadOnly), "all notification text is selectable read-only text");
                var idButton = Descendants<Button>(warningCard).Single(b => Equals(b.Content, "Копировать ID"));
                idButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(Clipboard.GetText() == warning.ElementIds, "copy ID copies only exact IDs to real Windows clipboard");
                Check(Equals(idButton.Content, "Скопировано"), "success feedback follows successful clipboard write");
                var supportButton = Descendants<Button>(warningCard).Single(b => Equals(b.Content, "Копировать"));
                supportButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(Clipboard.GetText() == warning.Support, "copy support includes complete exact diagnostic");
                var code = Descendants<TextBox>(warningCard).Single(t => t.Text == warning.Support); code.Focus(); code.SelectAll(); Pump();
                Check(code.SelectionLength == warning.Support.Length && warningCard.IsKeyboardFocusWithin, "keyboard focus and native text selection");
                var tick = typeof(NotificationCard).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                tick.Invoke(warningCard, new object[] { 0.0, false }); tick.Invoke(warningCard, new object[] { 3.0, false });
                Check(warningCard.Lifetime.RemainingSeconds == 10, "actual focused card pauses lifetime");
                Save(window, "03-selected-copied"); window.Close();
                CaptureCard("04-error", new NotificationCard(error));
                var failure = new NotificationCard(warning, text => { throw new ExternalException("Тест: буфер обмена занят"); });
                var failedWindow = CardWindow(failure); failedWindow.Show(); Pump();
                var failButton = Descendants<Button>(failure).Single(b => Equals(b.Content, "Копировать ID")); failButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(Equals(failButton.Content, "Не скопировано"), "clipboard failure never reports copied");
                Save(failedWindow, "05-clipboard-failure"); failedWindow.Close();
                var longCard = new NotificationCard(new NotificationMessage("SAB · Параметризация · очень длинное название модуля", "Завершено с пропусками", NotificationTone.Warning,
                    string.Join("\n", Enumerable.Repeat("Длинное фактическое сообщение о результатах записи параметров и проверке элементов модели.", 8)), warning.ElementIds,
                    string.Join("\n", Enumerable.Repeat(warning.Support, 12))));
                typeof(NotificationCard).GetMethod("BoundBody", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(longCard, new object[] { 550.0 });
                var longWindow = CardWindow(longCard, 380); longWindow.Show(); Pump();
                results.Add("Long card measured height: " + longWindow.ActualHeight);
                Save(longWindow, "06-long-minimum");
                Check(longWindow.ActualHeight <= 551, "long Russian text bounded with header and timer visible");
                Check(Descendants<ScrollViewer>(longCard).Any(s => s.ScrollableHeight > 0), "long diagnostic scrolls within card");
                Save(longWindow, "06-long-minimum"); Save(longWindow, "07-long-144dpi-render", 1.5); longWindow.Close();
                var owner = new Window { Title = "SAB notification test owner", Width = 400, Height = 200 }; owner.Show(); owner.Activate(); Pump();
                IntPtr foreground = GetForegroundWindow(); IntPtr handle = new WindowInteropHelper(owner).Handle;
                var stack = NotificationStack.Show(handle, success); Pump();
                BottomAnchor(stack);
                var initialTop=stack.Top;
                Check(GetForegroundWindow() == foreground, "show notification does not steal initial native activation");
                EnableWindow(handle, false); double disabledRemaining = stack.Cards[0].Lifetime.RemainingSeconds;
                Pump(); Pump(); Check(Math.Abs(stack.Cards[0].Lifetime.RemainingSeconds - disabledRemaining) < .15, "disabled Revit owner pauses actual stack timer during modal report");
                EnableWindow(handle, true);
                NotificationStack.Show(handle, error); NotificationStack.Show(handle, warning); Pump();
                BottomAnchor(stack);
                Check(stack.Top<initialTop, "adding notices grows stack upward from fixed bottom");
                Check(stack.Cards.Count == 3 && stack.Cards.Last().Message == warning, "newest at bottom in one owner stack");
                Check(Math.Abs(stack.Cards[0].Margin.Bottom - 12) < .1, "twelve DIP spacing between cards");
                Save(stack, "08-stack");
                for (int i = 0; i < 8; i++) NotificationStack.Show(handle, success); Pump();
                var viewport = (ScrollViewer)stack.Content;
                Check(viewport.ScrollableHeight > 0 && stack.Cards.Count == 11, "overflow queues all notices in bounded scrolling stack");
                Check(Math.Abs(viewport.VerticalOffset-viewport.ScrollableHeight)<1, "overflow opens at newest bottom notices");
                var oldest = stack.Cards.First(); double remaining = oldest.Lifetime.RemainingSeconds;
                for (int i = 0; i < 3; i++) { Pump(); System.Threading.Thread.Sleep(100); }
                Check(Math.Abs(oldest.Lifetime.RemainingSeconds - remaining) < .15, "offscreen queued notification does not expire");
                Save(stack, "09-overflow");
                var first = stack.Cards[0]; Descendants<Button>(first).First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
                Check(stack.Cards.Count == 10 && !stack.Cards.Contains(first), "close removes only selected notice");
                BottomAnchor(stack);
                viewport.ScrollToTop(); Pump();
                var reader=Descendants<TextBox>(stack.Cards.First()).First(); reader.Focus(); viewport.ScrollToTop(); Pump();
                double readingOffset=viewport.VerticalOffset;
                NotificationStack.Show(handle,success); Pump();
                Check(reader.IsKeyboardFocusWithin&&Math.Abs(viewport.VerticalOffset-readingOffset)<1, "adding bottom notice preserves focused reader and current scroll position");
                NotificationStack.CloseAll(); Pump(); Check(!stack.IsVisible, "close all releases stack window");
                var expiry = NotificationStack.Show(handle, success); Pump();
                var finish = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromSeconds(11) }; var wait = new DispatcherFrame();
                finish.Tick += (s,e) => { finish.Stop(); wait.Continue = false; }; finish.Start(); Dispatcher.PushFrame(wait);
                Check(!expiry.IsVisible, "actual DispatcherTimer closes unread card after ten active seconds"); owner.Close();
                results.Add("COMPLETED"); return 0;
            }
            catch (Exception ex) { results.Add("FAIL " + ex); return 1; }
            finally { NotificationStack.CloseAll(); File.WriteAllLines(Path.Combine(directory, "verification.txt"), results); Application.Current.Shutdown(); }
        }
        private static void Adapter()
        {
            var assembly = typeof(NotificationCard).Assembly; var issue = assembly.GetType("SAB.ParameterTools.Issue");
            var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(issue));
            var item = Activator.CreateInstance(issue, true); issue.GetProperty("Parameter").SetValue(item, "SAB_Материал_Описание");
            issue.GetProperty("Reason").SetValue(item, "Параметр доступен только для чтения.");
            issue.GetProperty("Technical").SetValue(item, "TransactionStatus: RolledBack");
            issue.GetProperty("ElementIds").SetValue(item, new long[] { 482015, 482016, 482015 }); list.Add(item);
            var result = (NotificationMessage)assembly.GetType("SAB.ParameterTools.UI.ParameterNotifications").GetMethod("Result", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { "Фактические счётчики", list, false, "Заполнение параметров" });
            Check(result.ElementIds == "482015, 482016", "module adapter preserves all structured IDs and deduplicates");
            Check(result.Tone == NotificationTone.Error && result.Outcome == "Операция отменена", "module adapter maps rolled back transaction to cancellation");
            Check(result.Support.Contains("SAB_Материал_Описание") && result.Support.Contains("TransactionStatus: RolledBack") && result.Support.Contains("Параметр доступен только для чтения."), "module adapter retains parameter reason and complete technical context");
        }
        private static Window CardWindow(NotificationCard card, double width = 416) => new Window { Title = "SAB · notification fixture", Width = width, SizeToContent = SizeToContent.Height, WindowStyle = WindowStyle.None,
            AllowsTransparency = true, Background = Brushes.Transparent, ResizeMode = ResizeMode.NoResize, Content = card, ShowInTaskbar = false };
        private static void CaptureCard(string name, NotificationCard card) { var window = CardWindow(card); window.Show(); Pump(); Save(window, name); window.Close(); }
        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is T typed) yield return typed; foreach (var nested in Descendants<T>(child)) yield return nested; } }
        private static void Save(Window window, string name, double scale = 1)
        {
            results.Add("CAPTURE " + name + "; runtimeDpi=" + VisualTreeHelper.GetDpi(window).PixelsPerInchX + "; renderDpi=" + (96 * scale));
            window.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth * scale), (int)Math.Ceiling(window.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(directory, name + ".png"))) encoder.Save(file);
        }
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left,Top,Right,Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor,Work; public uint Flags; }
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle,out Rect rect);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr handle,uint flags);
        [DllImport("user32.dll",CharSet=CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr handle,ref MonitorInfo info);
        static void BottomAnchor(Window stack)
        {
            var handle=new WindowInteropHelper(stack).Handle;var info=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};
            Check(GetWindowRect(handle,out var bounds)&&GetMonitorInfo(MonitorFromWindow(handle,2),ref info),"native notification/work-area bounds available");
            double inset=16*VisualTreeHelper.GetDpi(stack).DpiScaleX;
            Check(Math.Abs(info.Work.Bottom-bounds.Bottom-inset)<=2&&Math.Abs(info.Work.Right-bounds.Right-inset)<=2,"notification stack anchored sixteen DIP from bottom-right work area");
        }
        [DllImport("user32.dll")] private static extern bool EnableWindow(IntPtr window, bool enabled);
    }
}
