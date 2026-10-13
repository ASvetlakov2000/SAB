"""Read-only source inventory; writes audit artifacts beside this script."""
import collections, csv, hashlib, json, re, subprocess
from pathlib import Path
import xml.etree.ElementTree as ET

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[2]
SAB = ROOT / 'SAB'
NS = {'m': 'http://schemas.microsoft.com/developer/msbuild/2003'}
proj = ET.parse(SAB / 'SAB.csproj')
compiled = {p.attrib['Include'].replace('\\','/') for p in proj.findall('.//m:Compile', NS)}
included = {p.attrib['Include'].replace('\\','/') for p in proj.iter() if 'Include' in p.attrib}
UI = set('Window UserControl Page Button TextBox TextBlock Label CheckBox RadioButton ComboBox ComboBoxItem ToggleButton Slider ProgressBar Grid StackPanel DockPanel WrapPanel Border GroupBox Expander TabControl TabItem ScrollViewer GridSplitter DataGrid ListView GridView GridViewColumn ListBox ListBoxItem TreeView TreeViewItem DataGridCheckBoxColumn DataGridComboBoxColumn DataGridTemplateColumn DataGridTextColumn ToolTip ContextMenu Menu MenuItem Separator Image Hyperlink Popup StatusBar StatusBarItem ItemsControl ItemsPresenter ContentControl ContentPresenter ScrollContentPresenter ScrollBar Track Thumb RepeatButton Rectangle Ellipse Path Line Canvas UniformGrid Viewbox RichTextBox Run BulletDecorator VirtualizingStackPanel SymbolIcon AdaptiveSettingsGrid ParameterPicker CategorySelectionControl'.split())
XNS = '{http://schemas.microsoft.com/winfx/2006/xaml}'
def local(t): return t.rsplit('}',1)[-1]
def rel(p): return p.relative_to(ROOT).as_posix()
def line_at(text, pos): return text.count('\n',0,pos)+1
def mask(text):
    # Keep offsets and newlines; braces in strings/comments cannot split classes.
    pattern = r'@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*[\s\S]*?\*/'
    return re.sub(pattern, lambda m: ''.join('\n' if c=='\n' else ' ' for c in m[0]), text)
def class_spans(text):
    clean=mask(text); result=[]
    for m in re.finditer(r'\bclass\s+(\w+)\s*(?::\s*([^\{]+))?\s*\{',clean):
        start=m.end()-1; depth=1; end=start+1
        while end<len(clean) and depth:
            depth += (clean[end]=='{')-(clean[end]=='}'); end+=1
        result.append((m[1], (m[2] or '').strip(), m.start(),end))
    return result
def ctor_types(text):
    clean=mask(text)
    hits=[]
    for m in re.finditer(r'new\s+FrameworkElementFactory\s*\(\s*typeof\s*\(\s*(?:[\w]+\.)*(\w+)\s*\)|\bnew\s+(?:[\w]+\.)*(\w+)\s*(?=[({])',clean):
        t=m[1] or m[2]
        if t in UI: hits.append((t,line_at(text,m.start())))
    # Literal XAML used by runtime templates.
    for literal in re.finditer(r'@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"',text):
        if not re.search(r'<(?:ControlTemplate|DataTemplate|Style)\b',literal[0]): continue
        for m in re.finditer(r'<(?:[\w]+:)?([A-Z]\w*)\b',literal[0]):
            if m[1] in UI: hits.append((m[1],line_at(text,literal.start()+m.start())))
    return hits

