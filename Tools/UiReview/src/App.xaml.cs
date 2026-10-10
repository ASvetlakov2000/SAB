using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sab.UiLab;

namespace Sab.UiReview
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += (s, failure) => { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), failure.Exception.ToString()); failure.Handled = true; Shutdown(1); };
            var window = new ReviewWindow(e.Args.Contains("--parameters") ? 2 : e.Args.Contains("--sheets") ? 1 : 0); MainWindow = window; window.Show();
            int capture = Array.IndexOf(e.Args, "--capture");
            if (capture >= 0) window.Dispatcher.BeginInvoke(new Action(async () => {
                string output = Path.GetFullPath(e.Args[capture + 1]); Directory.CreateDirectory(output);
                try { await Verify(window, output); Shutdown(0); }
                catch (Exception error) { File.WriteAllText(Path.Combine(output, "failure.log"), error.ToString()); Shutdown(1); }
            }), DispatcherPriority.ApplicationIdle);
        }
        static async Task Flush(ReviewWindow window) { await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle); await Task.Delay(220); }
        static void Capture(ReviewWindow window, string path, double dpi = 96)
        {
            var surface = window.Surface; var bitmap = new RenderTargetBitmap((int)Math.Ceiling(surface.ActualWidth * dpi / 96), (int)Math.Ceiling(surface.ActualHeight * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(surface); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(path)) encoder.Save(stream);
        }
        static void Check(bool value, string name, List<string> log) { if (!value) throw new InvalidOperationException(name); log.Add("PASS " + name); }
        static async Task Verify(ReviewWindow window, string output)
        {
            Motion.Enabled = false; Motion.Stop(window.Surface); var log = new List<string>();
            string[] names = { "elevations", "views-and-sheets", "parameters" };
            for (int i = 0; i < 3; i++)
            {
                window.Switch(i); window.Width = 1400; window.Height = 900; await Flush(window); Capture(window, Path.Combine(output, names[i] + ".png"));
                Check(window.Validate() == null, names[i] + " demo validates", log);
                window.Width = 1100; window.Height = 700; await Flush(window); Capture(window, Path.Combine(output, names[i] + "-min.png"));
                var meta = ReviewWindow.Descendants<TextBlock>(window.Surface).First(t => t.Text.Contains(i == 2 ? "Пример для помещения" : "Масштаб 1:"));
                var previewBorder = Parent<Border>(meta); var bounds = meta.TransformToAncestor(previewBorder).TransformBounds(new Rect(meta.RenderSize));
                Check(bounds.Bottom <= previewBorder.ActualHeight - previewBorder.Padding.Bottom + 1, names[i] + " compact preview fully contained", log);
                var primary = ReviewWindow.Descendants<Wpf.Ui.Controls.Button>(window.Surface).First(b => System.Windows.Automation.AutomationProperties.GetName(b) == (i == 2 ? "Тест сохранения" : "Тест создания"));
                Check(((SolidColorBrush)((TextBlock)primary.Content).Foreground).Color == Colors.White, names[i] + " primary label is white", log);
            }
            window.Width = 1400; window.Height = 900;
            window.Switch(0); await Flush(window);
            for (int i = 1; i < 4; i++) { window.SelectSection(i); await Flush(window); Capture(window, Path.Combine(output, "elevations-section-" + i + ".png")); }
            window.SelectSection(0); await Flush(window);
            var input = ReviewWindow.Descendants<TextBox>(window.Surface).First(t => System.Windows.Automation.AutomationProperties.GetName(t) == "Глубина, мм"); input.Focus(); await Flush(window);
            var frame = (Border)input.Template.FindName("Root", input);
            Check(frame != null && frame.CornerRadius.TopLeft == 5 && frame.BorderThickness.Left == 1, "Single rounded field border", log);
            Check(((SolidColorBrush)frame.BorderBrush).Color == (Color)ColorConverter.ConvertFromString("#0F6CBD"), "Focus uses one blue border", log);
            Capture(window, Path.Combine(output, "focus-single-border.png"));
            input.Text = "abc"; await Flush(window); Check(Validation.GetHasError(input), "Numeric input marks invalid value", log); Check(window.Validate() != null, "Invalid numeric input blocks test", log); Capture(window, Path.Combine(output, "field-validation.png"));
            window.SelectSection(1); await Flush(window); Check(window.Validate()?.Contains("Глубина") == true, "Hidden cached numeric error still blocks test and names field", log); input.Text = "1500"; window.SelectSection(0); await Flush(window);
            foreach (var scroller in ReviewWindow.Descendants<ScrollViewer>(window.Surface).Where(s => s.Style == Current.FindResource("LabFormScrollViewerStyle")))
            { var bar = scroller.Template.FindName("PART_VerticalScrollBar", scroller) as FrameworkElement; Check(bar != null && bar.Margin.Left >= 12, "Form scrollbar has separate 12-DIP gutter", log); }
            window.Switch(1); await Flush(window); for (int i = 1; i < 4; i++) { window.SelectSection(i); await Flush(window); Capture(window, Path.Combine(output, "sheets-section-" + i + ".png")); }
            window.Data.Sheets[0].Scale = "bad"; Check(window.Validate() != null, "Invalid table scale blocks test", log); window.Data.Sheets[0].Scale = "100";
            window.Data.Sheets[1].Number = window.Data.Sheets[0].Number; Check(window.Validate() != null, "Duplicate sheet numbers block test", log); window.Data.Sheets[1].Number = "АР-02";
            window.Switch(2); await Flush(window); for (int i = 1; i < 4; i++) { window.SetParameterSection(i); await Flush(window); Capture(window, Path.Combine(output, "parameters-section-" + i + ".png")); }
            window.SetParameterSection(0); await Flush(window); Capture(window, Path.Combine(output, "parameters-192dpi.png"), 192);
            var roomParameter = ReviewWindow.Descendants<ComboBox>(window.Surface).First(c => System.Windows.Automation.AutomationProperties.GetName(c) == "Параметр помещения"); roomParameter.SelectedItem = "Назначение";
            window.ActiveGrid.SelectedIndex = 1; window.ActiveGrid.SelectedIndex = 0; await Flush(window);
            Check(ReviewWindow.Descendants<ComboBox>(window.Surface).First(c => System.Windows.Automation.AutomationProperties.GetName(c) == "Параметр помещения").SelectedItem.ToString() == "Назначение", "Room parameter survives source editor reconstruction", log);
            Check(ReviewWindow.Descendants<TextBlock>(window.Surface).Any(t => t.Text == "SAB_Зона = Общее помещение"), "Preview derives value from selected room parameter", log);
            var source = ReviewWindow.Descendants<ComboBox>(window.Surface).First(c => System.Windows.Automation.AutomationProperties.GetName(c) == "Откуда взять значение"); source.SelectedItem = "Из модели: параметр"; await Flush(window);
            var elementParameter = ReviewWindow.Descendants<ComboBox>(window.Surface).First(c => System.Windows.Automation.AutomationProperties.GetName(c) == "Исходный параметр элемента"); elementParameter.SelectedItem = "Марка";
            window.ActiveGrid.SelectedIndex = 1; window.ActiveGrid.SelectedIndex = 0; await Flush(window);
            Check(ReviewWindow.Descendants<ComboBox>(window.Surface).First(c => System.Windows.Automation.AutomationProperties.GetName(c) == "Исходный параметр элемента").SelectedItem.ToString() == "Марка", "Element parameter survives source editor reconstruction", log);
            Check(ReviewWindow.Descendants<TextBlock>(window.Surface).Any(t => t.Text == "SAB_Зона = Д-01"), "Preview derives value from selected element parameter", log);
            window.Data.Rules[0].Source = "Из помещения"; window.Data.Rules[0].RoomParameter = "Корпус"; await Flush(window);
            var original = window.Data.Rules[0].Name; window.Data.Rules[0].Name = "SAB_Изменено"; window.Switch(0); window.Switch(2); await Flush(window); Check(window.Data.Rules[0].Name == "SAB_Изменено", "Edited state survives instrument switch", log); window.Data.Rules[0].Name = original;
            for (int i = 0; i < 2000; i++) window.Data.Rules.Add(new ReviewRow { Name = "Параметр " + i, Source = "Постоянное значение" }); await Flush(window);
            int realized = ReviewWindow.Descendants<DataGridRow>(window.Surface).Count(); Check(realized > 0 && realized < 100, "2006 rules virtualized: " + realized + " rows realized", log);
            while (window.Data.Rules.Count > 6) window.Data.Rules.RemoveAt(6);
            Motion.Enabled = true; for (int i = 0; i < 8; i++) window.Switch(i % 3); Motion.Enabled = false; Motion.Stop(window.Surface); await Flush(window); Check(window.Surface.Opacity == 1, "Interrupted motion resets to visible base state", log);
            log.AddRange(window.Timings); log.Add("192 DPI bitmap rendering only; no real monitor-DPI transition or Revit execution verified."); File.WriteAllLines(Path.Combine(output, "verification.txt"), log); string failed = Path.Combine(output, "failure.log"); if (File.Exists(failed)) File.Delete(failed);
        }
        static T Parent<T>(DependencyObject node) where T : DependencyObject { while (node != null) { node = VisualTreeHelper.GetParent(node); if (node is T result) return result; } return null; }
    }
}
