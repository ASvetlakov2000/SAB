"""User feedback 0.4: shared geometry, native capsule tabs, bounded scroll surfaces."""
import xml.etree.ElementTree as E
import copy
from RefineSourceViews import t,key,D,set_value

def styles(root):
    # TextBox's text view already applies Padding: do not apply it again on its host.
    for template in root.iter(t('ControlTemplate')):
        for host in template.iter(t('ScrollViewer')):
            if host.get(key('Name'))=='PART_ContentHost':host.set('Margin','0')
        for editor in template.iter(t('TextBox')):
            if editor.get(key('Name'))=='PART_EditableTextBox':editor.set('Padding','0');editor.set('BorderThickness','0')
    for style in list(root):
        if style.get('TargetType')=='RadioButton' and not style.get(key('Key')):
            set_value(style,'Height','30');set_value(style,'MinHeight','0');set_value(style,'Padding','8,0')
        if style.get(key('Key')) in ['SabDataGridInlineTextBoxStyle','SabDataGridTextBoxEditingStyle']:
            set_value(style,'Margin','0');set_value(style,'Padding','9,0')
        if style.get('TargetType') in ['TabItem','TabControl'] and not style.get(key('Key')):
            root.remove(style)
    root.extend(list(E.fromstring('''<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:design="clr-namespace:SAB.UI.PreviewDesign;assembly=SAB">
    <Style TargetType="TabItem"><Setter Property="Height" Value="30"/><Setter Property="Padding" Value="16,0"/><Setter Property="FocusVisualStyle" Value="{StaticResource Focus}"/>
      <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TabItem"><Border Background="Transparent" Padding="{TemplateBinding Padding}"><ContentPresenter x:Name="HeaderLabel" ContentSource="Header" VerticalAlignment="Center" TextElement.Foreground="{DynamicResource SabBrush.TextSecondary}"/></Border><ControlTemplate.Triggers><Trigger Property="IsSelected" Value="True"><Setter TargetName="HeaderLabel" Property="TextElement.Foreground" Value="White"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
    <Style TargetType="TabControl"><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TabControl"><Grid><Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
      <Grid HorizontalAlignment="Left" Height="34" design:Capsule.IsGroup="True"><Border CornerRadius="17" Background="{DynamicResource SabBrush.TableHeaderBackground}" BorderBrush="{DynamicResource SabBrush.Border}" BorderThickness="1"/><Border Tag="CapsuleIndicator" CornerRadius="15" Background="{DynamicResource SabBrush.Accent}" HorizontalAlignment="Left" VerticalAlignment="Center" IsHitTestVisible="False"/><TabPanel IsItemsHost="True" Margin="2"/></Grid>
      <ContentPresenter Grid.Row="1" ContentSource="SelectedContent"/></Grid></ControlTemplate></Setter.Value></Setter></Style>
    <Style x:Key="SabCenteredHeaderStyle" TargetType="DataGridColumnHeader" BasedOn="{StaticResource {x:Type DataGridColumnHeader}}"><Setter Property="HorizontalContentAlignment" Value="Center"/></Style>
    </ResourceDictionary>''')))
    for style in root:
        if style.get(key('Key'))=='SabDataGridContainerBorderStyle':
            E.SubElement(style,t('Setter'),Property='design:RoundedClip.Radius',Value='6')
    combo=next(s for s in root if s.get('TargetType')=='ComboBox' and not s.get(key('Key')))
    numeric=E.SubElement(root,t('Style'),{key('Key'):'SabNumericComboBoxStyle','TargetType':'ComboBox','BasedOn':'{StaticResource SabDataGridComboBoxStyle}'})
    template=copy.deepcopy(next(s for s in combo.findall(t('Setter')) if s.get('Property')=='Template'));numeric.append(template)
    for label in template.iter(t('ContentPresenter')):
        if label.get(key('Name'))=='Label':label.set('Margin','0');label.set('HorizontalAlignment','Center')
    for editor in template.iter(t('TextBox')):
        if editor.get(key('Name'))=='PART_EditableTextBox':editor.set('Margin','0');editor.set('TextAlignment','Center')

