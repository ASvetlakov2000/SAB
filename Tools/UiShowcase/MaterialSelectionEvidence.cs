using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SAB.FilledRegionFromMaterial;

namespace SAB.UiShowcase
{
    internal static class MaterialSelectionEvidence
    {
        private static readonly List<string> Log = new List<string>();
        private static string _output;
        private static int _captures;

        internal static int Run(string output)
        {
            _output = output; Directory.CreateDirectory(output);
            Log.Add("Actual SAB.dll MaterialSelectionWindow; detached synthetic rows; clean Application resources; native WPF outside Revit.");
            Log.Add("Revit operations, physical input, OS DPI and model transactions: NOT RUN.");
            var rows = Fixture();
            var window = new MaterialSelectionWindow(rows) { Title = "SAB · Выбор материалов · тестовые данные" };
            try
            {
                window.Show(); Pump();
                var grid = Find<DataGrid>(window, "MaterialsGrid");
                var search = Find<TextBox>(window, "SearchBox");
                Check("exact four columns and no class", string.Join("|", grid.Columns.Select(c => c.Header)) == "В работу|Материал|Штриховка сечения|Действие");
                Check("all document fixture rows present", grid.Items.Count == rows.Count);
                Check("initially no material included and execution disabled", rows.All(r => !r.Included) && !Find<Button>(window, "RunButton").IsEnabled);
                Check("native Extended FullRow selection", grid.SelectionMode == DataGridSelectionMode.Extended && grid.SelectionUnit == DataGridSelectionUnit.FullRow);
                Check("shared row44 header38", grid.RowHeight == 44 && Descendants(grid).OfType<DataGridColumnHeader>().First(h => h.Content != null).ActualHeight == 38);
                CheckColors(window, rows, false); Capture(window, "normal");

                grid.SelectedItems.Add(rows[0]); grid.SelectedItems.Add(rows[1]); grid.SelectedItems.Add(rows[2]); Pump();
                Click(window, "EnableButton"); Pump();
                Check("enable highlighted package", rows.Take(3).All(r => r.Included) && rows.Skip(3).All(r => !r.Included));
                CheckColors(window, rows, true); Capture(window, "selected-colours");

                grid.ScrollIntoView(rows[0]); Pump();
                var firstRow = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(rows[0]);
                var toggle = Descendants(firstRow).OfType<ToggleButton>().First();
                ((IToggleProvider)new ToggleButtonAutomationPeer(toggle).GetPattern(PatternInterface.Toggle)).Toggle(); Pump();
                Check("automation toggle applies entire highlighted package", rows.Take(3).All(r => !r.Included));
                firstRow = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(rows[0]);
                toggle = Descendants(firstRow).OfType<ToggleButton>().First();
                var mouseArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent };
                toggle.RaiseEvent(mouseArgs); Pump();
                Check("routed mouse toggle preserves package", rows.Take(3).All(r => r.Included) && grid.SelectedItems.Count == 3 && mouseArgs.Handled);

                Find<RadioButton>(window, "IncludedFilter").IsChecked = true; Pump();
                Check("included filter has three rows", grid.Items.Count == 3); Capture(window, "included-only");
                Click(window, "SelectAllButton"); Click(window, "DisableButton"); Pump();
                Check("snapshot disable removes all filtered included rows", grid.Items.Count == 0 && rows.All(r => !r.Included));
                Find<RadioButton>(window, "ExcludedFilter").IsChecked = true; Pump();
                Check("excluded filter contains entire catalogue", grid.Items.Count == rows.Count);

                search.Text = rows[0].Name; Pump(); Click(window, "SelectAllButton");
                Check("Select All only visible search rows", grid.SelectedItems.Count == 1);
                Click(window, "EnableButton"); Pump();
                Check("enable snapshot removes row from excluded filter", grid.Items.Count == 0 && rows[0].Included);
                Find<RadioButton>(window, "AllFilter").IsChecked = true; search.Text = "ШТРИХОВКИ"; Pump();
                Check("case-insensitive search by material name", grid.Items.Count == 1);
                Check("hidden inclusion retained in counts", rows[0].Included && Find<TextBlock>(window, "CountsText").Text.Contains("Скрыто выбранных: 1"));
                Capture(window, "search-hidden-selection");
                search.Text = "Нет такого материала"; Pump();
                Check("empty search keeps hidden included material executable", grid.Items.Count == 0 && Find<Button>(window, "RunButton").IsEnabled);
                Check("empty state rendered", Find<TextBlock>(window, "EmptyText").IsVisible); Capture(window, "empty-search");

                rows[0].Included = false; rows[2].Included = true; search.Clear(); Pump();
                Check("only missing hatch disables run", !Find<Button>(window, "RunButton").IsEnabled);
                Check("missing hatch red wording", rows[2].ActionText == "Нет штриховки" && rows[2].WillSkip);
                Capture(window, "no-cut-pattern");
                rows[2].Included = false; Pump();
                grid.SelectedItems.Add(rows[0]); grid.SelectedItems.Add(rows[1]);
                var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(grid), Environment.TickCount, Key.Space) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                grid.RaiseEvent(key); Pump();
                Check("routed Space includes highlighted package", key.Handled && rows[0].Included && rows[1].Included);

