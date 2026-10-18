from Prepare import *
import csv
from collections import Counter

DOC = ROOT/'Docs/Ui/DesignSystem'
SHOTS = DOC/'screenshots-template-editor/revision-2'
ids = sorted(set(re.findall(r'<a id="sab-ui-(\d+)"', (DOC/'ErrorRegistry.md').read_text(encoding='utf-8-sig'))))
evidence = 'Docs/Ui/DesignSystem/screenshots-template-editor/revision-2/'
source = 'Tools/UiShowcase/TemplateEditor/source-contract.json'
read_files = ['AGENTS.md','.agents/skills/sab-wpf-ui/SKILL.md'] + ['Docs/Ui/DesignSystem/'+x for x in ['STATUS.json','Specification.md','ErrorRegistry.md','Workflow.md','ReviewRoles.md','LayoutPatterns.md','component-coverage.csv']]
na = {
 14:'Нет строковых Add/Delete icon-actions: добавление/удаление фильтров — исходные текстовые toolbar actions; disclosure дерева отдельно057.',
 17:'Нет отдельных numeric columns с numeric headers. Масштаб — свойство в общей текстовой колонке Значение.',
 21:'Нет RadioButton mode-групп; вкладки проверяются023–025.',22:'Нет скрываемых radio modes.',
 30:'Нет bound ItemsControl с исчезающей оболочкой: пустые DataGrid/ListBox сохраняют контур.',
 32:'Нет вложенных строк этажей/зон.',34:'Нет вложенных форм этажей/зон с icon-actions.',
 37:'Нет исходных Save/Load/Все–Выбранные групп категорий; добавлять функции запрещено.',
 38:'Нет импорта/экспорта конфигурации этого модуля.',
 39:'Нет row plus или duplicate add routes; исходное добавление фильтров только через toolbar modal picker.',
 40:'Нет CategorySelectionControl/режима Все–Выбранные. Extended highlight и IsTarget проверены отдельно053.',
 41:'Нет JSON Save/Load выборок в этом модуле.',49:'Нет DockablePane.',51:'Нет исходных восьми иконок этажей/зон.',52:'Нет ClearRowsCommand и blank creation row.',
 59:'Нет transient notifications в оформляемых трёх окнах; исходный modal command result не мигрируется.',
 60:'Нет notification copy/ID/support blocks.',61:'Нет notification lifetime.',62:'Нет notification stack.',63:'Нет notification work-area placement.',64:'Нет изменений result adapter/transaction/notification callbacks.',
 66:'Нет настройки выносок развёрток.',68:'Нет compact choice helper окон развёрток.',69:'Нет категорий отметок развёрток.',70:'Нет результата создания развёрток.',
 73:'Нет выбора помещений/направлений развёрток.',74:'Нет ParameterPicker/Sources tab.',75:'Нет проверки параметров категорий.',76:'Нет WhatCheck/CheckCategoriesWindow.',77:'Нет ribbon/hotkey проверки параметров.',
 78:'Нет preview направлений развёрток.',79:'Нет preview направлений развёрток.',80:'Нет preview направлений развёрток.',81:'Нет preview направлений развёрток.',82:'Нет preview направлений развёрток.',83:'Нет выбора направлений развёрток.',84:'Нет выбора материалов/синхронизации штриховок.',88:'Параллельная задача оформления развёрток; этот модуль отсутствует в редакторе шаблонов.',85:'Параллельное изменение группировки и палитры ribbon; в окнах редактора шаблонов ленты нет.'}
