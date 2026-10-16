"""Hash actual clean-resource hotfix states separately from the released 1.1.0 evidence."""
from pathlib import Path
import hashlib,json,re,html
from PIL import Image
from ElevationSourceContract import verify

ROOT=Path(__file__).resolve().parents[2]
D=ROOT/'Docs/Ui/DesignSystem'
OUT=D/'screenshots-elevation-hotfix'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
verify(ROOT)
parts={}
for name,folder,scope in [('actual',OUT,'Actual SAB.dll classes; no Application host/legacy dictionaries; detached option/selection fixtures'),('notifications',OUT/'notifications','Actual shared timer/clipboard/stack and parameter adapter with clean Application resources'),('sessions',OUT/'sessions','Source-derived session adapters; API/ExternalEvent not dispatched')]:
    log=(folder/'verification.txt').read_text(encoding='utf-8-sig')
    assert 'COMPLETED' in log and not re.search(r'^(FAIL|ERROR) ',log,re.M),name
    captures=[]
    for path in sorted(folder.glob('*.png')):
        assert path.stem in log,'Stale capture: '+str(path)
        with Image.open(path) as image:w,h=image.size
        captures.append(dict(path=path.relative_to(ROOT).as_posix(),sha256=sha(path),width=w,height=h))
    parts[name]=dict(scope=scope,checksPassed=len(re.findall(r'^PASS ',log,re.M)),captures=captures,logSha256=sha(folder/'verification.txt'))
for source,name in [('notification-baseline-probe','baseline-resource-probe.txt'),('notification-fixed-probe','fixed-resource-probe.txt')]:
    path=ROOT/'outputs/ui-production-preview'/source/'resource-probe.txt'
    (OUT/name).write_bytes(path.read_bytes())
assert 'SabBrush.DisabledBackground' in (OUT/'baseline-resource-probe.txt').read_text(encoding='utf-8')
assert (OUT/'fixed-resource-probe.txt').read_text(encoding='utf-8').startswith('PASS')
delta=json.loads((ROOT/'Tools/UiShowcase/elevations-hotfix-delta.json').read_text(encoding='utf-8'))
proof=dict(version='1.1.1',baseCommit=delta['baseCommit'],sourceDelta='Tools/UiShowcase/elevations-hotfix-delta.json',parts=parts,
           applicationHostDictionaries=0,nativeDpi=96,renderDpi=96,installationPerformed=False,
           sources={name:sha(ROOT/name) for name in delta['files']},
           testSources={name:sha(ROOT/name) for name in ['Tools/UiShowcase/ElevationHotfixPreview.cs','Tools/UiShowcase/SourcePreview.cs','Tools/UiShowcase/NotificationPreview.cs','Tools/UiShowcase/GenerateElevationPreview.py','Tools/UiShowcase/Sab.UiShowcase.csproj']},
           binarySha256=sha(ROOT/'outputs/ui-production-preview/plugin/SAB.dll'),hostSha256=sha(ROOT/'outputs/ui-production-preview/plugin/SAB.UI.Preview.exe'),
           unverified=['Actual Revit creation/transactions/rollback/native selection/ExternalEvent','Physical keyboard/accessibility','OS DPI/monitor transitions','Native API22 runtime'])
(OUT/'provenance.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
page='<!doctype html><html lang="ru"><meta charset="utf-8"><title>SAB1.1.1 — исправления развёрток</title><style>body{font:15px Segoe UI,sans-serif;background:#f6f7fa;color:#202b3c;margin:24px}section{padding:16px;margin:16px 0;background:white;border:1px solid #d6deed;border-radius:6px}img{display:block;max-width:100%;height:auto}h1{font-size:24px}h2{font-size:18px}</style><h1>Развёртки — исправления1.1.1</h1><p>Реальные классы SAB.dll вне Revit. Application без словарей тестового хоста. Данные помещений — fixtures, выполнение операций Revit не проверялось.</p>'
for capture in parts['actual']['captures']:
    path=Path(capture['path']).relative_to('Docs/Ui/DesignSystem').as_posix()
    page+='<section><h2>'+html.escape(Path(path).stem)+'</h2><img loading="lazy" src="'+html.escape(path)+'"></section>'
(D/'ElevationHotfix.html').write_text(page+'</html>',encoding='utf-8')
print('PASS hotfix evidence:',{name:(part['checksPassed'],len(part['captures'])) for name,part in parts.items()},'; before missing resource reproduced, after clean resource probe PASS')
