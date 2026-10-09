using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using SAB.ParameterTools.Core;
using Profile = SAB.ParameterTools.Core.Profile;
using Binding = System.Windows.Data.Binding;
using Grid = System.Windows.Controls.Grid;
using Control = System.Windows.Controls.Control;

namespace SAB.ParameterTools.UI
{
    internal enum SettingsAction { Cancel, Save, AssignCorpus }
    public sealed class Choice
    {
        public object Value { get; set; }
        public string Label { get; set; }
        internal Choice(object value, string label) { Value = value; Label = label; }
        public override string ToString() { return Label; }
    }
    public sealed class CorpusItem : INotifyPropertyChanged
    {
        private string _name;
        public string Name { get { return _name; } set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Name")); } }
        public event PropertyChangedEventHandler PropertyChanged;
    }
    internal sealed class SettingsWindow : Window
    {
        internal Profile Profile { get; }
        internal SettingsAction Action { get; private set; }
        private readonly List<ParameterRef> _targets, _roomParameters, _elementParameters;
        private readonly List<CategoryChoice> _categories;
        private readonly ObservableCollection<Rule> _rules;
        private readonly ObservableCollection<CorpusItem> _corpora;
        private readonly List<DataGrid> _grids = new List<DataGrid>();
        private readonly HashSet<Rule> _observed = new HashSet<Rule>();
        private readonly TextBlock _status;
        private TextBlock _ruleExplanation;
        private readonly TabControl _tabs;
        private DataGrid _rulesGrid, _corpusGrid;
        private StackPanel _sourcesPanel;
        private TextBox _corpusCount;
        private bool _statusPending, _sourcesPending;
        private string _sourceRuleId;
        private static readonly Choice[] Groups = { new Choice(ParameterGroup.Zone, "Зона"), new Choice(ParameterGroup.Level, "Уровень"),
            new Choice(ParameterGroup.Location, "Местоположение"), new Choice(ParameterGroup.Room, "Помещение") };
        private static readonly Choice[] Sources = { new Choice(RuleValueSource.Room, "Из помещения"), new Choice(RuleValueSource.Level, "Из модели: уровень"),
            new Choice(RuleValueSource.ElementParameter, "Из модели: параметр"), new Choice(RuleValueSource.Constant, "Задать значение"), new Choice(RuleValueSource.ManualCorpus, "Корпус вручную") };
        private static readonly Choice[] RoomFields = { new Choice(RoomField.Number, "Номер помещения"), new Choice(RoomField.Name, "Имя помещения"), new Choice(RoomField.Parameter, "Параметр помещения") };
        internal SettingsWindow(Document doc, Profile profile) : this(profile, Catalog.Targets(doc), Catalog.RoomParameters(doc), Catalog.Categories(doc, profile), Catalog.Levels(doc), Catalog.SourceParameters(doc)) { }
        internal SettingsWindow(Profile profile, List<ParameterRef> targets, List<ParameterRef> roomParameters, List<CategoryChoice> categories, List<LevelMapping> levels, List<ParameterRef> elementParameters = null)
        {
            Profile = Storage.Json.Deserialize<Profile>(Storage.Json.Serialize(profile));
            RuleEngine.ArchiveAdvancedOptions(Profile);
            _targets = targets; _roomParameters = roomParameters; _elementParameters = elementParameters ?? targets;
            _categories = categories.Select(c => new CategoryChoice { Id = c.Id, Name = c.Name, Selected = c.Selected }).ToList();
            foreach (var rule in Profile.Rules)
            {
                RuleEngine.NormalizeRule(Profile, rule); rule.Target = Match(_targets, rule.Target);
                rule.RoomParameter = Match(_roomParameters, rule.RoomParameter); rule.ElementParameter = Match(_elementParameters, rule.ElementParameter);
            }
            Profile.Levels = levels.Select(level => new LevelMapping { LevelUniqueId = level.LevelUniqueId, LevelName = level.LevelName,
                Value = Profile.Levels.FirstOrDefault(x => x.LevelUniqueId == level.LevelUniqueId)?.Value }).ToList();
            _rules = new ObservableCollection<Rule>(Profile.Rules);
            _corpora = new ObservableCollection<CorpusItem>(Profile.Corpora.Select(c => new CorpusItem { Name = c }));
            Title = "Настройки параметров"; Width = 1120; Height = 820; MinWidth = 980; MinHeight = 680; Theme.Window(this);
            _status = Theme.Text(""); _status.MaxHeight = 64; _status.FontSize = 12;
            _tabs = new TabControl(); Theme.Tabs(_tabs);
            RulesTab(); SourcesTab(); LevelsTab(); CategoriesTab();
            _tabs.SelectionChanged += (s, e) => { if (ReferenceEquals(e.Source, _tabs) && _tabs.SelectedIndex == 1) RefreshSources(); };
            var footer = new DockPanel(); var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var instructions = Theme.Button("Инструкция", (s, e) => new InstructionsWindow { Owner = this }.ShowDialog());
            instructions.ToolTip = "Порядок настройки, работа с группами и разбор ошибок записи";
            DockPanel.SetDock(instructions, Dock.Left); footer.Children.Add(instructions);
            buttons.Children.Add(Theme.Button("Отмена", (s, e) => Close()));
            var save = Theme.Button("Сохранить настройки", (s, e) => Save()); Theme.Primary(this, save); buttons.Children.Add(save);
            DockPanel.SetDock(buttons, Dock.Right); footer.Children.Add(buttons); footer.Children.Add(_status);
            Theme.Frame(this, "Настройки параметров", "Здесь задаются правила. Запись в элементы выполняется отдельной командой «Занести параметры».", _tabs, footer);
            ObserveRules(); _rules.CollectionChanged += (s, e) => { ObserveRules(); ScheduleSources(); ScheduleStatus(); };
            foreach (var category in _categories) category.PropertyChanged += (s, e) => ScheduleStatus();
            ObserveCorpora(); _corpora.CollectionChanged += (s, e) => { ObserveCorpora(); if (_corpusCount != null) _corpusCount.Text = _corpora.Count.ToString(); ScheduleStatus(); };
            RefreshSources(); UpdateStatus();
        }
        private static ParameterRef Match(IList<ParameterRef> list, ParameterRef parameter)
        { return parameter == null ? null : list.FirstOrDefault(p => !string.IsNullOrEmpty(parameter.SharedGuid) ? string.Equals(p.SharedGuid, parameter.SharedGuid, StringComparison.OrdinalIgnoreCase) : p.Id == parameter.Id); }
        private void Tab(string title, UIElement content)
        { _tabs.Items.Add(new TabItem { Header = title, Content = new Border { Padding = new Thickness(0, 10, 0, 0), Child = content } }); }
        private ComboBox Combo(object source, string path, System.Collections.IEnumerable items, bool choices = false)
        {
            var combo = new ComboBox { ItemsSource = items, Margin = new Thickness(4), VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center, DisplayMemberPath = choices ? "Label" : "Name", FontWeight = FontWeights.Normal };
            if (choices) { combo.SelectedValuePath = "Value"; combo.SetBinding(ComboBox.SelectedValueProperty, Bind(source, path)); }
            else combo.SetBinding(ComboBox.SelectedItemProperty, Bind(source, path));
            if (!choices) { combo.DisplayMemberPath = ""; combo.ItemTemplate = ParameterTemplate(); combo.Height = double.NaN; combo.MinHeight = 62; }
            combo.SelectionChanged += (s, e) => ScheduleStatus(); return combo;
        }
        private static DataTemplate ParameterTemplate()
        {
            var panel = new FrameworkElementFactory(typeof(StackPanel));
            var name = new FrameworkElementFactory(typeof(TextBlock)); name.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            name.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis); panel.AppendChild(name);
            var details = new FrameworkElementFactory(typeof(TextBlock)); details.SetBinding(TextBlock.TextProperty, new Binding("Details"));
            details.SetValue(TextBlock.FontSizeProperty, 11.0); details.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            details.SetResourceReference(TextBlock.ForegroundProperty, "SabBrush.TextSecondary"); panel.AppendChild(details);
            panel.SetBinding(FrameworkElement.ToolTipProperty, new Binding("Display"));
            return new DataTemplate { VisualTree = panel };
        }
        private static Binding Bind(object source, string path)
        { return new Binding(path) { Source = source, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }; }
        private void Field(System.Windows.Controls.Panel panel, string label, UIElement input)
        { panel.Children.Add(Theme.Text(label, true)); panel.Children.Add(input); }
        private DataGrid GridFor(object source)
        {
            var grid = new DataGrid { ItemsSource = source as System.Collections.IEnumerable, AutoGenerateColumns = false, CanUserAddRows = false,
                CanUserDeleteRows = false, HeadersVisibility = DataGridHeadersVisibility.Column, SelectionMode = DataGridSelectionMode.Extended,
                SelectionUnit = DataGridSelectionUnit.FullRow, RowHeaderWidth = 0 };
            grid.CellEditEnding += (s, e) => { ScheduleStatus(); if (grid == _rulesGrid) ScheduleSources(); }; _grids.Add(grid); return grid;
        }
        private void TextColumn(DataGrid grid, string label, string path, double weight, bool readOnly = false)
        {
            grid.Columns.Add(new DataGridTextColumn { Header = label, Binding = new Binding(path) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                Width = new DataGridLength(weight, DataGridLengthUnitType.Star), IsReadOnly = readOnly,
                ElementStyle = (Style)FindResource("SabDataGridTextBlockStyle"), EditingElementStyle = (Style)FindResource("SabDataGridTextBoxEditingStyle") });
        }
        private void PickerColumn(DataGrid grid, string header, string path, System.Collections.IEnumerable items, double weight, bool choices)
        {
            var picker = new FrameworkElementFactory(typeof(ComboBox)); picker.SetValue(ItemsControl.ItemsSourceProperty, items);
            picker.SetValue(ItemsControl.DisplayMemberPathProperty, choices ? "Label" : "Name"); picker.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 4, 0));
            if (!choices) { picker.SetValue(ItemsControl.DisplayMemberPathProperty, ""); picker.SetValue(ItemsControl.ItemTemplateProperty, ParameterTemplate());
                picker.SetValue(FrameworkElement.HeightProperty, double.NaN); picker.SetValue(FrameworkElement.MinHeightProperty, 62.0); }
            picker.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); picker.SetValue(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center);
            picker.SetValue(Control.FontWeightProperty, FontWeights.Normal);
            picker.AddHandler(ComboBox.SelectionChangedEvent, new SelectionChangedEventHandler((s, e) => ScheduleStatus()));
            if (choices) { picker.SetValue(ComboBox.SelectedValuePathProperty, "Value"); picker.SetBinding(ComboBox.SelectedValueProperty, new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }); }
            else picker.SetBinding(ComboBox.SelectedItemProperty, new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            grid.Columns.Add(new DataGridTemplateColumn { Header = header, Width = new DataGridLength(weight, DataGridLengthUnitType.Star), CellTemplate = new DataTemplate { VisualTree = picker } });
        }
        private void RulesTab()
        {
            var root = new DockPanel(); var header = Theme.Text("Каждая строка записывает один параметр. Выберите параметр и источник, затем нажмите «Настроить источник». «Проверять» ищет пустые значения и не влияет на запись.");
            DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
            var bottom = new StackPanel();
            _ruleExplanation = Theme.Text(""); _ruleExplanation.MaxHeight = 84;
            bottom.Children.Add(_ruleExplanation);
            var actions = new WrapPanel(); var add = Theme.Button("Добавить правило", (s, e) => AddRule()); Theme.Primary(this, add); actions.Children.Add(add);
            var configure = Theme.Button("Настроить источник", (s, e) => {
                if (!(_rulesGrid.SelectedItem is Rule rule)) { _status.Text = "Выберите строку правила, чтобы настроить её источник."; return; }
                CommitGrids(); _sourceRuleId = rule.Id; _tabs.SelectedIndex = 1; RefreshSources();
            });
            configure.ToolTip = "Открыть настройки источника выбранной строки"; actions.Children.Add(configure); bottom.Children.Add(actions);
            DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
            _rulesGrid = GridFor(_rules); _rulesGrid.MinRowHeight = 74; _rulesGrid.RowHeight = double.NaN;
            _rulesGrid.SelectionChanged += (s, e) => UpdateRuleExplanation();
            CheckColumn(_rulesGrid, "Вкл.", "Enabled", 46);
            TextColumn(_rulesGrid, "Сущность / название", "EntityName", 1.05);
            _rulesGrid.Columns.Add(new DataGridTextColumn { Header = "", Binding = new Binding("Id") { Converter = new ArrowConverter() }, IsReadOnly = true,
                Width = 26, ElementStyle = (Style)FindResource("SabDataGridCenteredTextBlockStyle") });
            PickerColumn(_rulesGrid, "Заполняемый параметр", "Target", _targets, 1.9, false);
            PickerColumn(_rulesGrid, "Откуда взять значение", "Source", Sources, 1.45, true);
            CheckColumn(_rulesGrid, "Проверять", "Required", 78);
            RowActions(_rulesGrid, (row, addRow) => { if (addRow) AddRule(); else if (row is Rule rule) _rules.Remove(rule); }, false);
            // Keep the identifiers readable when the native DataGrid is resized.
            _rulesGrid.Columns[1].Width = 145; _rulesGrid.Columns[3].Width = 300;
            _rulesGrid.Columns[4].Width = 210;
            _rulesGrid.SizeChanged += (s, e) => _rulesGrid.Columns[4].Width = Math.Max(210, _rulesGrid.ActualWidth - 655);
            root.Children.Add(_rulesGrid); Tab("Правила", Theme.Card(this, "Правила заполнения", root));
            if (_rules.Count > 0) _rulesGrid.SelectedIndex = 0;
        }
        private void UpdateRuleExplanation()
        {
            if (_ruleExplanation == null) return;
            var rule = _rulesGrid?.SelectedItem as Rule;
            _ruleExplanation.Text = rule == null ? "Выберите строку: здесь появится пояснение её действия." :
                (rule.Enabled ? "Будет записано: " : "Правило выключено: ") + (rule.Target?.Name ?? "параметр ещё не выбран") + " ← "
                + (Sources.FirstOrDefault(s => Equals(s.Value, rule.Source))?.Label ?? "источник не выбран")
                + ". Название правила служит подписью и не определяет логику записи.\n" + (rule.Target?.Details ?? "Выберите параметр в таблице.");
        }
        private void CheckColumn(DataGrid grid, string header, string path, double width)
        {
            var style = new Style(typeof(CheckBox)); style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)); style.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            grid.Columns.Add(new DataGridCheckBoxColumn { Header = header, Binding = new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, ElementStyle = style, EditingElementStyle = style, Width = width });
        }
        private void AddRule()
        {
            CommitGrids(); var rule = new Rule { Group = ParameterGroup.Room, EntityName = "Новая сущность", Source = RuleValueSource.Room };
            _rules.Add(rule); _rulesGrid.SelectedItem = rule; _rulesGrid.ScrollIntoView(rule);
        }
        private void RowActions(DataGrid grid, Action<object, bool> action, bool includePlus)
        {
            var panel = new FrameworkElementFactory(typeof(StackPanel)); panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            panel.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            foreach (var plus in includePlus ? new[] { true, false } : new[] { false })
            {
                var isPlus = plus; var button = new FrameworkElementFactory(typeof(Button)); button.SetValue(ContentControl.ContentProperty, isPlus ? "+" : "×");
                button.SetValue(FrameworkElement.ToolTipProperty, isPlus ? "Добавить строку ниже" : "Удалить строку"); button.SetValue(FrameworkElement.WidthProperty, 28.0);
                button.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, isPlus ? "Добавить строку ниже" : "Удалить строку");
                button.SetValue(FrameworkElement.MinWidthProperty, 28.0); button.SetValue(FrameworkElement.HeightProperty, 28.0); button.SetValue(Control.PaddingProperty, new Thickness(0));
                button.SetValue(FrameworkElement.MarginProperty, new Thickness(2)); button.SetValue(Control.FontSizeProperty, 18.0);
                button.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, e) => action(((FrameworkElement)s).DataContext, isPlus))); panel.AppendChild(button);
            }
            grid.Columns.Add(new DataGridTemplateColumn { Header = "", Width = includePlus ? 68 : 34, CellTemplate = new DataTemplate { VisualTree = panel } });
        }
        private void SourcesTab()
        {
            _sourcesPanel = new StackPanel(); Tab("Источники", new ScrollViewer { Content = _sourcesPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        }
        private void ObserveRules()
        {
            foreach (var rule in _observed.Where(r => !_rules.Contains(r)).ToList()) { rule.PropertyChanged -= RuleChanged; _observed.Remove(rule); }
            foreach (var rule in _rules.Where(r => !_observed.Contains(r))) { rule.PropertyChanged += RuleChanged; _observed.Add(rule); }
        }
        private void RuleChanged(object sender, PropertyChangedEventArgs args)
        { ScheduleStatus(); if (args.PropertyName == "Source" || args.PropertyName == "Enabled" || args.PropertyName == "Target") ScheduleSources(); }
        private void ScheduleSources()
        {
            if (_sourcesPanel == null || _sourcesPending) return; _sourcesPending = true;
            Dispatcher.BeginInvoke(new Action(() => { _sourcesPending = false; RefreshSources(); }), DispatcherPriority.Background);
        }
        private void RefreshSources()
        {
            if (_corpusGrid != null) { _corpusGrid.CommitEdit(DataGridEditingUnit.Row, true); _grids.Remove(_corpusGrid); _corpusGrid = null; }
            _sourcesPanel.Children.Clear(); var rules = _rules.Where(r => r.Enabled).ToList();
            if (rules.Count == 0) { _sourcesPanel.Children.Add(Theme.Text("Во вкладке «Правила» добавьте и включите хотя бы одну строку. Здесь появятся настройки её источника.")); return; }
            var shared = new WrapPanel(); _sourcesPanel.Children.Add(shared);
            if (rules.Any(r => RuleEngine.UsesRoom(Profile, r)))
            {
                var body = new StackPanel(); Field(body, "Как выбираем помещение", Combo(Profile, "RoomSourceMode", new[] { new Choice(RoomSourceMode.ManualPick, "Выбрать вручную после команды"), new Choice(RoomSourceMode.Automatic, "Определить автоматически") }, true));
                body.Children.Add(Theme.Text("Вручную — одно помещение для всего выделения. Автоматически — своё помещение для каждого элемента; подходит для групп на разных этажах. При неоднозначности параметр пропускается с причиной в отчёте.")); var card = Theme.Card(this, "Выбор помещения", body); card.Width = 433; shared.Children.Add(card);
            }
            if (rules.Any(r => RuleEngine.UsesLevel(Profile, r)))
            {
                var body = new StackPanel(); Field(body, "Откуда брать уровень", Combo(Profile, "LevelSource", new[] { new Choice(LevelSource.ActivePlan, "Уровень текущего плана"), new Choice(LevelSource.Element, "Уровень элемента") }, true));
                body.Children.Add(Theme.Text("Для выделения на нескольких этажах выберите «Уровень элемента». «Уровень текущего плана» обрабатывает только элементы этажа открытого плана."));
                body.Children.Add(Theme.Button("Матрица уровней →", (s, e) => _tabs.SelectedIndex = 2)); var card = Theme.Card(this, "Уровень модели", body); card.Width = 433; shared.Children.Add(card);
            }
            var picker = new ComboBox { ItemsSource = rules.Select(r => new Choice(r.Id, r.ToString())).ToList(), SelectedValuePath = "Value", DisplayMemberPath = "Label", Margin = new Thickness(4), VerticalContentAlignment = VerticalAlignment.Center };
            if (!rules.Any(r => r.Id == _sourceRuleId)) _sourceRuleId = rules[0].Id; picker.SelectedValue = _sourceRuleId;
            picker.SelectionChanged += (s, e) => { _sourceRuleId = picker.SelectedValue as string; ScheduleSources(); };
            _sourcesPanel.Children.Add(Theme.Text("Выберите правило, для которого настраиваете источник", true)); _sourcesPanel.Children.Add(picker);
            foreach (var rule in rules.Where(r => r.Id == _sourceRuleId && r.Source != RuleValueSource.Level && r.Source != RuleValueSource.ManualCorpus))
            {
                var body = new StackPanel(); var title = (rule.EntityName ?? Entity(rule.Group)) + " → " + (rule.Target?.Name ?? "параметр не выбран");
                if (rule.Source == RuleValueSource.Room)
                {
                    var field = Combo(rule, "RoomField", RoomFields, true); Field(body, "Что взять из помещения", field);
                    var parameter = new StackPanel(); Field(parameter, "Исходный параметр помещения", Combo(rule, "RoomParameter", _roomParameters)); body.Children.Add(parameter);
                    parameter.Visibility = rule.RoomField == RoomField.Parameter ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                    field.SelectionChanged += (s, e) => { parameter.Visibility = (RoomField)field.SelectedValue == RoomField.Parameter ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed; ScheduleStatus(); };
                }
                else if (rule.Source == RuleValueSource.Constant)
                {
                    var value = new TextBox { Margin = new Thickness(4), FontWeight = FontWeights.Normal }; value.SetBinding(TextBox.TextProperty, Bind(rule, "Constant"));
                    value.TextChanged += (s, e) => ScheduleStatus(); Field(body, "Значение для записи", value); body.Children.Add(Theme.Text("Одинаковое значение для всех выбранных элементов. Ноль — заполненное значение."));
                }
                else if (rule.Source == RuleValueSource.ElementParameter) Field(body, "Исходный параметр этого элемента", Combo(rule, "ElementParameter", _elementParameters));
                _sourcesPanel.Children.Add(Theme.Card(this, title, body));
            }
            if (rules.Any(r => r.Id == _sourceRuleId && r.Source == RuleValueSource.ManualCorpus)) CorpusCard();
            if (rules.Any(r => r.Id == _sourceRuleId && r.Source == RuleValueSource.Level)) _sourcesPanel.Children.Add(Theme.Text("Для этого правила заполните «Матрицу уровней». На виде без определённого уровня при выполнении появится выбор из заполненных строк."));
        }
        private void CorpusCard()
        {
            var root = new DockPanel(); var count = new WrapPanel(); count.Children.Add(Theme.Text("Количество", true));
            _corpusCount = new TextBox { Text = _corpora.Count.ToString(), Width = 58, Margin = new Thickness(4), FontWeight = FontWeights.Normal }; count.Children.Add(_corpusCount);
            count.Children.Add(Theme.Button("Задать", (s, e) => ResizeCorpora()));
            var choose = Theme.Button("Выбрать корпус…", (s, e) => SaveCore(SettingsAction.AssignCorpus));
            choose.ToolTip = "Сохранить настройки и назначить корпус выделенным элементам.";
            count.Children.Add(choose); DockPanel.SetDock(count, Dock.Top); root.Children.Add(count);
            if (_corpora.Count == 0) _corpora.Add(new CorpusItem { Name = "" });
            if (_corpusGrid != null) _grids.Remove(_corpusGrid); _corpusGrid = GridFor(_corpora); _corpusGrid.MinHeight = 100; _corpusGrid.MaxHeight = 240;
            TextColumn(_corpusGrid, "Название корпуса", "Name", 1);
            RowActions(_corpusGrid, (row, plus) => { CommitGrids(); var item = (CorpusItem)row;
                if (plus) _corpora.Insert(_corpora.IndexOf(item) + 1, new CorpusItem { Name = "" });
                else if (_corpora.Count > 1) _corpora.Remove(item); else item.Name = "";
            }, true);
            root.Children.Add(_corpusGrid); _sourcesPanel.Children.Add(Theme.Card(this, "Корпуса для ручного выбора", root));
        }
        private void ObserveCorpora() { foreach (var item in _corpora) { item.PropertyChanged -= CorpusChanged; item.PropertyChanged += CorpusChanged; } }
        private void CorpusChanged(object sender, PropertyChangedEventArgs e) { ScheduleStatus(); }
        private void ResizeCorpora()
        {
            int count; if (!int.TryParse(_corpusCount.Text, out count) || count < 1 || count > 1000) { _status.Text = "Количество корпусов: введите целое число от 1 до 1000 и нажмите «Задать»."; return; }
            CommitGrids(); while (_corpora.Count > count) _corpora.RemoveAt(_corpora.Count - 1);
            while (_corpora.Count < count) _corpora.Add(new CorpusItem { Name = "Корпус " + (_corpora.Count + 1) }); ScheduleStatus();
        }
        private void LevelsTab()
        {
            var grid = GridFor(Profile.Levels); TextColumn(grid, "Уровень модели", "LevelName", 2, true); TextColumn(grid, "Записываемый номер этажа", "Value", 1);
            var root = new DockPanel(); var hint = Theme.Text("Для используемых уровней укажите номер этажа. Эти значения нужны только правилам с источником «Из модели: уровень».");
            DockPanel.SetDock(hint, Dock.Top); root.Children.Add(hint); root.Children.Add(grid); Tab("Матрица уровней", Theme.Card(this, "Уровень → значение", root));
        }
        private void CategoriesTab()
        {
            var root = new DockPanel(); var options = new StackPanel();
            var groups = new CheckBox { Content = "Обрабатывать элементы внутри выделенных групп, включая вложенные", Margin = new Thickness(4, 8, 4, 8) };
            groups.SetBinding(CheckBox.IsCheckedProperty, Bind(Profile, "ExpandSelectedGroups")); options.Children.Add(groups);
            options.Children.Add(Theme.Text("В группе записываются только параметры, допускающие разные значения по экземплярам групп. Остальные пропускаются; отчёт содержит одну строку на параметр."));
            var extend = new CheckBox { Content = "Добавлять отсутствующие привязки параметров к выбранным категориям", Margin = new Thickness(4, 8, 4, 8),
                ToolTip = "Расширяет категории существующего параметра проекта. Новые параметры не создаются; свойство изменения по группам сохраняется." };
            extend.SetBinding(CheckBox.IsCheckedProperty, Bind(Profile, "AddMissingCategoryBindings")); options.Children.Add(extend);
            DockPanel.SetDock(options, Dock.Bottom); root.Children.Add(options);
            root.Children.Add(new CategorySelectionControl(_categories)); Tab("Категории", Theme.Card(this, "Область заполнения", root));
        }
        private void CommitGrids() { foreach (var grid in _grids) { grid.CommitEdit(DataGridEditingUnit.Cell, true); grid.CommitEdit(DataGridEditingUnit.Row, true); } }
        private static string Entity(ParameterGroup group) { return Groups.FirstOrDefault(c => Equals(c.Value, group))?.Label ?? "Сущность"; }
        private void SyncProfile() { Profile.Rules = _rules.ToList(); Profile.CategoryIds = _categories.Where(c => c.Selected).Select(c => c.Id).ToList(); Profile.Corpora = _corpora.Select(c => (c.Name ?? "").Trim()).ToList(); }
        private List<string> Problems()
        {
            SyncProfile(); var errors = new List<string>(); var active = _rules.Where(r => r.Enabled).ToList();
            if (active.Count == 0) errors.Add("«Правила»: нажмите + и включите хотя бы одну строку.");
            foreach (var rule in active)
            {
                string prefix = "Правило " + (_rules.IndexOf(rule) + 1) + " «" + (rule.EntityName ?? Entity(rule.Group)) + "»: ";
                if (string.IsNullOrWhiteSpace(rule.EntityName)) errors.Add(prefix + "введите название сущности в левом столбце вкладки «Правила».");
                if (rule.Target == null) errors.Add(prefix + "выберите заполняемый параметр во вкладке «Правила».");
                if (rule.Source == RuleValueSource.Constant && string.IsNullOrWhiteSpace(rule.Constant)) errors.Add(prefix + "введите значение во вкладке «Источники».");
                if (rule.Source == RuleValueSource.Room && rule.RoomField == RoomField.Parameter && rule.RoomParameter == null) errors.Add(prefix + "выберите параметр помещения во вкладке «Источники».");
                if (rule.Source == RuleValueSource.ElementParameter && rule.ElementParameter == null) errors.Add(prefix + "выберите исходный параметр элемента во вкладке «Источники».");
                if (rule.Source == RuleValueSource.ElementParameter && RuleEngine.SameParameter(rule.Target, rule.ElementParameter)) errors.Add(prefix + "в «Источниках» выберите другой исходный параметр: сейчас он совпадает с заполняемым.");
                if (rule.Target != null && rule.Conditions.Count == 0 && rule.CategoryIds.Count == 0 && active.Any(other => other != rule && other.Conditions.Count == 0 && other.CategoryIds.Count == 0 && RuleEngine.SameParameter(rule.Target, other.Target))) errors.Add(prefix + "этот параметр уже используется другой строкой. Отключите или удалите лишнюю строку во вкладке «Правила».");
            }
            if (!_categories.Any(c => c.Selected)) errors.Add("«Категории»: включите хотя бы одну категорию элементов.");
            if (active.Any(r => RuleEngine.UsesLevel(Profile, r)) && !Profile.Levels.Any(l => !string.IsNullOrWhiteSpace(l.Value))) errors.Add("«Матрица уровней»: укажите номер этажа хотя бы для одного используемого уровня.");
            if (active.Any(r => r.Source == RuleValueSource.ManualCorpus))
            {
                if (Profile.Corpora.Any(string.IsNullOrWhiteSpace)) errors.Add("«Источники»: заполните названия корпусов в пустых строках или удалите лишние строки крестиком.");
                if (Profile.Corpora.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Profile.Corpora.Count(c => !string.IsNullOrWhiteSpace(c))) errors.Add("«Источники»: названия корпусов должны различаться.");
            }
            if (errors.Count == 0) errors.AddRange(RuleEngine.Validate(Profile)); return errors.Distinct().ToList();
        }
        private void ScheduleStatus()
        {
            if (_statusPending || _status == null) return; _statusPending = true;
            Dispatcher.BeginInvoke(new Action(() => { _statusPending = false; UpdateStatus(); }), DispatcherPriority.Background);
        }
        private bool UpdateStatus()
        {
            UpdateRuleExplanation();
            var errors = Problems(); _status.Foreground = (System.Windows.Media.Brush)FindResource(errors.Count > 0 ? "SabBrush.Error" : "SabBrush.TextSecondary");
            _status.Text = errors.Count > 0 ? "Чтобы сохранить: " + errors[0] + (errors.Count > 1 ? " Ещё исправлений: " + (errors.Count - 1) + "." : "")
                : "Настройки готовы. Сохраните настройки, выделите элементы и запустите «Занести параметры».";
            _status.ToolTip = errors.Count > 0 ? string.Join("\n", errors) : _status.Text; return errors.Count == 0;
        }
        private void Save()
        { SaveCore(SettingsAction.Save); }
        private void SaveCore(SettingsAction action)
        {
            CommitGrids(); if (!UpdateStatus())
            {
                string first = Problems().First(); var match = System.Text.RegularExpressions.Regex.Match(first, @"Правило (\d+)");
                if (match.Success) { int index = int.Parse(match.Groups[1].Value) - 1; _sourceRuleId = _rules[index].Id; _rulesGrid.SelectedIndex = index; }
                else if (first.Contains("корпус")) _sourceRuleId = _rules.FirstOrDefault(r => r.Enabled && r.Source == RuleValueSource.ManualCorpus)?.Id;
                _tabs.SelectedIndex = first.Contains("«Источники»") || first.Contains("«Источниках»") ? 1 : first.Contains("«Матрица уровней»") ? 2 : first.Contains("«Категории»") ? 3 : 0;
                if (_tabs.SelectedIndex == 1) RefreshSources();
                return;
            }
            Action = action; Close();
        }
    }
    internal sealed class ArrowConverter : IValueConverter
    {
        public object Convert(object value, Type type, object parameter, System.Globalization.CultureInfo culture) { return "→"; }
        public object ConvertBack(object value, Type type, object parameter, System.Globalization.CultureInfo culture) { throw new NotSupportedException(); }
    }
}
