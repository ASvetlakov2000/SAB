using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Sab.UiLab;
using UiButton = Wpf.Ui.Controls.Button;
using ControlAppearance = Wpf.Ui.Controls.ControlAppearance;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Sab.UiReview
{
    public sealed class ReviewWindow : Window
    {
        public ReviewData Data { get; } = new ReviewData();
        public Grid Surface { get; } = new Grid();
        public int Instrument { get; private set; } = -1;
        public int Section { get; private set; }
        public DataGrid ActiveGrid { get; private set; }
        public ReviewRow SelectedRule => Data.Rules.FirstOrDefault(r => ReferenceEquals(r, ruleGrid?.SelectedItem)) ?? Data.Rules.FirstOrDefault();
        readonly Dictionary<int, FrameworkElement> pages = new Dictionary<int, FrameworkElement>();
        readonly Dictionary<int, DataGrid> mainGrids = new Dictionary<int, DataGrid>();
        readonly Dictionary<string, FrameworkElement> forms = new Dictionary<string, FrameworkElement>();
        readonly Dictionary<string, string> settings = new Dictionary<string, string>();
        readonly Dictionary<string, string> numericLabels = new Dictionary<string, string>();
        readonly List<UiButton> nav = new List<UiButton>();
        readonly List<string> timings = new List<string>();
        readonly ContentControl host = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        readonly TextBlock heading = Text("", 24, true), description = Hint(""), status = Hint("Демонстрационные данные. Изменения остаются в тестовом окне.");
        readonly TextBlock summary = Text("", 13), timing = Hint("");
        readonly UiButton primary;
        ContentControl settingsHost, paramHost; StackPanel sourceFields; DataGrid ruleGrid;
        TextBlock previewName, previewMeta; Border preview; List<UiButton> sectionButtons;
        readonly Dictionary<int, Tuple<TextBlock, TextBlock, Border>> previews = new Dictionary<int, Tuple<TextBlock, TextBlock, Border>>();
        bool running;
        public IReadOnlyList<string> Timings => timings;

        public ReviewWindow(int instrument = 0)
        {
            var clock = Stopwatch.StartNew();
            Title = "SAB — тестовые окна для согласования"; Width = 1400; Height = 900; MinWidth = 1100; MinHeight = 700;
            WindowStartupLocation = WindowStartupLocation.CenterScreen; FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
            Background = Brush("#F7F8FA"); Foreground = Brush("#1F2937"); UseLayoutRounding = true; SnapsToDevicePixels = true;
            Surface.Background = Background; Content = Surface;
            foreach (var height in new[] { new GridLength(62), GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, new GridLength(68) }) Surface.RowDefinitions.Add(new RowDefinition { Height = height });
            var top = Columns("*", "Auto"); top.Margin = new Thickness(24, 0, 24, 0);
            var tabs = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            tabs.Children.Add(Text("SAB", 19, true, new Thickness(0, 0, 28, 0)));
            string[] titles = { "Развёртки", "Виды и листы", "Параметризация" };
            for (int i = 0; i < titles.Length; i++) { int n = i; var b = Button(titles[i], () => Switch(n)); b.Margin = new Thickness(0, 0, 8, 0); tabs.Children.Add(b); nav.Add(b); }
            top.Children.Add(tabs);
            var options = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var motion = new CheckBox { Content = "Анимации", IsChecked = true, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            motion.Checked += (s, e) => Motion.Enabled = true;
            motion.Unchecked += (s, e) => { Motion.Enabled = false; Motion.Stop(Surface); foreach (var p in pages.Values) Motion.Stop(p); };
            options.Children.Add(motion); options.Children.Add(Button("Повторить открытие", () => Motion.Reveal(Surface, 180, 6)));
            Grid.SetColumn(options, 1); top.Children.Add(options); Add(Surface, LinePanel(top, false), 0);
            var title = Columns("*", "Auto"); title.Margin = new Thickness(24, 14, 24, 12);
            var titleText = new StackPanel(); titleText.Children.Add(heading); description.Margin = new Thickness(0, 5, 0, 0); titleText.Children.Add(description); title.Children.Add(titleText);
            var badge = new Border { Background = Brush("#EAF3FF"), CornerRadius = new CornerRadius(5), Padding = new Thickness(10, 7, 10, 7), VerticalAlignment = VerticalAlignment.Center, Child = Text("Макет для согласования", 12, true) };
            ((TextBlock)badge.Child).Foreground = Brush("#0F6CBD"); Grid.SetColumn(badge, 1); title.Children.Add(badge); Add(Surface, title, 1);
            host.Margin = new Thickness(24, 0, 24, 14); Add(Surface, host, 2);
            status.Margin = new Thickness(24, 0, 24, 12); Add(Surface, status, 3);
            var bottom = Columns("*", "Auto"); bottom.Margin = new Thickness(24, 12, 24, 12);
            var count = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; count.Children.Add(summary); timing.Margin = new Thickness(0, 4, 0, 0); count.Children.Add(timing); bottom.Children.Add(count);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; actions.Children.Add(Button("Закрыть", Close));
            primary = Button("Тест создания", Run); primary.Appearance = ControlAppearance.Primary; primary.Foreground = Brushes.White; primary.MinWidth = 172; primary.Margin = new Thickness(10, 0, 0, 0); actions.Children.Add(primary);
            Grid.SetColumn(actions, 1); bottom.Children.Add(actions); Add(Surface, LinePanel(bottom, true), 4);
            foreach (var row in Data.Elevations.Concat(Data.Sheets).Concat(Data.Rules)) row.PropertyChanged += RowChanged;
            Switch(instrument);
            SizeChanged += (s, e) => ApplyDensity();
            ContentRendered += (s, e) => { clock.Stop(); timings.Add($"Первый кадр: {clock.ElapsedMilliseconds} мс"); timing.Text = timings.Last(); Motion.Attach(Surface); Motion.Reveal(Surface, 180, 6); };
            Closed += (s, e) => { Motion.Stop(Surface); foreach (var page in pages.Values) Motion.Stop(page); };
        }

        public void Switch(int instrument)
        {
            if (running || Instrument == instrument) return;
            ActiveGrid?.CommitEdit(DataGridEditingUnit.Cell, true); ActiveGrid?.CommitEdit(DataGridEditingUnit.Row, true);
            var clock = Stopwatch.StartNew(); Instrument = instrument; Section = 0;
            if (!pages.TryGetValue(instrument, out var page)) pages[instrument] = page = BuildPage(instrument);
            host.Content = page;
            if (previews.TryGetValue(instrument, out var p)) { previewName = p.Item1; previewMeta = p.Item2; preview = p.Item3; }
            ActiveGrid = mainGrids[instrument];
            heading.Text = new[] { "Развёртки помещений", "Создание видов и листов", "Параметризация" }[instrument];
            description.Text = new[] { "Настройте комплект, проверьте имена и размещение перед созданием.", "Подготовьте таблицу и настройте образцы для каждого типа плана.", "Настройте правила заполнения и проверьте, откуда берётся каждое значение." }[instrument];
            for (int i = 0; i < nav.Count; i++) { nav[i].Appearance = i == instrument ? ControlAppearance.Primary : ControlAppearance.Transparent; nav[i].Foreground = i == instrument ? Brushes.White : Brush("#1F2937"); }
            SetButtonLabel(primary, instrument == 2 ? "Тест сохранения" : "Тест создания");
            status.Text = "Демонстрационные данные. Изменения остаются в тестовом окне.";
            UpdatePreview(); UpdateSummary(); Motion.Reveal(page, 140, 4);
            Dispatcher.BeginInvoke(new Action(() => { ApplyDensity(); clock.Stop(); timings.Add($"Переключение {instrument}: {clock.ElapsedMilliseconds} мс"); timing.Text = timings.Last(); Motion.Attach(page); }), DispatcherPriority.Loaded);
        }

        FrameworkElement BuildPage(int mode)
        {
            var outer = Rows("Auto", "*", "Auto");
            Add(outer, ContextStrip(mode), 0);
            var work = Columns("320", "18", "*"); work.Margin = new Thickness(0, 14, 0, 0);
            var left = Rows("Auto", "*"); var menu = new StackPanel(); menu.Children.Add(Text(mode == 2 ? "Источник выбранного правила" : "Настройки комплекта", 16, true));
            if (mode != 2)
            {
                var buttons = new WrapPanel { Margin = new Thickness(0, 10, 0, 10) }; sectionButtons = new List<UiButton>();
                string[] names = mode == 0 ? new[] { "Виды", "Лист", "План-схема", "Марки" } : new[] { "Образцы", "Размещение", "Копирование", "Структура" };
                var localHost = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch }; settingsHost = localHost;
                var localButtons = sectionButtons;
                for (int i = 0; i < names.Length; i++) { int section = i; var b = Button(names[i], () => SetForm(mode, section, localHost, localButtons)); b.Tag = "section"; b.Padding = new Thickness(8, 5, 8, 5); b.Margin = new Thickness(0, 0, 4, 4); localButtons.Add(b); buttons.Children.Add(b); }
                menu.Children.Add(buttons); Add(left, menu, 0); Add(left, localHost, 1); SetForm(mode, 0, localHost, localButtons);
            }
            else
            {
                menu.Children.Add(Hint("Выберите строку справа, чтобы изменить её источник.", new Thickness(0, 7, 0, 12))); Add(left, menu, 0);
                sourceFields = new StackPanel(); Add(left, Scroll(sourceFields), 1);
            }
            work.Children.Add(Panel(left));
            var right = Rows("*", "14", "Auto"); right.Tag = "work-right"; Grid.SetColumn(right, 2); work.Children.Add(right);
            FrameworkElement table = mode == 2 ? ParameterArea() : WorkArea(mode);
            Add(right, Panel(table, 0), 0);
            previewName = Text("", 18, true); previewName.TextWrapping = TextWrapping.Wrap; previewName.TextTrimming = TextTrimming.None; previewName.Margin = new Thickness(0, 6, 0, 6);
            previewMeta = Hint(""); previewMeta.TextTrimming = TextTrimming.None;
            var previewContent = new StackPanel(); var previewLabel = Hint(mode == 2 ? "Предпросмотр записи выбранного правила" : "Предпросмотр выбранной строки"); previewLabel.Tag = "compact-hide"; previewContent.Children.Add(previewLabel); previewContent.Children.Add(previewName); previewContent.Children.Add(previewMeta);
            preview = Panel(previewContent); preview.MinHeight = 120; Add(right, preview, 2); previews[mode] = Tuple.Create(previewName, previewMeta, preview);
            Add(outer, work, 1);
            if (mode == 2) UpdateSourceEditor();
            return outer;
        }

        FrameworkElement ContextStrip(int mode)
        {
            var grid = Columns("*", "Auto");
            var text = new StackPanel(); text.Children.Add(Text(mode == 0 ? "Помещение 101 · Переговорная" : mode == 1 ? "Рабочий комплект · Архитектурные решения" : "Настройки параметров текущей модели", 14, true));
            var contextHint = Hint(mode == 0 ? "Основная модель   /   01 этаж   /   8 линий контура" : mode == 1 ? "Одноэтажная структура · образцы и оформление заданы слева" : "Настройки сохраняют правила. Заполнение элементов запускается отдельной командой.", new Thickness(0, 5, 0, 0)); contextHint.Tag = "compact-hide"; text.Children.Add(contextHint); grid.Children.Add(text);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (mode == 0) { buttons.Children.Add(Button("Выбрать линии", () => Feedback("В рабочей версии здесь будет выбор линий в Revit. В тесте используется контур из 8 линий."))); buttons.Children.Add(Button("Помещения…", () => Feedback("В тесте выбрано помещение 101. Выбор одного или нескольких помещений будет подключён при переносе в SAB."))); }
            else { buttons.Children.Add(Button("Импорт…", () => Feedback("Импорт конфигурации будет подключён к действующей команде SAB после согласования."))); buttons.Children.Add(Button("Экспорт…", () => Feedback("Экспорт конфигурации будет подключён к действующей команде SAB после согласования."))); }
            foreach (FrameworkElement b in buttons.Children) b.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(buttons, 1); grid.Children.Add(buttons); return Panel(grid, 12);
        }

        void SetForm(int mode, int section, ContentControl target, List<UiButton> buttons)
        {
            string key = mode + ":" + section; Section = section;
            if (!forms.TryGetValue(key, out var form)) forms[key] = form = Scroll(BuildForm(mode, section));
            target.Content = form;
            for (int i = 0; i < buttons.Count; i++) { buttons[i].Appearance = i == section ? ControlAppearance.Primary : ControlAppearance.Transparent; buttons[i].Foreground = i == section ? Brushes.White : Brush("#1F2937"); }
            Motion.Reveal(form, 140, 4); Dispatcher.BeginInvoke(new Action(() => Motion.Attach(form)), DispatcherPriority.Loaded);
        }

        public void SelectSection(int section)
        {
            if (Instrument == 2) SetParameterSection(section);
            else
            {
                // The cached page owns its own form host and tab buttons.
                var page = pages[Instrument]; var all = Descendants<UiButton>(page).Where(b => b.Tag as string == "section").ToArray();
                if (all.Length > section) all[section].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            }
        }

        StackPanel BuildForm(int mode, int section)
        {
            var form = new StackPanel();
            string prefix = mode + ":" + section + ":";
            if (mode == 0 && section == 0)
            {
                Select(form, "Тип вида развёртки", "Развёртка интерьера", "Развёртка интерьера", "Разрез");
                Select(form, "Шаблон вида", "АР · Развёртки", "АР · Развёртки", "АР · Рабочий", "Без шаблона");
                Field(form, "Формула имени вида", "[Номер помещения] · [Имя помещения] · [Начальный угол]–[Конечный угол]", prefix + "formula", true, value => { for (int i = 0; i < Data.Elevations.Count; i++) Data.Elevations[i].Name = value.Replace("[Номер помещения]", "101").Replace("[Имя помещения]", "Переговорная").Replace("[Начальный угол]", (i + 1).ToString()).Replace("[Конечный угол]", (i + 2).ToString()); });
                form.Children.Add(Hint("Параметры в квадратных скобках подставляются из помещения и контура.", new Thickness(0, 5, 0, 0)));
                Field(form, "Заголовок вида", "[Начальный угол]–[Конечный угол]", prefix + "title");
                Pair(form, "Масштаб", "50", "Глубина, мм", "1500", prefix, true);
                Field(form, "Отступ вида от линии, мм", "100", prefix + "offset", numeric: true);
                var crop = new StackPanel(); Pair(crop, "Сверху, мм", "100", "Снизу, мм", "0", prefix + "crop", true); Pair(crop, "Слева, мм", "50", "Справа, мм", "50", prefix + "crop2", true); form.Children.Add(Disclosure("Подрезка вида", crop));
                var axes = new StackPanel(); Check(axes, "Подрезать оси основной модели", true); Pair(axes, "Сверху, мм", "3", "Снизу, мм", "5", prefix + "axes", true); axes.Children.Add(Hint("Размер выступа задаётся на листе.")); form.Children.Add(Disclosure("Оси на развёртке", axes));
            }
            else if (mode == 0 && section == 1)
            {
                Select(form, "Способ размещения", "Новый лист", "Новый лист", "Существующий лист", "Без размещения");
                Select(form, "Основная надпись", "А2 · Альбомная", "А2 · Альбомная", "А1 · Альбомная"); Field(form, "Формат листа (А)", "2", prefix + "format", numeric: true);
                Field(form, "Формула имени листа", "Развёртки. [Имя помещения]", prefix + "name", true);
                Field(form, "Поиск существующего листа", "", prefix + "search"); Select(form, "Целевой лист", "АР-101 · Развёртки. Переговорная", "АР-101 · Развёртки. Переговорная", "АР-102 · Развёртки. Кабинет");
                Select(form, "Раскладка видов", "Автоматически", "Автоматически", "По числу в строке", "От выбранной точки"); Field(form, "Видов в строке", "4", prefix + "count", numeric: true);
                Pair(form, "Старт X, мм", "20", "Старт Y, мм", "250", prefix, true); Pair(form, "Зазор X, мм", "15", "Зазор Y, мм", "15", prefix + "gap", true);
                Select(form, "Тип видового экрана", "Без заголовка", "Без заголовка", "Название под видом"); Select(form, "Привязка заголовка", "Слева снизу", "Слева снизу", "По центру"); Pair(form, "Смещение X, мм", "0", "Смещение Y, мм", "0", prefix + "titleoffset", true);
            }
            else if (mode == 0 && section == 2)
            {
                Select(form, "План-схема", "Создать новый", "Создать новый", "Использовать существующий", "Не добавлять");
                form.Children.Add(Hint("Схема повторяется на каждом листе с развёртками. Графическое изображение в этом окне не выводится.", new Thickness(0, 10, 0, 0)));
                Field(form, "Формула имени вида", "План-схема. [Номер помещения]", prefix + "formula"); Select(form, "Шаблон вида", "АР · План помещения", "АР · План помещения", "Без шаблона");
                Pair(form, "Масштаб", "100", "Отступ обрезки, мм", "300", prefix, true); Select(form, "Марка помещения", "Номер и имя", "Номер и имя", "Номер");
                Pair(form, "Справа, мм", "15", "Снизу, мм", "15", prefix + "position", true); form.Children.Add(Button("Указать положение в Revit", () => Feedback("Выбор точки подключается в рабочем SAB. В макете редактируйте отступы вручную.")));
            }
            else if (mode == 0)
            {
                Check(form, "Маркировать углы на плане", true); Select(form, "Тип марки на плане", "SAB · Марка угла", "SAB · Марка угла", "SAB · Угол с направлением");
                Check(form, "Маркировать углы на листе", true); Select(form, "Тип марки на листе", "SAB · Номер угла", "SAB · Номер угла", "SAB · Марка угла");
                Select(form, "Состав подписи", "Номер угла", "Номер угла", "Начальный и конечный угол"); Select(form, "Положение относительно вида", "Сверху", "Сверху", "Снизу"); form.Children.Add(Button("Настроить выравнивание углов…", () => Feedback("Дополнительное окно выравнивания будет согласовано отдельно. В этом макете показана точка входа.")));
            }
            else if (section == 0)
            {
                Select(form, "Тип плана", "План этажа", "План этажа", "План потолков"); Select(form, "Вид-образец", "01 · План этажа · Образец", "01 · План этажа · Образец", "01 · План потолков · Образец");
                Select(form, "Лист-образец", "АР-00 · Образец листа", "АР-00 · Образец листа", "АР-10 · План этажа"); Select(form, "Шаблон вида", "АР · Планы", "АР · Планы", "АР · Потолки", "Без шаблона");
                Select(form, "Основная надпись", "А2 · Альбомная", "А2 · Альбомная", "А1 · Альбомная"); Select(form, "Тип видового экрана", "Название под видом", "Название под видом", "Без заголовка");
                form.Children.Add(Hint("Индивидуальные имена, масштаб и шаблон редактируются в таблице.", new Thickness(0, 14, 0, 0)));
                form.Children.Add(Button("Загрузить таблицу…", () => Feedback("Загрузка таблицы будет использовать действующий импорт SAB после согласования макета.")));
            }
            else if (section == 1)
            {
                Select(form, "Положение вида", "Как на листе-образце", "Как на листе-образце", "По точке", "Ручной ввод"); Pair(form, "X центра, мм", "150", "Y центра, мм", "200", prefix, true); form.Children.Add(Button("Указать точку вида", () => Feedback("В тесте координаты задаются вручную. Выбор точки подключается в Revit.")));
                Select(form, "Положение заголовка", "Как на листе-образце", "Как на листе-образце", "По точке", "Ручной ввод"); Pair(form, "X заголовка, мм", "0", "Y заголовка, мм", "-10", prefix + "title", true); form.Children.Add(Button("Указать точку заголовка", () => Feedback("Выбор точки заголовка подключается при переносе в рабочий SAB."))); Check(form, "Сохранять настройки", true);
            }
            else if (section == 2)
            {
                Check(form, "Копировать лист с детализацией", true);
                foreach (string name in new[] { "Ведомости", "Легенды", "Чертёжные виды", "Линии детализации", "Области заливки", "Текстовые примечания", "Типовые аннотации", "Изображения" }) Check(form, name, name != "Изображения");
                form.Children.Add(Hint("Состав копирования относится к листу-образцу. Рисунок листа в предпросмотре не нужен.", new Thickness(0, 16, 0, 0)));
            }
            else
            {
                Select(form, "Структура комплекта", "Одноэтажная", "Одноэтажная", "Многоэтажная", "Многовидовая");
                form.Children.Add(Hint("Для каждого этажа и зоны можно задать собственные образцы.", new Thickness(0, 8, 0, 0)));
                Select(form, "Этаж", "01 этаж", "01 этаж", "02 этаж", "03 этаж"); Select(form, "Зона", "Корпус А", "Корпус А", "Корпус Б");
                Select(form, "Вид-образец этажа", "01 · План этажа · Образец", "01 · План этажа · Образец", "02 · План этажа · Образец"); Select(form, "Лист-образец зоны", "АР-00 · Образец листа", "АР-00 · Образец листа", "АР-10 · План этажа");
                Field(form, "Параметр группирования листов", "SAB_Раздел", prefix + "group"); Field(form, "Значение группы", "Архитектурные решения", prefix + "value");
            }
            return form;
        }

        FrameworkElement WorkArea(int mode)
        {
            var rows = mode == 0 ? Data.Elevations : Data.Sheets; var table = CreateGrid(rows); mainGrids[mode] = table;
            CheckColumn(table, "", "Enabled", 40);
            if (mode == 1) { TextColumn(table, "Этаж", "Floor", 75); TextColumn(table, "Тип плана", "Kind", 105); }
            TextColumn(table, "Имя вида", "Name", 1, true); TextColumn(table, "1 :", "Scale", 58); TextColumn(table, "Шаблон вида", "Template", 125);
            TextColumn(table, "№ листа", "Number", 84); TextColumn(table, "Имя листа", "Sheet", 0.8, true); DeleteColumn(table, rows);
            table.SelectionChanged += (s, e) => { UpdatePreview(); Motion.Reveal(preview, 160); }; table.SelectedIndex = 0;
            var root = Rows("Auto", "Auto", "*"); var bar = Columns("*", "Auto"); bar.Margin = new Thickness(16, 14, 16, 12);
            var names = new StackPanel(); names.Children.Add(Text(mode == 0 ? "Комплект развёрток" : "Создаваемые виды и листы", 16, true)); names.Children.Add(Hint(mode == 0 ? "Выберите нужные виды и проверьте результат наименования." : "Правьте значения в ячейках. Каждая строка — отдельный вид и лист.", new Thickness(0, 4, 0, 0))); bar.Children.Add(names);
            var add = Button("Добавить", () => { var row = new ReviewRow { Name = "Новый вид", Number = "АР-" + (rows.Count + 1).ToString("00"), Sheet = "Новый лист" }; row.PropertyChanged += RowChanged; rows.Add(row); table.SelectedItem = row; table.ScrollIntoView(row); UpdateSummary(); }); Grid.SetColumn(add, 1); bar.Children.Add(add); Add(root, bar, 0);
            var tools = Columns("*", "Auto"); tools.Margin = new Thickness(16, 0, 16, 12); var search = Search(table, "Поиск по видам и листам"); tools.Children.Add(search);
            var bulk = new StackPanel { Orientation = Orientation.Horizontal };
            bulk.Children.Add(Button("Выбрать все", () => { foreach (var r in rows) r.Enabled = true; UpdateSummary(); }));
            if (mode == 1) bulk.Children.Add(Button("Имя листа → вида", () => { foreach (var r in rows.Where(r => r.Enabled)) r.Name = r.Sheet; }));
            Grid.SetColumn(bulk, 1); tools.Children.Add(bulk); Add(root, tools, 1); Add(root, table, 2); return root;
        }

        FrameworkElement ParameterArea()
        {
            var root = Rows("Auto", "*"); var tabs = new WrapPanel { Margin = new Thickness(14, 12, 14, 12) };
            string[] names = { "Правила", "Источники", "Значения и сопоставления", "Категории" };
            paramHost = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
            for (int i = 0; i < names.Length; i++) { int n = i; var b = Button(names[i], () => SetParameterSection(n)); b.Tag = n; b.Margin = new Thickness(0, 0, 4, 0); tabs.Children.Add(b); }
            Add(root, tabs, 0); Add(root, paramHost, 1);
            ruleGrid = CreateGrid(Data.Rules); mainGrids[2] = ruleGrid; CheckColumn(ruleGrid, "Вкл.", "Enabled", 48); ComboColumn(ruleGrid, "Группа", "Group", Data.Groups, 135); TextColumn(ruleGrid, "Заполняемый параметр", "Name", 1.3, true); ComboColumn(ruleGrid, "Откуда взять значение", "Source", Data.Sources, 208); CheckColumn(ruleGrid, "Проверять", "Required", 90); DeleteColumn(ruleGrid, Data.Rules);
            ruleGrid.SelectionChanged += (s, e) => { UpdateSourceEditor(); UpdatePreview(); Motion.Reveal(preview, 160); };
            ruleGrid.SelectedIndex = 0; SetParameterSection(0); return root;
        }

        public void SetParameterSection(int section)
        {
            if (paramHost == null) return; Section = section; ruleGrid.CommitEdit(DataGridEditingUnit.Cell, true); ruleGrid.CommitEdit(DataGridEditingUnit.Row, true);
            var tabs = (WrapPanel)((Grid)paramHost.Parent).Children[0];
            foreach (UiButton b in tabs.Children) { b.Appearance = (int)b.Tag == section ? ControlAppearance.Primary : ControlAppearance.Transparent; b.Foreground = (int)b.Tag == section ? Brushes.White : Brush("#1F2937"); }
            string key = "parameter:" + section;
            if (!forms.TryGetValue(key, out var page))
            {
                if (section == 0)
                {
                    var grid = Rows("Auto", "Auto", "*"); var bar = Columns("*", "Auto"); bar.Margin = new Thickness(16, 4, 16, 10); bar.Children.Add(Text("Правила заполнения", 16, true));
                    var add = Button("Добавить правило", () => { var row = new ReviewRow { Name = "SAB_Новый параметр", Source = "Постоянное значение", Required = false }; row.PropertyChanged += RowChanged; Data.Rules.Add(row); ruleGrid.SelectedItem = row; UpdateSummary(); }); Grid.SetColumn(add, 1); bar.Children.Add(add); Add(grid, bar, 0);
                    var note = Hint("«Проверять» ищет пустые значения. Запись определяется галочкой «Вкл.».", new Thickness(16, 0, 16, 12)); Add(grid, note, 1); Add(grid, ruleGrid, 2); page = grid;
                }
                else if (section == 1)
                {
                    var stack = new StackPanel { Margin = new Thickness(18, 0, 18, 18) }; stack.Children.Add(Text("Общие источники", 16, true));
                    Select(stack, "Как выбирать помещение", "Вручную после команды", "Вручную после команды", "Определять автоматически");
                    stack.Children.Add(Hint("Выделите элементы → «Занести параметры» → укажите помещение. Одно помещение используется для всего выделения.", new Thickness(0, 8, 0, 0)));
                    var automatic = new StackPanel(); Select(automatic, "Сторона дверей и окон", "Только однозначное помещение", "Только однозначное помещение", "Из помещения (FromRoom)", "В помещение (ToRoom)"); automatic.Children.Add(Hint("Автоматический режим использует контакты с помещениями. Неоднозначные элементы пропускаются с причиной.")); stack.Children.Add(Disclosure("Автоматическое определение помещения", automatic));
                    Select(stack, "Откуда брать уровень", "Уровень элемента", "Уровень элемента", "Уровень текущего плана"); stack.Children.Add(Hint("Для выделения на нескольких этажах используйте уровень элемента.", new Thickness(0, 8, 0, 0))); page = Scroll(stack);
                }
                else if (section == 2)
                {
                    var stack = new StackPanel { Margin = new Thickness(18, 0, 18, 18) }; stack.Children.Add(Text("Таблица уровней", 16, true)); stack.Children.Add(Hint("Уровень модели → записываемое значение. Привязка сохраняется по ID уровня.", new Thickness(0, 6, 0, 12)));
                    var levels = CreateGrid(Data.Levels); levels.Height = 260; TextColumn(levels, "Уровень модели", "Input", 1, true); TextColumn(levels, "Значение параметра", "Output", 1, true); stack.Children.Add(levels);
                    stack.Children.Add(Text("Таблицы сопоставления", 16, true, new Thickness(0, 22, 0, 6))); Select(stack, "Исходное значение", "Имя помещения", "Имя помещения", "Номер помещения", "Параметр элемента", "Параметр помещения", "Название уровня");
                    var map = CreateGrid(Data.Mappings); map.Height = 140; TextColumn(map, "Исходное значение", "Input", 1, true); TextColumn(map, "Записать", "Output", 1, true); stack.Children.Add(map); Check(stack, "Не учитывать регистр", true); page = Scroll(stack);
                }
                else
                {
                    var root = Rows("Auto", "Auto", "*"); var options = new StackPanel { Margin = new Thickness(16, 0, 16, 14) }; options.Children.Add(Text("Область заполнения", 16, true)); Check(options, "Обрабатывать элементы внутри выделенных групп", true); Check(options, "Добавлять отсутствующие привязки к категориям", false); options.Children.Add(Hint("Существующие категории и заполненные значения сохраняются.", new Thickness(0, 8, 0, 0))); Add(root, options, 0);
                    var categories = CreateGrid(Data.Categories); CheckColumn(categories, "Выбрано", "Enabled", 80); TextColumn(categories, "Категория", "Name", 1, true);
                    var toolbar = Columns("*", "Auto"); toolbar.Margin = new Thickness(16, 0, 16, 12); toolbar.Children.Add(Search(categories, "Поиск категории")); var select = Button("Выбрать все", () => { foreach (var row in Data.Categories) row.Enabled = true; }); Grid.SetColumn(select, 1); toolbar.Children.Add(select); Add(root, toolbar, 1); Add(root, categories, 2); page = root;
                }
                forms[key] = page;
            }
            paramHost.Content = page; ActiveGrid = section == 0 ? ruleGrid : FindGrid(page); Motion.Reveal(page, 140, 4); Dispatcher.BeginInvoke(new Action(() => { ApplyDensity(); Motion.Attach(page); }), DispatcherPriority.Loaded);
        }

        void UpdateSourceEditor()
        {
            if (sourceFields == null || SelectedRule == null) return; sourceFields.Children.Clear(); var rule = SelectedRule;
            sourceFields.Children.Add(Text(rule.Name, 14, true)); sourceFields.Children.Add(Hint(rule.Group, new Thickness(0, 5, 0, 6)));
            var source = Select(sourceFields, "Откуда взять значение", rule.Source, Data.Sources); source.SelectionChanged += (s, e) => { if (source.SelectedItem != null) { rule.Source = source.SelectedItem.ToString(); UpdateSourceEditor(); UpdatePreview(); } };
            if (rule.Source == "Из помещения")
            {
                var choices = new[] { "Номер помещения", "Имя помещения", "Параметр помещения" };
                string current = rule.RoomField;
                var field = Select(sourceFields, "Что взять из помещения", current, choices);
                if (current == choices[2]) { var p = Select(sourceFields, "Параметр помещения", rule.RoomParameter, "Корпус", "Отделка", "Назначение"); p.SelectionChanged += (s, e) => { rule.RoomParameter = p.SelectedItem.ToString(); UpdatePreview(); }; }
                field.SelectionChanged += (s, e) => { rule.RoomField = field.SelectedItem.ToString(); UpdateSourceEditor(); UpdatePreview(); };
                sourceFields.Children.Add(Hint("Помещение выбирается вручную после запуска заполнения.", new Thickness(0, 14, 0, 0)));
            }
            else if (rule.Source == "Из модели: уровень")
            { sourceFields.Children.Add(Hint("Значение берётся из таблицы уровней в разделе «Значения и сопоставления».", new Thickness(0, 12, 0, 0))); sourceFields.Children.Add(Button("Открыть таблицу уровней", () => SetParameterSection(2))); }
            else if (rule.Source == "Из модели: параметр") { var p = Select(sourceFields, "Исходный параметр элемента", rule.ElementParameter, "Комментарии", "Марка", "Тип"); p.SelectionChanged += (s, e) => rule.ElementParameter = p.SelectedItem.ToString(); }
            else if (rule.Source == "По таблице сопоставления") { sourceFields.Children.Add(Hint("Настройте исходное поле и пары значений в разделе сопоставлений.", new Thickness(0, 12, 0, 0))); sourceFields.Children.Add(Button("Настроить сопоставление", () => SetParameterSection(2))); }
            else if (rule.Source == "Корпус вручную" || rule.Source == "Выбрать перед записью")
            { Field(sourceFields, "Допустимые значения, через ;", rule.ManualValues, "rule:" + rule.Id + ":choices", changed: v => rule.ManualValues = v); sourceFields.Children.Add(Hint("Перед записью появится выбор общего значения для выделения.", new Thickness(0, 12, 0, 0))); }
            else Field(sourceFields, "Постоянное значение", rule.Value, "rule:" + rule.Id + ":constant", changed: v => { rule.Value = v; UpdatePreview(); });
            var required = new CheckBox { Content = "Проверять заполненность", IsChecked = rule.Required }; required.Checked += (s, e) => rule.Required = true; required.Unchecked += (s, e) => rule.Required = false; sourceFields.Children.Add(required);
            var note = Hint("Заполняемый параметр и группу можно изменить в таблице правил.", new Thickness(0, 22, 0, 0)); sourceFields.Children.Add(note); Motion.Attach(sourceFields);
        }

        void RowChanged(object sender, PropertyChangedEventArgs e) { UpdateSummary(); UpdatePreview(); if (Instrument == 2 && ReferenceEquals(sender, SelectedRule) && (e.PropertyName == "Source" || e.PropertyName == "Name")) Dispatcher.BeginInvoke(new Action(UpdateSourceEditor)); }
        void UpdatePreview()
        {
            if (previewName == null) return;
            if (Instrument == 2)
            {
                var rule = SelectedRule;
                previewName.Text = rule == null ? "Добавьте правило заполнения" : rule.Name + " = " + RulePreviewValue(rule);
                previewMeta.Text = rule == null ? "Выберите параметр и источник значения." : $"{rule.Source}   /   {RuleSourceDetail(rule)}\nПример для помещения 101 «Переговорная». { (rule.Enabled ? "Правило включено." : "Правило выключено.") }";
            }
            else
            {
                var row = ActiveGrid?.SelectedItem as ReviewRow ?? (Instrument == 0 ? Data.Elevations.FirstOrDefault() : Data.Sheets.FirstOrDefault());
                previewName.Text = row?.Name ?? "Выберите строку";
                previewMeta.Text = row == null ? "Добавьте вид или измените поиск." : $"{row.Number}   /   {row.Sheet}\n{row.Template}   /   Масштаб 1:{row.Scale}";
            }
        }
        void UpdateSummary()
        {
            var rows = Instrument == 0 ? Data.Elevations : Instrument == 1 ? Data.Sheets : Data.Rules;
            summary.Text = Instrument == 2 ? $"Правил: {rows.Count}    Включено: {rows.Count(r => r.Enabled)}    Проверять: {rows.Count(r => r.Required)}" : $"Строк: {rows.Count}    Выбрано: {rows.Count(r => r.Enabled)}";
        }
        void ApplyDensity()
        {
            bool compact = Height < 790;
            description.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            foreach (var element in Descendants<FrameworkElement>(host).Where(e => e.Tag as string == "compact-hide")) element.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            if (previews.TryGetValue(Instrument, out var current)) { current.Item3.MinHeight = compact ? 104 : 120; current.Item3.Padding = new Thickness(compact ? 12 : 16); current.Item1.Margin = compact ? new Thickness(0, 0, 0, 6) : new Thickness(0, 6, 0, 6); }
        }
        string RulePreviewValue(ReviewRow rule)
        {
            if (rule.Source == "Из помещения") return rule.RoomField == "Номер помещения" ? "101" : rule.RoomField == "Имя помещения" ? "Переговорная" : rule.RoomParameter == "Корпус" ? "А" : rule.RoomParameter == "Отделка" ? "Тип 02" : "Общее помещение";
            if (rule.Source == "Из модели: уровень") return Data.Levels.FirstOrDefault()?.Output ?? "уровень не сопоставлен";
            if (rule.Source == "Из модели: параметр") return rule.ElementParameter == "Марка" ? "Д-01" : rule.ElementParameter == "Тип" ? "Дверь 900 × 2100" : "Пример комментария";
            if (rule.Source == "По таблице сопоставления") return Data.Mappings.FirstOrDefault(r => r.Input == "Переговорная")?.Output ?? "сопоставление не найдено";
            if (rule.Source == "Выбрать перед записью" || rule.Source == "Корпус вручную") return "выбор перед записью";
            return string.IsNullOrWhiteSpace(rule.Value) ? "значение не задано" : rule.Value;
        }
        static string RuleSourceDetail(ReviewRow rule) => rule.Source == "Из помещения" ? rule.RoomField + (rule.RoomField == "Параметр помещения" ? ": " + rule.RoomParameter : "") : rule.Source == "Из модели: параметр" ? rule.ElementParameter : rule.Source == "Постоянное значение" ? "Константа модели" : rule.Source == "Из модели: уровень" ? "Таблица уровней" : rule.Source == "По таблице сопоставления" ? "Имя помещения → значение" : rule.ManualValues;
        public string Validate()
        {
            var rows = Instrument == 0 ? Data.Elevations : Instrument == 1 ? Data.Sheets : Data.Rules;
            if (!rows.Any(r => r.Enabled)) return "Включите хотя бы одну строку перед тестом.";
            if (rows.Any(r => r.Enabled && string.IsNullOrWhiteSpace(r.Name))) return "Укажите имя вида или заполняемый параметр в каждой включённой строке.";
            if (Instrument != 2 && rows.Any(r => r.Enabled && (!int.TryParse(r.Scale, out int scale) || scale <= 0))) return "Масштаб должен быть целым числом больше нуля. Исправьте значение в таблице.";
            if (Instrument == 1 && rows.Where(r => r.Enabled).GroupBy(r => r.Number).Any(g => g.Count() > 1)) return "Номера создаваемых листов повторяются. Задайте уникальные номера.";
            var invalidKey = numericLabels.Keys.FirstOrDefault(key => key.StartsWith(Instrument + ":", StringComparison.Ordinal) && (!double.TryParse(settings[key], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out double value) || double.IsNaN(value) || double.IsInfinity(value)));
            if (invalidKey == null) return null;
            int section = int.Parse(invalidKey.Split(':')[1]); string[] sections = Instrument == 0 ? new[] { "Виды", "Лист", "План-схема", "Марки" } : new[] { "Образцы", "Размещение", "Копирование", "Структура" };
            return $"Введите число в поле «{numericLabels[invalidKey]}», раздел «{sections[section]}».";
        }
        public async void Run()
        {
            if (running) return; ActiveGrid?.CommitEdit(DataGridEditingUnit.Cell, true); ActiveGrid?.CommitEdit(DataGridEditingUnit.Row, true);
            string error = Validate(); if (error != null) { Feedback(error, true); return; }
            running = true; primary.IsEnabled = false; foreach (var b in nav) b.IsEnabled = false;
            Feedback(Instrument == 2 ? "Тест: проверяем профиль настроек…" : "Тест: проверяем выбранные строки…");
            await System.Threading.Tasks.Task.Delay(320);
            Feedback(Instrument == 2 ? "Профиль проверен в тесте. Настройки рабочего SAB и элементы Revit не изменены." : "Тест завершён. Таблица проверена; виды и листы в Revit не создавались.");
            running = false; primary.IsEnabled = true; foreach (var b in nav) b.IsEnabled = true;
        }
        void Feedback(string message, bool error = false) { status.Text = message; status.Foreground = Brush(error ? "#B42318" : "#667085"); Motion.Reveal(status, 140); }

        ComboBox Select(StackPanel parent, string label, string value, params string[] choices)
        {
            parent.Children.Add(Label(label)); var input = new ComboBox { ItemsSource = choices, SelectedItem = value, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 1) }; AutomationProperties.SetName(input, label); parent.Children.Add(input); return input;
        }
        void Field(StackPanel parent, string label, string value, string key, bool multiline = false, Action<string> changed = null, bool numeric = false)
        {
            parent.Children.Add(Label(label)); if (!settings.ContainsKey(key)) settings[key] = value;
            var input = new TextBox { Text = settings[key], TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multiline ? 72 : 34, VerticalContentAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center };
            if (numeric) { numericLabels[key] = label; input.SetBinding(TextBox.TextProperty, new Binding("Value") { Source = new NumericValue { Value = settings[key] }, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged, ValidatesOnDataErrors = true }); }
            AutomationProperties.SetName(input, label); input.TextChanged += (s, e) => { settings[key] = input.Text; changed?.Invoke(input.Text); }; parent.Children.Add(input);
        }
        void Pair(StackPanel parent, string l, string lv, string r, string rv, string prefix, bool numeric = false)
        {
            var grid = Columns("*", "12", "*"); var left = new StackPanel(); var right = new StackPanel(); Field(left, l, lv, prefix + l, numeric: numeric); Field(right, r, rv, prefix + r, numeric: numeric); grid.Children.Add(left); Grid.SetColumn(right, 2); grid.Children.Add(right); parent.Children.Add(grid);
        }
        static void MarkNumeric(TextBox input)
        {
            var expression = input.GetBindingExpression(TextBox.TagProperty);
            if (expression == null) { input.SetBinding(TextBox.TagProperty, new Binding("Text") { Source = input }); expression = input.GetBindingExpression(TextBox.TagProperty); }
            if (!double.TryParse(input.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out _)) { Validation.MarkInvalid(expression, new ValidationError(new ExceptionValidationRule(), expression, "Введите число", null)); input.ToolTip = "Введите число"; }
            else { Validation.ClearInvalid(expression); input.ClearValue(ToolTipProperty); }
        }
        static void Check(StackPanel parent, string label, bool value) { var input = new CheckBox { Content = label, IsChecked = value }; AutomationProperties.SetName(input, label); parent.Children.Add(input); }
        static TextBlock Label(string text) => Text(text, 12, true, new Thickness(0, 13, 0, 5));
        static Expander Disclosure(string title, FrameworkElement content) => new Expander { Header = title, Content = content, IsExpanded = false, Margin = new Thickness(0, 14, 0, 0) };
        static ScrollViewer Scroll(FrameworkElement child) => new ScrollViewer { Content = child, Style = (Style)Application.Current.FindResource("LabFormScrollViewerStyle"), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        static Border LinePanel(UIElement child, bool top) => new Border { Background = Brushes.White, BorderBrush = Brush("#D8DEE8"), BorderThickness = top ? new Thickness(0, 1, 0, 0) : new Thickness(0, 0, 0, 1), Child = child };
        static Border Panel(UIElement child, double padding = 16) => new Border { Background = Brushes.White, BorderBrush = Brush("#D8DEE8"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(padding), Child = child };
        static TextBlock Text(string text, double size = 13, bool bold = false, Thickness? margin = null) => new TextBlock { Text = text, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Foreground = Brush("#1F2937"), VerticalAlignment = VerticalAlignment.Center, Margin = margin ?? new Thickness(), TextTrimming = TextTrimming.CharacterEllipsis };
        static TextBlock Hint(string text, Thickness? margin = null) { var result = Text(text, 12, false, margin); result.Foreground = Brush("#667085"); result.TextWrapping = TextWrapping.Wrap; return result; }
        static SolidColorBrush Brush(string color) => (SolidColorBrush)new BrushConverter().ConvertFromString(color);
        static void SetButtonLabel(UiButton button, string text) { var label = new TextBlock { Text = text }; label.SetBinding(TextBlock.ForegroundProperty, new Binding("Foreground") { Source = button }); button.Content = label; AutomationProperties.SetName(button, text); }
        static UiButton Button(string content, Action click) { var result = new UiButton(); SetButtonLabel(result, content); result.Click += (s, e) => click(); return result; }
        static Grid Columns(params string[] sizes) { var grid = new Grid(); foreach (string size in sizes) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = (GridLength)new GridLengthConverter().ConvertFromString(size) }); return grid; }
        static Grid Rows(params string[] sizes) { var grid = new Grid(); foreach (string size in sizes) grid.RowDefinitions.Add(new RowDefinition { Height = (GridLength)new GridLengthConverter().ConvertFromString(size) }); return grid; }
        static void Add(Grid grid, UIElement child, int row) { Grid.SetRow(child, row); grid.Children.Add(child); }
        static DataGrid CreateGrid(ObservableCollection<ReviewRow> items)
        {
            var result = new DataGrid { ItemsSource = items, MinRowHeight = 42, CanUserResizeColumns = true, CanUserSortColumns = true, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            result.PreviewMouseLeftButtonDown += (s, e) => { var cell = Ancestor<DataGridCell>(e.OriginalSource as DependencyObject); if (cell == null || cell.IsReadOnly || Ancestor<ButtonBase>(e.OriginalSource as DependencyObject) != null || Ancestor<TextBox>(e.OriginalSource as DependencyObject) != null) return; cell.Focus(); result.BeginEdit(); result.Dispatcher.BeginInvoke(new Action(() => { var editor = Descendants<TextBox>(cell).FirstOrDefault(); if (editor != null) { editor.Focus(); editor.SelectAll(); } }), DispatcherPriority.Input); };
            return result;
        }
        static void TextColumn(DataGrid grid, string header, string path, double width, bool star = false)
        {
            grid.Columns.Add(new DataGridTextColumn { Header = header, Binding = new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = new DataGridLength(width, star ? DataGridLengthUnitType.Star : DataGridLengthUnitType.Pixel), MinWidth = star ? 130 : width, ElementStyle = (Style)Application.Current.FindResource("GridText"), EditingElementStyle = (Style)Application.Current.FindResource("GridEdit") });
        }
        static void CheckColumn(DataGrid grid, string header, string path, double width)
        {
            var factory = new FrameworkElementFactory(typeof(CheckBox)); factory.SetBinding(ToggleButton.IsCheckedProperty, new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }); factory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center); factory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); factory.SetValue(FrameworkElement.MarginProperty, new Thickness(0)); factory.SetValue(AutomationProperties.NameProperty, header.Length == 0 ? "Выбрать строку" : header);
            grid.Columns.Add(new DataGridTemplateColumn { Header = header, Width = width, CellTemplate = new DataTemplate { VisualTree = factory } });
        }
        static void ComboColumn(DataGrid grid, string header, string path, string[] choices, double width)
        {
            var factory = new FrameworkElementFactory(typeof(ComboBox)); factory.SetValue(ItemsControl.ItemsSourceProperty, choices); factory.SetBinding(Selector.SelectedItemProperty, new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }); factory.SetValue(FrameworkElement.MarginProperty, new Thickness(5, 4, 5, 4)); factory.SetValue(AutomationProperties.NameProperty, header);
            var display = new FrameworkElementFactory(typeof(TextBlock)); display.SetBinding(TextBlock.TextProperty, new Binding(path)); display.SetValue(FrameworkElement.StyleProperty, Application.Current.FindResource("GridText"));
            grid.Columns.Add(new DataGridTemplateColumn { Header = header, Width = width, CellTemplate = new DataTemplate { VisualTree = display }, CellEditingTemplate = new DataTemplate { VisualTree = factory } });
        }
        void DeleteColumn(DataGrid grid, ObservableCollection<ReviewRow> rows)
        {
            var factory = new FrameworkElementFactory(typeof(UiButton)); factory.SetValue(Control.PaddingProperty, new Thickness(4)); factory.SetValue(FrameworkElement.MinHeightProperty, 28.0); factory.SetValue(FrameworkElement.WidthProperty, 28.0); factory.SetValue(FrameworkElement.HeightProperty, 28.0); factory.SetValue(UiButton.AppearanceProperty, ControlAppearance.Transparent); factory.SetValue(FrameworkElement.ToolTipProperty, "Удалить строку"); factory.SetValue(AutomationProperties.NameProperty, "Удалить строку");
            var icon = new FrameworkElementFactory(typeof(SymbolIcon)); icon.SetValue(SymbolIcon.SymbolProperty, SymbolRegular.Delete24); icon.SetValue(Control.FontSizeProperty, 16.0); factory.AppendChild(icon);
            factory.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((s, e) => { var row = ((FrameworkElement)s).DataContext as ReviewRow; if (row == null) return; rows.Remove(row); if (grid.SelectedItem == null && rows.Count > 0) grid.SelectedIndex = 0; UpdateSummary(); UpdateSourceEditor(); UpdatePreview(); }));
            grid.Columns.Add(new DataGridTemplateColumn { Header = "", Width = 40, IsReadOnly = true, CellTemplate = new DataTemplate { VisualTree = factory } });
        }
        TextBox Search(DataGrid grid, string placeholder)
        {
            var input = new Wpf.Ui.Controls.TextBox { PlaceholderText = placeholder, Margin = new Thickness(0, 0, 12, 0), MaxWidth = 440, HorizontalAlignment = HorizontalAlignment.Stretch }; AutomationProperties.SetName(input, placeholder);
            var view = CollectionViewSource.GetDefaultView(grid.ItemsSource); input.TextChanged += (s, e) => { grid.CommitEdit(DataGridEditingUnit.Cell, true); grid.CommitEdit(DataGridEditingUnit.Row, true); string query = input.Text.Trim(); view.Filter = item => { var r = (ReviewRow)item; return string.IsNullOrEmpty(query) || (r.Name + " " + r.Number + " " + r.Sheet + " " + r.Floor).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0; }; Feedback(view.IsEmpty ? "Ничего не найдено. Измените запрос или очистите поиск." : "Поиск применяется к демонстрационным данным."); }; return input;
        }
        static DataGrid FindGrid(DependencyObject root) => Descendants<DataGrid>(root).FirstOrDefault();
        static T Ancestor<T>(DependencyObject node) where T : DependencyObject { while (node != null) { if (node is T result) return result; node = VisualTreeHelper.GetParent(node); } return null; }
        public static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject { if (root == null) yield break; if (root is T result) yield return result; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(root, i))) yield return child; }
    }
}
