using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SAB.ParameterTools.Core;
using SAB.ParameterTools;
using System.Collections;
using System.Reflection;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Controls.Primitives;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        SetDllDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Autodesk", "Revit 2023"));
        string folder = args.Length > 0 ? args[0] : "artifacts/preview"; Directory.CreateDirectory(folder);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var parameters = new List<ParameterRef>();
        string[] names = { "KS_Номер корпуса", "KS_Номер этажа", "KS_Подземная часть (0 или 1)", "KS_Номер помещения", "KS_Наименование" };
        ParameterGroup[] groups = { ParameterGroup.Zone, ParameterGroup.Level, ParameterGroup.Location, ParameterGroup.Room, ParameterGroup.Room };
        var profile = new Profile { Configured = true, AddMissingCategoryBindings = true };
        for (int i = 0; i < names.Length; i++)
        {
            var p = new ParameterRef { Id = i + 1, Name = names[i], SharedGuid = Guid.NewGuid().ToString(), DataType = "Текст", VariesAcrossGroups = i != 2 };
            parameters.Add(p); profile.Rules.Add(new Rule { Target = p, Group = groups[i], RoomField = i == 4 ? RoomField.Name : RoomField.Number });
        }
        profile.RoomCorpusParameter = parameters[0];
        profile.Levels.Add(new LevelMapping { LevelUniqueId = "a", LevelName = "01_Первый этаж", Value = "1" });
        profile.Levels.Add(new LevelMapping { LevelUniqueId = "b", LevelName = "02_Второй этаж", Value = "2" });
        profile.CategoryIds.Add(-2000011);
        parameters.Add(new ParameterRef { Id = 99, Name = names[0], SharedGuid = "11111111-2222-3333-4444-555555555555", DataType = "Целое число", VariesAcrossGroups = false });
        var categoryType = typeof(Profile).Assembly.GetType("SAB.ParameterTools.CategoryChoice");
        var categories = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(categoryType));
        foreach (var entry in new[] { new {Name="Стены",Id=-2000011}, new {Name="Перекрытия",Id=-2000032}, new {Name="Потолки",Id=-2000038} })
        {
            var category = Activator.CreateInstance(categoryType, true);
            categoryType.GetProperty("Name").SetValue(category,entry.Name);
            categoryType.GetProperty("Id").SetValue(category,entry.Id);
            categoryType.GetProperty("Selected").SetValue(category,true);
            categories.Add(category);
        }
        var settingsType = typeof(Profile).Assembly.GetType("SAB.ParameterTools.UI.SettingsWindow");
        var window = (Window)Activator.CreateInstance(settingsType, BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { profile, parameters, parameters, categories, profile.Levels, parameters }, null);
        window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -10000; window.Top = -10000;
        window.Show();
        var tabs = Find<TabControl>(window);
        if (tabs.Items.Count != 4 || tabs.Items.Cast<TabItem>().Any(t => Equals(t.Header, "Условия")))
            throw new Exception("Archived conditions are still present in the settings window.");
        for (int i = 0; i < tabs.Items.Count; i++)
        {
            tabs.SelectedIndex = i; window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Render(window, Path.Combine(folder, "settings-" + i + ".png"));
        }
        var ruleGrid = (DataGrid)settingsType.GetField("_rulesGrid", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
        var edited = (Profile)settingsType.GetProperty("Profile", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
        var status = (TextBlock)settingsType.GetField("_status", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
        tabs.SelectedIndex = 0;
        window.UpdateLayout(); window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var targetPicker = Find<ComboBox>(ruleGrid);
        if (targetPicker == null || targetPicker.ItemTemplate == null || targetPicker.ActualHeight < 62 || targetPicker.VerticalContentAlignment != VerticalAlignment.Center) throw new Exception("Parameter metadata or vertical alignment are incorrect.");
        if (FindButton(window, "Инструкция") == null || FindButton(window, "Настроить источник") == null) throw new Exception("Instructions or source navigation are missing.");
        targetPicker.SelectedItem = parameters.Last();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        if (edited.Rules[0].Target.SharedGuid != parameters.Last().SharedGuid) throw new Exception("Same-name parameter picker lost identity.");
        Render(window, Path.Combine(folder, "same-name-parameter.png"));
        targetPicker.SelectedItem = parameters[0];
        ruleGrid.SelectedIndex = 3;
        FindButton(window, "Настроить источник").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        if (tabs.SelectedIndex != 1 || (string)settingsType.GetField("_sourceRuleId", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window) != edited.Rules[3].Id)
            throw new Exception("Configure source did not open the selected rule.");
        tabs.SelectedIndex = 0;
        window.Width = window.MinWidth; window.Height = window.MinHeight; window.UpdateLayout();
        if (ruleGrid.Columns.Sum(c => c.ActualWidth) > ruleGrid.ActualWidth) throw new Exception("Rules require horizontal scrolling at the minimum window width.");
        Render(window, Path.Combine(folder, "settings-min.png"));
        window.Width = 1120; window.Height = 820;
        var instructionsType = typeof(Profile).Assembly.GetType("SAB.ParameterTools.UI.InstructionsWindow");
        var help = (Window)Activator.CreateInstance(instructionsType, true); help.Left = -10000; help.Top = -10000; help.Show();
        var helpTabs = Find<TabControl>(help);
        if (helpTabs.Items.Count != 4) throw new Exception("Instruction sections are missing.");
        for (int i = 0; i < helpTabs.Items.Count; i++) { helpTabs.SelectedIndex = i; help.UpdateLayout(); Render(help, Path.Combine(folder, "instructions-" + i + ".png")); }
        help.Close();
        settingsType.GetMethod("AddRule", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        if (!status.Text.Contains("Правило 6") || !status.Text.Contains("выберите заполняемый параметр")) throw new Exception("Status does not explain the missing target.");
        settingsType.GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
        if (!window.IsVisible || tabs.SelectedIndex != 0) throw new Exception("Invalid save did not remain open at the problem tab.");
        Render(window, Path.Combine(folder, "missing-target-status.png"));
        var rules = (IList)settingsType.GetField("_rules", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window); rules.RemoveAt(5);
        edited.Rules[0].Source = RuleValueSource.ManualCorpus;
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var countBox = (TextBox)settingsType.GetField("_corpusCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
        countBox.Text = "5"; settingsType.GetMethod("ResizeCorpora", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
        var corpora = (IList)settingsType.GetField("_corpora", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
        if (corpora.Count != 5) throw new Exception("Corpus count was not applied.");
        tabs.SelectedIndex = 1; window.UpdateLayout(); window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Render(window, Path.Combine(folder, "sources-five-corpora.png"));
        var corpusGrid = (DataGrid)settingsType.GetField("_corpusGrid", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
        corpusGrid.ScrollIntoView(corpora[0]); window.UpdateLayout();
        var plus = FindButton(corpusGrid, "+"); if (plus == null) throw new Exception("Inline corpus add is missing.");
        plus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        if (corpora.Count != 6 || !status.Text.Contains("пустых строках")) throw new Exception("Inline corpus add or live guidance failed.");
        corpusGrid.ScrollIntoView(corpora[1]); window.UpdateLayout();
        var row = (DataGridRow)corpusGrid.ItemContainerGenerator.ContainerFromItem(corpora[1]);
        FindButton(row, "×").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        if (corpora.Count != 5) throw new Exception("Inline corpus delete failed.");
        settingsType.GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
        if (window.IsVisible) throw new Exception("Valid settings could not be saved: " + status.Text);
        window.Close();
        var sourceType = typeof(Profile).Assembly.GetType("SAB.ParameterTools.UI.ModelSourceWindow");
        var choicesType = typeof(Profile).Assembly.GetType("SAB.ParameterTools.UI.ModelSourceChoice");
        var sourceChoices = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(choicesType));
        foreach (var level in profile.Levels) { var choice = Activator.CreateInstance(choicesType); choicesType.GetProperty("Id").SetValue(choice, level.LevelUniqueId); choicesType.GetProperty("Name").SetValue(choice, level.LevelName + " → этаж " + level.Value); sourceChoices.Add(choice); }
        var levelWindow = (Window)Activator.CreateInstance(sourceType, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { "Выбрать уровень", "Уровень не определён. Выберите заполненный уровень из матрицы.", sourceChoices }, null);
        levelWindow.WindowStartupLocation = WindowStartupLocation.Manual; levelWindow.Left = -10000; levelWindow.Top = -10000; levelWindow.Show(); levelWindow.UpdateLayout();
        Find<TextBox>(levelWindow).Text = "Первый"; if (Find<ListBox>(levelWindow).Items.Count != 1) throw new Exception("Model source search failed.");
        Render(levelWindow, Path.Combine(folder, "3d-level-picker.png")); levelWindow.Close();
        var corpus = (Window)Activator.CreateInstance(typeof(Profile).Assembly.GetType("SAB.ParameterTools.UI.CorpusChoiceWindow"), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { profile.Corpora }, null); corpus.WindowStartupLocation = WindowStartupLocation.Manual;
        corpus.Left = -10000; corpus.Top = -10000; corpus.Show(); corpus.UpdateLayout(); Render(corpus, Path.Combine(folder, "manual-corpus.png")); corpus.Close();
        var checkType = typeof(Profile).Assembly.GetType("SAB.ParameterTools.UI.CheckCategoriesWindow");
        var check = (Window)Activator.CreateInstance(checkType, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { categories }, null);
        check.WindowStartupLocation = WindowStartupLocation.Manual; check.Left = -10000; check.Top = -10000; check.Show(); check.UpdateLayout();
        Render(check, Path.Combine(folder, "check-categories.png"));
        var selector = Find((DependencyObject)check.Content, typeof(Profile).Assembly.GetType("SAB.ParameterTools.UI.CategorySelectionControl"));
        var list = (ListBox)selector.GetType().GetField("_list", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(selector);
        list.SelectedItems.Add(categories[0]); list.SelectedItems.Add(categories[1]);
        list.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(check), 0, Key.Space) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        if ((bool)categoryType.GetProperty("Selected").GetValue(categories[0]) || (bool)categoryType.GetProperty("Selected").GetValue(categories[1]) ||
            !(bool)categoryType.GetProperty("Selected").GetValue(categories[2])) throw new Exception("Space did not toggle only selected categories.");
        selector.GetType().GetMethod("SetSelected", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(selector, new object[] { true });
        if (!(bool)categoryType.GetProperty("Selected").GetValue(categories[0]) || !(bool)categoryType.GetProperty("Selected").GetValue(categories[1])) throw new Exception("Bulk enable failed.");
        var search = (TextBox)selector.GetType().GetField("_search", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(selector);
        search.Text = "стен"; if (list.Items.Count != 1) throw new Exception("Category search failed.");
        Render(check, Path.Combine(folder, "category-search.png")); check.Close();
        var stateType = typeof(Profile).Assembly.GetType("SAB.ParameterTools.HighlightService+State");
        var state = Activator.CreateInstance(stateType); var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
        var originalType = typeof(Profile).Assembly.GetType("SAB.ParameterTools.HighlightService+Original");
        var original = Activator.CreateInstance(originalType);
        originalType.GetProperty("ElementUniqueId").SetValue(original, "test-element");
        originalType.GetProperty("ProjectionColor").SetValue(original, -1);
        originalType.GetProperty("CutColor").SetValue(original, 0x123456);
        originalType.GetProperty("SurfacePattern").SetValue(original, -1L);
        originalType.GetProperty("CutPattern").SetValue(original, 123456L);
        originalType.GetProperty("SurfaceVisible").SetValue(original, false);
        originalType.GetProperty("CutVisible").SetValue(original, true);
        originalType.GetProperty("Halftone").SetValue(original, true);
        ((IList)stateType.GetProperty("Elements").GetValue(state)).Add(original);
        var json = serializer.Serialize(state); var roundtrip = serializer.Deserialize(json, stateType);
        var restoredRows = (IList)stateType.GetProperty("Elements").GetValue(roundtrip);
        if (restoredRows.Count != 1) throw new Exception("Persisted highlight state lost elements.");
        foreach (var property in originalType.GetProperties())
            if (!Equals(property.GetValue(original), property.GetValue(restoredRows[0]))) throw new Exception("Persisted highlight state lost " + property.Name);
        var issueType = typeof(Profile).Assembly.GetType("SAB.ParameterTools.Issue");
        var issues = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(issueType));
        var issue = Activator.CreateInstance(issueType, true); issueType.GetProperty("Element").SetValue(issue, "Стены · 123456");
        issueType.GetProperty("Parameter").SetValue(issue, "KS_Номер корпуса"); issueType.GetProperty("Expected").SetValue(issue, "Корпус 2");
        issueType.GetProperty("Reason").SetValue(issue, "Обязательный параметр не заполнен."); issues.Add(issue);
        var groupIssue = Activator.CreateInstance(issueType, true);
        issueType.GetProperty("Element").SetValue(groupIssue, "Группы · сводка");
        issueType.GetProperty("Parameter").SetValue(groupIssue, parameters.Last().Display);
        issueType.GetProperty("Reason").SetValue(groupIssue, "Параметр не изменён в группах: значения не могут различаться по экземплярам групп.");
        issueType.GetProperty("Technical").SetValue(groupIssue, "Тест технических подробностей: FailureDefinitionId и ID элементов сохраняются в отчёте."); issues.Add(groupIssue);
        var report = (Window)Activator.CreateInstance(typeof(Profile).Assembly.GetType("SAB.ParameterTools.UI.ReportWindow"), BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { null, issues, "Проверка параметров" }, null);
        report.WindowStartupLocation = WindowStartupLocation.Manual; report.Left = -10000; report.Top = -10000; report.Show(); report.UpdateLayout();
        var reportGrid = Find<DataGrid>(report); reportGrid.SelectedIndex = 1;
        if (reportGrid.Columns.Any(c => c.ActualWidth < 100)) throw new Exception("Report columns collapsed and hide diagnostics.");
        FindButton(report, "Выделить").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        FindButton(report, "Показать").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!Find<TextBox>(report).Text.Contains("Тест технических подробностей")) throw new Exception("Report hides the technical details.");
        Render(report, Path.Combine(folder, "check-report.png")); report.Close();
        var snapshot = new SAB.FilledRegionFromMaterial.ParameterSnapshot {
            MaterialParameters = new List<SAB.FilledRegionFromMaterial.ParameterOption>(),
            TargetParameters = new List<SAB.FilledRegionFromMaterial.ParameterOption>(), TotalMaterials = 20, UsedMaterials = 5
        };
        var fillWindow = new SAB.FilledRegionFromMaterial.SettingsWindow(new SAB.FilledRegionFromMaterial.PluginSettings(), snapshot);
        fillWindow.WindowStartupLocation = WindowStartupLocation.Manual; fillWindow.Left = -10000; fillWindow.Top = -10000;
        fillWindow.Dispatcher.BeginInvoke(new Action(() => {
            fillWindow.UpdateLayout(); Render(fillWindow, Path.Combine(folder, "filled-region-settings.png"));
            fillWindow.Width = fillWindow.MinWidth; fillWindow.Height = fillWindow.MinHeight;
            fillWindow.UpdateLayout(); Render(fillWindow, Path.Combine(folder, "filled-region-settings-min.png"));
            var prefix = (TextBox)fillWindow.FindName("PrefixTextBox"); prefix.Text = "";
            FindButton(fillWindow, "Создать / обновить").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (!fillWindow.IsVisible || fillWindow.RunRequested) throw new Exception("Run bypassed invalid filled-region settings.");
            prefix.Text = "SAB_Test_";
            FindButton(fillWindow, "Создать / обновить").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }), DispatcherPriority.ApplicationIdle);
        if (fillWindow.ShowDialog() != true || !fillWindow.RunRequested || fillWindow.Settings.TypeNamePrefix != "SAB_Test_")
            throw new Exception("Filled-region execution did not return the edited settings.");
        Console.WriteLine("PASS: four settings tabs, archived conditions removed, mapping controls, actionable status, corpus editing, source picker, category bulk search, legacy highlight serialization, filled-region Run validation.");
        Console.WriteLine("Rendered SAB windows to " + Path.GetFullPath(folder));
    }
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool SetDllDirectory(string path);
    private static T Find<T>(DependencyObject obj) where T : DependencyObject
    {
        if (obj is T) return (T)obj;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++) { var result = Find<T>(VisualTreeHelper.GetChild(obj, i)); if (result != null) return result; }
        return null;
    }
    private static Button FindButton(DependencyObject obj, string content)
    {
        if (obj is Button button && Equals(button.Content, content)) return button;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++) { var result = FindButton(VisualTreeHelper.GetChild(obj, i), content); if (result != null) return result; }
        return null;
    }
    private static DependencyObject Find(DependencyObject obj, Type type)
    {
        if (type.IsInstanceOfType(obj)) return obj;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++) { var result = Find(VisualTreeHelper.GetChild(obj, i), type); if (result != null) return result; }
        return null;
    }
    private static void Render(Window window, string path)
    {
        var content = (FrameworkElement)window.Content;
        double width = window.Width;
        double height = window.SizeToContent == SizeToContent.Height ? double.PositiveInfinity : window.Height;
        content.Measure(new Size(width, height));
        if (double.IsPositiveInfinity(height)) height = content.DesiredSize.Height;
        content.Arrange(new Rect(0, 0, width, height)); content.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(Brushes.WhiteSmoke, null, new Rect(0, 0, width, height));
            drawing.DrawRectangle(new VisualBrush(content) { Stretch = Stretch.Uniform }, null, new Rect(0, 0, width, height));
        }
        bitmap.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
    }
}
