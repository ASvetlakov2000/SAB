"""One-time layout derivation; the resulting preview XAML is reviewable source."""
from Prepare import *
import copy
P='{clr-namespace:SAB.TemplateEditorPreview;assembly=SAB.TemplateEditor.Preview}'
E.register_namespace('preview',P[1:-1])

def el(tag, **attrs): return E.Element(W+tag, attrs)
def set_style(root, key, prop, value):
    style = next(x for x in root.iter(W+'Style') if x.get(X+'Key') == key)
    setter = next((s for s in style.findall(W+'Setter') if s.get('Property') == prop), None)
    if setter is None: setter = E.SubElement(style, W+'Setter', {'Property':prop})
    setter.set('Value', value)

def footer(root, main=False):
    grid = list(root)[-1]
    border = grid.findall(W+'Border')[-1]
    border.set('Padding','24,12')
    old = list(border)[0]
    buttons = [e for e in old.iter(W+'Button') if e.get(X+'Name')]
    status = next((e for e in old.iter(W+'TextBlock') if e.get('Text') == '{Binding StatusText}'), None)
    border.clear(); border.attrib.update({'Grid.Row':'2','Background':'White','BorderBrush':'{DynamicResource SabBrush.BorderWeak}','BorderThickness':'0,1,0,0','Padding':'24,12'})
    new = el('Grid'); border.append(new)
    rows = el('Grid.RowDefinitions'); new.append(rows)
    for height in ['Auto','Auto']: rows.append(el('RowDefinition',Height=height))
    if main:
        state = el('Border',Style='{StaticResource SabValidationPanelStyle}',Padding='10,8',Margin='0,0,0,12')
        state.append(status); new.append(state)
    actions = el('Grid', **{'Grid.Row':'1'}); new.append(actions)
    help_button = el('Button',Content='Инструкция',HorizontalAlignment='Left',MinHeight='34')
    help_button.set(X+'Name','InstructionButton'); actions.append(help_button)
    right = el('StackPanel',Orientation='Horizontal',HorizontalAlignment='Right'); actions.append(right)
    for b in buttons:
        for k in ['Grid.Column','Grid.Row','Height']: b.attrib.pop(k,None)
        b.set('Margin','8,0,0,0'); b.set('MinHeight','34')
        if b.get(X+'Name') == 'CancelButton': b.set('IsCancel','True')
        if b.get(X+'Name') in ['ApplyButton','AddButton']: b.set('IsDefault','True')
        right.append(b)

