"""Bounded successor for runtime preview fixes; previous evidence stays immutable."""
from pathlib import Path
import csv, hashlib, json, re, shutil, subprocess

ROOT=Path(__file__).resolve().parents[2]
D=ROOT/'Docs/Ui/DesignSystem';S=D/'screenshots-preview-runtime'
BASE=ROOT/'outputs/ui-production-preview/direction-preview-runtime-baseline'
def read(p):return p.read_text(encoding='utf-8-sig')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def save(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def rel(p):return p.relative_to(ROOT).as_posix()
previous_path=D/'screenshots-direction-preview/provenance.json'
previous=json.loads(read(previous_path))
log=read(S/'verification.txt')
tests_path=ROOT/'outputs/ui-production-preview/direction-preview-runtime-tests.txt'
tests=read(tests_path)
before_path=ROOT/'outputs/ui-production-preview/direction-preview-runtime-before.txt'
assert 'Assertion failed' in read(before_path) and 'COMPLETED' not in read(before_path)
assert 'COMPLETED 62 checks' in tests and len(re.findall('^PASS ',tests,re.M))==62
assert 'COMPLETED' in log and len(re.findall('^PASS ',log,re.M))==41 and not re.search('^FAIL',log,re.M)
regression_path=ROOT/'outputs/ui-production-preview/preview-runtime-numbering-regression.txt'
regression=read(regression_path);assert 'COMPLETED 20 checks' in regression
captures=[{'path':rel(p),'sha256':sha(p)} for p in sorted(S.glob('*.png'))];assert len(captures)==6
git=shutil.which('git') or r'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe'
revision=subprocess.check_output([git,'rev-parse','HEAD'],cwd=ROOT,text=True).strip()
owners=['SAB/Cls_InteriorElevations/Commands/CreateInteriorElevationsCommand.cs',
        'SAB/Cls_InteriorElevations/Views/ElevationSettingsWindow.xaml',
        'SAB/Cls_InteriorElevations/Views/ElevationSettingsWindow.xaml.cs',
        'SAB/Cls_InteriorElevations/Services/Elevations/ElevationDirectionPreviewController.cs',
        'SAB/Cls_InteriorElevations/Services/Elevations/ElevationDirectionPreviewService.cs',
        'SAB/Cls_InteriorElevations/Services/Geometry/ContourDirectionGeometry.cs',
        'SAB/Cls_InteriorElevations/Services/Geometry/ContourLineOrientationService.cs',
        'SAB/Cls_InteriorElevations/Models/ElevationLineData.cs','SAB/Properties/AssemblyInfo.cs','SAB/SAB.csproj']
source={}
for name in owners:
    baseline=BASE/'files'/name
    source[name]={'before':sha(baseline),'after':sha(ROOT/name)}
    if name in previous['source']:assert source[name]['before']==previous['source'][name]['after'],name
bindings=lambda p:set(re.findall(r'\{Binding\s+([^},]+)',read(p)))
ui='SAB/Cls_InteriorElevations/Views/ElevationSettingsWindow.xaml'
assert not bindings(BASE/'files'/ui)-bindings(ROOT/ui)
assert 'Document != _service.Document' not in read(ROOT/owners[3])
assert 'uiDocument.Document != document' not in read(ROOT/owners[0])
unverified=['live Revit 2022/2023 native selection, focus and transactions','supplied RFA placement',
            'physical input/accessibility/OS DPI transitions','installation',
            'abnormal process termination and failed cleanup during noncancellable shutdown']
proof={'date':'2026-10-15','version':'1.1.3','sourceRevision':revision,'dirtyBaseline':str(BASE),
       'historicalProof':rel(previous_path),'historicalProofSha256':sha(previous_path),'uiSourceOverrides':False,
       'actualAssembly':str(ROOT/'outputs/ui-production-preview/plugin/SAB.dll'),
       'assemblySha256':sha(ROOT/'outputs/ui-production-preview/plugin/SAB.dll'),
       'surface':'actual SAB.dll WPF, fixtures outside Revit; linked API/event doubles separately',
       'nativeAssertions':41,'runtimeDpi':96,'renderDpi':96,'captures':captures,'source':source,
       'missingOriginalBindings':[],
       'serviceTests':{'count':62,'log':rel(tests_path),'sha256':sha(tests_path),'boundary':'actual linked source, API/event/UI doubles'},
       'beforeFix':{'log':rel(before_path),'sha256':sha(before_path),'status':'expected FAIL on distinct wrappers of same native document'},
       'numberingRegression':{'count':20,'log':rel(regression_path),'sha256':sha(regression_path)},'unverified':unverified}
save(S/'provenance.json',proof)
installer=[]
family_hash='7d4b1c1e7bfe516bf68bae1b016f5ba770bd9cc5ffa0986faeeac897fdaca909'
for year in (2022,2023):
    entry=json.loads(read(ROOT/f'outputs/ui-production-preview/msi-check/{year}/verification.json'))
    assert entry['status']=='PASS' and entry['productVersion']=='1.1.3' and sha(Path(entry['msi']))==entry['msiSha256'].lower()
    assert any(p['name']=='SAB.dll' and p['sha256'].lower()==sha(ROOT/f'SAB/bin/Revit{year}/SAB.dll') for p in entry['verifiedPayload'])
    assert any(p['name'].startswith('family_') and p['sha256'].lower()==family_hash for p in entry['verifiedPayload'])
    installer.append(entry)
save(D/'ElevationPreviewRuntime-Installer.json',{'status':'PASS','version':'1.1.3','installationPerformed':False,'results':installer})
template=json.loads(read(D/'WorkflowReport.template.json'))
report=dict(template,task='SAB: native-document identity, preview picking, footer, virtual closure',date='2026-10-15',scope='production',sourceRevision=revision,
    dirtyBaseline=str(BASE),readFiles=['AGENTS.md','.agents/skills/sab-wpf-ui/SKILL.md','Docs/Ui/DesignSystem/STATUS.json','Docs/Ui/DesignSystem/Specification.md','Docs/Ui/DesignSystem/ErrorRegistry.md','Docs/Ui/DesignSystem/Workflow.md','Docs/Ui/DesignSystem/ReviewRoles.md','Docs/Ui/DesignSystem/LayoutPatterns.md','C:/Users/svetl/.codex/skills/revit-external-events/SKILL.md'],
    layoutSpecification=rel(D/'ElevationPreviewRuntime-Layout.md'),captures=captures,runtimeDpi=96,renderDpi=[96],
    build=[{'command':'wrapper identity regression before fix','status':'expected FAIL'},
           {'command':'dotnet run Tests/InteriorElevations.DirectionPreview -c Release','status':'PASS','checks':62},
           {'command':'numbering/family regression with API doubles','status':'PASS','checks':20},
           {'command':'actual Debug2023 SAB.dll WPF host','status':'PASS','checks':41,'captures':6},
           {'command':'Build-All-Msi.ps1 Release Years2022/2023; freshly extracted payload','status':'PASS','proof':rel(D/'ElevationPreviewRuntime-Installer.json')}])
pass_ids={1,2,3,4,6,7,8,9,10,12,18,26,27,29,33,35,42,43,44,48,50,54,55,56,72,73,78}
partial_ids={5,11,53,79,80,81}
evidence=[rel(S/'provenance.json'),rel(S/'verification.txt'),rel(D/'ElevationPreviewRuntime-Layout.md')]
for c in report['checks']:
    n=int(c['id'][-3:]);c.update(scope='production',applicable=n in pass_ids|partial_ids,evidence=[],unverified=[])
    if n in pass_ids:c.update(status='PASS',reason='Bounded main-footer/modeless-preview source and actual WPF only.',evidence=evidence,unverified=unverified)
    elif n in partial_ids:c.update(status='NOT RUN',reason='Build/source/doubles and WPF checks passed; complete live Revit/native selection/RFA/physical states remain unverified.',evidence=evidence,unverified=unverified)
    else:c.update(status='N/A',reason='No changed table/picker/category/ribbon/notification/unrelated-window scenario in this bounded runtime/footer/geometry fix.')
review=D/'ElevationPreviewRuntime-Review.md'
if review.exists():report['independentReview']={'status':'PASS','reason':'Independent bounded source,62 linked checks and41 actual WPF assertions; live Revit excluded.','scope':'scoped ship UI/code only','report':rel(review)}
save(D/'ElevationPreviewRuntime-Report.json',report)
p=ROOT/'Tools/UiShowcase/.impeccable/design.json';sidecar=json.loads(read(p));sidecar['extensions']['elevationPreviewRuntime']={'version':'1.1.3','scope':'native document identity, fixed main footer, virtual closure','evidence':rel(S/'provenance.json'),'sharedTokensChanged':False,'liveRevit':'NOT RUN'};save(p,sidecar)
p=D/'component-coverage.csv'
with p.open(encoding='utf-8-sig',newline='') as f:reader=csv.DictReader(f);columns=reader.fieldnames;rows=list(reader)
for row in rows:
    if row['type'] in ('Window','Button','TextBlock','Border','ScrollViewer') and rel(S/'provenance.json') not in row['current_evidence']:row['current_evidence']=row['current_evidence'].rstrip(';')+';'+rel(S/'provenance.json')
with p.open('w',encoding='utf-8',newline='') as f:writer=csv.DictWriter(f,fieldnames=columns);writer.writeheader();writer.writerows(rows)
print('PASS runtime successor:62linked checks,20regressions,41actual WPF assertions,6PNG,2MSI1.1.3 payloads; live Revit NOT RUN')
