"""Shared refinement of the source preview; never writes production sources."""
import xml.etree.ElementTree as E
P='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
X='http://schemas.microsoft.com/winfx/2006/xaml'
D='clr-namespace:SAB.UI.PreviewDesign;assembly=SAB'
E.register_namespace('design', D)
def t(name): return '{'+P+'}'+name
def key(name): return '{'+X+'}'+name
def set_value(style, prop, value):
    current=next((n for n in style.findall(t('Setter')) if n.get('Property')==prop),None)
    if current is None: current=E.SubElement(style,t('Setter'),Property=prop)
    current.set('Value',value)

def refine_styles(root):
    for style in root.findall(t('Style')):
        if style.get('TargetType')=='TabControl':
            for border in style.iter(t('Border')): border.set('HorizontalAlignment','Left')
            for panel in style.iter(t('TabPanel')): panel.set('HorizontalAlignment','Left')
        if style.get('TargetType')=='GroupBox': set_value(style,'Margin','0,0,0,16')
        if style.get('TargetType')=='DataGridCell':
            set_value(style,'HorizontalContentAlignment','Stretch')
            for presenter in style.iter(t('ContentPresenter')):presenter.set('HorizontalAlignment','{TemplateBinding HorizontalContentAlignment}')
        if style.get(key('Key'))=='SabDataGridActionsPanelStyle':
            set_value(style,'VerticalAlignment','Center');set_value(style,'Margin','8,0')
    extra=E.fromstring('''<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Style x:Key="SabInformationBorderStyle" TargetType="Border">
      <Setter Property="Background" Value="{DynamicResource SabBrush.TableHeaderBackground}"/><Setter Property="BorderBrush" Value="{DynamicResource SabBrush.Border}"/><Setter Property="BorderThickness" Value="1"/><Setter Property="CornerRadius" Value="4"/><Setter Property="Padding" Value="12"/>
    </Style>
    <Style TargetType="RadioButton">
      <Setter Property="Foreground" Value="{DynamicResource SabBrush.TextSecondary}"/><Setter Property="MinHeight" Value="38"/><Setter Property="Padding" Value="8,6"/><Setter Property="Margin" Value="0"/><Setter Property="VerticalContentAlignment" Value="Center"/><Setter Property="FocusVisualStyle" Value="{StaticResource Focus}"/>
      <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="RadioButton"><Border Background="Transparent" Padding="{TemplateBinding Padding}"><TextBlock Text="{TemplateBinding Content}" Foreground="{TemplateBinding Foreground}" FontSize="12" TextWrapping="Wrap" TextAlignment="Center" VerticalAlignment="Center" HorizontalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter Property="Foreground" Value="White"/><Setter Property="FontWeight" Value="SemiBold"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
    </Style>
    <ControlTemplate x:Key="SabSwitchTemplate" TargetType="ToggleButton"><Border x:Name="Track" Background="{DynamicResource SabBrush.Border}" CornerRadius="11" Width="38" Height="22"><Ellipse x:Name="Knob" Fill="White" Width="16" Height="16" Margin="3" HorizontalAlignment="Left"/></Border><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Track" Property="Background" Value="{DynamicResource SabBrush.Accent}"/><Setter TargetName="Knob" Property="HorizontalAlignment" Value="Right"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.5"/></Trigger></ControlTemplate.Triggers></ControlTemplate>
    </ResourceDictionary>''')
    root.extend(list(extra))