def create():
    (HERE/'Views').mkdir(exist_ok=True)
    for name in ['ViewTemplateGraphicsWindow','GraphicOverrideEditorWindow','AddFiltersWindow']:
        root = E.parse(MODULE/'Views'/f'{name}.xaml').getroot()
        list(root)[-1].set('Background','{DynamicResource SabBrush.WindowBackground}')
        root.set('Title',root.get('Title')+' · макет, тестовые данные')
        for d in root.iter(W+'ResourceDictionary'):
            if 'Source' in d.attrib: d.set('Source','CANONICAL_FLUENT_DICTIONARY')
        for node in root.iter():
            if node.get('Text') in ['Свойства вида, графика и видимость для выбранных шаблонов', 'Пустое значение означает использование настроек стилей объектов.', 'Отметьте галочками один или несколько фильтров.']:
                node.set('Text','Макет · тестовые данные')
            if node.get('Height') in ['24','26','28','30','32']: node.attrib.pop('Height'); node.set('MinHeight','34')
            if node.tag == W+'Setter' and node.get('Property') == 'Height' and node.get('Value') in ['24','26','28','30','32']:
                node.set('Property','MinHeight'); node.set('Value','34')
            if node.tag == W+'DataGrid': node.set('ColumnHeaderHeight','38'); node.set('RowHeight','44')
        footer(root, name == 'ViewTemplateGraphicsWindow')
        for search_grid in list(root.iter(W+'Grid')):
            search_boxes=[c for c in search_grid if c.tag==W+'TextBox' and ('Search' in c.get('Tag','') or 'Search' in c.get(X+'Name',''))]
            definitions=search_grid.find(W+'Grid.ColumnDefinitions')
            if search_boxes and definitions is not None:
                definitions.insert(0,el('ColumnDefinition',Width='58'))
                for child in search_grid:
                    if child.tag != W+'Grid.ColumnDefinitions': child.set('Grid.Column',str(int(child.get('Grid.Column','0'))+1))
                search_grid.append(el('TextBlock',Text='Поиск',VerticalAlignment='Center',Foreground='{DynamicResource SabBrush.TextSecondary}'))
        if name == 'ViewTemplateGraphicsWindow':
            root.set('Width','1780'); root.set('Height','940'); root.set('MinWidth','1240'); root.set('MinHeight','760')
            for key, target in [('GraphicsTabControlStyle','TabControl'),('GraphicsTabItemStyle','TabItem')]:
                node = next(e for e in root.iter(W+'Style') if e.get(X+'Key') == key)
                node.clear(); node.attrib.update({X+'Key':key,'TargetType':'{x:Type '+target+'}','BasedOn':'{StaticResource {x:Type '+target+'}}'})
            # Derive the current canonical template; change only wrapping/indicator behavior
            # in the isolated preview. No hand-copied palette or production resource mutation.
            canonical=E.parse(ROOT/'SAB/UI/Styles/SABFluentLight.xaml').getroot()
            template=copy.deepcopy(next(e for e in canonical.iter(W+'ControlTemplate') if e.get('TargetType')=='TabControl'))
            for item in template.iter():
                if item.tag==W+'TabPanel': item.tag=W+'WrapPanel'
                for key in list(item.attrib):
                    if key.endswith('}Capsule.IsGroup'):
                        del item.attrib[key];item.set(P+'ResponsiveTabs.Enabled','True')
                if item.get('Tag')=='CapsuleIndicator': item.set('VerticalAlignment','Top')
                if item.tag==W+'Border' and item.get('CornerRadius')=='17': item.set('CornerRadius','22')
            tabstyle=next(e for e in root.iter(W+'Style') if e.get(X+'Key')=='GraphicsTabControlStyle')
            setter=E.SubElement(tabstyle,W+'Setter',{'Property':'Template'})
            value=E.SubElement(setter,W+'Setter.Value');value.append(template)
            set_style(root,'TemplateListBoxStyle','BorderThickness','0')
            set_style(root,'TemplateListBoxStyle','BorderBrush','{DynamicResource SabBrush.Border}')
            itemstyle = next(e for e in root.iter(W+'Style') if e.get(X+'Key') == 'TemplateListBoxItemStyle')
            # Keep original selection/visibility triggers; simplify row contours.
            for b in itemstyle.iter(W+'Border'): b.set('BorderThickness','0'); b.set('Padding','10,6')
            grid = list(root)[-1]; header = grid.findall(W+'Border')[0]
            header.set('Padding','24,14')
            next(e for e in header.iter(W+'TextBlock') if e.get('Text') == 'Пакетное редактирование шаблонов видов').set('Text','Редактор шаблонов')
            middle = grid.find(W+'Grid'); middle.set('Margin','24,16,24,0')
            cols = middle.find(W+'Grid.ColumnDefinitions')
            for col,width in zip(cols,['345.6','20','*']):
                col.set('Width',width); col.attrib.pop('MinWidth',None)
            cards = middle.findall(W+'Border'); templates, workspace = cards
            templates.set('Padding','16')
            tgrid = list(templates)[0]
            template_list=tgrid.find(W+'ListBox'); tgrid.remove(template_list)
            template_list.attrib.pop('Grid.Row',None)
            frame=el('Border',Style='{StaticResource SabDataGridContainerBorderStyle}',**{'Grid.Row':'4'})
            frame.append(template_list); tgrid.append(frame)
            workspace.set('Padding','16')
            wg = list(workspace)[0]; heading = wg.find(W+'Grid'); wg.remove(heading)
            defs = wg.find(W+'Grid.RowDefinitions'); defs.remove(list(defs)[0])
            tabs = wg.find(W+'TabControl'); tabs.set('Grid.Row','0')
            for tab in list(tabs):
                if tab.get('Header')=='Связанные файлы': tabs.remove(tab)
            # The shared tab template already owns the 12 DIP content gap.
            for e in root.iter(W+'Grid'):
                if e.get('Margin') == '0,12,0,0': e.set('Margin','0')
            # Preserve source metadata beside the selection list heading.
            meta = el('TextBlock', Text='{Binding GraphicsData.SourceTemplateName}',TextTrimming='CharacterEllipsis',MaxWidth='600',HorizontalAlignment='Right',VerticalAlignment='Center',Foreground='{DynamicResource SabBrush.TextSecondary}')
            header_content=list(header)[0]; header.remove(header_content)
            hg=el('Grid'); hg.append(header_content); hg.append(meta); header.append(hg)
            # Source cell triggers remain; only their visual base moves to shared inline styles.
            for style in root.iter(W+'Style'):
                if style.get('TargetType') == '{x:Type TextBox}' and style.get('BasedOn') == '{StaticResource SabTextBoxStyle}':
                    style.set('BasedOn','{StaticResource SabDataGridInlineTextBoxStyle}')
                    for setter in list(style.findall(W+'Setter')):
                        if setter.get('Property')=='Padding': style.remove(setter)
                if style.get('TargetType') == '{x:Type ComboBox}' and style.get('BasedOn') == '{StaticResource SabComboBoxStyle}':
                    style.set('BasedOn','{StaticResource SabDataGridComboBoxStyle}')
            for column in root.iter(W+'DataGridTextColumn'):
                column.set('ElementStyle','{StaticResource SabDataGridTextBlockStyle}')
            for gridnode in root.iter(W+'Grid'):
                if gridnode.get('Margin') == '2,1': gridnode.set('Margin','0')
            set_style(root,'GraphicsCellButtonStyle','Height','Auto')
            set_style(root,'GraphicsCellButtonStyle','BorderBrush','Transparent')
            set_style(root,'GraphicsCellButtonStyle','Background','Transparent')
            set_style(root,'GraphicsCellButtonStyle','Margin','0')
            set_style(root,'GraphicsCellButtonStyle','Padding','10,0')
            set_style(root,'GraphicsCellButtonStyle','HorizontalContentAlignment','Left')
            set_style(root,'GraphicsCellButtonStyle','HorizontalAlignment','Left')
            set_style(root,'TreeToggleButtonStyle','Width','28')
            set_style(root,'TreeToggleButtonStyle','Height','28')
            for column in root.iter(W+'DataGridTemplateColumn'):
                if column.get('Header') in ['Видимость','Включён','Включить','Выбор','Полутона','Статус'] and not any(n.get('Tag')=='BatchWorksetVisibility' for n in column.iter()):
                    column.set('HeaderStyle','{StaticResource SabCenteredHeaderStyle}')
            # WPF can shrink pixel-width columns to satisfy star minima. Service columns
            # keep their widths; a narrow workspace scrolls horizontally instead.
            for column in root.iter():
                if column.tag in [W+'DataGridTemplateColumn',W+'DataGridTextColumn']:
                    width=column.get('Width','')
                    if width.isdigit(): column.set('MinWidth',width)
            for button in root.iter(W+'Button'):
                if (button.get('Tag','').startswith('EditCategory') or button.get('Tag','').startswith('EditFilter')):
                    button.attrib.pop('HorizontalAlignment',None)
                    button.attrib.pop('HorizontalContentAlignment',None)
            for button in root.iter(W+'Button'):
                if button.get('Tag')=='OpenViewPropertyEditor':
                    button.set('BorderBrush','Transparent'); button.set('Background','Transparent'); button.set('HorizontalContentAlignment','Left'); button.set('HorizontalAlignment','Left')
            for parent in root.iter():
                for child in list(parent):
                    if child.tag == W+'Grid' and any(c.tag == W+'TextBox' and 'Search' in c.get('Tag','') for c in child):
                        # A visible label in the search field without adding a search function.
                        box=next(c for c in child if c.tag==W+'TextBox')
                        box.set('ToolTip',box.get('ToolTip','Поиск')); box.set('MinHeight','34')
            # All graph columns have fixed service columns and flexible content. Normal fits; minimum scrolls.
            for e in root.iter():
                if e.tag == W+'DataGridTemplateColumn':
                    h=e.get('Header','')
                    if h=='Видимость':
                        if any(n.get('Tag')=='BatchWorksetVisibility' for n in e.iter()): e.set('Width','*'); e.set('MinWidth','240')
                        else: e.set('Width','100')
                    if h=='Включён': e.set('Width','90')
                    if h=='Полутона': e.set('Width','94')
                    if h=='Детализация': e.set('Width','130')
                    if h=='Статус': e.set('Width','96')
                    if h in ['Проекция\nЛинии','Сечение\nЛинии']: e.set('MinWidth','118')
                    if h in ['Проекция\nШтриховки','Сечение\nШтриховки']: e.set('MinWidth','128')
                    if h=='Прозрачность': e.set('MinWidth','116')
                if e.tag == W+'DataGridTextColumn' and e.get('Header') in ['Рабочий набор','Связанный файл']: e.set('Width','*'); e.set('MinWidth','240')
            for e in root.iter(W+'DataGridTemplateColumn'):
                if e.get('Header') == 'Категория': e.set('Width','*'); e.set('MinWidth','250')
            # Capability-aware category cells retain bindings/handlers, without disabled placeholders.
            dictionary=root.find(W+'Window.Resources').find(W+'ResourceDictionary')
            converter=el('BooleanToVisibilityConverter'); converter.set(X+'Key','CategoryCapabilityVisibility');dictionary.insert(1,converter)
            search_converter=E.Element(P+'SearchActiveConverter');search_converter.set(X+'Key','TreeSearchActive');dictionary.insert(1,search_converter)
            category=next(t for t in root.iter(W+'DataTemplate') if t.get(X+'Key')=='CategoryTabTemplate')
            table=next(category.iter(W+'DataGrid'));table.set(P+'CategoryPresentation.Enabled','True')
            cap_by_tag={'BatchCategoryVisibility':'AllowsVisibilityControl','EditCategoryProjectionLines':'SupportsProjectionLines','EditCategorySurfacePatterns':'SupportsSurfacePatterns','EditCategoryTransparency':'SupportsTransparency','EditCategoryCutLines':'SupportsCut','EditCategoryCutPatterns':'SupportsCut','BatchCategoryHalftone':'SupportsHalftone','BatchCategoryDetailLevel':'SupportsDetailLevel'}
            for node in category.iter():
                if node.get('Tag') in cap_by_tag:
                    node.set('Visibility','{Binding '+cap_by_tag[node.get('Tag')]+', Converter={StaticResource CategoryCapabilityVisibility}}')
            namecol=next(c for c in table.iter(W+'DataGridTemplateColumn') if c.get('Header')=='Категория')
            name_template=next(namecol.iter(W+'DataTemplate'))
            stack=list(name_template)[0]
            for child in list(stack):
                if child.tag==W+'TextBlock' and child.get('Text')=='{Binding HierarchyGuide}': stack.remove(child)
                if child.tag==W+'TextBlock' and child.get('Text')=='{Binding DisplayName}': child.set('ToolTip','{Binding Name}')
            stack.set('Margin','10,0,6,0')
            name_template.remove(stack)
            tree=el('Grid');name_template.append(tree)
            guides=E.Element(P+'TreeGuides',{'HorizontalAlignment':'Stretch','IsHitTestVisible':'False','Margin':'10,0,0,0'});tree.append(guides);tree.append(stack)
            toggle=next(b for b in stack.iter(W+'Button') if b.get('Tag')=='ToggleCategory')
            content=el('Button.ContentTemplate');toggle.append(content)
            glyph=el('DataTemplate');content.append(glyph)
            path=el('Path',Data='M 3,1 L 8,6 L 3,11',Stroke='{DynamicResource SabBrush.TextSecondary}',StrokeThickness='1.5',Width='12',Height='12',Stretch='None',RenderTransformOrigin='0.5,0.5');glyph.append(path)
            transform=el('Path.RenderTransform');transform.append(el('RotateTransform',Angle='0'));path.append(transform)
            style_node=el('Path.Style');path.append(style_node)
            style=el('Style',TargetType='{x:Type Path}');style_node.append(style)
            triggers=el('Style.Triggers');style.append(triggers)
            trigger=el('DataTrigger',Binding='{Binding DataContext.IsExpanded, RelativeSource={RelativeSource AncestorType={x:Type Button}}}',Value='True');triggers.append(trigger)
            setter=el('Setter',Property='RenderTransform');trigger.append(setter);value=el('Setter.Value');setter.append(value);value.append(el('RotateTransform',Angle='90'))
            # Transform must come from the style for its expanded trigger to take precedence.
            path.remove(transform)
            default=el('Setter',Property='RenderTransform');style.insert(0,default);dv=el('Setter.Value');default.append(dv);dv.append(el('RotateTransform',Angle='0'))
            search_trigger=el('DataTrigger',Binding='{Binding DataContext.SearchText, RelativeSource={RelativeSource AncestorType={x:Type DataGrid}}, Converter={StaticResource TreeSearchActive}}',Value='True');triggers.append(search_trigger)
            search_set=el('Setter',Property='RenderTransform');search_trigger.append(search_set);search_value=el('Setter.Value');search_set.append(search_value);search_value.append(el('RotateTransform',Angle='90'))
            # Remove instructional comparison legend; colors/data/status remain in source rows.
            for parent in root.iter():
                for child in list(parent):
                    if child.tag == W+'TextBlock' and child.get('Text','').startswith(('Голубая строка','Порядок применяется')): parent.remove(child)
        else:
            for parent in root.iter():
                for child in list(parent):
                    if child.tag == W+'TextBlock' and child.get('Text','').startswith('В выделенных строках есть разные значения.'):
                        child.set('Text','Разные значения в выделенных строках')
            if name=='GraphicOverrideEditorWindow':
                root.set('Width','760'); root.set('MinWidth','680')
                for column in root.iter(W+'ColumnDefinition'):
                    if column.get('Width')=='190': column.set('Width','199')
        # Keep working API availability data; remove prose that explains implementation/navigation.
        for text in root.iter(W+'TextBlock'):
            value=text.get('Text','')
            if value.startswith('В Revit 2023 настройки отдельных связей'): text.set('Text','Редактирование отдельных связей недоступно в этой версии Revit.')
            if value.startswith('Revit 2024: API позволяет'): text.set('Text','Режим отображения и связанный вид доступны для редактирования.')
        for column in root.iter():
            if column.tag in [W+'DataGridTemplateColumn',W+'DataGridTextColumn']:
                width=column.get('Width','')
                if width.isdigit(): column.set('MinWidth',width)
                if column.tag==W+'DataGridTextColumn': column.set('ElementStyle','{StaticResource SabDataGridTextBlockStyle}')
                if column.get('Header')=='Выбор': column.set('HeaderStyle','{StaticResource SabCenteredHeaderStyle}')
        E.indent(root, space='    ')
        (HERE/'Views'/f'{name}.xaml').write_text(E.tostring(root,encoding='unicode'),encoding='utf-8-sig')

if __name__=='__main__': create()
