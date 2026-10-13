"""Generate isolated source-derived WPF views. Never writes SAB production sources.

All behavioral substitutions are explicit below: Document fixture loading, preference
isolation, Revit/ExternalEvent session adapter, and nonblocking shared notifications.
"""
from pathlib import Path
import copy, hashlib, json, re
from xml.etree import ElementTree as E

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'SAB/Cls_InteriorElevations'
OUT = ROOT / 'outputs/ui-production-preview/elevations-source'
VIEWS = ROOT / 'outputs/ui-production-preview/plugin/ElevationPreview/Views'
OUT.mkdir(parents=True, exist_ok=True)
VIEWS.mkdir(parents=True, exist_ok=True)
P = 'http://schemas.microsoft.com/winfx/2006/xaml/presentation'
X = 'http://schemas.microsoft.com/winfx/2006/xaml'
UI = 'clr-namespace:SAB.UI;assembly=SAB'
D = 'clr-namespace:SAB.UI.FluentLight;assembly=SAB'
for prefix, ns in [('', P), ('x', X), ('ui', UI), ('design', D)]: E.register_namespace(prefix, ns)
def q(t): return '{'+P+'}'+t
def kind(e): return e.tag.split('}')[-1]
def named(e): return e.get('{'+X+'}Name', '')
def el(t, **a): return E.Element(q(t), {k.replace('_', '.'): str(v) for k,v in a.items()})
def name(e,n): e.set('{'+X+'}Name',n);return e
def text(s,style=None):
    e=el('TextBlock',Text=s,TextWrapping='Wrap')
    if style:e.set('Style','{StaticResource '+style+'}')
    return e
def stack(*children):
    e=el('StackPanel');e.extend(children);return e
def resource(e,key):e.set('Style','{StaticResource '+key+'}');return e
def rows(g,*heights):
    e=el('Grid.RowDefinitions');e.extend(el('RowDefinition',Height=h) for h in heights);g.insert(0,e)
def columns(g,*widths):
    e=el('Grid.ColumnDefinitions');e.extend(el('ColumnDefinition',Width=w) for w in widths);g.insert(0,e)
def scroll(child):
    e=el('ScrollViewer',VerticalScrollBarVisibility='Auto',HorizontalScrollBarVisibility='Disabled',Padding='0,0,12,0')
    e.set('{'+D+'}ScrollEdges.Enabled','True');e.append(child);return e
def collection(child):
    e=resource(el('Border',Tag='CollectionFrame'),'SabCollectionBorderStyle');e.append(child);return e
def button(n,label,primary=False):
    e=name(el('Button',Content=label,Margin='8,0,0,0'),n)
    if primary:resource(e,'SabPrimaryButtonStyle')
    return e
def footer(buttons,status=None):
    g=el('Grid');rows(g,'Auto','Auto')
    if status is not None:
        b=status if kind(status)=='Border' else resource(el('Border'),'SabInformationBorderStyle')
        if b is not status:b.append(status)
        b.set('Margin','0,0,0,8');g.append(b)
    line=el('Grid',Grid_Row='1');columns(line,'Auto','*','Auto')
    help=el('Button',Content='Инструкция');help.set('{'+D+'}Instructions.File','SAB_HTML_Sozdanie_i_korrektirovka_razvertok.html');line.append(help)
    actions=el('WrapPanel',Grid_Column='2',HorizontalAlignment='Right');actions.extend(buttons);line.append(actions);g.append(line)
    b=el('Border',Background='White',BorderBrush='{DynamicResource SabBrush.Border}',BorderThickness='0,1,0,0',Padding='24,12');b.append(g);return b
