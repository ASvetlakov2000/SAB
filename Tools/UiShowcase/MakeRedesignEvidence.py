"""Record fresh actual production and isolated session evidence without rewriting old proofs."""
from pathlib import Path
import json,hashlib,re,html
from PIL import Image
from ElevationSourceContract import verify

ROOT=Path(__file__).resolve().parents[2]
D=ROOT/'Docs/Ui/DesignSystem'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
unchanged,delta=verify(ROOT)
parts={}
for name,scope in [('production','Actual SAB.dll elevation Window constructors; synthetic option data only'),('modules','Actual SAB.dll parameterization and views/sheets windows; synthetic DTOs'),('notifications','Actual SAB.dll shared notification classes and parameter adapter; synthetic results'),('sessions','Source-derived fixture session adapters; NOT Revit dispatch')]:
    directory=D/('screenshots-redesign-'+name)
    log=(directory/'verification.txt').read_text(encoding='utf-8-sig')
    assert 'COMPLETED' in log and not re.search(r'^(FAIL|ERROR) ',log,re.M),name
    captures=[]
    for p in sorted(directory.glob('*.png')):
        assert p.stem in log, 'Stale capture: '+str(p)
        with Image.open(p) as im:width,height=im.size
        captures.append(dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p),width=width,height=height))
    part=dict(scope=scope,checksPassed=len(re.findall(r'^PASS ',log,re.M)),captures=captures,logSha256=sha(directory/'verification.txt'))
    parts[name]=part
    (directory/'provenance.json').write_text(json.dumps(part,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
migration=json.loads((ROOT/'Tools/UiShowcase/elevations-production-migration.json').read_text(encoding='utf-8'))
proof=dict(version='1.1.0',name='Редизайн',productionMigrationAuthorized=True,installationPerformed=False,
           unchangedOriginalElevationFiles=unchanged,originalElevationFiles=93,originalFunctionalDeltaFiles=len(delta['files']),
           migration='Tools/UiShowcase/elevations-production-migration.json',parts=parts,
           migratedSources={p:sha(ROOT/p) for p in migration['files']},
           hostSources={p:sha(ROOT/p) for p in ['Tools/UiShowcase/RedesignProduction.cs','Tools/UiShowcase/SourcePreview.cs','Tools/UiShowcase/NotificationPreview.cs','Tools/UiShowcase/ElevationPreview.cs','Tools/UiShowcase/GenerateElevationPreview.py','Tools/UiShowcase/Sab.UiShowcase.csproj']},
           nativeDpi=96,offscreenRenderDpi=[96,144],
           actualAssemblySha256=sha(ROOT/'outputs/ui-production-preview/plugin/SAB.dll'),hostBinarySha256=sha(ROOT/'outputs/ui-production-preview/plugin/SAB.UI.Preview.exe'),
           unverified=['Revit model geometry and transactions','Native Revit selection and ExternalEvent dispatch','Physical keyboard/accessibility','OS DPI and monitor transitions','Native API22 runtime'])
(D/'RedesignMigration-Provenance.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
page='<!doctype html><html lang="ru"><meta charset="utf-8"><title>SAB1.1.0 — Редизайн</title><style>body{font:15px Segoe UI;background:#f6f7fa;color:#202b3c;margin:24px}section{padding:16px;margin:16px 0;background:white;border:1px solid #d6deed;border-radius:6px}img{display:block;max-width:100%;height:auto}h1{font-size:24px}h2{font-size:18px}</style><h1>SAB1.1.0 — Редизайн</h1><p>Снимки фактических окон SAB.dll вне Revit. Тестовые данные; операции с моделью не запускались.</p>'
for name in ['production','modules','notifications']:
    page+='<h2>'+html.escape(parts[name]['scope'])+'</h2>'
    for c in parts[name]['captures']:
        page+='<section><h2>'+html.escape(Path(c['path']).stem)+'</h2><img loading="lazy" src="'+html.escape(str(Path(c['path']).relative_to('Docs/Ui/DesignSystem')).replace('\\','/'))+'"></section>'
(D/'RedesignMigration.html').write_text(page+'</html>',encoding='utf-8')
print('PASS fresh redesign provenance:',{n:(v['checksPassed'],len(v['captures'])) for n,v in parts.items()})
