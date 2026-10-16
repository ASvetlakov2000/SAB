"""Fresh bounded direction-preview evidence; historical proofs are read-only."""
from pathlib import Path
import csv, hashlib, json, re, shutil, subprocess
from SourceHash import matches_bytes

ROOT=Path(__file__).resolve().parents[2]
D=ROOT/'Docs/Ui/DesignSystem'
S=D/'screenshots-direction-preview'
BASE=ROOT/'outputs/ui-production-preview/direction-preview-baseline'
def read(p): return p.read_text(encoding='utf-8-sig')
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def save(p,v): p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def rel(p): return p.relative_to(ROOT).as_posix()

template=json.loads(read(D/'WorkflowReport.template.json'))
if not any(c['id']=='SAB-UI-079' for c in template['checks']):
    template['checks'].append(dict(template['checks'][0],id='SAB-UI-079',criterion='ErrorRegistry.md#sab-ui-079'))
    save(D/'WorkflowReport.template.json',template)

git=shutil.which('git') or r'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe'
revision=subprocess.check_output([git,'rev-parse','HEAD'],cwd=ROOT,text=True).strip()
log=read(S/'verification.txt')
tests=read(ROOT/'outputs/ui-production-preview/direction-preview-tests.txt')
regression=read(ROOT/'outputs/ui-production-preview/direction-preview-numbering-regression.txt')
for data in (log,tests,regression): assert 'COMPLETED' in data and not re.search(r'^FAIL',data,re.M)
assert len(re.findall(r'^PASS ',tests,re.M))==46
assert len(re.findall(r'^PASS ',regression,re.M))==20
captures=[{'path':rel(p),'sha256':sha(p)} for p in sorted(S.glob('*.png'))]
assert len(captures)==4
family='SAB_Развертки_Направление.rfa'
family_hash='7d4b1c1e7bfe516bf68bae1b016f5ba770bd9cc5ffa0986faeeac897fdaca909'
for folder in ('2022','2023-2024'): assert sha(ROOT/f'SAB/Families for Plugin/CreateInteriorElevationsCommand/{folder}/{family}')==family_hash

owners=['SAB/SAB.csproj','SAB/Cls_InteriorElevations/Commands/CreateInteriorElevationsCommand.cs',
        'SAB/Cls_InteriorElevations/Views/ElevationSettingsWindow.xaml',
        'SAB/Cls_InteriorElevations/Views/ElevationSettingsWindow.xaml.cs',
        'SAB/Cls_InteriorElevations/Views/ElevationDirectionPreviewWindow.cs',
        'SAB/Cls_InteriorElevations/Models/ElevationLineData.cs','SAB/Cls_InteriorElevations/Models/ElevationViewData.cs',
        'SAB/Cls_InteriorElevations/Services/Geometry/ContourDirectionGeometry.cs',
        'SAB/Cls_InteriorElevations/Services/Geometry/ContourLineOrientationService.cs',
        'SAB/Cls_InteriorElevations/Services/Sheets/ViewportPlacementService.cs',
        'SAB/Services/PluginResources/PluginFamilyResourceService.cs']
owners += ['SAB/Cls_InteriorElevations/Services/Elevations/'+n+'.cs' for n in
           ('ElevationDirectionPreviewController','ElevationDirectionPreviewService','ElevationMarkerService','ElevationViewCreationService','ElevationCropService')]
historical=json.loads(read(ROOT/'Tools/UiShowcase/notification-source-contract.json'))
numbering=json.loads(read(D/'screenshots-elevation-numbering/provenance.json'))
source={name:{'before':sha(BASE/'files'/name) if (BASE/'files'/name).exists() else None,'after':sha(ROOT/name)} for name in owners}
for name,expected in historical['files'].items():
    if name not in source: continue
    previous=numbering.get('source',{}).get(name,{})
    if previous:
        assert previous['before']==expected
        expected=previous['after']
    assert matches_bytes(ROOT/name,(BASE/'files'/name).read_bytes(),expected),name
    source[name]['beforeProofHash']=expected
ui='SAB/Cls_InteriorElevations/Views/ElevationSettingsWindow.xaml'
bindings=lambda p:set(re.findall(r'\{Binding\s+([^},]+)',read(p)))
missing=sorted(bindings(BASE/'files'/ui)-bindings(ROOT/ui));assert not missing
unverified=['live Revit 2022/2023 geometry, transactions and supplied RFA placement',
            'physical input/accessibility and OS DPI transitions','installation',
            'abnormal process termination or failed cleanup in a noncancellable shutdown callback']
proof={'date':'2026-10-15','sourceRevision':revision,'dirtyBaseline':str(BASE),
       'historicalProof':'Docs/Ui/DesignSystem/screenshots-elevation-numbering/provenance.json',
       'uiSourceOverrides':False,'actualAssembly':str(ROOT/'outputs/ui-production-preview/plugin/SAB.dll'),
       'assemblySha256':sha(ROOT/'outputs/ui-production-preview/plugin/SAB.dll'),
       'surface':'actual SAB.dll WPF; null Document settings fixture; no Revit execution',
       'nativeAssertions':len(re.findall(r'^PASS ',log,re.M)),'runtimeDpi':96,'renderDpi':96,
       'captures':captures,'source':source,'missingOriginalBindings':missing,
       'serviceTests':{'count':46,'log':'outputs/ui-production-preview/direction-preview-tests.txt','boundary':'actual linked production code with API/event/UI doubles'},
       'regressionTests':{'count':20,'log':'outputs/ui-production-preview/direction-preview-numbering-regression.txt'},
       'family':{'name':family,'format':'2022','sha256':family_hash,'livePlacement':'NOT RUN'},'unverified':unverified}