def shell(title,w,h,mw,mh):
    e=el('Window',Title='SAB · '+title,Width=w,Height=h,MinWidth=mw,MinHeight=mh,ResizeMode='CanResize',WindowStartupLocation='CenterScreen',FontFamily='Segoe UI',FontSize='13',Background='{DynamicResource SabBrush.WindowBackground}',UseLayoutRounding='True')
    res=el('Window.Resources');rd=el('ResourceDictionary');md=el('ResourceDictionary.MergedDictionaries');md.append(el('ResourceDictionary',Source='../../UI/Styles/SABFluentLight.xaml'));rd.append(md)
    # Only compatibility aliases for source metadata/formula components, all based on shared owners.
    for key,typ,base in [('SabAccentInfoPanelStyle','Border','SabInformationBorderStyle'),('SabSelectionPendingPanelStyle','Border','SabInformationBorderStyle'),('SabAccentInfoTextStyle','TextBlock','SabFieldHintTextStyle'),('SabSelectionPendingTextStyle','TextBlock','SabFieldHintTextStyle'),('SabFormulaEditorBorderStyle','Border','SabInformationBorderStyle')]:
        st=el('Style',TargetType=typ,BasedOn='{StaticResource '+base+'}');st.set('{'+X+'}Key',key)
        if 'Formula' in key:
            for prop,val in [('Padding','1'),('Background','White'),('CornerRadius','4'),('design:RoundedClip.Radius','4')]:st.append(el('Setter',Property=prop,Value=val))
        rd.append(st)
    st=el('Style',TargetType='RichTextBox');st.set('{'+X+'}Key','SabFormulaEditorStyle')
    for prop,val in [('MinHeight','34'),('Padding','10,6'),('BorderThickness','0'),('VerticalScrollBarVisibility','Disabled'),('FontFamily','Segoe UI'),('FontSize','13')]:st.append(el('Setter',Property=prop,Value=val))
    rd.append(st)
    tabs=el('Style',TargetType='TabItem',BasedOn='{StaticResource {x:Type TabItem}}')
    for prop,val in [('Height','Auto'),('MinHeight','40'),('FontSize','14'),('Padding','20,8')]:tabs.append(el('Setter',Property=prop,Value=val))
    rd.append(tabs);res.append(rd);e.append(res)
    root=name(el('Grid',Background='{DynamicResource SabBrush.WindowBackground}'),'WindowRoot');rows(root,'Auto','Auto','*','Auto');e.append(root)
    bar=name(el('Border',Background='White',BorderBrush='{DynamicResource SabBrush.Border}',BorderThickness='0,0,0,1',Padding='24,12'),'TitleBar')
    bg=el('Grid');columns(bg,'Auto','*','Auto')
    bg.append(text('SAB','SabPanelHeaderTextStyle'));bg.append(el('TextBlock',Grid_Column='1',Text='Развёртки · Предпросмотр · модель Revit не подключена',Foreground='{DynamicResource SabBrush.TextSecondary}',VerticalAlignment='Center',Margin='20,0,0,0'))
    controls=el('StackPanel',Grid_Column='2',Orientation='Horizontal')
    # Retain names required by incumbent chrome handlers; native OS chrome owns visible buttons.
    controls.extend([name(el('Button',Visibility='Collapsed'),'MinimizeWindowButton'),name(el('Button',Visibility='Collapsed'),'CloseWindowButton')]);bg.append(controls);bar.append(bg);root.append(bar)
    root.append(el('TextBlock',Grid_Row='1',Text=title,Style='{StaticResource SabWindowTitleTextStyle}',Margin='24,20,24,16'))
    return e,root

PROSE = ['Введите обычный текст','Доступно:','Оси основной модели','Выберите способ','Список фильтруется','Если автоматическая','План-схема размещается','Привязка —','Выбор точки заполняет','Уровень, относительно','Ближняя линия:','Положение марки заранее','Оборудование, мебель','Оформление работает только','Выберите роль, затем','Добавляйте','Для каждого помещения','Команда работает','Проверьте параметры','Укажите, как','Выберите, продолжить']
ALIASES={'ElevationTabControlStyle':None,'ElevationTabItemStyle':None,'DecorationTabControlStyle':None,'DecorationTabItemStyle':None,'ElevationWrappingRadioButtonStyle':'SabRadioButtonStyle','DecorationWrappingCheckBoxStyle':'SabCheckBoxStyle','ElevationWrappingCheckBoxStyle':'SabCheckBoxStyle'}
def transform(e,inside_table=False):
    e=copy.deepcopy(e);k=kind(e);inside_table=inside_table or k in ['DataGrid','ListView']
    for a in ['Width','Height','MinWidth','MinHeight']:
        if k in ['Button','TextBox','ComboBox','RadioButton','CheckBox','RichTextBox','StackPanel','WrapPanel','Grid','AdaptiveSettingsGrid']:e.attrib.pop(a,None)
    s=e.get('Style','')
    for key,val in ALIASES.items():
        if key in s:
            if val:e.set('Style','{StaticResource '+val+'}')
            else:e.attrib.pop('Style',None)
    if k=='TextBlock':
        value=e.get('Text','')+''.join(c.get('Text','') for c in e)
        if (not named(e) and (any(p in value for p in PROSE) or value.strip() in ['☷','✓','▣','▤','☰','□','-'])) or 'RoomPlanModeHint' in value:return None
        e.set('TextWrapping','Wrap')
        e.attrib.pop('TextTrimming',None)
    if k in ['Button','TextBox','ComboBox'] and not inside_table:
        e.set('VerticalAlignment','Center')
        if k in ['TextBox','ComboBox']:e.set('Margin','0,4,0,12')
    if k=='GroupBox':
        e.set('Margin','0,0,0,16');e.attrib.pop('Style',None)
    if 'DecorationComposition' in s:e.attrib.pop('Style',None)
    if k=='TabControl':e.attrib.pop('ItemContainerStyle',None);e.set('Margin','0')
    if k=='TabItem':e.attrib.pop('Style',None)
    if k in ['DataGrid','ListView']:
        e.attrib.pop('Height',None);e.attrib.pop('MinHeight',None);e.set('BorderThickness','0')
    if k=='DataGrid':
        e.set('RowHeight','44');e.set('ColumnHeaderHeight','38');e.set('CanUserAddRows','False')
        e.set('ScrollViewer.HorizontalScrollBarVisibility','Auto');e.set('ScrollViewer.VerticalScrollBarVisibility','Auto')
    old=list(e);e[:]=[]
    for c in old:
        t=transform(c,inside_table)
        if t is not None:e.append(t)
    # Put contiguous source radio choices in the shared capsule's child-panel contract.
    if k in ['StackPanel','WrapPanel'] and any(kind(c)=='RadioButton' for c in e):
        if all(kind(c)=='RadioButton' for c in e):
            group=capsule(list(e))
            for a,v in e.attrib.items():
                if a.startswith('Grid.') or a in ['IsEnabled','Visibility']:group.set(a,v)
            return group
        children=list(e);e[:]=[];i=0
        while i<len(children):
            if kind(children[i])!='RadioButton':e.append(children[i]);i+=1;continue
            radios=[]
            while i<len(children) and kind(children[i])=='RadioButton':radios.append(children[i]);i+=1
            e.append(capsule(radios))
    return e