proof = {
1:[source,'Docs/Ui/DesignSystem/TemplateEditor-Layout.md'],2:[source,evidence+'tab-1.png'],3:[source,evidence+'verification.txt'],
4:[source],5:[source,'Tools/UiShowcase/TemplateEditor/TemplateEditor.csproj'],6:['Docs/Ui/DesignSystem/TemplateEditor-Layout.md','Docs/Ui/DesignSystem/STATUS.json'],
8:['Docs/Ui/DesignSystem/TemplateEditor-Layout.md'],9:[evidence+'provenance.json'],10:[evidence+'provenance.json'],
11:[evidence+'verification.txt',evidence+'input-verification.json'],12:[evidence+'tab-1.png',evidence+'editor-SurfacePatterns.png'],
13:[evidence+'verification.txt',evidence+'tab-1.png'],15:[evidence+'tab-0.png',evidence+'property-editor-focus.png'],16:[evidence+'verification.txt'],19:[evidence+'verification.txt'],25:[evidence+'verification.txt'],
18:[evidence+'minimum-tab-5.png',evidence+'minimum-editor-SurfacePatterns.png'],
20:[evidence+'mixed-selection.png',evidence+'verification.txt'],23:[evidence+'tab-1.png',evidence+'minimum-tab-6.png'],24:[evidence+'tab-1.png'],
26:[evidence+'tab-1.png',evidence+'editor-SurfacePatterns.png'],27:['Docs/Ui/DesignSystem/TemplateEditor-Layout.md'],
28:[evidence+'tab-0.png',evidence+'minimum-tab-5.png'],29:[evidence+'minimum-editor-SurfacePatterns.png',evidence+'minimum-tab-5.png'],
31:[evidence+'tab-1.png',evidence+'zero-targets.png'],33:[evidence+'editor-ProjectionLines.png',evidence+'editor-SurfacePatterns.png'],
35:[evidence+'verification.txt',evidence+'editor-ProjectionLines.png'],36:[evidence+'template-search.png',evidence+'tab-5.png'],
42:[evidence+'tab-1.png',evidence+'editor-SurfacePatterns.png',evidence+'add-filters.png'],
43:['Docs/PluginInstructions/SAB_HTML_Redaktor_shablonov.html',evidence+'verification.txt'],
44:[evidence+'dirty-validation.png',evidence+'tab-6.png',source],45:[evidence+'verification.txt',evidence+'minimum-tab-5.png'],46:[evidence+'minimum-tab-5.png'],
47:[evidence+'editor-popup.png',evidence+'input-verification.json'],48:['Tools/UiShowcase/TemplateEditor/TemplateEditor.csproj','Docs/Ui/DesignSystem/component-coverage.csv'],
50:[evidence+'minimum-tab-5.png',evidence+'minimum-editor-SurfacePatterns.png'],53:[source,evidence+'verification.txt',evidence+'input-verification.json'],
54:[source,'Tools/UiShowcase/TemplateEditor/Preview.cs'],55:[evidence+'dirty-validation.png',evidence+'add-filters-validation.png'],56:['Docs/Ui/DesignSystem/TemplateEditor-Report.json','Docs/Ui/DesignSystem/TemplateEditor-Review.md'],
57:[evidence+'category-expanded.png',evidence+'verification.txt'],58:[evidence+'minimum-tab-5.png',evidence+'minimum-tab-5-right.png',evidence+'long-template-name.png',evidence+'editor-popup.png'],
65:[evidence+'launch-verification.json','Tools/UiShowcase/TemplateEditor/Launch.ps1'],67:[evidence+'verification.txt',evidence+'stretch.png'],
71:[evidence+'verification.txt'],72:[evidence+'dirty-validation.png',evidence+'add-filters-validation.png'],86:[evidence+'annotation-capabilities.png',evidence+'analytical-capabilities.png',evidence+'verification.txt','Docs/Ui/DesignSystem/TemplateEditor-Revision2-Layout.md'],89:[evidence+'minimum-tab-0.png',evidence+'minimum-tab-4.png',evidence+'verification.txt'],87:[evidence+'tree-depth-stress.png',evidence+'tree-search-path.png',evidence+'verification.txt']}
not_run = {7:'Production migration intentionally deferred until this suite is explicitly approved; source-derived preview does not prove actual production classes.',
43:'One footer help per window and exact existing local HTML target verified by source/counts. Actual default-browser opening/rendering not exercised; no Computer Use per user request.'}

checks=[]
for number in ids:
    n=int(number); item={'id':'SAB-UI-'+number,'criterion':'ErrorRegistry.md#sab-ui-'+number,'applicable':n not in na,'scope':'preview',
        'status':'N/A' if n in na else ('NOT RUN' if n in not_run else 'PASS'),
        'reason':na.get(n,not_run.get(n,'')), 'evidence':proof.get(n,[]) if n not in na else [],
        'unverified':[] if n in na else ['Production classes/Revit API operations/transactions, installation, OS DPI transitions and exhaustive accessibility are not verified.']}
    if n not in na and n not in not_run and n not in proof: item.update(status='NOT RUN',reason='No dedicated current evidence for this registry criterion.')
    checks.append(item)
