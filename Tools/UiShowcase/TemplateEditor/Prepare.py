"""Isolated stage-one preview. Production files are read, never rewritten."""
from pathlib import Path
import re, json, hashlib, subprocess, xml.etree.ElementTree as E

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
MODULE = ROOT / 'SAB/Cls_ViewTemplateGraphics'
OUT = ROOT / 'outputs/ui-production-preview/template-editor'
GEN = OUT / 'source'
W = '{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'
X = '{http://schemas.microsoft.com/winfx/2006/xaml}'
E.register_namespace('', W[1:-1]); E.register_namespace('x', X[1:-1])

def body_replace(text, signature, replacement):
    start = text.index(signature)
    opening = text.index('{', start)
    depth = 1; i = opening + 1
    while depth:
        depth += (text[i] == '{') - (text[i] == '}'); i += 1
    return text[:opening] + '{\n' + replacement + '\n        }' + text[i:]

def contracts(path):
    root = E.parse(path).getroot()
    result = {'bindings': [], 'names': [], 'tags': [], 'columns': []}
    for el in root.iter():
        for key, val in el.attrib.items():
            if '{Binding' in val: result['bindings'].append(val)
            if key == X+'Name': result['names'].append(val)
            if key == 'Tag': result['tags'].append(val)
        if el.tag.split('}')[-1].startswith('DataGrid') and 'Column' in el.tag and 'Header' in el.attrib:
            result['columns'].append(el.get('Header'))
    return {k: sorted(v) for k, v in result.items()}

