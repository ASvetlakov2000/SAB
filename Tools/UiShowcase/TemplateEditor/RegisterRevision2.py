from Prepare import *

doc=ROOT/'Docs/Ui/DesignSystem'
template_path=doc/'WorkflowReport.template.json'
template=json.loads(template_path.read_text(encoding='utf-8-sig'))
for ident in ['086','087','089']:
    if not any(c['id']=='SAB-UI-'+ident for c in template['checks']):
        template['checks'].append({'id':'SAB-UI-'+ident,'criterion':'ErrorRegistry.md#sab-ui-'+ident,'applicable':None,'status':'NOT RUN','scope':'','reason':'Форма ещё не заполнена','evidence':[],'unverified':[]})
template_path.write_text(json.dumps(template,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
status_path=doc/'STATUS.json'
status=json.loads(status_path.read_text(encoding='utf-8-sig'))
status['templateEditorPreview'].update(revision=2,stage='pending_user_approval',productionMigrationAuthorized=False,
 layout='Docs/Ui/DesignSystem/TemplateEditor-Revision2-Layout.md',tabs=7,
 feedback='User17.10: vertical template sidebar; remove per-link settings unless template visibility API exists; capability-aware categories; readable parent/subcategory tree.',
 evidence='Docs/Ui/DesignSystem/screenshots-template-editor/revision-2/provenance.json')
status_path.write_text(json.dumps(status,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Revision2 registered; unrelated approvals retained; migration false.')