def capsule(radios):
    grid=E.Element(t('Grid'),{'Margin':'0,0,0,12','{'+D+'}Capsule.IsGroup':'True'})
    E.SubElement(grid,t('Border'),Background='{DynamicResource SabBrush.TableHeaderBackground}',BorderBrush='{DynamicResource SabBrush.Border}',BorderThickness='1',CornerRadius='20')
    E.SubElement(grid,t('Border'),Tag='CapsuleIndicator',Background='{DynamicResource SabBrush.Accent}',CornerRadius='18',HorizontalAlignment='Left',VerticalAlignment='Center',IsHitTestVisible='False')
    items=E.SubElement(grid,t('UniformGrid'),Rows='1',Columns=str(len(radios)),Margin='3')
    for radio in radios:
        radio.set('Margin','0');radio.set('ToolTip',radio.get('Content',''))
        labels={'Одноэтажная структура':'Один этаж','Многоэтажная структура':'Несколько этажей','Многовидовая структура':'Несколько видов','Размещать вид как на листе-образце':'Как на образце','Размещать вид по точкам':'По координатам'}
        radio.set('Content',labels.get(radio.get('Content'),radio.get('Content')))
        items.append(radio)
    return grid

def refine_markup(win, drawer):
    for panel in list(win.iter()):
        radios=[n for n in list(panel) if n.tag==t('RadioButton')]
        if not radios:continue
        index=list(panel).index(radios[0])
        for radio in radios:panel.remove(radio)
        if panel.tag==t('StackPanel') and panel.get('Orientation')=='Horizontal':panel.set('Orientation','Vertical')
        panel.insert(index,capsule(radios))
    # Each conditional source section keeps its style/triggers, with its own card.
    for expander in list(drawer.iter(t('Expander'))):
        expander.set('Template','{StaticResource SabSectionCardTemplate}');expander.set('IsExpanded','True');expander.set('Margin','0,0,0,16')
    for button in win.iter(t('Button')):
        if button.get('Style') in ['{StaticResource SabRowAddButtonStyle}','{StaticResource SabRowDeleteButtonStyle}']:
            button.set('Margin','4,0');button.set('VerticalAlignment','Center')
    for column in win.iter(t('DataGridTemplateColumn')):
        if column.get('Header')=='Действия':column.set('Width','88');column.set('MinWidth','88');column.set('MaxWidth','88')
        if column.get('Header')=='Масштаб':column.set('Width','90');column.set('MinWidth','90')
        if column.get('Header')=='№ листа':column.set('Width','100')
        if column.get('Header')=='Шаблон вида':column.set('MinWidth','150')
        if column.get('Header')=='Имя листа':column.set('MinWidth','125')
    for parent in list(win.iter()):
        for child in list(parent):
            if child.tag!=t('TextBlock'):continue
            text=child.get('Text','')
            if len(text)<65 or text.startswith('{'):continue
            if child.get('Style') not in ['{StaticResource SabFieldHintTextStyle}','{StaticResource SabWindowSubtitleTextStyle}']:continue
            index=list(parent).index(child);parent.remove(child)
            frame=E.Element(t('Border'),Style='{StaticResource SabInformationBorderStyle}',Margin='0,8,0,12');child.set('Margin','0');frame.append(child);parent.insert(index,frame)

def add_section_template(root):
    root.append(E.fromstring('''<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" x:Key="SabSectionCardTemplate" TargetType="Expander"><Border Background="White" BorderBrush="{DynamicResource SabBrush.Border}" BorderThickness="1" CornerRadius="6" Padding="16"><StackPanel><ContentPresenter ContentSource="Header" Margin="0,0,0,16"/><ContentPresenter/></StackPanel></Border></ControlTemplate>'''))

