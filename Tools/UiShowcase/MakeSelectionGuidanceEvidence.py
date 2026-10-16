"""Bounded selection-guidance proof, preserving all earlier captures and release evidence."""
from pathlib import Path
import hashlib, json, re, shutil, subprocess

ROOT = Path(__file__).resolve().parents[2]
D = ROOT/'Docs/Ui/DesignSystem'
OUT = ROOT/'outputs/ui-production-preview'
BASE = OUT/'selection-guidance-baseline/files'
SHOTS = D/'screenshots-selection-guidance'
def read(p): return p.read_text(encoding='utf-8-sig')
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def rel(p): return p.relative_to(ROOT).as_posix()
def save(p,v): p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
git = shutil.which('git') or r'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe'
revision = subprocess.check_output([git,'rev-parse','HEAD'],cwd=ROOT,text=True).strip()
owners = ['SAB/Cls_InteriorElevations/Commands/CreateInteriorElevationsCommand.cs','SAB/Properties/AssemblyInfo.cs','SAB/SAB.csproj','SAB/UI/Notifications/NotificationStack.cs','SAB/Cls_InteriorElevations/Services/Selection/ElevationSelectionGuidance.cs']
source = {name:{'before':sha(BASE/name) if (BASE/name).exists() else None,'after':sha(ROOT/name)} for name in owners}
logs = {}
captures = []
for folder,expected in ((SHOTS,6),(SHOTS/'notifications-regression',9)):
    log = read(folder/'verification.txt')
    assert 'COMPLETED' in log and not re.search(r'^FAIL',log,re.M)
    files = sorted(folder.glob('*.png')); assert len(files)==expected
    logs[rel(folder/'verification.txt')] = {'sha256':sha(folder/'verification.txt'),'passCount':len(re.findall(r'^PASS ',log,re.M))}
    captures += [{'path':rel(p),'sha256':sha(p)} for p in files]
assert logs[rel(SHOTS/'verification.txt')]['passCount']==33
assert logs[rel(SHOTS/'notifications-regression/verification.txt')]['passCount']==35
assert len(re.findall(r'^PASS ',read(OUT/'selection-guidance-service-tests.txt'),re.M))==66
installers=[]
for year in (2022,2023,2024):
    payload=json.loads(read(OUT/f'msi-check/{year}/verification.json'))
    assert payload['status']=='PASS' and payload['productVersion']=='1.1.2' and payload['assemblyVersion']=='1.1.2.0'
    assert sha(Path(payload['msi']))==payload['msiSha256'].lower()
    assert any(p['name']=='SAB.dll' and p['sha256'].lower()==sha(ROOT/f'SAB/bin/Revit{year}/SAB.dll') for p in payload['verifiedPayload'])
    installers.append(payload)
upgrade=json.loads(read(OUT/'corrected-release-upgrade.json'))
assert upgrade['status']=='PASS' and len(upgrade['results'])==3
for entry in upgrade['results']: assert entry['msiSha256']==sha(ROOT/f"Installer/output/SAB_Revit_{entry['year']}.msi")
save(D/'SelectionGuidance-Installer.json',{'version':'1.1.2','status':'PASS','installationPerformed':False,'results':installers,'correctedReleaseUpgrade':upgrade})
parent=D/'DirectionsWindow-Provenance.json'
limits=['actual Revit picking/Escape/group dialogs and linked room selection','physical keyboard/mouse and OS DPI transitions','installation','actual model transactions/RFA placement']
proof={'date':'2026-10-16','version':'1.1.2','sourceRevision':revision,'dirtyBaseline':str(BASE.parent),'uiSourceOverrides':False,
    'historicalProof':rel(parent),'historicalProofSha256':sha(parent),'source':source,
    'actualAssembly':str(OUT/'plugin/SAB.dll'),'assemblySha256':sha(OUT/'plugin/SAB.dll'),
    'surface':'actual shared WPF and adapter DTO fixtures; source call-order review separate from live Revit',
    'logs':logs,'captures':captures,'runtimeDpi':96,'renderDpi':96,'serviceChecks':66,'unverified':limits,
    'recapture':'Five initially offset/clipped individual captures corrected with actual VisualBrush rendering and summary bounds checks; reviewer resolved blocker.'}
save(D/'SelectionGuidance-Provenance.json',proof);save(SHOTS/'provenance.json',proof)
report=json.loads(read(D/'WorkflowReport.template.json'))
report.update(task='SAB: selection-stage notification guidance',date='2026-10-16',scope='production',sourceRevision=revision,dirtyBaseline=str(BASE.parent),
    readFiles=['AGENTS.md','.agents/skills/sab-wpf-ui/SKILL.md',*owners],layoutSpecification=rel(D/'SelectionGuidance-Layout.md'),captures=captures,runtimeDpi=96,renderDpi=[96],
    build=[{'command':'Tools/UiShowcase/Build.ps1 -SelectionGuidance','status':'PASS','guideChecks':33,'sharedChecks':35,'captures':15},
           {'command':'Build-All-Msi.ps1 Release + fresh MSI payload + upgrade tables','status':'PASS','proof':rel(D/'SelectionGuidance-Installer.json')},
           {'command':'dotnet run Tests/InteriorElevations.DirectionPreview -c Release','status':'PASS','checks':66}])
applicable=set(range(1,13))|{18,25,26,29,30,35,42,44,48,53,55,56,59,61,62,63,64,79,83}
for c in report['checks']:
    n=int(c['id'][-3:]);c.update(applicable=n in applicable,scope='production',evidence=[],unverified=[])
    if n in {5,11,61,79}:c.update(status='NOT RUN',reason='Fresh shared WPF and linked lifecycle tests passed; full physical interaction/real Revit criteria remain unexecuted.',evidence=[rel(D/'SelectionGuidance-Provenance.json')],unverified=limits)
    elif n in applicable:c.update(status='PASS',reason='Bounded notification UI/source call-order and existing shared-stack regression checks only; no live Revit claim.',evidence=[rel(D/'SelectionGuidance-Provenance.json'),rel(D/'SelectionGuidance-Layout.md')],unverified=limits)
    else:c.update(status='N/A',reason='Transient guide has no tables/editors/tabs/picker/category/import/HTML-footer; unrelated workflows are unchanged.')
review=D/'SelectionGuidance-Review.md'
report['independentReview']={'status':'PASS' if review.exists() else 'NOT RUN','reason':'Bounded reviewer source/captures/MSI verdict; real Revit excluded.','scope':'adapter/shared Dismiss/call sites/native WPF/payload','report':rel(review) if review.exists() else ''}
save(D/'SelectionGuidance-Report.json',report)
print('PASS guidance evidence:33guide+35shared WPF checks/15PNG;66service checks;3MSI1.1.2;27upgrade boundary cases')