def capsule(radios):
        g=el('Grid',MinHeight='34',Margin='0,4,0,12');g.set('{'+D+'}Capsule.IsGroup','True')
        g.append(el('Border',CornerRadius='17',Background='{DynamicResource SabBrush.TableHeaderBackground}',BorderBrush='{DynamicResource SabBrush.Border}',BorderThickness='1'))
        g.append(el('Border',Tag='CapsuleIndicator',CornerRadius='15',Background='{DynamicResource SabBrush.Accent}',HorizontalAlignment='Left',VerticalAlignment='Center',IsHitTestVisible='False'))
        choices=el('Grid',Margin='2');columns(choices,*(['*']*len(radios)))
        for i,c in enumerate(radios):c.set('Grid.Column',str(i));c.set('Margin','0');choices.append(c)
        g.append(choices)
        return g
def find(root,n):return next(e for e in root.iter() if named(e)==n)
def topgroups(root):
    out=[]
    def walk(e):
        if kind(e)=='GroupBox':out.append(e);return
        for c in e:walk(c)
    walk(root);return out
def withbody(root,child):child.set('Grid.Row','2');child.set('Margin','24,0,24,16');root.append(child)
def withfooter(root,child):child.set('Grid.Row','3');root.append(child)
def main(original):
    e,r=shell('Настройки развёрток',1280,880,1040,650)
    work=el('Grid');rows(work,'Auto','*')
    source=topgroups(original)[0];choices=list(source.iter(q('RadioButton')))
    bar=el('Grid');columns(bar,'*','20','*','20','Auto')
    for i,label in enumerate(['Помещения за один запуск','Источник помещения']):
        field=stack(text(label,'SabFieldLabelTextStyle'),capsule([transform(c) for c in choices[i*2:i*2+2]]));field.set('Grid.Column',str(i*2));bar.append(field)
    selection=stack();selection.set('Grid.Column','4');selection.set('MinWidth','220')
    b=transform(find(original,'PickLinesButton'));b.set('Content','Выбрать линии');b.set('Margin','0,0,0,8');b.attrib.pop('Grid.Column',None);selection.append(b)
    status=transform(find(original,'SelectionStatusBorder'));status.attrib.pop('Grid.Column',None);selection.append(status);bar.append(selection)
    card=resource(el('Border',Margin='0,0,0,16'),'SabPanelBorderStyle');card.append(bar);work.append(card)
    tabs=transform(find(original,'SettingsTabControl'));tabs.set('Grid.Row','1')
    for tab in tabs:
        cards=[transform(c) for c in topgroups(tab)]
        tab[:]=[scroll(stack(*cards))]
    work.append(tabs);withbody(r,work)
    warning=transform(find(original,'WarningInfoBorder'));warning.set('Style','{StaticResource SabInformationBorderStyle}');warning.set('Margin','0,0,0,8')
    # Empty guidance is suppressed by the preview adapter; real validation remains.
    withfooter(r,footer([button('CancelButton','Отмена'),button('OkButton','Создать развёртки',True)],warning))
    return e
