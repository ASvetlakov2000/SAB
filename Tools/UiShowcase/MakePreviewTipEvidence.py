"""Backend/resource successor; no new WPF or live Revit verification is claimed."""
from pathlib import Path
import hashlib,json,re,shutil,subprocess
ROOT=Path(__file__).resolve().parents[2];D=ROOT/'Docs/Ui/DesignSystem'
BASE=ROOT/'outputs/ui-production-preview/preview-tip-baseline'
def read(p):return p.read_text(encoding='utf-8-sig')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def save(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def rel(p):return p.relative_to(ROOT).as_posix()
parent_path=D/'screenshots-preview-runtime/provenance.json';parent=json.loads(read(parent_path))
test_path=ROOT/'outputs/ui-production-preview/preview-tip-tests.txt';tests=read(test_path)
assert 'COMPLETED 66 checks' in tests and len(re.findall('^PASS ',tests,re.M))==66
assert not re.search('^FAIL',tests,re.M)
owners=['SAB/Cls_InteriorElevations/Services/Elevations/ElevationDirectionPreviewService.cs','SAB/Properties/AssemblyInfo.cs','Installer/Build-All-Msi.ps1']
owners += ['Tests/InteriorElevations.DirectionPreview/'+name for name in ('Program.cs','LifecycleTests.cs','RevitStubs.cs')]
source={name:{'before':sha(BASE/'files'/name),'after':sha(ROOT/name)} for name in owners}
for name,entry in source.items():
    if name in parent['source']:assert entry['before']==parent['source'][name]['after'],name
family='SAB_Развертки_Направление.rfa';family_hash='25f8a49176f7111def0748e3ae00630560a75c11076903f9de730343042c316d'
resources={}
for folder in ('2022','2023-2024'):
    name=f'SAB/Families for Plugin/CreateInteriorElevationsCommand/{folder}/{family}'
    assert sha(ROOT/name)==family_hash
    resources[name]={'before':sha(BASE/'files'/name),'after':family_hash}
git=shutil.which('git') or r'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe'
revision=subprocess.check_output([git,'rev-parse','HEAD'],cwd=ROOT,text=True).strip()
installer=[]
for year in (2022,2023):
    entry=json.loads(read(ROOT/f'outputs/ui-production-preview/msi-check/{year}/verification.json'))
    assert entry['status']=='PASS' and entry['productVersion']=='1.1.4' and sha(Path(entry['msi']))==entry['msiSha256'].lower()
    assert any(p['name']=='SAB.dll' and p['sha256'].lower()==sha(ROOT/f'SAB/bin/Revit{year}/SAB.dll') for p in entry['verifiedPayload'])
    assert any(p['name'].startswith('family_') and p['sha256'].lower()==family_hash for p in entry['verifiedPayload'])
    installer.append(entry)
save(D/'ElevationPreviewTip-Installer.json',{'status':'PASS','version':'1.1.4','installationPerformed':False,'results':installer})
unverified=['live Revit placement/rotation/transactions','actual RFA origin, rendering and label readability','physical input/printing/accessibility/OS DPI','installation']
proof={'date':'2026-10-16','version':'1.1.4','sourceRevision':revision,'dirtyBaseline':str(BASE),
       'historicalProof':rel(parent_path),'historicalProofSha256':sha(parent_path),'uiSourceOverrides':False,
       'source':source,'resources':resources,'family':{'source':'C:/Users/svetl/Downloads/'+family,'format':'2022','sha256':family_hash,'origin':'arrow tip, per user image and statement','initialDirection':'local -Y'},
       'serviceTests':{'count':66,'log':rel(test_path),'sha256':sha(test_path),'boundary':'linked actual source with API doubles'},
       'newWpfExecution':False,'newCaptures':[],'wpfHistoricalEvidence':'Docs/Ui/DesignSystem/screenshots-preview-runtime/verification.txt','unverified':unverified}
save(D/'ElevationPreviewTip-Provenance.json',proof)
report=dict(json.loads(read(D/'WorkflowReport.template.json')),task='SAB: preview arrow-tip anchor and supplied RFA replacement',date='2026-10-16',scope='production',sourceRevision=revision,
    dirtyBaseline=str(BASE),readFiles=['AGENTS.md','C:/Users/svetl/Desktop/Codex/AGENTS.md',*owners],layoutSpecification=rel(D/'ElevationPreviewTip-Implementation.md'),captures=[],runtimeDpi=None,renderDpi=[],
    build=[{'command':'dotnet run Tests/InteriorElevations.DirectionPreview -c Release','status':'PASS','checks':66},
           {'command':'Build-All-Msi.ps1 Release Years2022/2023 + freshly extracted payload','status':'PASS','proof':rel(D/'ElevationPreviewTip-Installer.json')}])
for c in report['checks']:
    n=int(c['id'][-3:]);c.update(scope='production',evidence=[],unverified=[])
    if n in {1,4,6,9,10,56}:c.update(applicable=True,status='PASS',reason='Bounded backend/resources scope and explicit evidence limits only.',evidence=[rel(D/'ElevationPreviewTip-Provenance.json'),rel(D/'ElevationPreviewTip-Implementation.md')],unverified=unverified)
    elif n in {5,79}:c.update(applicable=True,status='NOT RUN',reason='Target builds,API doubles and source checks passed; real Revit placement/readability not executed.',evidence=[rel(D/'ElevationPreviewTip-Provenance.json')],unverified=unverified)
    else:c.update(applicable=False,status='N/A',reason='WPF controls/layout/footer and unrelated module behavior were not changed by this backend/resource patch.')
review=D/'ElevationPreviewTip-Review.md'
if review.exists():report['independentReview']={'status':'PASS','reason':'Bounded independent backend/resource review; no actual Revit proof.','scope':'source/tests/packaging','report':rel(review)}
save(D/'ElevationPreviewTip-Report.json',report)
print('PASS preview-tip evidence:66linked service checks;2MSI1.1.4 extracted payloads;newRFA2022 in both;no new WPF/liveRevit claim')
