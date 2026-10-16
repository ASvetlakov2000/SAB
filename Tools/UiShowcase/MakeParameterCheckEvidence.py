"""Record the current parameter-check fix; retain all previous approved evidence."""
from pathlib import Path
import hashlib, html, json, re
from PIL import Image
from SourceHash import matches

ROOT=Path(__file__).resolve().parents[2]
D=ROOT/'Docs/Ui/DesignSystem'
OUT=D/'screenshots-parameter-checks'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
delta=json.loads((ROOT/'Tools/UiShowcase/parameter-check-delta.json').read_text(encoding='utf-8'))
for name,change in delta['files'].items():
    assert matches(ROOT/name,change['after']),name
parts={}
for name,folder,scope in [('actual',OUT,'Actual SAB.dll classes, clean Application; full-catalog fixtures and routed input; no Revit execution'),('regressions',OUT/'regressions','Actual parameterization and views/sheets SAB.dll classes, clean Application, synthetic data outside Revit')]:
    log=(folder/'verification.txt').read_text(encoding='utf-8-sig')
    assert 'COMPLETED' in log and not re.search(r'^(FAIL|ERROR) ',log,re.M),name
    captures=[]
    for p in sorted(folder.glob('*.png')):
        assert p.stem in log,'Stale capture '+str(p)
        with Image.open(p) as im:width,height=im.size
        captures.append(dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p),width=width,height=height))
    parts[name]=dict(scope=scope,checksPassed=len(re.findall(r'^PASS ',log,re.M)),captures=captures,logSha256=sha(folder/'verification.txt'))
proof=dict(version='1.1.2',baseCommit=delta['baseCommit'],precedingUncommittedDelta=delta['precedingUncommittedTask'],sourceDelta='Tools/UiShowcase/parameter-check-delta.json',parts=parts,applicationHostDictionaries=0,nativeDpi=96,renderDpi=96,
           sources={name:sha(ROOT/name) for name in delta['files']},
           testSources={name:sha(ROOT/name) for name in ['Tools/UiShowcase/ParameterCheckPreview.cs','Tools/UiShowcase/SourcePreview.cs','Tools/UiShowcase/VerifyNotificationSource.py','Tools/UiShowcase/ElevationSourceContract.py','Tools/UiShowcase/Sab.UiShowcase.csproj']},
           assemblySha256=sha(ROOT/'outputs/ui-production-preview/plugin/SAB.dll'),hostSha256=sha(ROOT/'outputs/ui-production-preview/plugin/SAB.UI.Preview.exe'),installationPerformed=False,
           unverified=['Actual Revit category enumeration and model profile transactions','Native check commands/hotkeys/ribbon appearance','Native graphics filters and automatic unhighlight model effects','Physical input/F4/paste/IME/accessibility','OS DPI/monitor transitions','Native runtime/API22'])
(OUT/'provenance.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
page='<!doctype html><html lang="ru"><meta charset="utf-8"><title>SAB1.1.2 — параметризация</title><style>body{font:15px Segoe UI,sans-serif;background:#f6f7fa;color:#202b3c;margin:24px}section{padding:16px;margin:16px 0;background:white;border:1px solid #d6deed;border-radius:6px}img{display:block;max-width:100%;height:auto}h1{font-size:24px}h2{font-size:18px}</style><h1>Параметризация — исправления1.1.2</h1><p>Фактические WPF-классы SAB.dll, тестовые данные вне Revit. Выполнение команд и операции с моделью не проверены. Popup снят отдельно с actual PART_Popup.Child.</p>'
for capture in parts['actual']['captures']:
    p=Path(capture['path']).relative_to('Docs/Ui/DesignSystem').as_posix()
    page+='<section><h2>'+html.escape(Path(p).stem)+'</h2><img loading="lazy" src="'+html.escape(p)+'"></section>'
(D/'ParameterCheck.html').write_text(page+'</html>',encoding='utf-8')
packages=[]
for year in ['2022','2023','2024']:
    path=ROOT/f'outputs/ui-production-preview/msi-check/{year}/verification.json'
    package=json.loads(path.read_text(encoding='utf-8-sig'))
    assert package['status']=='PASS' and package['assemblyVersion']=='1.1.2.0' and package['productVersion']=='1.1.2',year
    assert sha(Path(package['msi'])).upper()==package['msiSha256'],year
    packages.append(package)
installer=dict(version='1.1.2',date='2026-10-13',entryPoint='Build-Msi.bat',configuration='Release',exitCode=0,scope='Parameter picker input fix, full independent check category scope, separate settings command/ribbon order, silent report-free highlight with one activation notice; previous Elevations fixes preserved',packages=packages,installationPerformed=False)
(D/'ParameterCheck-Installer.json').write_text(json.dumps(installer,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('PASS parameter check evidence:',{name:(p['checksPassed'],len(p['captures'])) for name,p in parts.items()},'; three current1.1.2 MSI payloads verified; no installation')