save(S/'provenance.json',proof)

sidecar_path=ROOT/'Tools/UiShowcase/.impeccable/design.json';sidecar=json.loads(read(sidecar_path))
sidecar['extensions']['elevationDirectionPreview']={'scope':'Current Revit plan with temporary annotation instances and modeless Compact Dialog','sharedTokensChanged':False,'evidence':rel(S/'provenance.json'),'liveRevit':'NOT RUN'}
save(sidecar_path,sidecar)
coverage_path=D/'component-coverage.csv'
with coverage_path.open(encoding='utf-8-sig',newline='') as f: reader=csv.DictReader(f);columns=reader.fieldnames;rows=list(reader)
for row in rows:
    if row['type'] in ('Window','Button','TextBlock','Border','ScrollViewer') and rel(S/'provenance.json') not in row['current_evidence']:
        row['current_evidence']=row['current_evidence'].rstrip(';')+';'+rel(S/'provenance.json')
with coverage_path.open('w',encoding='utf-8',newline='') as f: writer=csv.DictWriter(f,fieldnames=columns);writer.writeheader();writer.writerows(rows)

installer=[]
for year in (2022,2023):
    entry=json.loads(read(ROOT/f'outputs/ui-production-preview/msi-check/{year}/verification.json'))
    assert entry['status']=='PASS' and sha(Path(entry['msi']))==entry['msiSha256'].lower()
    assert any(p['name']=='SAB.dll' and p['sha256'].lower()==sha(ROOT/f'SAB/bin/Revit{year}/SAB.dll') for p in entry['verifiedPayload'])
    assert any(p['name'].startswith('family_') and p['sha256'].lower()==family_hash for p in entry['verifiedPayload'])
    installer.append(entry)
save(D/'ElevationDirectionPreview-Installer.json',{'status':'PASS','installationPerformed':False,'results':installer})
report=dict(template,task='SAB: line-based elevation direction preview',date='2026-10-15',scope='production',sourceRevision=revision,
            dirtyBaseline=str(BASE),readFiles=['AGENTS.md','.agents/skills/sab-wpf-ui/SKILL.md','Docs/Ui/DesignSystem/STATUS.json','Docs/Ui/DesignSystem/Specification.md','Docs/Ui/DesignSystem/ErrorRegistry.md','Docs/Ui/DesignSystem/Workflow.md','Docs/Ui/DesignSystem/ReviewRoles.md','Docs/Ui/DesignSystem/LayoutPatterns.md','C:/Users/svetl/.codex/skills/revit-external-events/SKILL.md'],
            layoutSpecification=rel(D/'ElevationDirectionPreview-Layout.md'),captures=captures,runtimeDpi=96,renderDpi=[96],
            build=[{'command':'Debug2023 SAB.dll + actual WPF host','status':'PASS'},
                   {'command':'actual linked direction services, Revit API/event/UI doubles','status':'PASS','checks':46},
                   {'command':'numbering/family regression, Revit API doubles','status':'PASS','checks':20},
                   {'command':'Installer/Build-All-Msi.ps1 -Configuration Release -Years 2022,2023','status':'PASS','proof':rel(D/'ElevationDirectionPreview-Installer.json')}])
pass_ids={1,2,3,4,6,7,8,9,10,12,18,26,27,29,33,35,42,43,44,48,50,54,55,56,72,73,78}
partial_ids={5,11,53,79}
evidence=[rel(S/'provenance.json'),rel(S/'verification.txt'),rel(D/'ElevationDirectionPreview-Layout.md')]
for c in report['checks']:
    n=int(c['id'][-3:]);c.update(scope='production',applicable=n in pass_ids|partial_ids,evidence=[],unverified=[])
    if n in pass_ids: c.update(status='PASS',reason='Bounded preview window/entry change only; source and actual WPF evidence.',evidence=evidence,unverified=unverified)
    elif n in partial_ids: c.update(status='NOT RUN',reason='Applicable production requirement is only partially verified outside Revit; live API/RFA and full physical input state matrix remain open.',evidence=evidence,unverified=unverified)
    else: c.update(status='N/A',reason='No changed table/picker/category/ribbon/notification or unrelated window scenario in this bounded preview implementation.')
review=D/'ElevationDirectionPreview-Review.md'
if review.exists(): report['independentReview']={'status':'PASS','reason':'Bounded independent review; no live Revit acceptance claim.','scope':'source, actual WPF evidence and test-double lifecycle','report':rel(review)}
save(D/'ElevationDirectionPreview-Report.json',report)
print('PASS fresh direction evidence:',proof['nativeAssertions'],'WPF assertions,4PNG,46linked checks,20regressions,2extracted MSI payloads; live Revit NOT RUN')