def markup(win,main=True):
    for grid in win.iter(t('Grid')):
        if grid.get('{'+D+'}Capsule.IsGroup')=='True':
            grid.set('Height','34')
            for u in grid.findall(t('UniformGrid')):u.set('Margin','2');u.set('Columns','0')
        # Place nested zone inputs and actions in one common control row, below labels.
        if not any(b.get('ToolTip')=='Добавить этаж ниже' for b in grid.findall(t('Button'))):continue
        defs=E.Element(t('Grid.RowDefinitions'));grid.insert(0,defs)
        E.SubElement(defs,t('RowDefinition'),Height='Auto');E.SubElement(defs,t('RowDefinition'),Height='34')
        for panel in list(grid.findall(t('StackPanel'))):
            col=panel.get('Grid.Column','0');grid.remove(panel)
            for child in list(panel):
                child.set('Grid.Column',col);child.set('Grid.Row','0' if child.tag==t('TextBlock') else '1');grid.append(child)
        for b in grid.findall(t('Button')):b.set('Grid.Row','1');b.set('VerticalAlignment','Center')
    for col in [n for n in win.iter() if n.tag in [t('DataGridTemplateColumn'),t('DataGridTextColumn')]]:
        if col.get('Header') in ['№','Масштаб','№ листа','Действия']:col.set('HeaderStyle','{StaticResource SabCenteredHeaderStyle}')
        if col.get('Header')=='Масштаб':
            for combo in col.iter(t('ComboBox')):combo.set('Style','{StaticResource SabNumericComboBoxStyle}')
        if col.get('Header')=='№ листа':
            for text in col.iter(t('TextBox')):text.set('TextAlignment','Center')
    # Explicit source inline margins were adding a second inset to shared styles.
    for box in win.iter(t('TextBox')):
        if box.get('Style')=='{StaticResource SabDataGridInlineTextBoxStyle}':box.set('Margin','0');box.set('Padding','9,0')
    # Auxiliary transfer actions live in the fixed footer, keeping original command bindings.
    if not main:return
    bottom=next(n for n in win.iter(t('Border')) if n.get('Grid.Row')=='3')
    fg=list(bottom)[0];status=next(n for n in list(fg) if n.tag==t('TextBlock'))
    fg.remove(status);status.set('Margin','0,0,0,8');status.set('Grid.Row','0');status.set('Grid.ColumnSpan','2')
    rows=E.Element(t('Grid.RowDefinitions'));fg.insert(0,rows);E.SubElement(rows,t('RowDefinition'),Height='Auto');E.SubElement(rows,t('RowDefinition'),Height='Auto');fg.append(status)
    for panel in fg.findall(t('StackPanel')):panel.set('Grid.Row','1')
    auxiliary=E.SubElement(fg,t('StackPanel'),{'Grid.Row':'1','Orientation':'Horizontal','VerticalAlignment':'Center'})
    E.SubElement(auxiliary,t('TextBlock'),Text='Настройки',VerticalAlignment='Center',Margin='0,0,12,0',Foreground='{DynamicResource SabBrush.TextSecondary}')
    for parent in list(win.iter()):
        for b in list(parent):
            if b.tag==t('Button') and b.get('Command') in ['{Binding ImportSettingsCommand}','{Binding ExportSettingsCommand}']:
                parent.remove(b);b.set('Margin','0,0,8,0');auxiliary.append(b)
    # Persistent outside border and scroll continuation fade, shared with secondary settings.
    for scroll in win.iter(t('ScrollViewer')):
        if scroll.get(key('Name'))=='SettingsDrawerPanel':scroll.set('{'+D+'}ScrollEdges.Enabled','True')

