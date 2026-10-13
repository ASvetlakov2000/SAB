"""Adapt existing SAB templates into the isolated preview; never writes SAB source."""
from pathlib import Path
import json, re, hashlib
import xml.etree.ElementTree as E

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
SOURCE=ROOT/'SAB/UI/Styles/SABWindowStyles.xaml'
P='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
X='http://schemas.microsoft.com/winfx/2006/xaml'
E.register_namespace('',P); E.register_namespace('x',X); E.register_namespace('ui','clr-namespace:Wpf.Ui.Controls;assembly=Wpf.Ui')
styles=E.fromstring(SOURCE.read_text(encoding='utf-8-sig'))
for child in list(styles):
    if child.tag in {f'{{{P}}}SolidColorBrush',f'{{{P}}}ResourceDictionary.MergedDictionaries'}: styles.remove(child)
for node in styles.iter():
    for k,v in list(node.attrib.items()):
        node.attrib[k]=re.sub(r'\{StaticResource (SabBrush\.[^}]+)\}',r'{DynamicResource \1}',v)
        if k=='CornerRadius' and v in {'4','5','6','8'}: node.attrib[k]='{DynamicResource SabUi.ControlRadius}'
bykey={n.attrib.get(f'{{{X}}}Key'):n for n in styles}
def setter(key,prop,value):
    n=bykey[key]; old=next((s for s in n if s.tag==f'{{{P}}}Setter' and s.attrib.get('Property')==prop),None)
    if old is not None: n.remove(old)
    n.insert(0,E.Element(f'{{{P}}}Setter',{'Property':prop,'Value':value}))
for key in ['SabButtonBaseStyle','SabTextBoxStyle','SabComboBoxStyle']:
    setter(key,'Height','{DynamicResource SabUi.ControlHeight}')
setter('SabButtonBaseStyle','MinWidth','0')
setter('SabButtonBaseStyle','FocusVisualStyle','{DynamicResource SabUi.FocusVisual}')
setter('SabOutlineButtonStyle','BorderBrush','{DynamicResource SabBrush.Border}')
setter('SabOutlineButtonStyle','Foreground','{DynamicResource SabBrush.Text}')
setter('SabTextBoxStyle','BorderThickness','{DynamicResource SabUi.FieldBorder}')
setter('SabComboBoxStyle','BorderThickness','{DynamicResource SabUi.FieldBorder}')
setter('SabDataGridStyle','RowHeight','{DynamicResource SabUi.RowHeight}')
setter('SabDataGridStyle','GridLinesVisibility','Horizontal')
setter('SabDataGridStyle','BorderThickness','0')
setter('SabDataGridStyle','EnableRowVirtualization','True')
setter('SabDataGridStyle','EnableColumnVirtualization','True')
setter('SabDataGridColumnHeaderStyle','Height','{DynamicResource SabUi.HeaderHeight}')
setter('SabDataGridColumnHeaderStyle','HorizontalContentAlignment','Left')
setter('SabDataGridColumnHeaderStyle','BorderThickness','0,0,0,1')
setter('SabDataGridCellStyle','BorderThickness','0')
for n in bykey['SabDataGridRowStyle']:
    if n.tag==f'{{{P}}}Setter' and n.attrib.get('Property')=='ToolTip': n.attrib['Value']='{Binding ErrorText}'
trigger=E.Element(f'{{{P}}}Trigger',{'Property':'IsPressed','Value':'True'})
trigger.append(E.Element(f'{{{P}}}Setter',{'Property':'Opacity','Value':'0.78'}))
bykey['SabButtonBaseStyle'].find(f'{{{P}}}Style.Triggers').append(trigger)
(HERE/'Styles').mkdir(exist_ok=True)
E.indent(styles)
(HERE/'Styles/Controls.xaml').write_text('<!-- Derived for preview from SABWindowStyles.xaml; see provenance.json. -->\n'+E.tostring(styles,encoding='unicode'),encoding='utf8')

themes={
 'FluentLight':dict(label='Fluent Light',control=36,row=72,header=38,pad=24,radius=6,title=24,bg='#F5F7FB',line='#D8DEE8',weak='#E8EDF4',table='#F4F7FB',secondary='#596579'),
 'FluentCompact':dict(label='Fluent Compact',control=28,row=54,header=32,pad=18,radius=4,title=22,bg='#F3F5F8',line='#CDD5E0',weak='#E3E8F0',table='#EDF1F6',secondary='#536074'),
 'MinimalSaas':dict(label='Minimal SaaS',control=34,row=66,header=36,pad=28,radius=3,title=24,bg='#FFFFFF',line='#DDE2E9',weak='#EEF0F4',table='#FAFBFC',secondary='#626C7A'),
 'Unified':dict(label='SAB Unified',control=32,row=60,header=34,pad=20,radius=5,title=23,bg='#F6F8FB',line='#D5DDE8',weak='#E8EDF4',table='#F4F6FA',secondary='#566479'),
}
for name,t in themes.items():
    colors={'WindowBackground':t['bg'],'PanelBackground':'#FFFFFF','TableHeaderBackground':t['table'],'Border':t['line'],'BorderWeak':t['weak'],'Text':'#202B3C','TextSecondary':t['secondary'],'Accent':'#0F6CBD','AccentHover':'#095B9F','AccentLight':'#EAF3FF','Error':'#B42318','Warning':'#8A4B08','Success':'#067647','DisabledBackground':'#F2F4F7','DisabledText':'#667085','ScrollBarTrack':'Transparent','ScrollBarThumb':'#B6C0CE','ScrollBarThumbHover':'#8795A7','ScrollBarThumbPressed':'#65758C'}
    text='<ResourceDictionary xmlns="'+P+'" xmlns:x="'+X+'" xmlns:sys="clr-namespace:System;assembly=mscorlib">\n'
    text+='\n'.join('  <SolidColorBrush x:Key="SabBrush.'+k+'" Color="'+v+'" />' for k,v in colors.items())+'\n'
    for k,v in [('ControlHeight',t['control']),('RowHeight',t['row']),('HeaderHeight',t['header']),('TitleSize',t['title'])]:text+=f'  <sys:Double x:Key="SabUi.{k}">{v}</sys:Double>\n'
    text+=f'  <Thickness x:Key="SabUi.PagePadding">{t["pad"]}</Thickness>\n  <CornerRadius x:Key="SabUi.ControlRadius">{t["radius"]}</CornerRadius>\n'
    text+='  <Thickness x:Key="SabUi.FieldBorder">'+('0,0,0,1' if name=='MinimalSaas' else '1')+'</Thickness>\n'
    text+='  <SolidColorBrush x:Key="SabUi.ErrorBackground" Color="#FEF3F2" />\n  <SolidColorBrush x:Key="SabUi.WarningBackground" Color="#FFFAEB" />\n  <SolidColorBrush x:Key="SabUi.SuccessBackground" Color="#ECFDF3" />\n</ResourceDictionary>\n'
    (HERE/'Styles'/f'{name}.xaml').write_text(text,encoding='utf8')
(HERE/'Styles/provenance.json').write_text(json.dumps({'source':'SAB/UI/Styles/SABWindowStyles.xaml','sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'changes':['brush references dynamic','density and radius tokens','table horizontal separators','visible keyboard focus','button pressed feedback'],'status':'preview-only / pending approval','themes':themes},ensure_ascii=False,indent=2),encoding='utf8')
print('Generated 4 token dictionaries and adapted SAB controls. Production source unchanged.')
