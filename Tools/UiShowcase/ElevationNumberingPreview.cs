using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Settings;
using SAB.InteriorElevations.ViewModels;
using SAB.InteriorElevations.Views;

namespace SAB.UiShowcase
{
    internal static class ElevationNumberingPreview
    {
        static readonly List<string> log = new List<string>();
        static string output;
        static void Check(bool ok, string name)
        {
            log.Add((ok ? "PASS " : "FAIL ") + name);
            File.WriteAllLines(Path.Combine(output, "verification.txt"), log);
            if (!ok) throw new InvalidOperationException(name);
        }
        static T Find<T>(Window window, string name) where T : FrameworkElement => Elevations.ElevationSession.Find<T>(window, name);
        static void Pump() => Elevations.ElevationPreview.Pump(220);
        static void Shot(Window window, string name)
        {
            window.UpdateLayout(); Pump();
            var body = (FrameworkElement)window.Content;
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(body.ActualWidth), (int)Math.Ceiling(body.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(body);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(Path.Combine(output, name + ".png"))) encoder.Save(stream);
            log.Add("CAPTURE " + name + "; actualType=" + window.GetType().FullName + "; assembly=" + window.GetType().Assembly.Location +
                "; sizeDIP=" + body.ActualWidth + "x" + body.ActualHeight + "; nativeDPI=" + VisualTreeHelper.GetDpi(body).PixelsPerInchX + "; renderDPI=96");
        }
        internal static int Run(string directory)
        {
            output = directory; Directory.CreateDirectory(output); log.Clear();
            var vm = RedesignProduction.Settings();
            var window = new ElevationSettingsWindow(vm) { ShowInTaskbar = false };
            // Main window writes a size preference on close. Restore the existing file after this fixture.
            string preferenceFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SAB", "WindowSizes");
            var original = Directory.Exists(preferenceFolder) ? Directory.GetFiles(preferenceFolder, "InteriorElevations*.json").ToDictionary(p => p, File.ReadAllBytes) : new Dictionary<string, byte[]>();
            try
            {
                window.Width = 790; window.Height = 980; window.Show(); Pump();
                var prefix = Find<TextBox>(window, "DetailNumberPrefixEditor");
                var suffix = Find<TextBox>(window, "DetailNumberSuffixEditor");
                var preview = Find<TextBlock>(window, "DetailNumberPreviewText");
                var block = Find<Border>(window, "DetailNumberSettings");
                Check(window.GetType().Assembly == typeof(ElevationSettingsViewModel).Assembly, "Actual SAB.dll window and VM");
                Check(prefix.Text == "" && suffix.Text == "-Р" && preview.Text == "1-Р, 2-Р, 3-Р", "Default affix fields and live example");
                Check(Elevations.ElevationPreview.Visual<GroupBox>(window).Single(g => Equals(g.Header, "Наименование вида")).IsAncestorOf(block), "Number controls stay inside requested naming card");
                Check(block.TranslatePoint(new Point(), window).Y > Find<RichTextBox>(window, "ElevationTitleFormulaEditor").TranslatePoint(new Point(), window).Y, "Number fields follow existing names");
                Check(Math.Abs(prefix.ActualHeight - 34) <= 1 && Math.Abs(suffix.ActualHeight - 34) <= 1, "Shared 34 DIP editors");
                Check(Math.Abs(prefix.ActualWidth - suffix.ActualWidth) <= 1, "Equal field columns");
                Shot(window, "numbering-normal");
                prefix.Text = "ЭЛВ-"; suffix.Text = "-А"; Pump();
                Check(vm.DetailNumberPrefix == "ЭЛВ-" && vm.DetailNumberSuffix == "-А", "Actual TextBox edits update VM immediately");
                Check(preview.Text == "ЭЛВ-1-А, ЭЛВ-2-А, ЭЛВ-3-А", "Live example reflects both affixes");
                prefix.Focus(); Pump(); Check(prefix.IsKeyboardFocused, "Prefix accepts programmatic keyboard focus");
                prefix.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); Pump();
                Check(suffix.IsKeyboardFocused, "Programmatic next focus reaches suffix");
                ElevationSettings settings; string error;
                Check(vm.TryBuildSettings(out settings, out error, false, false), "Actual settings validation accepts fixture: " + error);
                Check(settings.DetailNumberPrefix == "ЭЛВ-" && settings.DetailNumberSuffix == "-А", "VM exports numbering to model");
                var storage = new ElevationSettingsStorageService();
                string isolated = Path.Combine(output, "settings-fixture.json");
                typeof(ElevationSettingsStorageService).GetField("_settingsFilePath", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(storage, isolated);
                storage.SaveSettings(settings); var restored = storage.LoadSettings();
                Check(restored.DetailNumberPrefix == "ЭЛВ-" && restored.DetailNumberSuffix == "-А", "Schema15 JSON persistence round trip");
                var restoredVm = new ElevationSettingsViewModel(null, restored);
                Check(restoredVm.DetailNumberPreview == preview.Text, "Reopened VM restores both numbering affixes");
                File.WriteAllText(isolated, "{\"SchemaVersion\":14}"); var legacy = storage.LoadSettings();
                Check(legacy.DetailNumberPrefix == "" && legacy.DetailNumberSuffix == "-Р", "Schema14 migration supplies numbering defaults");
                prefix.Text = ""; suffix.Text = ""; Pump(); storage.SaveSettings(new ElevationSettings { DetailNumberSuffix = "" });
                Check(preview.Text == "1, 2, 3" && storage.LoadSettings().DetailNumberSuffix == "", "Explicit empty affixes survive persistence");
                Shot(window, "numbering-empty");
                prefix.Text = "Очень длинный префикс развёртки помещения — "; suffix.Text = " — очень длинный суффикс";
                window.Width = window.MinWidth; window.Height = window.MinHeight; Pump(); block.BringIntoView(); Pump();
                DependencyObject parent = block;
                while (parent != null && !(parent is ScrollViewer)) parent = VisualTreeHelper.GetParent(parent);
                if (parent is ScrollViewer scroll) { scroll.ScrollToVerticalOffset(scroll.VerticalOffset + 60); Pump(); }
                Check(prefix.Text == vm.DetailNumberPrefix && suffix.Text == vm.DetailNumberSuffix, "Long text retains full values at minimum size");
                Check(preview.ActualWidth <= block.ActualWidth && preview.TextWrapping == TextWrapping.Wrap, "Long preview wraps inside naming card");
                Check(prefix.ActualWidth > 80 && suffix.ActualWidth > 80, "Both minimum-size editors remain usable");
                Check(Elevations.ElevationPreview.Visual<Button>(window).Where(b => b.IsVisible && Equals(b.Content, "Инструкция")).Count() == 1, "Single instruction footer retained");
                Shot(window, "numbering-minimum-long");
                log.Add("COMPLETED; actual WPF runtime outside Revit; no physical input or OS DPI transitions tested.");
                return 0;
            }
            finally
            {
                window.Close();
                if (Directory.Exists(preferenceFolder)) foreach (string path in Directory.GetFiles(preferenceFolder, "InteriorElevations*.json"))
                    if (original.ContainsKey(path)) File.WriteAllBytes(path, original[path]); else File.Delete(path);
                File.WriteAllLines(Path.Combine(output, "verification.txt"), log);
            }
        }
    }
}
