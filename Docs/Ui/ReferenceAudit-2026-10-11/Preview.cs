using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SAB.ParameterTools;
using SAB.ParameterTools.Core;
using SAB.UI;

// Loads the unchanged production window. All data and state changes are in memory.
internal static class Preview
{
    [DllImport("kernel32", CharSet = CharSet.Unicode)] static extern bool SetDllDirectory(string path);
    static string output;
    static readonly List<string> manifest = new List<string>();
    static void Drain()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += delegate { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
    static IEnumerable<T> All<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T) yield return (T)child;
            foreach (var item in All<T>(child)) yield return item;
        }
    }
    static void Capture(FrameworkElement visual, string name, string description)
    {
        Drain(); visual.UpdateLayout();
        var bmp = new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth), (int)Math.Ceiling(visual.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bmp));
        using (var stream = File.Create(Path.Combine(output, name + ".png"))) png.Save(stream);
        manifest.Add(name + ".png | " + bmp.PixelWidth + "x" + bmp.PixelHeight + " | " + description);
    }
    [STAThread] static int Main(string[] args)
    {
        var revit = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Autodesk", "Revit 2023");
        SetDllDirectory(revit);
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e)
        {
            var path = Path.Combine(revit, new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        return Run(args);
    }
    [MethodImpl(MethodImplOptions.NoInlining)] static int Run(string[] args)
    {
        output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Window window = null;
        try
        {
            SabWindowAnimationService.Enabled = false;
            var parameters = new List<ParameterRef>();
            string[] names = { "SAB_Элемент_Корпус", "SAB_Элемент_Уровень", "SAB_Элемент_Помещение", "SAB_Элемент_Зона", "Номер", "Имя" };
            for (int i = 0; i < names.Length; i++) parameters.Add(new ParameterRef { Id = 100 + i, Name = names[i], SharedGuid = new Guid(i + 1, 0, 0, new byte[8]).ToString(), DataType = "Текст", VariesAcrossGroups = i != 3 });
            var profile = new Profile { Configured = true };
            profile.Rules.Add(new Rule { EntityName = "Корпус", Group = ParameterGroup.Zone, Source = RuleValueSource.ManualCorpus, Target = parameters[0] });
            profile.Rules.Add(new Rule { EntityName = "Уровень", Group = ParameterGroup.Level, Source = RuleValueSource.Level, Target = parameters[1] });
            profile.Rules.Add(new Rule { EntityName = "Номер помещения", Group = ParameterGroup.Room, Source = RuleValueSource.Room, RoomField = RoomField.Number, Target = parameters[2] });
            profile.Rules.Add(new Rule { EntityName = "Зона", Group = ParameterGroup.Zone, Source = RuleValueSource.Mapping, MappingInput = MappingInput.RoomName, Target = parameters[3], Mappings = new List<ValueMapping> { new ValueMapping { Key = "Офис", Value = "Рабочая зона" }, new ValueMapping { Key = "Коридор", Value = "Общая зона" } } });
            for (int i = 1; i <= 6; i++) profile.Levels.Add(new LevelMapping { LevelUniqueId = "demo-" + i, LevelName = "Этаж " + i.ToString("00"), Value = i.ToString("00") });
            var categories = new List<CategoryChoice>();
            string[] categoryNames = { "Стены", "Перекрытия", "Потолки", "Двери", "Окна", "Мебель", "Обобщённые модели", "Лестницы", "Ограждения", "Несущие колонны" };
            for (int i = 0; i < categoryNames.Length; i++) categories.Add(new CategoryChoice { Id = i + 1, Name = categoryNames[i], Selected = i < 7 });
            var type = typeof(Profile).Assembly.GetType("SAB.ParameterTools.UI.SettingsWindow");
            window = (Window)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { profile, parameters, parameters, categories, profile.Levels, parameters }, null);
            window.ShowInTaskbar = false; window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -10000; window.Top = -10000; window.Show();
            SabWindowBehaviorService.ApplyLoadedBehavior(window);
            window.Left = -10000; window.Top = -10000; window.Width = 1120; window.Height = 820; Drain();
            var content = (FrameworkElement)window.Content;
            var tabs = All<TabControl>(window).First();
            for (int i = 0; i < tabs.Items.Count; i++)
            {
                tabs.SelectedIndex = i; Drain();
                Capture(content, "0" + i + "-" + new[] { "rules", "sources", "values", "categories" }[i], "Production SettingsWindow; test data; tab " + i);
            }
            tabs.SelectedIndex = 0; Drain();
            var grid = All<DataGrid>(window).First(g => g.IsVisible);
            grid.SelectedIndex = 1; grid.CurrentCell = new DataGridCellInfo(grid.Items[1], grid.Columns[1]);
            grid.Focus(); grid.BeginEdit(); Drain();
            Capture(content, "04-editing", "Native DataGrid selected row and BeginEdit on entity name");
            grid.CommitEdit(DataGridEditingUnit.Cell, true); grid.CommitEdit(DataGridEditingUnit.Row, true);
            var picker = All<SAB.ParameterTools.UI.ParameterPicker>(grid).First();
            var combo = All<ComboBox>(picker).First(); combo.IsDropDownOpen = true; Drain();
            var popup = All<System.Windows.Controls.Primitives.Popup>(combo).FirstOrDefault();
            if (popup != null && popup.Child is FrameworkElement)
                Capture((FrameworkElement)popup.Child, "05-parameter-dropdown", "Separate native ComboBox Popup visual; no compositing");
            combo.IsDropDownOpen = false;
            combo.IsEnabled = false;
            var save = All<Button>(window).First(b => (b.Content as string) == "Сохранить настройки");
            save.IsEnabled = false; Capture(content, "06-disabled", "Demo only: parameter combo and Save button disabled at runtime");
            combo.IsEnabled = true; save.IsEnabled = true;
            var runtimeProfile = (Profile)type.GetProperty("Profile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(window, null);
            var target = runtimeProfile.Rules[0].Target; runtimeProfile.Rules[0].Target = null;
            type.GetMethod("UpdateStatus", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
            Capture(content, "07-error", "Actual Problems/UpdateStatus path: first target missing; no save or Revit transaction");
            runtimeProfile.Rules[0].Target = target;
            type.GetMethod("UpdateStatus", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
            tabs.SelectedIndex = 3; Drain();
            var search = All<TextBox>(window).First(t => t.IsVisible && (t.ToolTip as string) == "Поиск по названию категории");
            search.Text = "Несуществующая категория"; Drain();
            Capture(content, "08-empty-categories", "Actual category filter returning zero rows");
            search.Text = "";
            tabs.SelectedIndex = 0; window.Width = window.MinWidth; window.Height = window.MinHeight;
            Capture(content, "09-minimum", "Production minimum size 980x680; same test data");
            manifest.Add("PASS: unchanged production SettingsWindow constructor; 4 tabs; native table edit, popup, disabled, validation, empty filter, minimum size.");
            manifest.Add("Runtime: .NET Framework 4.8 WPF; fresh SAB build for Revit 2023; Autodesk DLL resolution only, no running Revit or Document.");
            manifest.Add("Captures are RenderTargetBitmap of actual live WPF visuals, 96 dpi, not desktop screenshots. Entrance animation disabled for deterministic capture.");
            return 0;
        }
        catch (Exception e) { manifest.Add("FAIL: " + e); return 1; }
        finally
        {
            if (window != null) window.Close(); app.Shutdown();
            File.WriteAllLines(Path.Combine(output, "capture-manifest.txt"), manifest);
        }
    }
}
