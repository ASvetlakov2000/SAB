"""Record fresh native captures and their actual production-source provenance."""
from pathlib import Path
import hashlib, json, html
ROOT=Path(__file__).resolve().parents[2]
D=ROOT/'Docs/Ui/DesignSystem'
shots=D/'screenshots-production-0.5'
captures=[{'path':p.relative_to(ROOT).as_posix(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),
           'origin':'Actual production SAB UI/VM/classes and scoped resources; synthetic DTOs outside Revit; no presentation overrides'} for p in sorted(shots.glob('*.png'))]
log=(shots/'verification.txt').read_text(encoding='utf-8-sig')
assert len(captures)==27 and 'COMPLETED' in log and not any(s.startswith('FAIL') for s in log.splitlines())
proof={'version':'0.5-production-migration','captures':captures,'productionContract':'Tools/UiShowcase/production-source-contract.json',
       'passCount':sum(s.startswith('PASS ') for s in log.splitlines()),'limitations':['No Revit Document/API execution','Programmatic input; physical input not tested','144dpi is offscreen rendering, not OS scaling','External HTML browser launch not tested']}
(shots/'provenance.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
cards=''.join('<figure><figcaption>'+html.escape(Path(c['path']).stem)+'</figcaption><img src="screenshots-production-0.5/'+Path(c['path']).name+'"></figure>' for c in captures)
(D/'ProductionReview.html').write_text('<!doctype html><meta charset="utf-8"><title>SAB — production UI0.5</title><style>body{font:14px Segoe UI;background:#f6f7fa;margin:24px}figure{margin:24px 0}img{display:block;max-width:100%;border:1px solid #d6deed}figcaption{margin-bottom:8px}</style><h1>SAB: рабочие UI-классы</h1><p>Свежая проверка вне Revit.27снимков; '+str(proof['passCount'])+' программных PASS. Демонстрационные данные, API модели не запускался.</p>'+cards,encoding='utf8')
print('PASS production capture provenance:',len(captures),'PNGs;',proof['passCount'],'runtime checks')
