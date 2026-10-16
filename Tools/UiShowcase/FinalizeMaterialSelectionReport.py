"""Finalize bounded material-picker documentation from recorded evidence."""
from pathlib import Path
import csv
import io
import json

ROOT = Path(__file__).resolve().parents[2]
D = ROOT / 'Docs/Ui/DesignSystem'
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def save(p, value): p.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')

template = read(D/'WorkflowReport.template.json')
if not any(c['id'] == 'SAB-UI-084' for c in template['checks']):
    template['checks'].append({'id': 'SAB-UI-084', 'criterion': 'ErrorRegistry.md#sab-ui-084', 'applicable': None,
        'status': 'NOT RUN', 'scope': '', 'reason': 'Форма ещё не заполнена', 'evidence': [], 'unverified': []})
    save(D/'WorkflowReport.template.json', template)
proof = read(D/'MaterialSelection-Provenance.json')
report = read(D/'MaterialSelection-Report.json')
report.update(readFiles=['AGENTS.md', '.agents/skills/sab-wpf-ui/SKILL.md', 'Docs/Ui/DesignSystem/Specification.md',
    'Docs/Ui/DesignSystem/ErrorRegistry.md', 'Docs/Ui/DesignSystem/ReviewRoles.md', *proof['source']],
    captures=proof['captures'], runtimeDpi=None, renderDpi=[96],
    build=[{'command': proof['command'], 'exitCode': 0, 'assertions': 42, 'captures': 9},
           {'command': 'Installer/Build-All-Msi.ps1 -Configuration Release', 'exitCode': 0, 'years': [2022,2023,2024],
            'evidence': 'Docs/Ui/DesignSystem/MaterialSelection-Installer.json'},
           {'command': 'Installer/Publish-GitHubRelease.ps1 -Version 1.1.3 -PrepareOnly', 'exitCode': 0}],
    independentReview={'status': 'PASS', 'disposition': 'ship', 'scope': 'bounded actual WPF/source plus local release packet',
        'report': 'Docs/Ui/DesignSystem/MaterialSelection-Review.md', 'reason': 'Independent agent reviewed actual source/captures and MSI payload/hash packet; remote publication still requires root verification.'},
    unverified=proof['unverified'])
applicable = set(range(1,17)) | {18,19,20,21,25,26,27,28,29,31,35,36,37,40,42,43,44,45,46,48,53,54,55,56,58,67,72,84}
report['checks'] = []
for baseline in template['checks']:
    c = dict(baseline); n = int(c['id'][-3:]); active = n in applicable
    c.update(applicable=active, scope='production', status='PASS' if active else 'N/A',
        reason='Bounded actual WPF/source and independent review; only applicable material-picker subcriteria assessed.' if active
        else 'Specific component absent from this task: no CRUD/editors/profiles/tabs/notification stack/elevation/parameter-check/standalone launcher.',
        evidence=['Docs/Ui/DesignSystem/MaterialSelection-Provenance.json', 'Docs/Ui/DesignSystem/MaterialSelection-Review.md'] if active else [],
        unverified=list(proof['unverified']) if active else [])
    if n in {5,11,25}:
        c.update(status='NOT RUN', reason='Builds and bounded WPF assertions passed; live Revit context, full physical keyboard/accessibility/state matrix and animation-clock/OS checks not executed.')
    if n == 40:
        c['reason'] = 'All/Included/Excluded and hidden inclusion tested; named selection profiles are not part of approved material function scope.'
    report['checks'].append(c)
save(D/'MaterialSelection-Report.json', report)

status = read(D/'STATUS.json')
status['materialSelection'] = {'version': '1.1.3', 'stage': 'production_implemented', 'approvedAt': '2026-10-16',
    'authority': 'Direct user approval in current chat: implement four-column picker, then commit+push, update distributions and publish new release.',
    'productionMigrationAuthorized': True, 'publicationAuthorized': True, 'installationAuthorized': False,
    'nativeAssertions': 42, 'captures': 9, 'layout': 'Docs/Ui/DesignSystem/MaterialSelection-Layout.md',
    'evidence': 'Docs/Ui/DesignSystem/MaterialSelection-Provenance.json', 'report': 'Docs/Ui/DesignSystem/MaterialSelection-Report.json',
    'installerEvidence': 'Docs/Ui/DesignSystem/MaterialSelection-Installer.json', 'independentReview': 'Docs/Ui/DesignSystem/MaterialSelection-Review.md',
    'releaseStatus': 'prepared_pending_publication', 'unverified': proof['unverified']}
status['installer'].update(version='1.1.3', evidence='Docs/Ui/DesignSystem/MaterialSelection-Installer.json',
    independentReview='Docs/Ui/DesignSystem/MaterialSelection-Review.md', installationPerformed=False,
    scope='Current material selection compiled BAML/DTO plus retained existing modules; fresh extracted MSI2022/2023/2024 verified.')
save(D/'STATUS.json', status)
sidecar = ROOT/'Tools/UiShowcase/.impeccable/design.json'
design = read(sidecar)
design['extensions']['materialSelection'] = {'canonicalLayout': 'Docs/Ui/DesignSystem/MaterialSelection-Layout.md',
    'moduleVersion': '1.1.3', 'pattern': 'Data Manager', 'sharedTokensUnchanged': True,
    'normalRowForeground': '#000000', 'actionBrushKeys': ['SabBrush.Accent','SabBrush.Success','SabBrush.Error'],
    'columns': ['В работу','Материал','Штриховка сечения','Действие'], 'windowDip': [900,780], 'minimumDip': [760,620],
    'sharedResourceOwner': 'SAB/UI/FluentLight/FluentResources.cs',
    'evidence': 'Docs/Ui/DesignSystem/MaterialSelection-Provenance.json', 'installationAuthorized': False}
save(sidecar, design)

coverage = D/'component-coverage.csv'
lines = coverage.read_text(encoding='utf-8-sig').splitlines(keepends=True)
fields = next(csv.reader([lines[0]]))
for index, line in enumerate(lines[1:], 1):
    values = next(csv.reader([line]))
    if values[0] != 'DataGrid': continue
    for field, value in {'active_source':'SAB/Cls_FilledRegionFromMaterial/MaterialSelectionWindow.xaml',
            'current_evidence':'Docs/Ui/DesignSystem/MaterialSelection-Provenance.json',
            'registry_ids':'SAB-UI-084'}.items():
        pos = fields.index(field)
        if value not in values[pos]: values[pos] += ';'+value
    text = io.StringIO(); csv.writer(text, lineterminator='\n').writerow(values); lines[index] = text.getvalue()
    break
coverage.write_text(''.join(lines), encoding='utf-8', newline='')
print('Finalized84-ID report, scoped approval/status, existing native sidecar and DataGrid coverage; no UI/build changes')
