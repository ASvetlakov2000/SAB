"""Record actual production WPF captures; no image compositing or synthetic pass counts."""
from pathlib import Path
import hashlib,json,re
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
HERE=Path(__file__).resolve().parent
D=ROOT/'Docs/Ui/DesignSystem/screenshots-notifications'
sha=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
log=(D/'verification.txt').read_text(encoding='utf-8-sig')
assert 'COMPLETED' in log and not any(line.startswith('FAIL ') for line in log.splitlines()), 'Incomplete/failed native capture'
contract=json.loads((HERE/'notification-source-contract.json').read_text(encoding='utf8'))
for name,digest in contract['files'].items():assert sha(ROOT/name)==digest, 'Stale source: '+name
captures=[]
for path in sorted(D.glob('*.png')):
    runtime,render=re.search(r'CAPTURE '+re.escape(path.stem)+r'; runtimeDpi=([\d.]+); renderDpi=([\d.]+)',log).groups()
    with Image.open(path) as image:width,height=image.size
    captures.append({'path':path.relative_to(ROOT).as_posix(),'sha256':sha(path),'width':width,'height':height,'runtimeDpi':float(runtime),'renderDpi':float(render),'source':'actual SAB.UI.Notifications production controls; synthetic result fixtures'})
assert len(captures)==9
proof={'scope':'Native WPF outside Revit; production initialization and classes; synthetic result DTOs','checksPassed':sum(line.startswith('PASS ') for line in log.splitlines()),'captures':captures,'sourceContractSha256':sha(HERE/'notification-source-contract.json'),'hostSources':{name:sha(HERE/name) for name in ('SourcePreview.cs','NotificationPreview.cs','Sab.UiShowcase.csproj')},'loadedAssembly':{'path':'outputs/ui-production-preview/plugin/SAB.dll','sha256':sha(ROOT/'outputs/ui-production-preview/plugin/SAB.dll'),'configuration':'Debug','revitApiYear':2023},'unverified':['Actual Revit operations/transactions','Physical pointer and keyboard traversal','Multiple monitor transitions','OS DPI transitions','Native API22 runtime'],'installationPerformed':False}
(D/'provenance.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print('PASS notification evidence:',proof['checksPassed'],'native assertions,',len(captures),'actual captures; explicit unverified scope retained')
