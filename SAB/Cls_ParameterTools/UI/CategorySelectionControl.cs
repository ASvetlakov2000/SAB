using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.ComponentModel;
using Binding = System.Windows.Data.Binding;

namespace SAB.ParameterTools.UI
{
    internal sealed class CategorySelectionControl : UserControl
    {
        private readonly List<CategoryChoice> _categories;
        private readonly ListBox _list;
        private readonly ICollectionView _view;
        private readonly TextBox _search;
        private readonly TextBlock _count;
        private bool _selectedOnly; private FrameworkElement _keyboardHint;
        private TextBlock _selectionStatus;
        internal sealed class SelectionSet { public string Format { get; set; } public int Version { get; set; } public List<int> CategoryIds { get; set; } }

        private FrameworkElement _emptyMessage;
        internal CategorySelectionControl(List<CategoryChoice> categories)
        {
            Resources.MergedDictionaries.Add(SAB.UI.FluentLight.FluentResources.CreateDictionary());
            _categories = categories;
            var root = new DockPanel();
            var top = new StackPanel();
            top.Children.Add(FluentTheme.Text("Поиск категории", true));
            _search = new TextBox { Margin = new Thickness(4, 0, 4, 6), ToolTip = "Поиск по названию категории" };
            top.Children.Add(_search);
            var actions = new WrapPanel();
            actions.Children.Add(FluentTheme.Button("Выделить все", (s, e) => _list.SelectAll()));
            actions.Children.Add(FluentTheme.Button("Включить", (s, e) => SetSelected(true)));
            actions.Children.Add(FluentTheme.Button("Выключить", (s, e) => SetSelected(false)));
            top.Children.Add(actions);
            var selectionTools = new WrapPanel { Margin = new Thickness(0,8,0,8) };
            var modes = new Grid { Width = 190, MinHeight = 34, Margin = new Thickness(0,0,16,0) };
            SAB.UI.FluentLight.Capsule.SetIsGroup(modes, true);
            modes.Children.Add(new Border { CornerRadius = new CornerRadius(20), Background = (System.Windows.Media.Brush)FindResource("SabBrush.TableHeaderBackground"), BorderBrush = (System.Windows.Media.Brush)FindResource("SabBrush.Border"), BorderThickness = new Thickness(1) });
            modes.Children.Add(new Border { Tag = "CapsuleIndicator", CornerRadius = new CornerRadius(18), Background = (System.Windows.Media.Brush)FindResource("SabBrush.Accent"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center });
            var choices = new UniformGrid { Columns = 2, Rows = 1, Margin = new Thickness(2) };
            var groupName = "CategoryFilter" + Guid.NewGuid().ToString("N");
            var all = new RadioButton { Content = "Все", GroupName = groupName, IsChecked = true };
            var selected = new RadioButton { Content = "Выбранные", GroupName = groupName };
            all.Checked += (s,e) => { _selectedOnly = false; RefreshSelectionView(); };
            selected.Checked += (s,e) => { _selectedOnly = true; RefreshSelectionView(); };
            choices.Children.Add(all); choices.Children.Add(selected); modes.Children.Add(choices); selectionTools.Children.Add(modes);
            selectionTools.Children.Add(FluentTheme.Button("Сохранить", (s,e) => SelectionFile(false)));
            selectionTools.Children.Add(FluentTheme.Button("Загрузить", (s,e) => SelectionFile(true)));
            top.Children.Insert(2,selectionTools);
            _selectionStatus = FluentTheme.Text(""); _selectionStatus.Visibility = Visibility.Collapsed;
            var selectionFeedback = SAB.UI.FluentLight.Presentation.Information(_selectionStatus); selectionFeedback.Padding = new Thickness(12,6,12,6); selectionFeedback.Margin = new Thickness(0,8,0,8); top.Children.Add(selectionFeedback);
            var hint = FluentTheme.Text("Shift — диапазон, Ctrl — отдельные строки. Тумблер и пробел переключают выделенные категории.");
            hint.FontSize = 12; _keyboardHint = SAB.UI.FluentLight.Presentation.Information(hint); top.Children.Add(_keyboardHint);
            // Logical groups first; search is the last element directly above the list.
            var searchLabel = top.Children.OfType<TextBlock>().First(x => x.Text == "Поиск категории");
            top.Children.Remove(searchLabel); top.Children.Remove(_search); top.Children.Remove(actions); top.Children.Remove(selectionTools);
            selectionTools.Children.Remove(modes);
            var groups = new WrapPanel { Margin = new Thickness(0,0,0,4) };
            groups.Children.Add(SAB.UI.FluentLight.Presentation.ActionGroup("Показать",modes));
            groups.Children.Add(SAB.UI.FluentLight.Presentation.ActionGroup("Выборки",selectionTools));
            groups.Children.Add(SAB.UI.FluentLight.Presentation.ActionGroup("Выделение",actions));
            top.Children.Insert(0,groups);
            selectionTools.Margin = new Thickness(0); actions.Margin = new Thickness(0); _search.Margin = new Thickness(0,0,0,8);
            top.Children.Add(searchLabel); top.Children.Add(_search);
            DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
            _count = FluentTheme.Text(""); _count.Margin = new Thickness(0,6,0,0); DockPanel.SetDock(_count, Dock.Bottom); root.Children.Add(_count);
            _view = new ListCollectionView(categories); _view.Filter = item => (!_selectedOnly || ((CategoryChoice)item).Selected) && (string.IsNullOrWhiteSpace(_search.Text) ||
                ((CategoryChoice)item).Name.IndexOf(_search.Text.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0);
            _list = new ListBox { ItemsSource = _view, SelectionMode = SelectionMode.Extended, BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, Background = System.Windows.Media.Brushes.White, Margin = new Thickness(0) };
            var row = new FrameworkElementFactory(typeof(DockPanel)); row.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 7, 8, 7));
            var toggle = new FrameworkElementFactory(typeof(ToggleButton));
            toggle.SetValue(FrameworkElement.WidthProperty, 38.0); toggle.SetValue(FrameworkElement.HeightProperty, 22.0);
            toggle.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 10, 0));
            toggle.SetValue(Control.TemplateProperty, FluentTheme.Toggle().Template);
            toggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding("Selected") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            toggle.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(Toggled)); row.AppendChild(toggle);
            var name = new FrameworkElementFactory(typeof(TextBlock)); name.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            name.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); row.AppendChild(name);
            _list.ItemTemplate = new DataTemplate { VisualTree = row };
            _list.PreviewKeyDown += (s, e) => { if (e.Key == Key.Space) { var rows = _list.SelectedItems.Cast<CategoryChoice>().ToList();
                if (rows.Count > 0) Apply(rows, !rows.All(c => c.Selected)); e.Handled = true; } };
            _search.TextChanged += (s, e) => { _view.Refresh(); UpdateCount(); UpdateEmpty(); };
            foreach (var category in categories) category.PropertyChanged += (s, e) => RefreshSelectionView();
            var frame = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(216, 222, 232)),
                Background = System.Windows.Media.Brushes.White, Margin = new Thickness(0), Padding = new Thickness(4), Child = null };
            var listBody = new Grid();listBody.Children.Add(_list);
            var empty = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            empty.Children.Add(new TextBlock { Text = "Категории не найдены", FontSize = 16, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });

            _emptyMessage = empty;listBody.Children.Add(empty);frame.Child = listBody;
            _list.Margin = new Thickness(0);
            root.Children.Add(frame); Content = root; UpdateCount(); UpdateEmpty();
        }
        private void UpdateEmpty() { if (_emptyMessage != null) _emptyMessage.Visibility = _view.IsEmpty ? Visibility.Visible : Visibility.Collapsed; }
        internal FrameworkElement DetachKeyboardHint() { ((Panel)_keyboardHint.Parent).Children.Remove(_keyboardHint); return _keyboardHint; }
        private void RefreshSelectionView()
        { if (_view == null) return; _view.Refresh(); UpdateCount(); UpdateEmpty(); }
        private void SelectionFile(bool import)
        {
            try {
                if(import) {
                    var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Загрузить выборку категорий", Filter = "Выборка SAB (*.json)|*.json", CheckFileExists = true };
                    if(dialog.ShowDialog(Window.GetWindow(this)) == true) ImportSelection(dialog.FileName);
                } else {
                    var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Сохранить выборку категорий", Filter = "Выборка SAB (*.json)|*.json", DefaultExt = ".json", AddExtension = true, FileName = "SAB-категории.json" };
                    if(dialog.ShowDialog(Window.GetWindow(this)) == true) ExportSelection(dialog.FileName);
                }
            } catch(Exception ex) { SelectionStatus("Не удалось " + (import ? "загрузить" : "сохранить") + " выборку: " + ex.Message, true); }
        }
        internal void ExportSelection(string path)
        {
            var file = new SelectionSet { Format = "SAB.CategorySelection", Version = 1, CategoryIds = _categories.Where(c => c.Selected).Select(c => c.Id).Distinct().ToList() };
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                System.IO.File.WriteAllText(temporary, Storage.Json.Serialize(file), new System.Text.UTF8Encoding(false));
                if(System.IO.File.Exists(path)) System.IO.File.Replace(temporary,path,null); else System.IO.File.Move(temporary,path);
            } finally { if(System.IO.File.Exists(temporary)) System.IO.File.Delete(temporary); }
            SelectionStatus("Выборка сохранена. Категорий: " + file.CategoryIds.Count + ".",false);
        }
        internal void ImportSelection(string path)
        {
            var file = Storage.Json.Deserialize<SelectionSet>(System.IO.File.ReadAllText(path));
            if(file == null || file.Format != "SAB.CategorySelection" || file.Version != 1 || file.CategoryIds == null) throw new InvalidOperationException("Это не выборка категорий SAB версии 1. Текущий выбор сохранён.");
            var ids = new HashSet<int>(file.CategoryIds);
            var available = new HashSet<int>(_categories.Select(c => c.Id));
            var unknown = ids.Count(id => !available.Contains(id));
            foreach(var category in _categories) category.Selected = ids.Contains(category.Id);
            RefreshSelectionView();
            SelectionStatus("Выборка загружена. Выбрано: " + _categories.Count(c => c.Selected) + "." + (unknown > 0 ? " Недоступных категорий пропущено: " + unknown + "." : ""),false);
        }
        private void SelectionStatus(string text, bool error)
        { _selectionStatus.Text = text; _selectionStatus.SetResourceReference(TextBlock.ForegroundProperty,error ? "SabBrush.Error" : "SabBrush.TextSecondary"); _selectionStatus.Visibility = Visibility.Visible; }
        private void Toggled(object sender, RoutedEventArgs args)
        {
            var toggle = (ToggleButton)sender; var row = toggle.DataContext as CategoryChoice; if (row == null) return;
            var selected = _list.SelectedItems.Cast<CategoryChoice>().ToList();
            Apply(selected.Contains(row) ? selected : new List<CategoryChoice> { row }, toggle.IsChecked == true);
        }
        private void SetSelected(bool enabled) { Apply(_list.SelectedItems.Cast<CategoryChoice>().ToList(), enabled); }
        private void Apply(IEnumerable<CategoryChoice> rows, bool enabled)
        { foreach (var row in rows) row.Selected = enabled; UpdateCount(); }
        private void UpdateCount()
        { _count.Text = "Включено: " + _categories.Count(c => c.Selected) + " из " + _categories.Count; }
    }

    internal sealed class CheckCategoriesWindow : Window
    {
        internal List<int> CategoryIds { get; private set; }
        internal CheckCategoriesWindow(List<CategoryChoice> categories)
        {
            Title = "SAB · Настройки проверок"; Width = 760; Height = 820; MinWidth = 680; MinHeight = 760; FluentTheme.Window(this);
            var picker = new CategorySelectionControl(categories);
            var footer = new StackPanel();
            var description = new Border { Name = "CheckDescriptionBorder", Style = (Style)FindResource("SabInformationBorderStyle"), Margin = new Thickness(0,0,0,8),
                Child = new TextBlock { Text = "Фильтры пустых обязательных параметров для выбранных категорий текущего вида. После заполнения подсветка снимается автоматически.", TextWrapping = TextWrapping.Wrap } };
            footer.Children.Add(description);
            var errors = new TextBlock { Name = "CheckValidationText", Foreground = System.Windows.Media.Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Margin = new Thickness(0,0,0,8) };
            footer.Children.Add(errors);
            var actions = new Grid();
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var help = FluentTheme.Button("Инструкция", (s,e) => { });
            SAB.UI.FluentLight.Instructions.SetFile(help, SAB.Instructions.InstructionLauncher.ParametersFile);
            actions.Children.Add(help);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(FluentTheme.Button("Отмена", (s, e) => Close()));
            var check = FluentTheme.Button("Проверить", (s, e) => {
                var ids = categories.Where(c => c.Selected).Select(c => c.Id).Distinct().ToList();
                if (ids.Count == 0) { errors.Text = "Выберите категории для проверки."; errors.Visibility = Visibility.Visible; return; }
                CategoryIds = ids; DialogResult = true;
            }); FluentTheme.Primary(this, check); check.Margin = new Thickness(0); buttons.Children.Add(check);
            Grid.SetColumn(buttons,2); actions.Children.Add(buttons); footer.Children.Add(actions);
            FluentTheme.Frame(this, "Что проверяем", null, FluentTheme.Card(this, "Категории проверки", picker), footer);
        }
    }
}
