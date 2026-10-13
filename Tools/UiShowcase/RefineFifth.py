"""Shared feedback 0.5: content-sized capsules, tab gap, aligned forms, collection outlines."""
import xml.etree.ElementTree as E
from RefineSourceViews import t, key, D, set_value

def styles(root):
    for style in root:
        if style.get('TargetType') == 'DataGrid' and not style.get(key('Key')):
            set_value(style, 'design:TableScroll.Enabled', 'True')
        if style.get('TargetType') == 'RadioButton' and not style.get(key('Key')):
            set_value(style, 'Height', 'Auto')
            set_value(style, 'MinHeight', '30')
            set_value(style, 'Padding', '8,6')
        if style.get('TargetType') == 'TabControl' and not style.get(key('Key')):
            for grid in style.iter(t('Grid')):
                if grid.get('{'+D+'}Capsule.IsGroup') == 'True':
                    grid.set('Height','Auto'); grid.set('MinHeight','34')
            for presenter in style.iter(t('ContentPresenter')):
                if presenter.get('ContentSource') == 'SelectedContent':
                    presenter.set(key('Name'),'SelectedBody'); presenter.set('Margin','0,12,0,0')
    outline = E.SubElement(root,t('Style'),{key('Key'):'SabCollectionBorderStyle','TargetType':'Border','BasedOn':'{StaticResource SabDataGridContainerBorderStyle}'})
    set_value(outline,'Padding','0')
    items=E.SubElement(root,t('Style'),{key('Key'):'SabItemsCollectionBorderStyle','TargetType':'Border','BasedOn':'{StaticResource SabCollectionBorderStyle}'})
    set_value(items,'Padding','8')
    set_value(items,'design:CollectionChrome.Enabled','True')

def sources(small, settings):
    # Space belongs to the common TabControl template, never the conditional tab body.
    settings = settings.replace('Padding = new Thickness(0, 10, 0, 0), Child = content', 'Padding = new Thickness(0), Child = content')
    settings = settings.replace('var parameter = new ParameterPicker { Parameters = items, Margin = new Thickness(4) };','var parameter = new ParameterPicker { Parameters = items, Margin = new Thickness(0,4,0,4) };')
    return small, settings

def markup(window):
    for grid in window.iter(t('Grid')):
        if grid.get('{'+D+'}Capsule.IsGroup') == 'True':
            grid.set('Height','Auto'); grid.set('MinHeight','34')
    for parent in list(window.iter()):
        for grid in list(parent):
            if grid.tag != t('DataGrid') and not (grid.tag == t('ItemsControl') and grid.get('ItemsSource')): continue
            if parent.tag == t('Border') and parent.get('Style') in ['{StaticResource SabCollectionBorderStyle}','{StaticResource SabItemsCollectionBorderStyle}']: continue
            index = list(parent).index(grid); parent.remove(grid)
            frame = E.Element(t('Border'),Style='{StaticResource SabCollectionBorderStyle}' if grid.tag == t('DataGrid') else '{StaticResource SabItemsCollectionBorderStyle}',Tag='CollectionFrame',Margin='16,0,16,16' if grid.tag == t('DataGrid') else grid.get('Margin','0'))
            for attribute in ['Grid.Row','Grid.Column','Grid.RowSpan','Grid.ColumnSpan','DockPanel.Dock']:
                if attribute in grid.attrib: frame.set(attribute,grid.attrib.pop(attribute))
            grid.set('Margin','0'); frame.append(grid); parent.insert(index,frame)
    for column in window.iter():
        role=column.get('SortMemberPath')
        if role=='PlanKind': column.set('Width','150')
        if role=='ViewName': column.set('MinWidth','130')
        if role=='ViewScaleText': column.set('Width','84');column.set('MinWidth','84')
        if role=='SelectedViewTemplate.Name': column.set('MinWidth','140')
        if role=='SheetNumber': column.set('Width','90');column.set('MinWidth','90')
        if role=='SheetName': column.set('MinWidth','120')
        if column.get('Binding')=='{Binding RowNumber}':column.set('Width','44')
    for items in window.iter(t('ItemsControl')):
        if items.get('ItemsSource')!='{Binding Floors}':continue
        template=items.find(t('ItemsControl.ItemTemplate'))
        if template is None:continue
        for data in template:
            for row in list(data):
                if row.tag==t('Border'):
                    row.set('BorderThickness','0,0,0,1');row.set('CornerRadius','0')

def categories(source):
    source = source.replace('Width = 190, Height = 34,', 'Width = 190, MinHeight = 34,')
    return source.replace('Background = System.Windows.Media.Brushes.White, Margin = new Thickness(4)', 'Background = System.Windows.Media.Brushes.White, Margin = new Thickness(0)')