def decoration(original):
    e,r=shell('Оформление развёрток',1100,880,860,620)
    tabs=find(original,'SettingsTabControl');cards=topgroups(tabs[0]);body=stack()
    composition=el('Grid',Margin='0,0,0,16');columns(composition,'*','16','*');rows(composition,'Auto','16','Auto')
    spotcard=name(el('GroupBox',Margin='0'),'SpotSelectionCard')
    spotheader=el('GroupBox.Header');spotheader.append(transform(find(original,'PlaceSpotElevationsCheckBox')));spotcard.append(spotheader)
    spots=el('WrapPanel',IsEnabled='{Binding IsChecked, ElementName=PlaceSpotElevationsCheckBox}')
    for n,label in [('PlaceFloorSpotsCheckBox','Полы'),('PlaceCeilingSpotsCheckBox','Потолки'),('PlaceSlabSpotsCheckBox','Перекрытия')]:spots.append(name(el('CheckBox',Content=label,IsChecked='True',Margin='0,0,16,0'),n))
    spotcard.append(spots);composition.append(spotcard)
    dimcard=name(el('GroupBox',Grid_Row='2',Margin='0'),'DimensionSelectionCard')
    dimheader=el('GroupBox.Header');d=transform(find(original,'PlaceDimensionsCheckBox'));d.set('Content','Размеры');dimheader.append(d);dimcard.append(dimheader)
    dims=el('WrapPanel',IsEnabled='{Binding IsChecked, ElementName=PlaceDimensionsCheckBox}')
    for n in ['PlaceVerticalDimensionsCheckBox','PlaceHorizontalDimensionsCheckBox']:
        c=transform(find(original,n));c.attrib.pop('Grid.Column',None);c.set('Margin','0,0,16,0');dims.append(c)
    dimcard.append(dims);composition.append(dimcard)
    tagselect=name(el('GroupBox',Grid_Column='2',Grid_RowSpan='3',Margin='0'),'TagSelectionCard')
    tagheader=el('GroupBox.Header');t=transform(find(original,'PlaceTagsCheckBox'));t.set('Content','Марки');tagheader.append(t);tagselect.append(tagheader)
    tagchoices=el('Grid',IsEnabled='{Binding IsChecked, ElementName=PlaceTagsCheckBox}');columns(tagchoices,'*','*','*');rows(tagchoices,'Auto','Auto')
    for i,category in enumerate(['Wall','Floor','Ceiling','Plinth','Door','Window']):
        c=transform(find(original,'Place'+category+'TagsCheckBox'));c.set('Grid.Column',str(i%3));c.set('Grid.Row',str(i//3));c.set('Margin','0,0,8,8');tagchoices.append(c)
    tagselect.append(tagchoices);composition.append(tagselect);body.append(composition)
    for card in cards[1:3]:body.append(transform(card))
    tagcard=el('GroupBox',Header='Марки',IsEnabled='{Binding IsChecked, ElementName=PlaceTagsCheckBox}',Margin='0,0,0,16')
    tg=el('Grid');columns(tg,'*','20','*','20','*');rows(tg,'Auto','16','Auto')
    for i,(category,label) in enumerate([('Wall','Стены'),('Floor','Полы'),('Ceiling','Потолки'),('Plinth','Плинтусы'),('Door','Двери'),('Window','Окна')]):
        field=stack(text(label,'SabFieldLabelTextStyle'));field.set('Grid.Column',str(i%3*2));field.set('Grid.Row',str(i//3*2))
        line=el('Grid');columns(line,'*','12','Auto')
        combo=transform(find(original,category+'TagTypeComboBox'));combo.attrib.pop('Grid.Row',None);combo.attrib.pop('Grid.Column',None);combo.set('Margin','0');line.append(combo)
        leader=transform(find(original,category+'LeaderCheckBox'));leader.attrib.pop('Grid.Row',None);leader.set('Grid.Column','2');leader.set('Margin','0');leader.set('VerticalAlignment','Center');line.append(leader)
        field.append(line);tg.append(field)
    offset=stack(text('Видимая длина прямой выноски, мм на листе','SabFieldLabelTextStyle'),transform(find(original,'TagOffsetTextBox')))
    offset[-1].set('HorizontalAlignment','Stretch');offset[-1].set('Width','220');offset[-1].set('HorizontalAlignment','Left')
    tagcard.append(stack(offset,tg));body.append(tagcard)
    body.append(transform(topgroups(tabs[2])[0]));withbody(r,scroll(body))
    title=next(c for c in r if kind(c)=='TextBlock' and c.get('Grid.Row')=='1');r.remove(title)
    heading=el('Grid',Grid_Row='1',Margin='24,20,24,16');columns(heading,'*','Auto');title.attrib.pop('Grid.Row',None);title.set('Margin','0');heading.append(title)
    cb=transform(find(original,'ConfigureCatalogButton'));cb.set('Grid.Column','1');cb.set('Content','Каталог');cb.set('Margin','16,0,0,0');heading.append(cb);r.append(heading)
    status=transform(find(original,'ValidationStatusTextBlock'));status.set('Text','Настройки готовы.')
    withfooter(r,footer([button('CancelButton','Отмена'),button('OkButton','Оформить',True)],status));return e
def catalog(original):
    e,r=shell('Каталог оформления',1020,720,820,520)
    body=el('Grid');rows(body,'Auto','*')
    toolbar=stack(text('Роль элемента','SabFieldLabelTextStyle'),transform(find(original,'RoleComboBox')))
    commands=el('WrapPanel',Margin='0,0,0,12')
    for n in ['AddHostButton','AddLinkedButton','RemoveSelectedButton','ClearRoleButton']:
        b=transform(find(original,n));b.set('Margin','0,0,8,8');commands.append(b)
    toolbar.append(commands);body.append(toolbar)
    lv=name(el('DataGrid',AutoGenerateColumns='False',IsReadOnly='True',CanUserAddRows='False',RowHeight='44',ColumnHeaderHeight='38',BorderThickness='0'),'EntriesListView')
    cols=el('DataGrid.Columns')
    for header,binding,width in [('Типоразмер','TypeDisplayName','3*'),('Источник','SourceDocumentTitle','2*')]:
        cols.append(el('DataGridTextColumn',Header=header,Binding='{Binding '+binding+'}',Width=width,MinWidth='180',ElementStyle='{StaticResource SabDataGridTextBlockStyle}'))
    lv.append(cols)
    c=collection(lv);c.set('Grid.Row','1');body.append(c);withbody(r,body)
    withfooter(r,footer([button('DoneButton','Готово',True)]));return e
def rooms(original):
    e,r=shell('Список помещений',1280,740,960,520)
    grid=transform(find(original,'RoomsDataGrid'))
    for c in grid.iter():
        if kind(c)=='DataGridTextColumn':
            c.set('ElementStyle','{StaticResource '+('SabDataGridCenteredTextBlockStyle' if c.get('Header') in ['№','Групп линий','Выбрано линий','Статус'] else 'SabDataGridTextBlockStyle')+'}')
            if c.get('Header') in ['№','Групп линий','Выбрано линий']:c.set('HeaderStyle','{StaticResource SabCenteredHeaderStyle}')
        if kind(c)=='Button':
            c.set('Margin','0' if named(c)=='DeleteRoomButton' else '0,0,8,0')
            if named(c) in ['AddRoomButton','DeleteRoomButton']:
                resource(c,'SabRowAddButtonStyle' if named(c)=='AddRoomButton' else 'SabRowDeleteButtonStyle')
                c.attrib.pop('Content',None);c.set('Width','28');c.set('Height','28')
                c.append(el('Path',Data='M1 6 H11 M6 1 V11' if named(c)=='AddRoomButton' else 'M2 3 H10 M4 3 V1 H8 V3 M3 4 V11 H9 V4 M5 5 V9 M7 5 V9',Stroke='{DynamicResource SabBrush.TextSecondary}',StrokeThickness='1.4',Width='12',Height='12',Stretch='Uniform'))
                c.set('ToolTip','Добавить помещение' if named(c)=='AddRoomButton' else 'Удалить помещение')
                c.set('AutomationProperties.Name',c.get('ToolTip'))
        if kind(c)=='DataGridTemplateColumn' and c.get('Header')=='Действия':c.set('Width','200')
        if kind(c) in ['DataGridTemplateColumn','DataGridTextColumn']:
            if c.get('Header') in ['Номер помещения','Имя помещения']:
                c.set('Width','*' if c.get('Header')=='Номер помещения' else '2*');c.set('MinWidth','150' if c.get('Header')=='Номер помещения' else '170')
            if c.get('Header') in ['Статус','Действия']:c.set('HeaderStyle','{StaticResource SabCenteredHeaderStyle}')
        if kind(c)=='StackPanel' and any(named(b)=='AddRoomButton' for b in c):
            c.set('HorizontalAlignment','Center');list(c)[-1].set('Margin','0')
    withbody(r,collection(grid))
    status=stack(text('{Binding ValidationText}'),text('{Binding StatusText}'))
    withfooter(r,footer([button('CancelButton','Отмена'),button('TransferButton','Передать список помещений',True)],status));return e
def compact(which,original):
    specs={
      'LineGroupSelectionWindow':('Выбор групп линий',680,400,620,360,[('SingleGroupButton','По одной группе линий'),('MultipleGroupsButton','По нескольким группам линий')],[('CancelButton','Отмена')]),
      'LineGroupNextActionWindow':('Выбор следующей группы',760,400,660,360,[('NextGroupButton','Выбрать следующую группу линий'),('FinishButton','Завершить выбор групп')],[('CancelButton','Отмена')]),
      'CropByExampleActionWindow':('Обрезка по примеру',660,400,600,360,[('PickLineButton','Выбрать линию'),('CreateLineButton','Создать линию')],[('CloseButton','Закрыть')]),
      'CropByExampleLineCreationWindow':('Развёртка-пример',820,360,760,320,[],[('CancelButton','Отмена'),('SelectLineButton','Выбрать линию'),('CreateViewButton','Создать вид'),('AcceptCropButton','Принять границы')]),
      'ElevationCropAdjustmentWindow':('Смещение границ вида',600,460,520,420,[],[('CancelButton','Отмена'),('ApplyButton','Применить')])}
    title,w,h,mw,mh,choices,actions=specs[which];e,r=shell(title,w,h,mw,mh);body=stack()
    fixed=which in ['LineGroupSelectionWindow','LineGroupNextActionWindow','CropByExampleActionWindow']
    if fixed:
        for a in ['Width','Height','MinWidth','MinHeight']:e.attrib.pop(a,None)
        e.set('SizeToContent','WidthAndHeight');e.set('ResizeMode','NoResize')
        next(c for c in r if kind(c)=='Grid.RowDefinitions')[2].set('Height','Auto')
        r.remove(find(r,'TitleBar'));r.insert(1,name(el('Border',Visibility='Collapsed'),'TitleBar'))
        for n in ['MinimizeWindowButton','CloseWindowButton']:r.append(name(el('Button',Visibility='Collapsed'),n))
    if which=='LineGroupNextActionWindow':body.append(name(text('Группа №1 добавлена.','SabPanelHeaderTextStyle'),'HeaderTextBlock'))
    if which=='CropByExampleLineCreationWindow':
        body.extend([name(text('Линия ещё не выбрана.','SabPanelHeaderTextStyle'),'StatusHeaderTextBlock'),name(text('','SabFieldHintTextStyle'),'StatusDescriptionTextBlock')])
    if which=='ElevationCropAdjustmentWindow':
        for a in ['Width','Height','MinWidth','MinHeight']:e.attrib.pop(a,None)
        e.set('SizeToContent','WidthAndHeight');e.set('ResizeMode','NoResize');next(c for c in r if kind(c)=='Grid.RowDefinitions')[2].set('Height','Auto')
        g=el('Grid',HorizontalAlignment='Center');rows(g,'Auto','12','Auto','12','Auto');columns(g,'130','12','150','12','130')
        for n,l,row,col in [('TopTextBox','Сверху, мм',0,2),('BottomTextBox','Снизу, мм',4,2),('LeftTextBox','Слева, мм',2,0),('RightTextBox','Справа, мм',2,4)]:
            label=text(l,'SabFieldLabelTextStyle');label.set('TextAlignment','Center')
            field=stack(label,name(el('TextBox',Text='0',Margin='0,4,0,0',HorizontalContentAlignment='Center'),n));field.set('Grid.Column',str(col));field.set('Grid.Row',str(row));field.set('VerticalAlignment','Center');g.append(field)
        boundary=name(el('Grid',Grid_Row='2',Grid_Column='2',Height='68'),'ViewBoundaryDiagram')
        boundary.append(el('Rectangle',Stroke='{DynamicResource SabBrush.TextSecondary}',StrokeThickness='1',StrokeDashArray='4,3',Fill='Transparent',RadiusX='4',RadiusY='4'))
        boundary.append(el('TextBlock',Text='Граница вида',HorizontalAlignment='Center',VerticalAlignment='Center',Foreground='{DynamicResource SabBrush.TextSecondary}'));g.append(boundary)
        body.append(g)
    if choices:
        wp=el('WrapPanel',Margin='0,12,0,0',HorizontalAlignment='Center')
        for i,(n,l) in enumerate(choices):b=button(n,l,i==len(choices)-1);b.set('Margin','0,0,8,8' if i<len(choices)-1 else '0,0,0,8');wp.append(b)
        body.append(wp)
    card=resource(el('Border'),'SabPanelBorderStyle');card.append(body);withbody(r,card)
    f=footer([button(n,l,i==len(actions)-1 and n not in ['CancelButton','CloseButton']) for i,(n,l) in enumerate(actions)])
    if fixed:
        line=next(c for c in f.iter() if kind(c)=='Grid' and c.get('Grid.Row')=='1');line.set('HorizontalAlignment','Center')
    withfooter(r,f);return e

def replace_body(s,method,body):
    match=re.search(r'\b'+re.escape(method)+r'\s*\([^;{}]*\)\s*\{',s)
    if not match:raise ValueError('Missing method '+method)
    start=match.end();depth=1;i=start
    while depth:
        if s[i]=='{':depth+=1
        elif s[i]=='}':depth-=1
        i+=1
    return s[:start]+'\n'+body+'\n        '+s[i-1:]

def generate():
    from ElevationSourceContract import verify
    unchanged,delta=verify(ROOT)
    manifest={'productionUiMigration':True,'baselineFilesUnchanged':unchanged,'authorizedFunctionalDelta':delta,'baseline':'Tools/UiShowcase/elevations-baseline.json','views':{},'adapters':[]}
    active=['ElevationSettingsWindow','ElevationDecorationWindow','ElevationDecorationCatalogWindow','MultiRoomSelectionWindow','ElevationCropAdjustmentWindow','LineGroupSelectionWindow','LineGroupNextActionWindow','CropByExampleActionWindow','CropByExampleLineCreationWindow']
    for n in active:
        source=SOURCE/'Views'/(n+'.xaml');old=E.parse(source).getroot()
        # Production owns approved markup. Only session/Document fixture behavior is isolated.
        new=copy.deepcopy(old)
        if n in ['ElevationDecorationWindow','ElevationDecorationCatalogWindow']:
            for node in new.iter():
                if node.get('Content')=='Инструкция':node.set('{'+D+'}Instructions.File','SAB_HTML_Oformlenie_razvertok.html')
        path=VIEWS/(n+'.xaml');E.indent(new,space='  ');E.ElementTree(new).write(path,encoding='utf-8',xml_declaration=True)
        # Preserve source field bindings; only removed educational status/hints are allowlisted.
        b=lambda r:sorted((kind(e),'Binding' if k=='DisplayMemberBinding' else k,v) for e in r.iter() for k,v in e.attrib.items() if '{Binding ' in v)
        before=b(old);after=b(new)
        missing=[v for v in before if not any(a[1:]==v[1:] for a in after) and v[0] not in ['Condition','DataTrigger'] and not any(x in v[2] for x in ['RoomPlanModeHint','NamingPreviewStatusText'])]
        assert not missing,(n,missing)
        manifest['views'][n]={'source':str(source.relative_to(ROOT)),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'bindings':len(before),'bindingPreservation':True}
        cpp=SOURCE/'Views'/(n+'.xaml.cs')
        if not cpp.exists():cpp=SOURCE/'Views'/(n+'.cs')
        s=cpp.read_text(encoding='utf-8-sig').replace('namespace SAB.InteriorElevations.Views','namespace SAB.UiShowcase.Elevations.Views')
        s=s.replace('using SAB.InteriorElevations.ViewModels;','using SAB.UiShowcase.Elevations.ViewModels;') if n=='ElevationSettingsWindow' else s
        # Only main VM needs a collector adapter; MultiRoomSelection uses production VM directly.
        s=s.replace('"Cls_InteriorElevations"','"ElevationPreview"')
        s=s.replace('Height = loadedWindow.Height;', 'Height = loadedWindow.Height;\n                SizeToContent = loadedWindow.SizeToContent;')
        s=re.sub(r'WindowSizeSettingsService\.Apply\(.*?\);','// Preview: user size preferences remain untouched.',s,flags=re.S)
        s=s.replace('SabLayoutAssist.Apply(this);','{ };')
        s=s.replace('ToastNotifier.ShowWarning(', 'SAB.UiShowcase.Elevations.ElevationSession.Warning(')
        if n=='ElevationDecorationCatalogWindow':s=re.sub(r'\bListView\b','DataGrid',s)
        if n=='ElevationSettingsWindow':
            s=s.replace('Content = loadedWindow.Content;', 'Content = loadedWindow.Content;\n                string radioScope = Guid.NewGuid().ToString("N");\n                foreach (var radio in SAB.UiShowcase.Elevations.ElevationSession.Logical<RadioButton>(Content as DependencyObject))\n                    if (!string.IsNullOrEmpty(radio.GroupName)) radio.GroupName += radioScope;')
            s=s.replace('"Перед созданием нажмите Выбрать линии, затем укажите линии детализации и помещение в активном плане."','string.Empty')
            s=s.replace('_warningInfoTextBlock.Text = string.IsNullOrWhiteSpace(text)', 'if (_warningInfoBorder != null) _warningInfoBorder.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;\n            _warningInfoTextBlock.Text = string.IsNullOrWhiteSpace(text)')
        if n=='ElevationDecorationWindow':
            s=replace_body(s,'PopulateTypeLists','            SAB.UiShowcase.Elevations.ElevationSession.PopulateDecoration(this);\n            _catalog = SAB.UiShowcase.Elevations.ElevationSession.Catalog;\n            RefreshCatalogStatus();')
            s=replace_body(s,'ConfigureCatalogButton_Click','            SAB.UiShowcase.Elevations.ElevationSession.EditCatalog(this, _catalog);\n            RefreshCatalogStatus();')
            s=replace_body(s,'ShowValidationErrors','            string details = string.Join(Environment.NewLine, validationErrors);\n            _validationStatusText.Text = details;\n            _validationStatusText.Foreground = Brushes.Firebrick;')
        if n=='CropByExampleLineCreationWindow':
            s=replace_body(s,'RaiseExternalEvent','            SAB.UiShowcase.Elevations.ElevationSession.RequestCrop(this, operation);')
            s=s.replace('Activate();','if (IsVisible) Activate();')
            # Status descriptions in source are educational prose; state header remains live.
            s=s.replace('_statusDescriptionTextBlock.Text = string.IsNullOrWhiteSpace(description) ? string.Empty : description;','_statusDescriptionTextBlock.Text = string.Empty;')
        (OUT/(n+'.cs')).write_text(s,encoding='utf-8')
    vm=(SOURCE/'ViewModels/ElevationSettingsViewModel.cs').read_text(encoding='utf-8-sig').replace('namespace SAB.InteriorElevations.ViewModels','namespace SAB.UiShowcase.Elevations.ViewModels')
    first=vm.index('            if (document != null)');last=vm.index('            ExistingSheetsView =',first)
    original_loads=vm[first:last]
    vm=vm[:first]+'            if (document != null) {\n'+original_loads+'            } else { SAB.UiShowcase.Elevations.ElevationSession.PopulateSettings(this); LoadViewTitleAnchors(); }\n\n'+vm[last:]
    vm=vm.replace('"Пример для первого вида."','"Первый вид."').replace('"Выберите линии и помещение — здесь появятся итоговые имена и заголовки."','"Нет данных для наименований."')
    vm=vm.replace('        public void SetExistingRoomPlanViewSelection(ViewPlan planView)', '''        // Explicit fixture adapter: a Revit ViewPlan cannot exist without a Document.
        public void SetPreviewExistingRoomPlanSelection(ElementId id, string name)
        {
            _existingRoomPlanViewId = id;
            _existingRoomPlanViewName = name;
            OnPropertyChanged("ExistingRoomPlanViewId");
            OnPropertyChanged("ExistingRoomPlanSummary");
            OnPropertyChanged("HasExistingRoomPlanSelection");
        }
        public void SetExistingRoomPlanViewSelection(ViewPlan planView)''')
    (OUT/'ElevationSettingsViewModel.cs').write_text(vm,encoding='utf-8')
    # Shared progress keeps incumbent Report() and close guard behavior.
    progress=ROOT/'SAB/Cls_CreateViewsAndSheets/Views/CreateViewsAndSheetsProgressWindow.cs'
    ps=progress.read_text(encoding='utf-8-sig').replace('namespace SAB.CreateViewsAndSheets.Views','namespace SAB.UiShowcase.Elevations.Views').replace('"Cls_CreateViewsAndSheets"','"ElevationPreview"')
    (OUT/'CreateViewsAndSheetsProgressWindow.cs').write_text(ps,encoding='utf-8')
    # User explicitly retained the incumbent progress, including16DIP bar and
    # rotating blue message. No substitute shell/extra footer.
    pw=E.parse(progress.with_suffix('.xaml')).getroot()
    originalbody=next(c for c in pw if kind(c)=='Grid');pw.remove(originalbody)
    captureRoot=el('Border',Background=pw.get('Background'));captureRoot.append(originalbody);pw.append(captureRoot)
    E.ElementTree(pw).write(VIEWS/'CreateViewsAndSheetsProgressWindow.xaml',encoding='utf-8',xml_declaration=True)
    # Proposal only: derive the actual common notification implementation; do not
    # change the installed template until the user approves its new presentation.
    for n in ['NotificationMessage','NotificationCard','NotificationStack']:
        s=(ROOT/'SAB/UI/Notifications'/(n+'.cs')).read_text(encoding='utf-8-sig').replace('namespace SAB.UI.Notifications','namespace SAB.UiShowcase.NotificationProposal')
        if n=='NotificationStack':s=s.replace('520','416')
        if n=='NotificationCard':
            s=s.replace('Text = message.Outcome, FontSize = 12, FontWeight = FontWeights.SemiBold','Text = message.Outcome, FontSize = 16, FontWeight = FontWeights.Bold')
            s=s.replace('if (!string.IsNullOrWhiteSpace(message.Summary)) content.Children.Add(Selectable(message.Summary, false));','if (!string.IsNullOrWhiteSpace(message.Summary)) content.Children.Add(new Border { BorderBrush = Brush("SabBrush.Border"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(10), Background = Brushes.Transparent, Child = Selectable(message.Summary, false) });')
        (OUT/('Proposal'+n+'.cs')).write_text(s,encoding='utf-8')
    manifest['adapters']=['namespace/XAML path isolation','skip preference writes and legacy adaptive tree mutation','main VM null-Document collection fixture branch; original collectors retained','decoration fixture type lists/catalog; no settings storage','catalog ListView→shared DataGrid, same ItemsSource/SelectedItem/handlers and two column bindings','catalog edit in child dialog retains raw input (fixes documented baseline loss)','crop modeless external-event request→synthetic state adapter','instructional prose removed; real validation retained','validation messages→existing shared notification or persistent validation text']
    manifest['adapters'].extend(['main radio GroupName scoped per window before DataContext to avoid closed-instance interference','explicit synthetic existing-plan ID/name setter; original validation retained; cancelled placement leaves values intact','crop action cancellation returns same settings VM; accepted action opens separate modeless session'])
    (OUT/'source-contract.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print('PASS',unchanged,'production hashes unchanged;',len(delta['files']),'authorized functional files;',len(active),'views; bindings preserved; explicit adapters recorded')
if __name__=='__main__':generate()