def refine_parameter_sources(small, settings):
    small=small.replace('internal static ToggleButton Toggle()','internal static ToggleButton Toggle()')
    small=small.replace('FindResource("CheckTemplate")','FindResource("SabSwitchTemplate")').replace('Width = 20, Height = 22','Width = 38, Height = 22')
    small=small.replace('Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(4)','Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(0,0,8,0), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left')
    small=small.replace('Margin = new Thickness(4, 6, 4, 6), TextWrapping','Margin = new Thickness(0, 8, 0, 8), VerticalAlignment = VerticalAlignment.Center, Tag = !bold && text.Length >= 65 ? "SabExplanation" : null, TextWrapping')
    small=small.replace('return new GroupBox { Header = title,','SAB.UI.PreviewDesign.Presentation.Decorate(body); return new GroupBox { Header = title,')
    small=small.replace('window.Content = root;','SAB.UI.PreviewDesign.Presentation.Decorate(body); if (footer != null) SAB.UI.PreviewDesign.Presentation.Decorate(footer); window.Content = root;')
    small=small.replace('window.Content = root;','window.Content = root; window.AddHandler(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent, new SelectionChangedEventHandler((sender,args) => { if(args.OriginalSource is TabControl) window.Dispatcher.BeginInvoke(new Action(() => SAB.UI.PreviewDesign.Presentation.Decorate(body)), System.Windows.Threading.DispatcherPriority.Loaded); }));')
    small=small.replace('new Thickness(24,22,24,18)','new Thickness(24,18,24,12)').replace('new Thickness(20,14,20,14)','new Thickness(24,14,24,14)')
    small=small.replace('header.Children.Add(new TextBlock { Text = subtitle, Style = (Style)window.FindResource("SabWindowSubtitleTextStyle"), Margin = new Thickness(0,6,0,0) });','header.Children.Add(SAB.UI.PreviewDesign.Presentation.Information(new TextBlock { Text = subtitle, Style = (Style)window.FindResource("SabWindowSubtitleTextStyle") }));')
    small=small.replace('Grid.SetRow(header,1);root.Children.Add(header);','foreach(var info in header.Children.OfType<Border>()) info.Margin = new Thickness(0,6,0,0); Grid.SetRow(header,1);root.Children.Add(header);')
    settings=settings.replace('Loaded += (s, e) => SAB.UI.SabLayoutAssist.Apply(this);','Loaded += (s, e) => SAB.UI.PreviewDesign.Presentation.Decorate(this);')
    settings=settings.replace('_rulesGrid.MinRowHeight = 86;', '_rulesGrid.MinRowHeight = 44;')
    settings=settings.replace('_ruleExplanation = Theme.Text("");','_ruleExplanation = Theme.Text(""); _ruleExplanation.Tag = "SabExplanation";')
    settings=settings.replace('_status = Theme.Text("");','_status = Theme.Text(""); _status.Tag = "SabExplanation";')
    # All fixed and minimum widths fit the minimum content viewport (868 DIP).
    settings=settings.replace('_rulesGrid.Columns[1].MinWidth = 190;', '_rulesGrid.Columns[1].MinWidth = 150;')
    settings=settings.replace('_rulesGrid.Columns[3].MinWidth = 300;', '_rulesGrid.Columns[3].MinWidth = 220;')
    settings=settings.replace('_rulesGrid.Columns[4].MinWidth = 210;', '_rulesGrid.Columns[4].MinWidth = 180;')
    settings=settings.replace('transferButtons.Children.Add(Theme.Text("Правила и значения можно передать другому пользователю."));','var transferHint = SAB.UI.PreviewDesign.Presentation.Information(Theme.Text("Правила и значения можно передать другому пользователю.")); transferHint.Padding = new Thickness(12,6,12,6); transferHint.Margin = new Thickness(0); transferHint.VerticalAlignment = VerticalAlignment.Center; transferButtons.Children.Add(transferHint);')
    settings=settings.replace('transfer.Children.Add(transferButtons);','transferButtons.Margin = new Thickness(0,0,0,12); transfer.Children.Add(transferButtons);')
    settings=settings.replace('"Экспорт конфигурации…"','"Экспорт конфигурации"').replace('"Импорт конфигурации…"','"Импорт конфигурации"')
    settings=settings.replace('new Thickness(2)); button.SetValue(Control.FontSizeProperty','new Thickness(4,0,4,0)); button.SetValue(Control.FontSizeProperty')
    settings=settings.replace('panel.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);','panel.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); panel.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center); panel.SetValue(FrameworkElement.MarginProperty, new Thickness(8,0,8,0));')
    settings=settings.replace('Header = "", Width = includePlus ? 68 : 34','Header = "Действия", Width = 88')
    # The category list has a bounded usable height; options remain reachable in the tab scroll.
    settings=settings.replace('var root = new DockPanel(); var options = new StackPanel();','var root = new StackPanel(); var options = new StackPanel();')
    settings=settings.replace('DockPanel.SetDock(options, Dock.Bottom); root.Children.Add(options);\n            root.Children.Add(new CategorySelectionControl(_categories)); Tab("Категории", Theme.Card(this, "Область заполнения", root));','root.Children.Add(Theme.Card(this,"Область заполнения",new CategorySelectionControl(_categories) { Height = 460 })); root.Children.Add(Theme.Card(this,"Обработка групп и привязки",options)); Tab("Категории",new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });')
    return small,settings