xamls=[]; windows=[]; sources={}; hashes={}
for p in sorted(ROOT.rglob('*.xaml')):
    if any(part.lower() in {'bin','obj','packages','.git','outputs','node_modules','backup','backups'} for part in p.relative_to(ROOT).parts): continue
    text=p.read_text(encoding='utf-8-sig'); hashes[rel(p)]=hashlib.sha256(p.read_bytes()).hexdigest()
    try: tree=ET.fromstring(text)
    except ET.ParseError as e:
        xamls.append({'path':rel(p),'error':str(e)}); continue
    path=rel(p); is_sab=p.is_relative_to(SAB); inside=p.relative_to(SAB).as_posix() if is_sab else ''
    scope='active' if is_sab and inside in included and not inside.startswith('Archive/') else 'archive' if '/Archive/' in path else 'prototype/tool'
    direct=collections.Counter(); templ=collections.Counter(); controls=collections.Counter()
    def walk(n,in_template=False,in_resources=False):
        t=local(n.tag); it=in_template or t in {'ControlTemplate','DataTemplate','ItemsPanelTemplate'}; ir=in_resources or t=='ResourceDictionary' or t.endswith('.Resources')
        if t in UI:
            controls[t]+=1
            (templ if it or ir else direct)[t]+=1
        for c in n: walk(c,it,ir)
    walk(tree)
    defs=[]
    for n in tree.iter():
        t=local(n.tag)
        if t in {'ResourceDictionary','ControlTemplate','DataTemplate','ItemsPanelTemplate','Style'}:
            defs.append({'kind':t,'key':n.attrib.get(XNS+'Key',''),'target':n.attrib.get('TargetType',''),'source':n.attrib.get('Source','')})
    row={'path':path,'scope':scope,'root':local(tree.tag),'class':tree.attrib.get(XNS+'Class',''),'title':tree.attrib.get('Title',''),'width':tree.attrib.get('Width','auto'),'height':tree.attrib.get('Height','auto'),'minWidth':tree.attrib.get('MinWidth','—'),'minHeight':tree.attrib.get('MinHeight','—'),'direct':dict(direct),'templates':dict(templ),'all':dict(controls),'definitions':defs,'attributes':sorted({local(k) for n in tree.iter() for k in n.attrib})}
    xamls.append(row)
    if row['root']=='Window': windows.append(row)
    if scope=='active':
        for t in controls:
            sources.setdefault(t,[]).append(path)

csrows=[]; classes=[]; anonymous=[]
for p in sorted(ROOT.rglob('*.cs')):
    if p.is_relative_to(OUT): continue
    if any(part.lower() in {'bin','obj','packages','.git','outputs','node_modules','backup','backups'} for part in p.relative_to(ROOT).parts): continue
    inside=p.relative_to(SAB).as_posix() if p.is_relative_to(SAB) else ''
    text=p.read_text(encoding='utf-8-sig'); hashes[rel(p)]=hashlib.sha256(p.read_bytes()).hexdigest()
    scope='active' if inside in compiled else 'archive' if inside.startswith('Archive/') else 'prototype/tool' if not inside else 'uncompiled'
    hits=ctor_types(text); spans=class_spans(text)
    for name,base,start,end in spans:
        if re.search(r'\b(Window|UserControl|Page|StackPanel|Grid|Button|Control|ContentControl)\b',base):
            body=text[start:end]; types=collections.Counter(t for t,_ in ctor_types(body))
            row={'name':name,'base':base,'path':rel(p),'line':line_at(text,start),'scope':scope,'types':dict(types),'titles':re.findall(r'\bTitle\s*=\s*"([^"]+)"',body),'dimensions':re.findall(r'\b(Width|Height|MinWidth|MinHeight|MaxHeight)\s*=\s*([\d.]+)',body)}
            classes.append(row)
    for m in re.finditer(r'\bnew\s+Window\s*[{(]',mask(text)):
        owner=next((n for n,b,s,e in reversed(spans) if s<=m.start()<e),'(global)')
        anonymous.append({'path':rel(p),'line':line_at(text,m.start()),'owner':owner,'scope':scope})
    if hits:
        csrows.append({'path':rel(p),'scope':scope,'types':dict(collections.Counter(t for t,_ in hits)),'evidence':[{'type':t,'line':ln} for t,ln in hits]})
        if scope=='active':
            for t,_ in hits: sources.setdefault(t,[]).append(rel(p))

data={'commit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),'xaml':xamls,'classes':classes,'anonymous_windows':anonymous,'cs_ui':csrows,'type_sources':{t:sorted(set(v)) for t,v in sorted(sources.items())},'source_sha256':hashes}
(OUT/'inventory.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
with (OUT/'windows.csv').open('w',newline='',encoding='utf-8-sig') as f:
    w=csv.writer(f); w.writerow(['scope','window','path','width','height','minWidth','minHeight','direct_types','template_types','all_types'])
    for r in windows: w.writerow([r['scope'],r['title'],r['path'],r['width'],r['height'],r['minWidth'],r['minHeight'],len(set(r['direct'])-{'Window'}),len(r['templates']),', '.join(sorted(set(r['all'])-{'Window'}))])
print('XAML roots:',dict(collections.Counter((r.get('scope'),r.get('root')) for r in xamls)))
print('Active UI classes:',[(r['name'],r['base']) for r in classes if r['scope']=='active'])
print('Anonymous:',anonymous)
print('ACTIVE WINDOWS:')
for r in windows:
    if r['scope']=='active': print(Path(r['path']).stem, r['width']+'x'+r['height'],len(set(r['direct'])-{'Window'}), ','.join(sorted(set(r['all'])-{'Window'})))
print('Active types:', ', '.join(data['type_sources']))
print('Definitions:',dict(collections.Counter(d['kind'] for r in xamls if r.get('scope')=='active' for d in r['definitions'])))
