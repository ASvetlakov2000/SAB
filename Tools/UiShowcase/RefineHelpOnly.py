"""Shared presentation rule: instructions live in local files, never in window prose."""
import xml.etree.ElementTree as E
from RefineSourceViews import t, D

VIEWS_FILE = 'SAB_HTML_Sozdat_vidy_i_listy.html'

def sources(small, settings):
    # Theme.Frame is shared by current and subsequent source-based dialog templates.
    small = small.replace('header.Children.Add(SAB.UI.PreviewDesign.Presentation.Information(new TextBlock { Text = subtitle, Style = (Style)window.FindResource("SabWindowSubtitleTextStyle") }));', '')
    small = small.replace('block.SetResourceReference(TextBlock.ForegroundProperty, "SabBrush.Text"); return block;', 'block.SetResourceReference(TextBlock.ForegroundProperty, "SabBrush.Text"); SAB.UI.PreviewDesign.Presentation.PrepareText(block, bold); return block;')
    small = small.replace('window.Content = root;', 'SAB.UI.PreviewDesign.Instructions.EnsureFooter(window, root, footer); window.Content = root;')
    settings = settings.replace('_ruleExplanation.Tag = "SabExplanation";', '_ruleExplanation.Tag = "SabDataExplanation";')
    settings = settings.replace('Presentation.Information(_status)', 'Presentation.Status(_status)')
    # Selected rule data stays visible; the generic instructions attached to it do not.
    settings = settings.replace('Выберите строку: здесь появится пояснение её действия.', 'Строка не выбрана.')
    settings = settings.replace('. Название правила служит подписью и не определяет логику записи.\\n', '\\n')
    settings = settings.replace('Выберите параметр в таблице.', 'Параметр не выбран.')
    return small, settings

def markup(window, main):
    for parent in list(window.iter()):
        for child in list(parent):
            if child.tag != t('TextBlock'): continue
            text = child.get('Text', '')
            if text.startswith('{'): continue  # Actual bound data and validation are retained.
            if child.get('Style') == '{StaticResource SabWindowSubtitleTextStyle}' or (len(text) >= 65 and child.get('Style') == '{StaticResource SabFieldHintTextStyle}'):
                parent.remove(child)
    # Remove only newly empty information wrappers, avoiding residual blank framed rows.
    for parent in list(window.iter()):
        for child in list(parent):
            if child.tag == t('Border') and child.get('Style') == '{StaticResource SabInformationBorderStyle}' and not len(child): parent.remove(child)
    if main:
        footer = next(n for n in window.iter(t('StackPanel')) if n.get('Grid.Row') == '1' and n.get('Orientation') == 'Horizontal' and not n.get('Grid.Column'))
        footer.insert(0, E.Element(t('Button'), {'Content':'Инструкция', 'Height':'34', 'Margin':'0,0,16,0', '{'+D+'}Instructions.File':VIEWS_FILE}))
    else:
        footer = next(n for n in window.iter(t('StackPanel')) if n.get('Grid.Row') == '1' and n.get('HorizontalAlignment') == 'Right')
        parent = next(p for p in window.iter() if footer in list(p))
        parent.remove(footer)
        layout = E.SubElement(parent, t('Grid'), {'Grid.Row':'1', 'Margin':'0,16,0,0'})
        footer.attrib.pop('Grid.Row', None); footer.set('Margin','0'); layout.append(footer)
        E.SubElement(layout, t('Button'), {'Content':'Инструкция', 'Height':'34', 'HorizontalAlignment':'Left', '{'+D+'}Instructions.File':VIEWS_FILE})

def categories(source):
    # The empty state remains, without a tutorial line beneath it.
    source = source.replace('empty.Children.Add(new TextBlock { Text = "Измените запрос или очистите поле поиска.", FontSize = 12, Margin = new Thickness(0,8,0,0) });', '')
    return source