def refine_category_source(category):
    category=category.replace('private readonly TextBlock _count;', '''private readonly TextBlock _count;
        private bool _selectedOnly;
        private TextBlock _selectionStatus;
        internal sealed class SelectionSet { public string Format { get; set; } public int Version { get; set; } public List<int> CategoryIds { get; set; } }
''')
    category=category.replace('top.Children.Add(actions);','''top.Children.Add(actions);
            var selectionTools = new WrapPanel { Margin = new Thickness(0,8,0,8) };
            var modes = new Grid { Width = 220, Margin = new Thickness(0,0,8,0) };
            SAB.UI.PreviewDesign.Capsule.SetIsGroup(modes, true);
            modes.Children.Add(new Border { CornerRadius = new CornerRadius(20), Background = (System.Windows.Media.Brush)FindResource("SabBrush.TableHeaderBackground"), BorderBrush = (System.Windows.Media.Brush)FindResource("SabBrush.Border"), BorderThickness = new Thickness(1) });
            modes.Children.Add(new Border { Tag = "CapsuleIndicator", CornerRadius = new CornerRadius(18), Background = (System.Windows.Media.Brush)FindResource("SabBrush.Accent"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center });
            var choices = new UniformGrid { Columns = 2, Rows = 1, Margin = new Thickness(3) };
            var groupName = "CategoryFilter" + Guid.NewGuid().ToString("N");
            var all = new RadioButton { Content = "Все", GroupName = groupName, IsChecked = true };
            var selected = new RadioButton { Content = "Выбранные", GroupName = groupName };
            all.Checked += (s,e) => { _selectedOnly = false; RefreshSelectionView(); };
            selected.Checked += (s,e) => { _selectedOnly = true; RefreshSelectionView(); };
            choices.Children.Add(all); choices.Children.Add(selected); modes.Children.Add(choices); selectionTools.Children.Add(modes);
            selectionTools.Children.Add(Theme.Button("Сохранить выборку", (s,e) => SelectionFile(false)));
            selectionTools.Children.Add(Theme.Button("Загрузить выборку", (s,e) => SelectionFile(true)));
            top.Children.Insert(2,selectionTools);
            _selectionStatus = Theme.Text(""); _selectionStatus.Visibility = Visibility.Collapsed;
            top.Children.Add(SAB.UI.PreviewDesign.Presentation.Information(_selectionStatus));''')
    category=category.replace('_view.Filter = item => string.IsNullOrWhiteSpace(_search.Text) ||','_view.Filter = item => (!_selectedOnly || ((CategoryChoice)item).Selected) && (string.IsNullOrWhiteSpace(_search.Text) ||')
    category=category.replace('StringComparison.CurrentCultureIgnoreCase) >= 0;','StringComparison.CurrentCultureIgnoreCase) >= 0);')
    category=category.replace('category.PropertyChanged += (s, e) => UpdateCount();','category.PropertyChanged += (s, e) => RefreshSelectionView();')
    category=category.replace('private void Toggled(', '''private void RefreshSelectionView()
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
        private void Toggled(''')
    category=category.replace('Theme.Text("Shift — диапазон, Ctrl — отдельные строки. Тумблер и пробел переключают выделенные категории.")','Theme.Text("Shift — диапазон, Ctrl — отдельные строки. Тумблер и пробел переключают выделенные категории.")')
    category=category.replace('top.Children.Add(hint);','top.Children.Add(SAB.UI.PreviewDesign.Presentation.Information(hint));')
    return category
