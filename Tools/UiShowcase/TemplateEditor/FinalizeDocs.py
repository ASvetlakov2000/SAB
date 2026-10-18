"""Document the completed bounded preview. No UI execution or production mutation."""
from Prepare import *
import csv, io

DOC=ROOT/'Docs/Ui/DesignSystem'
report_path=DOC/'TemplateEditor-Report.json'
report=json.loads(report_path.read_text(encoding='utf-8-sig'))
assert report['runtimeSummary'].startswith('COMPLETED ')
assert 'правки2' in report['task']
assert 'Правки2' in (DOC/'TemplateEditor-Review.md').read_text(encoding='utf-8-sig'), 'Needs fresh independent revision2 review'
report['independentReview']={'status':'PASS','reason':'Independent read-only reviewer: revision2 F-R2-1/F-R2-2 resolved; sidebar20percent and responsive tabs reviewed; ship in preview scope only.',
 'scope':'preview','report':'Docs/Ui/DesignSystem/TemplateEditor-Review.md','reviewer':'/root/template_editor_reviewer',
 'disposition':'ship_preview_scope','hostSha256':hashlib.sha256((OUT/'SAB.TemplateEditor.Preview.exe').read_bytes()).hexdigest(),
 'unverified':['SAB-UI-043 actual browser opening/rendering','Production migration/Revit/physical input/OS DPI/install/MSI']}
report['concurrentChangesOutsideTask']=['SAB/Cls_MainPanel/MainPanel.cs','SAB/Cls_ParameterTools/ParameterToolsIcons.cs','SAB/Helpers/Ribbon/Ribbon.cs','Tools/Ribbon','SAB/SAB.csproj (decoration SVG resource only)','SAB/Cls_InteriorElevations/Views/ElevationDecorationWindow.xaml','Docs/Ui/DesignSystem/RibbonModules-Layout.md','Docs/Ui/DesignSystem/screenshots-ribbon-modules']
report['documentValidation']={
 'taskPacket':{'status':'PASS','command':'python Tools/UiShowcase/TemplateEditor/Verify.py','scope':'current registry IDs, evidence files/hashes, original module hashes, unchanged production owners, reviewed host, launch and migration gate'},
 'globalCanonicalValidator':{'status':'NOT RUN','command':'python Tools/UiShowcase/ValidateUiDocs.py --report Docs/Ui/DesignSystem/TemplateEditor-Report.json','reason':'Updated after actual validator execution for revision2.'}}
report_path.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')

provenance_path=DOC/'screenshots-template-editor/revision-2/provenance.json'
provenance=json.loads(provenance_path.read_text(encoding='utf-8-sig'))
provenance['sharedAssemblySha256']=hashlib.sha256((OUT/'SAB.dll').read_bytes()).hexdigest()
provenance['canonicalDictionary']={'path':'SAB/UI/Styles/SABFluentLight.xaml','sha256':hashlib.sha256((ROOT/'SAB/UI/Styles/SABFluentLight.xaml').read_bytes()).hexdigest()}
provenance['independentReview']='Docs/Ui/DesignSystem/TemplateEditor-Review.md'
provenance_path.write_text(json.dumps(provenance,ensure_ascii=False,indent=2),encoding='utf-8')

coverage=DOC/'component-coverage.csv'
text=coverage.read_text(encoding='utf-8-sig')
lines=text.splitlines()
fields=['template_editor_preview_owner','template_editor_preview_evidence','template_editor_preview_scope','template_editor_preview_unverified']
if fields[0] not in next(csv.reader([lines[0]])):
    used={'Border','Button','CheckBox','ComboBox','ComboBoxItem','ContentPresenter','DataGrid','DataGridTemplateColumn','DataGridTextColumn','Grid','ItemsPresenter','ListBox','Popup','ScrollBar','ScrollContentPresenter','ScrollViewer','Slider','StackPanel','TabControl','TabItem','TextBlock','TextBox','Thumb','ToggleButton','ToolTip','Track','Window','WrapPanel'}
    result=[lines[0]+','+','.join(fields)]
    for line in lines[1:]:
        typ=next(csv.reader([line]))[0]
        values=['Tools/UiShowcase/TemplateEditor/TemplateEditor.csproj;SAB/UI/Styles/SABFluentLight.xaml',
            'Docs/Ui/DesignSystem/TemplateEditor-Report.json;Docs/Ui/DesignSystem/screenshots-template-editor/revision-2/provenance.json',
            'isolated source-derived preview;'+report['runtimeSummary']+'; original production untouched',
            'Only listed states exercised; physical keyboard/OS DPI/Revit/browser render NOT RUN; no all-states PASS'] if typ in used else ['']*4
        buffer=io.StringIO();csv.writer(buffer,lineterminator='').writerow(values);result.append(line+','+buffer.getvalue())
    coverage.write_text('\n'.join(result)+'\n',encoding='utf-8-sig')
else:
    records=list(csv.DictReader(io.StringIO(text)));fieldnames=list(records[0])
    for record in records:
        if record.get(fields[0]):
            record[fields[0]]='Tools/UiShowcase/TemplateEditor/TemplateEditor.csproj;Tools/UiShowcase/TemplateEditor/Presentation.cs;SAB/UI/Styles/SABFluentLight.xaml'
            record[fields[1]]='Docs/Ui/DesignSystem/TemplateEditor-Report.json;Docs/Ui/DesignSystem/screenshots-template-editor/revision-2/provenance.json'
            record[fields[2]]='revision2 isolated source-derived preview;'+report['runtimeSummary']+'; original production untouched'
            record[fields[3]]='Synthetic category capability metadata only; real category/view probes and physical/OS DPI/Revit/browser render NOT RUN'
    buffer=io.StringIO();writer=csv.DictWriter(buffer,fieldnames=fieldnames,lineterminator='\n');writer.writeheader();writer.writerows(records)
    coverage.write_text(buffer.getvalue(),encoding='utf-8-sig')
print('Current preview registered; global51-type coverage statuses and other approvals retained.')