def sources(small,settings):
    # Recompose only constructor presentation. Business transfer methods remain byte-identical.
    settings=settings.replace('var body = new DockPanel(); var transfer = new StackPanel(); var transferButtons = new WrapPanel();','var body = new DockPanel(); var transfer = new StackPanel(); var transferButtons = new WrapPanel();')
    start=settings.index('            var transferHint =')
    end=settings.index('            _configurationStatus =',start)
    settings=settings[:start]+'''            var auxiliary = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            footer.Children.Remove(instructions); auxiliary.Children.Add(instructions);
            auxiliary.Children.Add(new TextBlock { Text = "Конфигурация", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12,0,12,0), Foreground = (System.Windows.Media.Brush)FindResource("SabBrush.TextSecondary") });
            foreach(var button in transferButtons.Children.OfType<Button>()) button.Content = ((string)button.Content).StartsWith("Импорт") ? "Импорт" : "Экспорт";
            auxiliary.Children.Add(transferButtons);
            footer.Children.Remove(_status); footer.Children.Remove(buttons);
            DockPanel.SetDock(auxiliary,Dock.Left); footer.Children.Add(auxiliary);
            DockPanel.SetDock(buttons,Dock.Right); footer.Children.Add(buttons);
            var footerRoot = new StackPanel();
            var statusFrame = SAB.UI.PreviewDesign.Presentation.Information(_status); statusFrame.Padding = new Thickness(12,0,12,0); statusFrame.MinHeight = 34; statusFrame.Margin = new Thickness(0,0,0,8); _status.Tag = null;
            footerRoot.Children.Add(statusFrame); footerRoot.Children.Add(footer);
'''+settings[end:]
    settings=settings.replace('body, footer);','body, footerRoot);')
    settings=settings.replace('root.Children.Add(Theme.Card(this,"Область заполнения",new CategorySelectionControl(_categories) { Height = 460 })); root.Children.Add(Theme.Card(this,"Обработка групп и привязки",options)); Tab("Категории",new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });', '''var layout = new Grid(); layout.ColumnDefinitions.Add(new ColumnDefinition()); layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            var categoryPicker = new CategorySelectionControl(_categories);
            options.Children.Add(categoryPicker.DetachKeyboardHint());
            groups.Content = new TextBlock { Text = (string)groups.Content, TextWrapping = TextWrapping.Wrap };
            extend.Content = new TextBlock { Text = (string)extend.Content, TextWrapping = TextWrapping.Wrap };
            var selectionCard = Theme.Card(this,"Область заполнения",categoryPicker); selectionCard.Margin = new Thickness(0,0,16,0); layout.Children.Add(selectionCard);
            var optionCard = Theme.Card(this,"Обработка групп и привязки",new ScrollViewer { Content = options, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); optionCard.Margin = new Thickness(0); Grid.SetColumn(optionCard,1); layout.Children.Add(optionCard);
            Tab("Категории",layout);''')
    # Every table uses shared cell geometry rather than two nested control margins.
    settings=settings.replace('new Thickness(4, 0, 4, 0)','new Thickness(0)')
    # Rules action toolbar above its search-free table, selected rule explanation stays below.
    settings=settings.replace('actions.Children.Add(configure); bottom.Children.Add(actions);','actions.Children.Add(configure); DockPanel.SetDock(actions,Dock.Top); root.Children.Insert(1,actions); actions.Margin = new Thickness(0,0,0,12);')
    settings=settings.replace('Tab("Правила", Theme.Card(this, "Правила заполнения", root))','Tab("Правила", SAB.UI.PreviewDesign.Presentation.TableCard(this, "Правила заполнения", root))')
    small=small.replace('new Thickness(24,14,24,14), Child = footer','new Thickness(24,12,24,12), Child = footer')
    return small,settings

def categories(category):
    category=category.replace('private bool _selectedOnly;','private bool _selectedOnly; private FrameworkElement _keyboardHint;')
    category=category.replace('top.Children.Add(SAB.UI.PreviewDesign.Presentation.Information(hint));','_keyboardHint = SAB.UI.PreviewDesign.Presentation.Information(hint); top.Children.Add(_keyboardHint);')
    category=category.replace('private void RefreshSelectionView()', 'internal FrameworkElement DetachKeyboardHint() { ((Panel)_keyboardHint.Parent).Children.Remove(_keyboardHint); return _keyboardHint; }\n        private void RefreshSelectionView()')
    category=category.replace('_count = Theme.Text("");','_count = Theme.Text(""); _count.Margin = new Thickness(0,6,0,0);')
    category=category.replace('top.Children.Add(SAB.UI.PreviewDesign.Presentation.Information(_selectionStatus));','var selectionFeedback = SAB.UI.PreviewDesign.Presentation.Information(_selectionStatus); selectionFeedback.Padding = new Thickness(12,6,12,6); selectionFeedback.Margin = new Thickness(0,8,0,8); top.Children.Add(selectionFeedback);')
    category=category.replace('var modes = new Grid { Width = 220, Margin = new Thickness(0,0,8,0) };','var modes = new Grid { Width = 190, Height = 34, Margin = new Thickness(0,0,16,0) };')
    category=category.replace('Columns = 2, Rows = 1, Margin = new Thickness(3)','Columns = 2, Rows = 1, Margin = new Thickness(2)')
    category=category.replace('Theme.Button("Сохранить выборку",','Theme.Button("Сохранить",').replace('Theme.Button("Загрузить выборку",','Theme.Button("Загрузить",')
    marker='            DockPanel.SetDock(top, Dock.Top);'
    category=category.replace(marker,'''            // Logical groups first; search is the last element directly above the list.
            var searchLabel = top.Children.OfType<TextBlock>().First(x => x.Text == "Поиск категории");
            top.Children.Remove(searchLabel); top.Children.Remove(_search); top.Children.Remove(actions); top.Children.Remove(selectionTools);
            selectionTools.Children.Remove(modes);
            var groups = new WrapPanel { Margin = new Thickness(0,0,0,4) };
            groups.Children.Add(SAB.UI.PreviewDesign.Presentation.ActionGroup("Показать",modes));
            groups.Children.Add(SAB.UI.PreviewDesign.Presentation.ActionGroup("Выборки",selectionTools));
            groups.Children.Add(SAB.UI.PreviewDesign.Presentation.ActionGroup("Выделение",actions));
            top.Children.Insert(0,groups);
            selectionTools.Margin = new Thickness(0); actions.Margin = new Thickness(0); _search.Margin = new Thickness(0,0,0,8);
            top.Children.Add(searchLabel); top.Children.Add(_search);
'''+marker)
    return category
