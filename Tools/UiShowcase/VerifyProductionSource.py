"""Verify migrated production sources against the pre-migration contract. Never rewrites UI."""
from pathlib import Path
from collections import Counter
import hashlib, json, re
import xml.etree.ElementTree as E

ROOT=Path(__file__).resolve().parents[2]
HERE=Path(__file__).resolve().parent
P='{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'
X='{http://schemas.microsoft.com/winfx/2006/xaml}'

def digest(data): return hashlib.sha256(data).hexdigest()
def body(text, signature):
    start=text.index(signature); opening=text.index('{',start)
    mask=re.sub(r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*[\s\S]*?\*/',lambda m:' '*(m.end()-m.start()),text)
    depth=1; end=opening+1
    while depth:
        if mask[end]=='{':depth+=1
        elif mask[end]=='}':depth-=1
        end+=1
    return text[opening:end]

def inventory(path):
    tree=E.parse(path).getroot()
    return Counter(v for n in tree.iter() for v in n.attrib.values() if '{Binding' in v)

def verify():
    baseline=json.loads((HERE/'production-baseline.json').read_text(encoding='utf8'))
    proof={'version':'0.5-production-migration','uiSourceOverrides':False,'checks':[], 'businessMethods':[], 'files':{}}
    def check(ok,message):
        if not ok: raise RuntimeError(message)
        proof['checks'].append(message)
    for path, expected in baseline['bindingInventories'].items():
        actual=inventory(ROOT/path); missing=Counter(expected)-actual
        check(not missing,'All original bindings preserved: '+path)
        proof.setdefault('bindingCounts',{})[path]={'original':sum(expected.values()),'current':sum(actual.values()),'missing':list(missing.elements())}
    markup=E.parse(ROOT/'SAB/Cls_CreateViewsAndSheets/Views/CreateViewsAndSheetsWindow.xaml').getroot()
    grid=next(n for n in markup.iter(P+'DataGrid') if n.get(X+'Name')=='CreationRowsDataGrid')
    check(len(grid.find(P+'DataGrid.Columns'))==10,'All ten original table columns retained')
    settings=(ROOT/'SAB/Cls_ParameterTools/UI/SettingsWindow.cs').read_text(encoding='utf8')
    for method in baseline['methods']:
        actual=body(settings,method['method'])
        # Explicit presentation helper rename only. No other normalization or business edits allowed.
        normalized=actual.replace('FluentTheme.','Theme.')
        check(digest(normalized.encode())==method['sha256'],'Business body preserved (presentation helper rename only): '+method['method'])
        proof['businessMethods'].append({'method':method['method'],'originalSha256':method['sha256'],'currentSha256':digest(actual.encode()),'presentationHelperRenamed':actual!=normalized})
    for path, original in baseline['unchangedSources'].items():
        check(digest((ROOT/path).read_bytes())==original,'Unchanged production source: '+path)
    shared=(ROOT/'SAB/UI/FluentLight/Presentation.cs').read_text(encoding='utf8')
    theme=(ROOT/'SAB/Cls_ParameterTools/UI/FluentTheme.cs').read_text(encoding='utf8')
    check('Application.Current.FindResource' not in shared+theme,'Shared Fluent layer has no dependency on application-global Fluent resources')
    check('Key.Escape' in theme and 'combo.IsDropDownOpen' in theme,'Parameter native window retains Escape/dropdown keyboard handling')
    for path in baseline['bindingInventories']:
        text=(ROOT/path).read_text(encoding='utf8')
        check('Предпросмотр · тестовые данные' not in text,'Production markup has no synthetic-data badge: '+path)
    for path in baseline['bindingInventories']:
        check('SABFluentLight.xaml' in (ROOT/path).read_text(encoding='utf8'),'Production view loads scoped Fluent dictionary: '+path)
    files=list(baseline['unchangedSources'])+list(baseline['bindingInventories'])+[
        'SAB/Cls_ParameterTools/UI/SettingsWindow.cs','SAB/Cls_ParameterTools/UI/ParameterPicker.cs',
        'SAB/Cls_ParameterTools/UI/CategorySelectionControl.cs','SAB/Cls_ParameterTools/UI/FluentTheme.cs',
        'SAB/UI/FluentLight/FluentResources.cs','SAB/UI/FluentLight/Presentation.cs','SAB/UI/Styles/SABFluentLight.xaml','SAB/SAB.csproj']
    proof['files']={p:digest((ROOT/p).read_bytes()) for p in files}
    (HERE/'production-source-contract.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    print('PASS production source contract:',len(proof['checks']),'checks;135/103 original bindings;10 columns/methods;actual sources without visual substitution')

if __name__=='__main__':verify()
