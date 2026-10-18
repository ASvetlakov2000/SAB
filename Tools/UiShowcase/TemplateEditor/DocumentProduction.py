"""Record actual completed production-class verification. Never claim live Revit execution."""
from Prepare import *
from collections import Counter

DOC=ROOT/'Docs/Ui/DesignSystem';SHOTS=DOC/'screenshots-template-editor-production';PROD=OUT/'production'
log=(SHOTS/'verification.txt').read_text(encoding='utf-8-sig')
assert log.splitlines()[-1].startswith('COMPLETED ') and not any(x.startswith('FAIL') for x in log.splitlines())
baseline=OUT/'production-baseline'
owners=[p for p in MODULE.rglob('*') if p.is_file()]+[ROOT/'SAB/UI/FluentLight/Presentation.cs',ROOT/'SAB/UI/Styles/SABFluentLight.xaml',ROOT/'SAB/SAB.csproj',ROOT/'Installer/Verify-MsiPayload.ps1']
current={p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in owners}
contract={'scope':'actual production sources, UI runtime from SAB.dll; no Revit operation proof','baseline':'outputs/ui-production-preview/template-editor/production-baseline/baseline.json','currentOwners':current,'windows':{}}
legacy=json.loads((HERE/'source-contract.json').read_text(encoding='utf-8-sig'))
for path in (MODULE/'Views').glob('*.xaml'):
    before=contracts(baseline/path.relative_to(ROOT));after=contracts(path)
    allowed=legacy['windows'][path.name].get('authorizedRemovals',{})
    missing={key:[value for value in before[key] if value not in after[key] and value not in allowed.get(key,[]) and value!='TabBorder'] for key in before}
    assert not any(missing.values()),missing
    contract['windows'][path.name]={'missing':missing,'authorizedRemovals':allowed,'current':after}
for unchanged in ['Commands/EditViewTemplateGraphicsCommand.cs','Services/RevitLinkGraphicsReflectionService.cs','ViewModels/GraphicOverrideEditorViewModel.cs']:
    assert (MODULE/unchanged).read_bytes()==(baseline/'SAB/Cls_ViewTemplateGraphics'/unchanged).read_bytes(),unchanged
contract['unchangedOwners']=['command/transaction boundary','Revit link reflection service (unused UI path)','graphics editor VM']
parent=DOC/'RibbonModules-Provenance.json'
before=json.loads((baseline/'baseline.json').read_text(encoding='utf-8-sig'))
contract['migrationSuccessor']={'uiSourceOverrides':False,'historicalProof':parent.relative_to(ROOT).as_posix(),
 'historicalProofSha256':hashlib.sha256(parent.read_bytes()).hexdigest(),'baseline':baseline.relative_to(ROOT).as_posix(),
 'source':{name:{'before':before[name],'after':current[name]} for name in ['SAB/SAB.csproj','SAB/UI/FluentLight/Presentation.cs','SAB/UI/Styles/SABFluentLight.xaml']}}
