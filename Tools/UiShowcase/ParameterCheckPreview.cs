using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using SAB.ParameterTools;
using SAB.ParameterTools.Core;
using SAB.ParameterTools.UI;
using SAB.UI.FluentLight;
using SAB.UI.Notifications;
using Point = System.Windows.Point;
using Profile = SAB.ParameterTools.Core.Profile;

namespace SAB.UiShowcase
{
    internal static class ParameterCheckPreview
    {
        static readonly List<string> log = new List<string>();
        static string output;
        static readonly Assembly plugin = typeof(ParameterPicker).Assembly;
        static IEnumerable<T> Visual<T>(DependencyObject root) where T : DependencyObject => Elevations.ElevationPreview.Visual<T>(root);
        static void Pump() => Elevations.ElevationPreview.Pump(160);
        static void Check(bool valid, string message) { log.Add((valid ? "PASS " : "FAIL ") + message); if (!valid) throw new InvalidOperationException(message); }
        static Window Create(string name, params object[] arguments) => (Window)Activator.CreateInstance(plugin.GetType("SAB.ParameterTools.UI." + name), BindingFlags.Instance | BindingFlags.NonPublic, null, arguments, null);
        static Button FindButton(Window window, string text) => Visual<Button>(window).Single(b => b.IsVisible && Equals(b.Content, text));
        static void Click(Window window, string text) => FindButton(window, text).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        static void Show(Window window) { window.ShowInTaskbar = false; window.Show(); Pump(); window.UpdateLayout(); }
        static void Shot(Window window, string name)
        {
            var body = (FrameworkElement)window.Content;
            var image = new RenderTargetBitmap((int)Math.Ceiling(body.ActualWidth), (int)Math.Ceiling(body.ActualHeight), 96, 96, PixelFormats.Pbgra32); image.Render(body);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using (var file = File.Create(Path.Combine(output, name + ".png"))) encoder.Save(file);
            log.Add("CAPTURE " + name + " | " + body.ActualWidth + "x" + body.ActualHeight + " DIP | nativeDpi=" + VisualTreeHelper.GetDpi(body).PixelsPerInchX + " | actualType=" + window.GetType().FullName + " | actualAssembly=" + window.GetType().Assembly.GetName().Name);
        }
        static void SendKey(ComboBox combo, Key key) => combo.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(combo), Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        static void Input(TextBox text, string query)
        {
            text.Focus();
            text.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, text, query)) { RoutedEvent = TextCompositionManager.PreviewTextInputEvent });
            text.Text = query;
        }
        static List<CategoryChoice> Categories() => Enumerable.Range(1, 120).Select(i => new CategoryChoice { Id = -2000000 - i, Name = i == 1 ? "Стены" : "Категория " + i.ToString("000") + " · Длинное название категории элементов модели", Selected = i <= 7 }).ToList();
        static List<CategoryChoice> CheckCategories(List<CategoryChoice> categories, params int[] selected)
        {
            var method = plugin.GetType("SAB.ParameterTools.Catalog").GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(m => m.Name == "CheckCategories" && m.GetParameters()[0].ParameterType == typeof(IEnumerable<CategoryChoice>));
            return (List<CategoryChoice>)method.Invoke(null, new object[] { categories, selected });
        }
        static void Footer(Window window)
        {
            var info = Visual<Border>(window).Single(b => b.Name == "CheckDescriptionBorder");
            Check(Visual<ListBox>(window).First().ActualHeight >= 90, "Category list retains usable height at minimum and with validation");
            var cancel = FindButton(window, "Отмена"); var run = FindButton(window, "Проверить"); var help = FindButton(window, "Инструкция");
            double y = cancel.TranslatePoint(new Point(), window).Y;
            Check(info.IsVisible && info.Background != null && info.Background != Brushes.Transparent, "Requested explanatory text has its own filled frame");
            Check(info.TranslatePoint(new Point(0, info.ActualHeight), window).Y + 7 <= y, "Explanatory frame stays above action row with gap");
            Check(Math.Abs(y - run.TranslatePoint(new Point(), window).Y) <= 1, "Cancel and Check share one axis");
            Check(Math.Abs(y - help.TranslatePoint(new Point(), window).Y) <= 1, "Instruction shares footer action row");
            foreach (var button in new[] { help, cancel, run })
            {
                var at = button.TranslatePoint(new Point(), window);
                Check(at.X >= 0 && at.Y >= 0 && at.X + button.ActualWidth <= window.ActualWidth && at.Y + button.ActualHeight <= window.ActualHeight, "Footer button remains inside viewport: " + button.Content);
            }
            Check(SAB.UI.FluentLight.Instructions.GetFile(help) == "SAB_HTML_Zapolnenie_parametrov.html", "Instruction links to installed parameterization HTML");
            Check(!Visual<Button>(window).Any(b => (b.Content as string ?? "").IndexOf("Отчёт", StringComparison.OrdinalIgnoreCase) >= 0), "No highlighting report button in settings");
        }
        internal static int Run(string directory)
        {
            output = directory; Directory.CreateDirectory(output); log.Clear();
            try
            {
                Check(Application.Current.Resources.MergedDictionaries.Count == 0, "Clean Application without global host dictionaries");
                var refs = new List<ParameterRef> {
                    new ParameterRef { Id = 101, Name = "KS_Номер этажа", SharedGuid = Guid.NewGuid().ToString(), DataType = "Число", VariesAcrossGroups = false },
                    new ParameterRef { Id = 102, Name = "KS_Номер этажа", SharedGuid = Guid.NewGuid().ToString(), DataType = "Текст", VariesAcrossGroups = true },
                    new ParameterRef { Id = 103, Name = "Уникальный параметр помещения", SharedGuid = Guid.NewGuid().ToString(), DataType = "Текст" }
                };
                var profile = new Profile { Configured = true, CategoryIds = new List<int> { -2000011 } };
                profile.Rules.Add(new Rule { EntityName = "Помещение", Group = ParameterGroup.Room, Source = RuleValueSource.Room, RoomField = RoomField.Parameter, Target = refs[0], RoomParameter = refs[1] });
                var window = Create("SettingsWindow", profile, refs, refs, Categories(), new List<LevelMapping>(), refs);
                Show(window); var tabs = Visual<TabControl>(window).First();
                foreach (int cycle in Enumerable.Range(0, 3))
                {
                    tabs.SelectedIndex = 0; Pump(); tabs.SelectedIndex = 1; Pump();
                    var sourcePickers = Visual<ParameterPicker>(window).Where(p => p.IsVisible).ToList();
                    Check(sourcePickers.Count > 0 && sourcePickers.All(p => !Visual<ComboBox>(p).First().IsDropDownOpen), "Sources tab load/reload does not open any parameter popup: " + cycle);
                    Check(sourcePickers.Any(p => p.SelectedParameter?.SharedGuid == refs[1].SharedGuid), "Sources tab preserves selected duplicate-name GUID: " + cycle);
                }
                Shot(window, "sources-closed-after-load"); window.Width = window.MinWidth; window.Height = window.MinHeight; Pump(); Shot(window, "sources-minimum"); window.Close();

                var first = new ParameterPicker { Parameters = refs, SelectedParameter = refs[1] };
                var second = new ParameterPicker { Parameters = refs, SelectedParameter = refs[0] };
                var body = new StackPanel { Margin = new Thickness(24), Background = (Brush)FluentResources.Find("SabBrush.PanelBackground") }; body.Children.Add(first); body.Children.Add(second);
                var ground = new System.Windows.Controls.Grid { Background = (Brush)FluentResources.Find("SabBrush.WindowBackground") }; ground.Children.Add(body);
                var pickerWindow = new Window { Title = "Actual ParameterPicker interaction fixture", Content = ground, Width = 680, Height = 430 };
                pickerWindow.Resources.MergedDictionaries.Add(FluentResources.CreateDictionary()); Show(pickerWindow);
                var combo = Visual<ComboBox>(first).First(); var text = (TextBox)combo.Template.FindName("PART_EditableTextBox", combo);
                Check(!combo.IsDropDownOpen && ReferenceEquals(first.SelectedParameter, refs[1]), "Standalone picker programmatic construction stays closed with selection");
                text.Focus(); text.Text = "Служебное обновление"; Pump();
                Check(!combo.IsDropDownOpen && ReferenceEquals(first.SelectedParameter, refs[1]), "Focused programmatic text change is not treated as user search");
                typeof(ParameterPicker).GetMethod("Restore", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(first, null);
                Input(text, "Уникальный"); Pump();
                Check(combo.IsDropDownOpen && combo.Items.Count == 1 && ReferenceEquals(combo.Items[0], refs[2]), "Explicit routed text input opens filtered unique match");
                Check(Visual<ComboBox>(second).First().Items.Count == 3 && ReferenceEquals(second.SelectedParameter, refs[0]), "Search remains isolated from second picker");
                SendKey(combo, Key.Escape); Pump();
                Check(!combo.IsDropDownOpen && ReferenceEquals(first.SelectedParameter, refs[1]) && pickerWindow.IsVisible, "Escape cancels search and retains duplicate-name identity");
                Input(text, "Уникальный"); Pump(); SendKey(combo, Key.Enter); Pump();
                Check(!combo.IsDropDownOpen && ReferenceEquals(first.SelectedParameter, refs[2]), "Enter commits sole filtered parameter by identity");
                Shot(pickerWindow, "picker-after-selection");
                combo.IsDropDownOpen = true; Pump();
                Check(combo.IsDropDownOpen && combo.Items.Count == 3, "Explicit dropdown remains available after search");
                var popup=(Popup)combo.Template.FindName("PART_Popup",combo);var popupBody=(FrameworkElement)popup.Child;popupBody.UpdateLayout();
                Check(popup.IsOpen && popupBody.IsVisible && Visual<TextBlock>(popupBody).Any(t=>t.IsVisible && t.Text==refs[0].Name),"Explicit popup contains visible actual parameter names");
                var popupImage=new RenderTargetBitmap((int)Math.Ceiling(popupBody.ActualWidth),(int)Math.Ceiling(popupBody.ActualHeight),96,96,PixelFormats.Pbgra32);popupImage.Render(popupBody);
                var popupEncoder=new PngBitmapEncoder();popupEncoder.Frames.Add(BitmapFrame.Create(popupImage));using(var file=File.Create(Path.Combine(output,"picker-explicit-popup.png")))popupEncoder.Save(file);
                log.Add("CAPTURE picker-explicit-popup | nativeDpi="+VisualTreeHelper.GetDpi(popupBody).PixelsPerInchX+" | actualOwner=SAB.ParameterTools.UI.ParameterPicker | actualAssembly=SAB | actualPART_Popup.Child");
                combo.IsDropDownOpen = false; pickerWindow.Close();

                var catalog = Categories(); var selectedIds = new[] { catalog[87].Id, catalog[119].Id };
                var choices = CheckCategories(catalog, selectedIds);
                Check(choices.Select(c => c.Id).SequenceEqual(catalog.Select(c => c.Id)), "Check scope contains exact full parameterization category catalog120");
                Check(choices.Where(c => c.Selected).Select(c => c.Id).SequenceEqual(selectedIds), "Saved check selection independent from seven filling categories");
                choices[0].Selected = true;
                Check(!ReferenceEquals(choices[0], catalog[0]) && catalog.Count(c => c.Selected) == 7, "Check choices are detached copies and do not mutate fill selection");
                var defaults = CheckCategories(new List<CategoryChoice> { new CategoryChoice { Id = (int)BuiltInCategory.OST_Walls, Name = "Стены" }, new CategoryChoice { Id = -2000032, Name = "Перекрытия", Selected = true } });
                Check(defaults[0].Selected && !defaults[1].Selected, "First check preserves incumbent default Walls independently of filling");
                foreach (bool many in new[] { true, false })
                {
                    var values = many ? choices : new List<CategoryChoice>();
                    var settings = Create("CheckCategoriesWindow", values); Show(settings); Footer(settings); Shot(settings, many ? "check-settings-normal" : "check-settings-empty");
                    settings.Width = settings.MinWidth; settings.Height = settings.MinHeight; Pump(); Footer(settings); Shot(settings, many ? "check-settings-minimum" : "check-settings-empty-minimum");
                    var list = Visual<ListBox>(settings).First();
                    Check(list.Items.Count == values.Count, "Check category viewer retains full list without eight-category cap");
                    if (many) {
                        list.ScrollIntoView(list.Items[list.Items.Count-1]); Pump();
                        var last=(ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(list.Items.Count-1);
                        var at=last.TranslatePoint(new Point(),list);
                        Check(last.IsVisible && at.Y>=-1 && at.Y+last.ActualHeight<=list.ActualHeight+1,"Last category remains fully readable at minimum");
                        Shot(settings,"check-settings-minimum-last-row");
                    }
                    var search = Visual<TextBox>(settings).First(); search.Text = "Категория 120"; Pump();
                    Check(list.Items.Count == (many ? 1 : 0), "Full check catalog supports search including last category");
                    Shot(settings, many ? "check-settings-search-last" : "check-settings-empty-search"); search.Text = ""; Pump();
                    foreach (var category in values) category.Selected = false;
                    Click(settings, "Проверить"); Pump();
                    Check(settings.IsVisible && Visual<TextBlock>(settings).Any(t => t.IsVisible && t.Text == "Выберите категории для проверки."), "Empty scope validates without closing settings");
                    Footer(settings); Shot(settings, many ? "check-settings-validation" : "check-settings-empty-validation");
                    Click(settings, "Отмена");
                }
                var accepted = CheckCategories(catalog, selectedIds); var confirm = Create("CheckCategoriesWindow", accepted);
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
                timer.Tick += (s,e) => { timer.Stop(); Click(confirm, "Проверить"); };
                timer.Start(); bool? ok = confirm.ShowDialog();
                var committedIds = (List<int>)confirm.GetType().GetProperty("CategoryIds", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(confirm);
                Check(ok == true && committedIds.SequenceEqual(selectedIds), "Check settings dialog returns exact separate category scope");
                var toast = plugin.GetType("SAB.ParameterTools.UI.Toast").GetMethod("Show", BindingFlags.Static | BindingFlags.NonPublic);
                toast.Invoke(null, new object[] { IntPtr.Zero, "Подсветка элементов активирована", 10 }); Pump();
                var notification = Application.Current.Windows.Cast<Window>().Single(w => w is NotificationStack);
                Check(Visual<TextBox>(notification).Any(t => t.Text == "Подсветка элементов активирована"), "Actual existing toast adapter shows exact requested activation text");
                Check(!Application.Current.Windows.Cast<Window>().Any(w => w.GetType().FullName == "SAB.ParameterTools.UI.ReportWindow"), "Activation adapter does not open report window");
                Shot(notification, "check-activation-notification"); notification.Close();
                Check(Application.Current.Resources.MergedDictionaries.Count == 0, "All parameter surfaces retain zero host dictionaries");
                log.Add("NOT RUN actual Revit Catalog enumeration, commands/hotkeys/ribbon, profile transactions/native filters and model effects; physical input/F4/paste/IME/accessibility; OS DPI.");
                log.Add("COMPLETED"); return 0;
            }
            catch (Exception error) { log.Add("ERROR " + error); return 1; }
            finally { foreach (Window window in Application.Current.Windows.Cast<Window>().ToArray()) window.Close(); File.WriteAllLines(Path.Combine(output, "verification.txt"), log); }
        }
    }
}
