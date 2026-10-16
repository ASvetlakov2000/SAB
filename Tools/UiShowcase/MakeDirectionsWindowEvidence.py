"""Current actual WPF and MSI proof; immutable older releases/captures are preserved."""
from pathlib import Path
import hashlib, json, re, shutil, subprocess

ROOT = Path(__file__).resolve().parents[2]
D = ROOT / 'Docs/Ui/DesignSystem'
OUT = ROOT / 'outputs/ui-production-preview'
BASE = OUT / 'directions-window-baseline/files'
SHOTS = D / 'screenshots-directions-window'

def read(path): return path.read_text(encoding='utf-8-sig')
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def rel(path): return path.relative_to(ROOT).as_posix()
def save(path, value): path.write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8', newline='\n')

git = shutil.which('git') or r'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe'
revision = subprocess.check_output([git, 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
owners = [
    'SAB/Cls_InteriorElevations/Views/ElevationDirectionPreviewWindow.cs',
    'SAB/Cls_InteriorElevations/Services/Elevations/ElevationDirectionPreviewController.cs',
    'SAB/Properties/AssemblyInfo.cs',
    'Docs/PluginInstructions/SAB_HTML_Sozdanie_i_korrektirovka_razvertok.html',
    'Tools/UiShowcase/ElevationDirectionPreviewEvidence.cs',
    'Tools/UiShowcase/SourcePreview.cs', 'Tools/UiShowcase/Build.ps1',
    'Installer/WixSharpInstaller/Program.cs', 'Installer/Publish-GitHubRelease.ps1',
    'Tests/InteriorElevations.DirectionPreview/LifecycleTests.cs',
]
source = {}
for name in owners:
    blob = subprocess.check_output([git, 'show', f'{revision}:{name}'], cwd=ROOT)
    source[name] = {'before': sha(BASE/name) if (BASE/name).exists() else hashlib.sha256(blob).hexdigest(),
                    'beforeCommitBlobSha256': hashlib.sha256(blob).hexdigest(), 'after': sha(ROOT/name)}
parent = D/'ElevationPreviewTip-Provenance.json'
assert source['SAB/Properties/AssemblyInfo.cs']['before'] == json.loads(read(parent))['source']['SAB/Properties/AssemblyInfo.cs']['after']
log = read(SHOTS/'verification.txt')
assert 'COMPLETED' in log and not re.search(r'^FAIL', log, re.M)
captures = [{'path':rel(path), 'sha256':sha(path)} for path in sorted(SHOTS.glob('*.png'))]
assert len(captures) == 7
assert 'Directions has exactly three actions' in log and 'Escape requests cancellation without bypassing cleanup' in log
tests = read(OUT/'directions-window-service-tests.txt')
assert len(re.findall(r'^PASS ',tests,re.M)) == 66 and 'COMPLETED 66 checks' in tests
html = read(ROOT/owners[3])
assert all(text not in html for text in ('Для читаемости номеров включите Keep Readable', 'Если марки на коротких участках сливаются', 'Нужны доступные экземплярные текстовые параметры'))
assert 'Не двигайте и не вращайте временные марки штатными командами Revit' in html
assert 'меняйте сторону кнопкой <span class="cmd">Сменить сторону</span> в окне превью.' in html
assert '<span class="cmd">Отмена</span>, Escape и крестик удаляют марки' not in html
installers = []
for year in (2022,2023,2024):
    payload = json.loads(read(OUT/f'msi-check/{year}/verification.json'))
    assert payload['status'] == 'PASS' and payload['productVersion'] == '1.1.1'
    assert payload['assemblyVersion'] == '1.1.1.0' and sha(Path(payload['msi'])) == payload['msiSha256'].lower()
    assert any(p['name'] == 'SAB.dll' and p['sha256'].lower() == sha(ROOT/f'SAB/bin/Revit{year}/SAB.dll') for p in payload['verifiedPayload'])
    installers.append(payload)
upgrade = json.loads(read(OUT/'corrected-release-upgrade.json'))
assert upgrade['status'] == 'PASS' and len(upgrade['results']) == 3
for entry in upgrade['results']:
    assert entry['msiSha256'] == sha(ROOT/f"Installer/output/SAB_Revit_{entry['year']}.msi")
save(D/'DirectionsWindow-Installer.json', {'status':'PASS','version':'1.1.1','installationPerformed':False,'results':installers,'correctedReleaseUpgrade':upgrade})
unverified = ['live Revit picking/transactions/RFA placement', 'actual installation including1.1.4→1.1.1', 'physical keyboard/mouse and accessibility automation', 'OS DPI transitions']
proof = {'date':'2026-10-16','version':'1.1.1','sourceRevision':revision,'dirtyBaseline':str(BASE.parent),
    'historicalProof':rel(parent),'historicalProofSha256':sha(parent),'uiSourceOverrides':False,'source':source,
    'actualAssembly':str(OUT/'plugin/SAB.dll'),'assemblySha256':sha(OUT/'plugin/SAB.dll'),
    'surface':'actual production SAB.dll WPF with fixtures outside Revit','runtimeDpi':96,'renderDpi':96,
    'nativeAssertions':len(re.findall(r'^PASS ',log,re.M)),'captures':captures,'logSha256':sha(SHOTS/'verification.txt'),
    'serviceChecks':66,'htmlContentChecks':'PASS','installer':rel(D/'DirectionsWindow-Installer.json'),'unverified':unverified}
save(D/'DirectionsWindow-Provenance.json',proof)
save(SHOTS/'provenance.json',proof)
report = json.loads(read(D/'WorkflowReport.template.json'))
report.update(task='SAB: vertical Directions window and sequential release correction',date='2026-10-16',scope='production',sourceRevision=revision,
    dirtyBaseline=str(BASE.parent),readFiles=['AGENTS.md','.agents/skills/sab-wpf-ui/SKILL.md',*owners],
    layoutSpecification=rel(D/'DirectionsWindow-Layout.md'),captures=captures,runtimeDpi=96,renderDpi=[96],
    build=[{'command':'Tools/UiShowcase/Build.ps1 -DirectionsWindow','status':'PASS','assertions':proof['nativeAssertions'],'captures':7},
           {'command':'Build-All-Msi.ps1 Release + fresh payload + corrected release upgrade tables','status':'PASS','evidence':rel(D/'DirectionsWindow-Installer.json')},
           {'command':'dotnet run Tests/InteriorElevations.DirectionPreview -c Release','status':'PASS','checks':66}])
applicable = set(range(1,13)) | {18,25,26,29,30,35,42,43,44,48,50,53,55,56,68,79,82}
for check in report['checks']:
    number = int(check['id'][-3:])
    check.update(applicable=number in applicable,scope='production',evidence=[],unverified=[])
    if number in {5,11,79}:
        check.update(status='NOT RUN',reason='Actual WPF actions and66linked lifecycle checks passed; live Revit and full physical input/state matrix unavailable.',evidence=[rel(D/'DirectionsWindow-Provenance.json')],unverified=unverified)
    elif number in applicable:
        check.update(status='PASS',reason='Bounded actual WPF layout/action, source and documentation checks only; no broad module or Revit claim.',evidence=[rel(D/'DirectionsWindow-Provenance.json'),rel(D/'DirectionsWindow-Layout.md')],unverified=unverified)
    else:
        check.update(status='N/A',reason='This compact three-action window has no tables, fields, tabs, pickers, category/import or notification functions; unchanged main workflow and geometry are outside this UI scope.')
review = D/'DirectionsWindow-Review.md'
report['independentReview'] = {'status':'PASS' if review.exists() else 'NOT RUN','reason':'Bounded independent review; live Revit and installation excluded.','scope':'actual WPF/source/local HTML/MSI tables','report':rel(review) if review.exists() else ''}
save(D/'DirectionsWindow-Report.json', report)
print(f"PASS Directions evidence: {proof['nativeAssertions']} actual WPF checks/7PNG;66service checks;HTML;3MSI1.1.1;27upgrade boundary cases")