(HERE/'production-source-contract.json').write_text(json.dumps(contract,ensure_ascii=False,indent=2),encoding='utf-8')
captures=[{'path':p.relative_to(ROOT).as_posix(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'method':'RenderTargetBitmap96 of live actual SAB.dll Window Content; live Popup.Child composited at measured offset','scope':'actual production WPF classes, detached synthetic DTO'} for p in sorted(SHOTS.glob('*.png'))]
builds=[]
for year,folder in [(2022,OUT/'production-2022'),(2023,PROD),(2024,OUT/'production-2024')]:
    dll=folder/'SAB.dll';assert dll.exists()
    builds.append({'revitVersion':year,'configuration':'Debug','status':'PASS','assembly':dll.relative_to(ROOT).as_posix(),'sha256':hashlib.sha256(dll.read_bytes()).hexdigest(),'scope':'compile; actual WPF capture only API2023 DLL'})
msi=[]
for year in [2022,2023,2024]:
    p=ROOT/f'outputs/ui-production-preview/msi-check/{year}/verification.json';proof=json.loads(p.read_text(encoding='utf-8-sig'))
    assert proof['status']=='PASS' and proof['installationPerformed'] is False
    for name in ['ViewTemplateGraphicsWindow.xaml','GraphicOverrideEditorWindow.xaml','AddFiltersWindow.xaml','SABFluentLight.xaml','SAB_HTML_Redaktor_shablonov.html','SAB.dll']:
        assert any(x['name']==name for x in proof['verifiedPayload']),name
    assert hashlib.sha256(Path(proof['msi']).read_bytes()).hexdigest().upper()==proof['msiSha256'].upper()
    msi.append({'year':year,'status':'PASS','evidence':p.relative_to(ROOT).as_posix(),'msi':str(Path(proof['msi'])),'sha256':proof['msiSha256'],'productVersion':proof['productVersion'],'installationPerformed':False,'verifiedPayload':proof['verifiedPayload']})
(DOC/'TemplateEditor-Production-Installer.json').write_text(json.dumps({'command':'Build-Msi.bat Release < nul','exitCode':0,'log':'outputs/ui-production-preview/template-editor/production-msi-build.log','targets':msi,'publicationPerformed':False,'installationPerformed':False},ensure_ascii=False,indent=2),encoding='utf-8')
ids=sorted(set(re.findall(r'<a id="sab-ui-(\d+)"',(DOC/'ErrorRegistry.md').read_text(encoding='utf-8-sig'))))
old=json.loads((DOC/'TemplateEditor-Report.json').read_text(encoding='utf-8-sig'));old_checks={c['id']:c for c in old['checks']}
prefix='Docs/Ui/DesignSystem/screenshots-template-editor-production/'
checks=[]
for ident in ids:
    c=dict(old_checks.get('SAB-UI-'+ident,{'id':'SAB-UI-'+ident,'criterion':'ErrorRegistry.md#sab-ui-'+ident,'applicable':True,'status':'PASS','reason':'','evidence':[],'unverified':[]}))
    c['scope']='production'
    if c['status']!='N/A':
        c['evidence']=[prefix+'verification.txt','Tools/UiShowcase/TemplateEditor/production-source-contract.json']
        c['unverified']=['Live Revit document/category combinations, model writes and transactions, physical input, OS DPI transitions, default browser rendering NOT RUN. Surface/transparency use legacy SAB policy, not granular capability queries.']
    if ident=='007':c.update(status='PASS',reason='Direct user approval and actual production SAB.dll classes; API22/23/24 builds and extracted MSI proof. Live Revit execution separately NOT RUN.')
    if ident=='065':c.update(applicable=False,status='N/A',reason='Production entry is the existing Revit command; isolated preview launcher is historical.',evidence=[],unverified=[])
    if ident=='043':c.update(status='NOT RUN',reason='Exact footer target exists in all three MSI packages; actual default-browser opening/rendering not executed.')
    if ident=='090':c['evidence']=[prefix+'verification.txt',prefix+'tall-bottom-spacing.png',prefix+'minimum-tab-0.png','Docs/Ui/DesignSystem/TemplateEditor-Production-Layout.md']
    checks.append(c)
report={'schemaVersion':1,'task':'Рабочий редактор шаблонов — утверждённый перенос и нижний зазор16','date':'2026-10-17','scope':'production','sourceRevision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
 'dirtyBaseline':'Existing preview/ribbon/decoration changes saved before migration; source and installer baselines in existing canonical output, no commit/reset.',
 'readFiles':['AGENTS.md','.agents/skills/sab-wpf-ui/SKILL.md']+['Docs/Ui/DesignSystem/'+name for name in ['STATUS.json','Specification.md','ErrorRegistry.md','Workflow.md','ReviewRoles.md','LayoutPatterns.md','component-coverage.csv']],
 'layoutSpecification':'Docs/Ui/DesignSystem/TemplateEditor-Production-Layout.md','build':builds,'runtimeSummary':log.splitlines()[-1],
 'runtimeDpi':96,'renderDpi':[96],'captures':captures,'checks':checks,'sourceContract':'Tools/UiShowcase/TemplateEditor/production-source-contract.json',
 'productionMigrationAuthorized':True,'approvalStatusSource':'Docs/Ui/DesignSystem/STATUS.json','installer':'Docs/Ui/DesignSystem/TemplateEditor-Production-Installer.json',
 'installationPerformed':False,'publicationPerformed':False,'commitPushPerformed':False,
 'independentReview':{'status':'NOT RUN','report':'Docs/Ui/DesignSystem/TemplateEditor-Production-Review.md','reason':'Fresh production review in progress.'},
 'limitations':['Actual UI classes use internal detached loader; public constructor requires Document and existing collectors remain. This does not execute Revit operations.','Common/cut/parent capabilities use API calls in production, compile/source verified. Actual runtime category/view probes NOT RUN.','Surface/transparency retain original SAB policy; annotation surface exclusion is legacy policy, not universal API proof. Analytical DetailLevel limited to documented model-root support.','Actual UI runtime uses API2023 DLL; API22/24 compile and MSI payload checked, live target Revit execution NOT RUN.','Physical input/OS DPI/default browser/installation/publication NOT RUN.'],
 'documentValidation':{'status':'NOT RUN','reason':'Recorded after actual validator execution.'}}
(DOC/'TemplateEditor-Production-Report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
provenance={'date':'2026-10-17','scope':'actual SAB.dll WPF classes with detached fixtures','assembly':str(PROD/'SAB.dll'),'sha256':hashlib.sha256((PROD/'SAB.dll').read_bytes()).hexdigest(),'hostSha256':hashlib.sha256((PROD/'SAB.TemplateEditor.ProductionCheck.exe').read_bytes()).hexdigest(),'currentOwners':current,'captures':captures,'summary':report['runtimeSummary'],'runtimeDpi':96,'renderDpi':96,'persistence':'Production default true; internal process-only flag false in capture host; user window geometry not read/written.','builds':builds}
(SHOTS/'provenance.json').write_text(json.dumps(provenance,ensure_ascii=False,indent=2),encoding='utf-8')
print(report['runtimeSummary']);print('Production report',len(checks),'IDs',dict(Counter(c['status'] for c in checks)),';',len(captures),'fresh PNG;3 MSI payloads verified.')
