import hashlib,json,re,struct,sys
from pathlib import Path
import xml.etree.ElementTree as E
R=Path(__file__).resolve().parents[3];D=R/'Docs/Ui/DesignSystem'
S=D/'screenshots-template-editor-footer';O=R/'outputs/ui-production-preview/template-editor/footer'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def rel(p):return p.relative_to(R).as_posix()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
reviewed='--reviewed' in sys.argv
log=(S/'verification.txt').read_text(encoding='utf-8-sig')
assert 'COMPLETED 103 PASS / 6 PNG' in log and not re.search(r'^FAIL',log,re.M)
source={}
for name in ['SAB/UI/Styles/SABFluentLight.xaml','SAB/Cls_ViewTemplateGraphics/Views/ViewTemplateGraphicsWindow.xaml']:
    before=R/'outputs/ui-production-preview/template-editor/footer-baseline'/name.replace('/','__')
    source[name]=dict(beforePath=rel(before),before=sha(before),after=sha(R/name))
version_before=R/'outputs/ui-production-preview/notification-settings/pre-release-source/SAB__Properties__AssemblyInfo.cs'
source['SAB/Properties/AssemblyInfo.cs']=dict(beforePath=rel(version_before),before=sha(version_before),after=sha(R/'SAB/Properties/AssemblyInfo.cs'))
parent=R/'Tools/UiShowcase/NotificationSettings/production-source-contract.json'
name='SAB/Cls_ViewTemplateGraphics/Views/ViewTemplateGraphicsWindow.xaml'
before=(R/source[name]['beforePath']).read_text(encoding='utf-8-sig');after=(R/name).read_text(encoding='utf-8-sig')
oldstyles=E.parse(R/source['SAB/UI/Styles/SABFluentLight.xaml']['beforePath']).getroot()
styles=E.parse(R/'SAB/UI/Styles/SABFluentLight.xaml').getroot()
xkey='{http://schemas.microsoft.com/winfx/2006/xaml}Key'
def resource(tree,key):return E.tostring(next(e for e in tree if e.get(xkey)==key),encoding='unicode').strip()
sourcechecks={
    'original_status_binding_preserved':re.findall(r'\{Binding[^}]*\}',before)==re.findall(r'\{Binding[^}]*\}',after),
    'original_names_preserved':set(re.findall(r'x:Name="([^"]+)"',before))<=set(re.findall(r'x:Name="([^"]+)"',after)),
    'button_tags_and_parameters_preserved':re.findall(r'(?:Tag|CommandParameter)="([^"]+)"',before)==re.findall(r'(?:Tag|CommandParameter)="([^"]+)"',after),
    'original_validation_style_unchanged':resource(oldstyles,'SabValidationPanelStyle')==resource(styles,'SabValidationPanelStyle'),
    'original_status_text_style_unchanged':resource(oldstyles,'SabStatusTextStyle')==resource(styles,'SabStatusTextStyle'),
    'large_notification_switch_unchanged':resource(oldstyles,'SabLargeSwitchTemplate')==resource(styles,'SabLargeSwitchTemplate'),
    'small_switch_unchanged':resource(oldstyles,'SabSwitchTemplate')==resource(styles,'SabSwitchTemplate'),
}
assert all(sourcechecks.values()),sourcechecks
write(D/'TemplateEditorFooter-SourceContract.json',dict(date='2026-10-18',scope='footer-only source successor',uiSourceOverrides=False,
    historicalProof=rel(parent),historicalProofSha256=sha(parent),source=source,checks=sourcechecks))
shots=[]
for p in sorted(S.glob('*.png')):
    width,height=struct.unpack('>II',p.read_bytes()[16:24])
    shots.append(dict(path=rel(p),sha256=sha(p),bytes=p.stat().st_size,widthPx=width,heightPx=height,renderDpi=96,
        surface='actual SAB.dll Window.Content' if p.name!='footer-detail.png' else 'actual attached footer VisualBrush at local origin',
        data='detached fixture DTO; outside Revit; no OS-DPI/physical verification'))
assert len(shots)==6
targets=[]
for year in ['2022','2023','2024']:
    p=R/f'outputs/ui-production-preview/msi-check/{year}/verification.json';v=read(p)
    assert v['status']=='PASS' and v['productVersion']=='1.1.4' and v['assemblyVersion']=='1.1.4.0'
    assert v['notificationDefaultEnabled'] is False and v['firstLaunchDefaultCheck']=='PASS' and v['notificationSettingsBaml']
    assert sha(Path(v['msi'])).upper()==v['msiSha256']
    upgrades=read(p.parent/'upgrade-rows.json')
    assert any(row['versionMax']=='1.1.4' and row['attributes'] & 512 for row in upgrades)
    targets.append(dict(year=year,status='PASS',productVersion=v['productVersion'],assemblyVersion=v['assemblyVersion'],
        msi=v['msi'],sha256=v['msiSha256'],apiReferences=v['apiReferences'],verifiedPayload=v['verifiedPayload'],
        notificationDefaultEnabled=False,firstLaunchDefaultCheck='PASS',notificationSettingsBaml=True,
        upgradeRows=upgrades,installationPerformed=False))