captures=[]
for path in sorted(SHOTS.glob('*.png')):
    captures.append({'path':str(path.relative_to(ROOT)).replace('\\','/'),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'scope':'source-derived preview',
        'method':'Windows.Graphics.Capture via computer-use' if path.name.startswith('native-') else 'RenderTargetBitmap96 of live shown WPF Content, not OS screenshot'})
log=(SHOTS/'verification.txt').read_text(encoding='utf-8-sig')
assert 'COMPLETED' in log and not any(x.startswith('FAIL') for x in log.splitlines()), 'Incomplete/failed current run'
runtime_dpi=float(re.search(r'Runtime DPI ([\d.]+)',log).group(1))
input_proof={'scope':'WPF routed input/Keyboard.Focus/AccessKeyManager, not physical OS keyboard',
 'userDirection':'Continue without Computer Use; feedback2 explicitly changes layout, link entity and category/tree presentation',
 'checks':[line for line in log.splitlines() if any(s in line for s in ['focus','Enter','Escape','POPUP','popup contains'])],
 'physicalInput':'NOT RUN','browserRendering':'NOT RUN'}
(SHOTS/'input-verification.json').write_text(json.dumps(input_proof,ensure_ascii=False,indent=2),encoding='utf-8')
report={'schemaVersion':1,'task':'Редактор шаблонов — этап1 WPF-макет, правки2','date':'2026-10-17','scope':'preview',
 'sourceRevision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),'dirtyBaseline':'Revision1 preview and unrelated concurrent module changes were dirty; scoped preview baseline saved to outputs/ui-production-preview/template-editor/revision-1. Unrelated owners untouched.',
 'readFiles':read_files+['Docs/Ui/DesignSystem/TemplateEditor-Revision2-Layout.md'],'layoutSpecification':'Docs/Ui/DesignSystem/TemplateEditor-Revision2-Layout.md',
 'build':[{'command':'powershell -NoProfile -ExecutionPolicy Bypass -File Tools/UiShowcase/TemplateEditor/Build.ps1 -Capture','exitCode':0,'target':'isolated net48 x64 preview, API2023 enum references; no production migration/MSI'}],
 'captures':captures,'runtimeDpi':runtime_dpi,'renderDpi':[96],
 'independentReview':{'status':'NOT RUN','reason':'Independent review in progress; updated after verdict','scope':'preview','report':'Docs/Ui/DesignSystem/TemplateEditor-Review.md'},
 'approvalStatusSource':'Docs/Ui/DesignSystem/STATUS.json','checks':checks,
 'productionMigrationAuthorized':False,'installationPerformed':False,'productionSourceChanges':[],
 'runtimeSummary':log.splitlines()[-1],'limitations':['Revit2022/2023/2024 operations NOT RUN; target production builds/MSI belong to stage2 after approval.','Fixture adapters are explicitly isolated; no live Document or ApplyService.','Per-category granular capabilities are synthetic fixture metadata; actual IsCategoryOverridable/IsCuttable/view probes NOT RUN. Analytical panel fixture is not proof that every real analytical category supports surface/transparency/detail.','RenderTargetBitmap excludes OS chrome; editor-popup composites actual live popup child at measured position.','Keyboard tests use WPF routed events/AccessKeyManager; physical OS input and default-browser rendering NOT RUN.']}
(DOC/'TemplateEditor-Report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
provenance={'date':'2026-10-17','scope':'isolated source-derived preview, not production/Revit','host':str(OUT/'SAB.TemplateEditor.Preview.exe'),
 'hostSha256':hashlib.sha256((OUT/'SAB.TemplateEditor.Preview.exe').read_bytes()).hexdigest(),'productionSources':json.loads((HERE/'source-contract.json').read_text(encoding='utf-8'))['sources'],
 'captures':captures,'summary':report['runtimeSummary'],'runtimeDpi':runtime_dpi,'renderDpi':96,'applicationResources':'empty, checked at runtime','productionMigrationAuthorized':False}
(SHOTS/'provenance.json').write_text(json.dumps(provenance,ensure_ascii=False,indent=2),encoding='utf-8')
print('Report:',len(checks),'registry IDs;',dict(Counter(x['status'] for x in checks)),';',len(captures),'captures')