def prepare():
    GEN.mkdir(parents=True, exist_ok=True)
    (OUT / 'Cls_ViewTemplateGraphics/Views').mkdir(parents=True, exist_ok=True)
    manifest = {'scope': 'isolated source-derived WPF preview, not production/Revit', 'sources': {}, 'windows': {},
                'adaptations': ['namespace isolation', 'Document reads replaced by synthetic fixtures only in preview VM',
                    'TaskDialog mapped to preview validation', 'window size persistence suppressed',
                    'scoped dictionary resolves canonical file, no Application resources', 'preview footer help/key adapters']}
    files = list((MODULE/'Models').glob('*.cs')) + list((MODULE/'ViewModels').glob('*.cs')) + list((MODULE/'Views').glob('*.cs'))
    for path in files:
        source = path.read_text(encoding='utf-8-sig')
        manifest['sources'][str(path.relative_to(ROOT)).replace('\\','/')] = hashlib.sha256(path.read_bytes()).hexdigest()
        source = source.replace('SAB.ViewTemplateGraphics', 'SAB.TemplateEditorPreview')
        source = source.replace('using RevitTaskDialog = Autodesk.Revit.UI.TaskDialog;', 'using RevitTaskDialog = SAB.TemplateEditorPreview.PreviewDialogs;')
        source = re.sub(r'WindowSizeSettingsService\.Apply\([^;]+;', '// Preview does not read or write production geometry.', source)
        if path.name == 'ViewTemplateGraphicsViewModel.cs':
            source = source.replace('document ?? throw new ArgumentNullException("document")', 'document')
            source = body_replace(source, 'private void LoadSourceTemplateData(', '            GraphicsData = Fixtures.Build(new List<TemplateSelectionItem> { sourceTemplate });')
            source = body_replace(source, 'private void RefreshSelectedTemplatesData()', '''            var selected = new List<TemplateSelectionItem>();
            _lastTargetTemplateIds.Clear(); _selectedSourceTemplate = null;
            foreach (var item in Templates) { item.IsSource = false; if (item.IsTarget) { selected.Add(item); _lastTargetTemplateIds.Add(item.TemplateIdValue); } }
            GraphicsData = selected.Count > 0 ? Fixtures.Build(selected) : null;
            if (selected.Count > 0) { _selectedSourceTemplate = selected[0]; selected[0].IsSource = true; }
            OnPropertyChanged("SelectedSourceTemplate"); RaiseStatusPropertiesChanged();''')
        if path.name.endswith('Window.xaml.cs'):
            source = source.replace('DataContext = _viewModel;', 'DataContext = _viewModel;\n            PreviewShell.Attach(this);')
            source = source.replace('DataContext = this;', 'DataContext = this;\n            PreviewShell.Attach(this);')
        if path.name == 'GraphicOverrideEditorWindow.xaml.cs':
            source=source.replace('return 700.0;', 'return 560.0;').replace('return 460.0;', 'return 420.0;')
        if path.name == 'ViewTemplateGraphicsWindow.xaml.cs':
            source=source.replace('List<CategoryOverrideRow> selectedRows = GetSelectedRows(button, categoryRow);',
                'List<CategoryOverrideRow> selectedRows = GetSelectedRows(button, categoryRow);\n                    selectedRows.RemoveAll(row => !CategoryPresentation.SupportsEditor(row, GetEditorSection(action)));')
            source=source.replace('selected[i].Graphics.HalftoneState = value;',
                'if (selected[i].SupportsHalftone) selected[i].Graphics.HalftoneState = value;',1)
        if path.name == 'ViewTemplateGraphicsModels.cs':
            source=source.replace('Math.Max(0, IndentLevel) * 18.0','Math.Max(0, IndentLevel) * 24.0')
            source=source.replace('public bool AllowsVisibilityControl { get; set; }','public bool AllowsVisibilityControl { get; set; }\n        public bool SupportsProjectionLines { get; set; } = true;\n        public bool SupportsHalftone { get; set; } = true;')
        (GEN/path.name).write_text(source, encoding='utf-8-sig')
    for path in (HERE/'Views').glob('*.xaml'):
        original = contracts(MODULE/'Views'/path.name)
        actual = contracts(path)
        missing = {k: [v for v in original[k] if v not in actual[k]] for k in original}
        # Internal legacy template part, not a window field or routed-handler target.
        # Its trigger is replaced by the canonical shared TabItem template.
        template_parts = [v for v in missing['names'] if v == 'TabBorder']
        missing['names'] = [v for v in missing['names'] if v not in template_parts]
        removed_by_request={k:[] for k in original}
        if path.name=='ViewTemplateGraphicsWindow.xaml':
            original_tree=E.parse(MODULE/'Views'/path.name).getroot()
            removed_tab=next(e for e in original_tree.iter(W+'TabItem') if e.get('Header')=='Связанные файлы')
            for node in removed_tab.iter():
                for key,value in node.attrib.items():
                    if '{Binding' in value: removed_by_request['bindings'].append(value)
                    if key==X+'Name': removed_by_request['names'].append(value)
                    if key=='Tag': removed_by_request['tags'].append(value)
                if 'Column' in node.tag and 'Header' in node.attrib: removed_by_request['columns'].append(node.get('Header'))
            removed_by_request['bindings'].append('{Binding HierarchyGuide}')
            for key in missing: missing[key]=[value for value in missing[key] if value not in removed_by_request[key]]
        if any(missing.values()): raise RuntimeError(f'Missing contract in {path.name}: {missing}')
        manifest['windows'][path.name] = {'original': original, 'preview': actual, 'missing': missing, 'replacedInternalTemplateParts': template_parts, 'authorizedRemovals':removed_by_request}
        text = path.read_text(encoding='utf-8-sig').replace('CANONICAL_FLUENT_DICTIONARY', (ROOT/'SAB/UI/Styles/SABFluentLight.xaml').as_uri())
        (OUT/'Cls_ViewTemplateGraphics/Views'/path.name).write_text(text, encoding='utf-8-sig')
    (HERE/'source-contract.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    manifest['adaptations']+=['User feedback2: remove per-link tab; source model retained outside preview UI','Category cell/column capability presentation and section/halftone batch guard; source heuristic is not live API proof','Tree indentation24 and geometric guides/chevron; original ParentId/search/expand semantics','Current canonical TabControl template source-derived with WrapPanel and preview-only 2D capsule geometry; no production resource mutation']
    (HERE/'source-contract.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    print('Source contracts: retained except explicit user-authorized link tab/ASCII guide removals; production untouched.')

if __name__ == '__main__': prepare()
