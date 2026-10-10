using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.ComponentModel;
using System.Windows.Threading;
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
        private readonly CheckBox _selectedOnly;
        private bool _refreshPending;
        internal CategorySelectionControl(List<CategoryChoice> categories)
        {
            _categories = categories;
            var root = new DockPanel();
            var top = new StackPanel();
            top.Children.Add(Theme.Text("Поиск категории", true));
            _search = new TextBox { Margin = new Thickness(4, 0, 4, 6), ToolTip = "Поиск по названию категории" };
            top.Children.Add(_search);
            _selectedOnly = new CheckBox { Content = "Показать только выбранные категории", Margin = new Thickness(4, 4, 4, 8),
                ToolTip = "Показать категории с включённым тумблером. Поиск продолжает действовать; состав выбора не меняется." };
            _selectedOnly.Checked += (s, e) => RefreshFilter();
            _selectedOnly.Unchecked += (s, e) => RefreshFilter();
            top.Children.Add(_selectedOnly);
            var actions = new WrapPanel();
            actions.Children.Add(Theme.Button("Выделить все", (s, e) => _list.SelectAll()));
            actions.Children.Add(Theme.Button("Включить", (s, e) => SetSelected(true)));
            actions.Children.Add(Theme.Button("Выключить", (s, e) => SetSelected(false)));
            top.Children.Add(actions);
            var hint = Theme.Text("Shift — диапазон, Ctrl — отдельные строки. Тумблер и пробел переключают выделенные категории.");
            hint.FontSize = 12; top.Children.Add(hint);
            DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
            _count = Theme.Text(""); DockPanel.SetDock(_count, Dock.Bottom); root.Children.Add(_count);
            _view = new ListCollectionView(categories); _view.Filter = item =>
                (_selectedOnly.IsChecked != true || ((CategoryChoice)item).Selected) &&
                (string.IsNullOrWhiteSpace(_search.Text) ||
                ((CategoryChoice)item).Name.IndexOf(_search.Text.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0);
            _list = new ListBox { ItemsSource = _view, SelectionMode = SelectionMode.Extended, BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, Background = System.Windows.Media.Brushes.White, Margin = new Thickness(4) };
            var row = new FrameworkElementFactory(typeof(DockPanel)); row.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 7, 8, 7));
            var toggle = new FrameworkElementFactory(typeof(ToggleButton));
            toggle.SetValue(FrameworkElement.WidthProperty, 38.0); toggle.SetValue(FrameworkElement.HeightProperty, 22.0);
            toggle.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 10, 0));
            toggle.SetValue(Control.TemplateProperty, Theme.Toggle().Template);
            toggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding("Selected") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            toggle.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(Toggled)); row.AppendChild(toggle);
            var name = new FrameworkElementFactory(typeof(TextBlock)); name.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            name.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); row.AppendChild(name);
            _list.ItemTemplate = new DataTemplate { VisualTree = row };
            _list.PreviewKeyDown += (s, e) => { if (e.Key == Key.Space) { var rows = _list.SelectedItems.Cast<CategoryChoice>().ToList();
                if (rows.Count > 0) Apply(rows, !rows.All(c => c.Selected)); e.Handled = true; } };
            _search.TextChanged += (s, e) => RefreshFilter();
            foreach (var category in categories) category.PropertyChanged += (s, e) => {
                UpdateCount();
                // Refresh after the whole multi-selection operation, never during its iteration.
                if (_selectedOnly.IsChecked != true || _refreshPending) return;
                _refreshPending = true;
                Dispatcher.BeginInvoke(new Action(() => { _refreshPending = false; RefreshFilter(); }), DispatcherPriority.Background);
            };
            var frame = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(216, 222, 232)),
                Background = System.Windows.Media.Brushes.White, Margin = new Thickness(4), Padding = new Thickness(4), Child = _list };
            _list.Margin = new Thickness(0);
            root.Children.Add(frame); Content = root; UpdateCount();
        }
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
        { _count.Text = "Включено: " + _categories.Count(c => c.Selected) + " из " + _categories.Count
            + " · Показано: " + _view.Cast<object>().Count()
            + (_view.IsEmpty ? ". Нет категорий по текущему фильтру." : ""); }
        private void RefreshFilter()
        { if (_view == null) return; _view.Refresh(); UpdateCount(); }
    }

    internal sealed class CheckCategoriesWindow : Window
    {
        internal List<int> CategoryIds { get; private set; }
        internal CheckCategoriesWindow(List<CategoryChoice> categories)
        {
            Title = "Проверка заполненности параметров"; Width = 470; Height = 550; MinWidth = 420; MinHeight = 430; Theme.Window(this);
            var picker = new CategorySelectionControl(categories);
            var bottom = new DockPanel(); var errors = Theme.Text(""); errors.Foreground = System.Windows.Media.Brushes.Firebrick;
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(Theme.Button("Отмена", (s, e) => Close()));
            var check = Theme.Button("Проверить", (s, e) => {
                var ids = categories.Where(c => c.Selected).Select(c => c.Id).ToList();
                if (ids.Count == 0) { errors.Text = "Выберите категории для проверки."; return; }
                CategoryIds = ids; DialogResult = true;
            }); Theme.Primary(this, check); buttons.Children.Add(check);
            DockPanel.SetDock(buttons, Dock.Right); bottom.Children.Add(buttons); bottom.Children.Add(errors);
            Theme.Frame(this, "Что проверяем", "Фильтры пустых обязательных параметров для выбранных категорий текущего вида. После заполнения подсветка снимается автоматически.",
                Theme.Card(this, "Категории проверки", picker), bottom);
        }
    }
}