                window.Width = window.MinWidth; window.Height = window.MinHeight; Pump();
                Capture(window, "minimum");
                var run = Find<Button>(window, "RunButton");
                Check("minimum footer within client", Bounds(run, window).Bottom <= window.ActualHeight && run.IsVisible);
                var viewer = Descendants(grid).OfType<ScrollViewer>().First(v => v.Name == "DG_ScrollViewer");
                Check("normal columns fit minimum width", viewer.ScrollableWidth < 1);
                var bar = Descendants(grid).OfType<ScrollBar>().First(b => b.Orientation == Orientation.Vertical);
                var header = Descendants(grid).OfType<DataGridColumnHeadersPresenter>().First();
                Check("scrollbar below header", Bounds(bar, grid).Top >= Bounds(header, grid).Bottom - 1);
                grid.ScrollIntoView(rows.Last()); Pump(); Capture(window, "minimum-last-row");
                Check("last long material row reachable", grid.ItemContainerGenerator.ContainerFromItem(rows.Last()) is DataGridRow);

                int count = rows.Count(r => r.Included);
                window.Close(); Pump();
                Check("close returns no selection result", window.SelectedMaterialUniqueIds == null && count == 2);

                var confirmRows = Fixture().Take(3).ToList(); confirmRows[0].Included = confirmRows[2].Included = true;
                var confirmation = new MaterialSelectionWindow(confirmRows);
                confirmation.Dispatcher.BeginInvoke(new Action(() => Click(confirmation, "RunButton")), DispatcherPriority.ApplicationIdle);
                Check("modal confirmation succeeds", confirmation.ShowDialog() == true);
                Check("confirmation contains only included stable IDs including explicit skip", confirmation.SelectedMaterialUniqueIds.SequenceEqual(new[] { confirmRows[0].UniqueId, confirmRows[2].UniqueId }));

                var cancel = new MaterialSelectionWindow(Fixture());
                cancel.Dispatcher.BeginInvoke(new Action(() => Click(cancel, "CancelButton")), DispatcherPriority.ApplicationIdle);
                Check("modal cancellation has no IDs", cancel.ShowDialog() != true && cancel.SelectedMaterialUniqueIds == null);

                var legacy = new MaterialSelectionItem("legacy", "Нет штриховки", "—", false, false, false, "SAB_Пустой");
                Check("skip-disabled legacy behaviour preserved", !legacy.WillSkip && legacy.ActionDescription.Contains("пропуск выключен"));
                var empty = new MaterialSelectionWindow(new MaterialSelectionItem[0]); empty.Show(); Pump(); Capture(empty, "empty-document");
                Check("empty catalogue disables all selection/execution", !Find<Button>(empty, "SelectAllButton").IsEnabled && !Find<Button>(empty, "RunButton").IsEnabled); empty.Close();
                Log.Add("COMPLETED; assertions=" + Log.Count(s => s.StartsWith("PASS")) + "; captures=" + _captures);
                return 0;
            }
            catch (Exception error) { Log.Add("FAIL " + error); return 1; }
            finally { window.Close(); File.WriteAllLines(Path.Combine(output, "verification.txt"), Log); Application.Current.Shutdown(); }
        }

        private static List<MaterialSelectionItem> Fixture()
        {
            var result = new List<MaterialSelectionItem>();
            for (int i = 0; i < 80; i++)
            {
                string name = i == 0 ? "Тест 01 · Бетон В25" : i == 1 ? "Тест 02 · Кирпич керамический" : i == 2 ? "Тест 03 · Материал без штриховки" : "Тест " + (i + 1).ToString("00") + " · Длинное название материала для отделки помещений и проверки горизонтальной прокрутки";
                result.Add(new MaterialSelectionItem("fixture-" + i, name, i == 2 ? "—" : i % 2 == 0 ? "Бетон / Сплошная заливка" : "Кирпичная кладка", i != 2, i % 2 != 0, true, "SAB_" + name));
            }
            return result;
        }

        private static void CheckColors(MaterialSelectionWindow window, IList<MaterialSelectionItem> rows, bool selected)
        {
            var grid = Find<DataGrid>(window, "MaterialsGrid");
            string[] expected = { "#FF0F6CBD", "#FF067647", "#FFB42318" };
            for (int i = 0; i < 3; i++)
            {
                grid.ScrollIntoView(rows[i]); Pump();
                var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(rows[i]);
                var text = Descendants(row).OfType<TextBlock>().First(t => t.Text == rows[i].Name);
                var action = Descendants(row).OfType<TextBlock>().First(t => t.Text == rows[i].ActionText);
                Check((selected ? "selected" : "normal") + " material text black " + i, text.Foreground.ToString() == "#FF000000");
                Check((selected ? "selected" : "normal") + " action colour " + i, action.Foreground.ToString() == expected[i]);
            }
        }

        private static T Find<T>(Window w, string name) where T : FrameworkElement { return (T)w.FindName(name); }
        private static void Click(Window w, string name) { Find<Button>(w, name).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); }
        private static void Check(string name, bool pass) { Log.Add((pass ? "PASS " : "FAIL ") + name); if (!pass) throw new InvalidOperationException(name); }
        private static void Pump()
        {
            var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            System.Threading.Thread.Sleep(200);
            var settled = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => settled.Continue = false)); Dispatcher.PushFrame(settled);
        }
        private static Rect Bounds(FrameworkElement child, Visual ancestor) { return new Rect(child.TranslatePoint(new Point(), (UIElement)ancestor), new Size(child.ActualWidth, child.ActualHeight)); }
        private static IEnumerable<DependencyObject> Descendants(DependencyObject d)
        { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++) { var c = VisualTreeHelper.GetChild(d, i); yield return c; foreach (var x in Descendants(c)) yield return x; } }
        private static void Capture(Window window, string name)
        {
            Pump(); var content = (FrameworkElement)window.Content;
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(content); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(_output, name + ".png"))) encoder.Save(file);
            _captures++;
        }
    }
}
