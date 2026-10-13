"""Hash source-derived native WPF preview captures. Never composites or alters PNGs."""
from pathlib import Path
import hashlib, json, re, html
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
D = ROOT / 'Docs/Ui/DesignSystem/screenshots-elevations-preview'
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
log = (D / 'verification.txt').read_text(encoding='utf-8-sig')
assert 'COMPLETED' in log and not re.search(r'^(FAIL|ERROR) ', log, re.M)
from ElevationSourceContract import verify
unchanged,delta = verify(ROOT)
contract = ROOT / 'outputs/ui-production-preview/elevations-source/source-contract.json'
generated = list(contract.parent.glob('*.cs')) + list((ROOT / 'outputs/ui-production-preview/plugin/ElevationPreview/Views').glob('*.xaml'))
captures = []
for p in sorted(D.glob('*.png')):
    match = re.search(r'CAPTURE ' + re.escape(p.stem) + r' \| ([\d.,]+)x([\d.,]+) DIP \| nativeDpi=([\d.,]+) renderDpi=([\d.,]+)', log)
    assert match, 'Stale capture: ' + p.name
    with Image.open(p) as im:
        width, height = im.size
    captures.append(dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p), width=width, height=height,
                         runtimeDpi=float(match[3].replace(',', '.')), renderDpi=float(match[4].replace(',', '.')), source='Actual source-derived WPF preview; explicit Document/session fixtures'))
assert len(captures) == len(re.findall(r'^CAPTURE ', log, re.M))
proof = dict(scope='Isolated source-derived preview outside Revit; not production UI migration',
             checksPassed=len(re.findall(r'^PASS ', log, re.M)), captures=captures,
             baselineFilesUnchanged=unchanged, authorizedFunctionalDelta=delta, sourceContractSha256=sha(contract),
             hostSources={str(p.relative_to(ROOT).as_posix()): sha(p) for p in [HERE/'ElevationPreview.cs', HERE/'GenerateElevationPreview.py', HERE/'Sab.UiShowcase.csproj']},
             generatedSources={p.relative_to(ROOT).as_posix(): sha(p) for p in generated},
             loadedAssembly=dict(path='outputs/ui-production-preview/plugin/SAB.dll',sha256=sha(ROOT/'outputs/ui-production-preview/plugin/SAB.dll'),revitApiYear=2023),
             hostBinarySha256=sha(ROOT/'outputs/ui-production-preview/plugin/SAB.UI.Preview.exe'),
             unverified=['Revit Document/transactions/native picking/ExternalEvent', 'Physical keyboard/accessibility', 'OS DPI and monitor transitions', 'Browser launch', 'API22 native runtime'],
             productionMigrationAuthorized=False, installationPerformed=False)
(D/'provenance.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('PASS elevations evidence:',proof['checksPassed'],'assertions;',len(captures),'captures;',proof['baselineFilesUnchanged'],'production hashes unchanged')
primary = [('Основные настройки ·4 вкладки','ElevationSettingsWindow-normal'),('Оформление ·единая форма','ElevationDecorationWindow-normal'),('Марки ·зазор выносок','decoration-tags'),('Каталог оформления','ElevationDecorationCatalogWindow-normal'),('Список помещений','MultiRoomSelectionWindow-normal'),('Смещение границ вида','ElevationCropAdjustmentWindow-normal'),('Выбор групп линий','LineGroupSelectionWindow-normal'),('Следующая группа','LineGroupNextActionWindow-normal'),('Обрезка по примеру','CropByExampleActionWindow-normal'),('Развёртка-пример','CropByExampleLineCreationWindow-normal'),('Результат создания ·успех','creation-notification-success'),('Результат создания ·предупреждения','creation-notification-warning'),('Результат создания ·ошибка','creation-notification-error'),('Предложение уведомлений ·416DIP','notification-proposal')]
page = '<!doctype html><html lang="ru"><meta charset="utf-8"><title>SAB · Развёртки · макеты</title><style>body{font:15px Segoe UI,sans-serif;background:#f6f7fa;color:#202b3c;margin:24px}h1{font-size:24px}section{background:white;border:1px solid #d6deed;border-radius:6px;margin:16px 0;padding:16px}img{display:block;max-width:100%;height:auto;border:1px solid #d6deed;border-radius:6px}a{color:#0f6cbd}summary{cursor:pointer;padding:12px}h2{font-size:18px}</style><h1>Развёртки — весь комплект макетов</h1><p>Изолированный WPF Preview. Синтетические данные; Revit не подключён. Перенос в рабочий плагин ожидает утверждения всего комплекта.</p><p><a href="../../../../Tools/UiShowcase/Open-Elevations.cmd">Запуск интерактивного WPF Preview</a> · <a href="Elevations-Verification.md">Проверки и ограничения</a></p>'
for label,stem in primary:
    page += '<section><h2>'+html.escape(label)+'</h2><img loading="lazy" src="screenshots-elevations-preview/'+stem+'.png" alt="'+html.escape(label)+'"></section>'
page = page.replace('../../../../Tools/','../../../Tools/')
page += '<details><summary>Все'+str(len(captures))+' снимков: вкладки, minimum и состояния</summary>'
for c in captures:
    name=Path(c['path']).name
    page += '<section><h2>'+html.escape(name)+'</h2><img loading="lazy" src="screenshots-elevations-preview/'+name+'" alt="'+html.escape(name)+'"></section>'
page += '</details><p>Планы-схемы находятся в основной вкладке. Отдельное legacy окно без вызывающих команд не включено в комплект.144DPI — offscreen render; Revit и physical/OS-DPI проверки не выполнены.</p></html>'
(D.parent/'Elevations-Preview.html').write_text(page,encoding='utf-8')