installer=dict(version='1.1.4',tag='v1.1.4-notifications',configuration='Release',buildCommand='Build-Msi.bat Release 1.1.4',buildExitCode=0,
    prepareOnlyExitCode=0,targets=targets,firstLaunchEnabled=False,automaticConfirmationsImplemented=False,installationPerformed=False,
    historicalTagPreserved=dict(tag='v1.1.4',commit='8b6979551c123892b3a790030139edb15495f966'),
    baseLatestRelease='v1.1.3',publicationStatus='pending',scope='current cumulative approved source; footer fix included; MSI extracted-payload checks only')
write(D/'NotificationSettings-Installer.json',installer)
template=read(D/'WorkflowReport.template.json')
passed={1,2,3,4,5,6,7,8,9,10,12,26,29,35,42,44,48,53,54,55,56,90,92}
checks=[]
for c in template['checks']:
    number=int(c['id'][-3:]);c['scope']='production'
    c['applicable']=number in passed or number in {11,43}
    c['status']='PASS' if number in passed else 'NOT RUN' if c['applicable'] else 'N/A'
    c['evidence']=[rel(S/'verification.txt'),rel(D/'TemplateEditorFooter-SourceContract.json')] if number in passed else []
    c['reason']='Footer-only refinement; this owner/scenario is unchanged or absent in the modified footer.'
    if number in passed:c['reason']='Current actual footer source/captures/103 bounds assertions within the declared footer-only scope.'
    if number==5:c['reason']='Current API22/23/24 Release builds and extracted MSI verified; API execution in Revit NOT RUN.'
    if number==9:c['reason']='Six current actual WPF captures; initial blank detail replaced using actual VisualBrush, fresh host run PASS.'
    if number==11:c['reason']='Static layout/error/long states tested; full physical input/focus/accessibility matrix not run.'
    if number==43:c['reason']='Instruction button and binding retained; browser launch not tested.'
    if number==90:c['reason']='Both card bottoms align; measured card-to-status gap16 at all viewports, long/error states and7tabs.'
    if number==92:c['reason']='Actual text layout and glyph ink clear all frame edges; min44/padding12,10/wrap/autogrow and12DIP footer gap verified.'
    c['unverified']=['Live Revit, physical input, OS DPI/monitor transitions and installation NOT RUN'] if c['applicable'] else []
    checks.append(c)
assert len(checks)==92
files=[R/'SAB/UI/Styles/SABFluentLight.xaml',R/name]+[
    R/'Tools/UiShowcase/TemplateEditor'/f for f in ['FooterCheck.cs','FooterCheck.csproj','PrepareFooterCheck.py','BuildFooterCheck.ps1']
]+[O/'plugin/SAB.dll',O/'plugin/SAB.TemplateEditor.FooterCheck.exe',S/'verification.txt']
provenance=dict(date='2026-10-18',scope='actual SAB.dll footer outside Revit',source={rel(p):sha(p) for p in files},
    captures=shots,assertions=103,pngCount=6,sourceChecks=sourcechecks,renderDpi=96,runtimeOsDpi=None,
    userReference=dict(path=rel(S/'reference-feedback/status-border.png'),sha256=sha(S/'reference-feedback/status-border.png')),
    installationPerformed=False,independentReview='ship_ui_code_payload' if reviewed else 'pending')
write(S/'provenance.json',provenance)
report=dict(schemaVersion=1,task='Редактор шаблонов — внутренние отступы нижнего статуса',date='2026-10-18',
    scope='production footer-only source and actual WPF outside Revit',sourceRevision='0d5b17db7f418fa9d837a553cdd1feac0e2cbfc5',
    dirtyBaseline='Pre-existing cumulative approved UI changes retained; current footer before-files saved separately under outputs/ui-production-preview/template-editor/footer-baseline.',
    readFiles=['AGENTS.md','.agents/skills/sab-wpf-ui/SKILL.md']+[rel(D/p) for p in ['STATUS.json','Specification.md','ErrorRegistry.md','Workflow.md','ReviewRoles.md']],
    layoutSpecification=rel(D/'TemplateEditorFooter-Layout.md'),
    build=[dict(command='BuildFooterCheck.ps1 -Capture',status='PASS',exitCode=0,assertions=103,pngCount=6,evidence=rel(S/'verification.txt')),
        dict(command='Build-Msi.bat Release 1.1.4',status='PASS',exitCode=0,evidence=rel(D/'NotificationSettings-Installer.json'))],
    captures=shots,runtimeDpi=None,renderDpi=[96],checks=checks,approvalStatusSource=rel(D/'STATUS.json'),
    independentReview=dict(status='PASS' if reviewed else 'NOT RUN',scope='footer source/current native captures and latest extracted MSI only',
        report=rel(D/'TemplateEditorFooter-Review.md') if reviewed else '',reason='One rejected detail capture corrected and verified' if reviewed else 'Awaiting final handoff'),
    installationPerformed=False,publicationPerformed=False,
    unverified=['Live Revit operations','Physical input/accessibility traversal','OS DPI/monitor transitions','MSI installation'])
write(D/'TemplateEditorFooter-Report.json',report)
print('Footer evidence:103 PASS/6 PNG/7 source checks/92 registry IDs; three fresh MSI1.1.4 verified')
