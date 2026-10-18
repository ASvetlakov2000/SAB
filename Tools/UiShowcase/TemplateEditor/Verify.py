"""Read-only verification of this preview packet, independent of concurrent module docs."""
from Prepare import *

doc=ROOT/'Docs/Ui/DesignSystem'
report=json.loads((doc/'TemplateEditor-Report.json').read_text(encoding='utf-8-sig'))
ids=set(re.findall(r'<a id="sab-ui-(\d+)"',(doc/'ErrorRegistry.md').read_text(encoding='utf-8-sig')))
assert {c['id'][-3:] for c in report['checks']}==ids
assert len(report['checks'])==len(ids)
assert all((ROOT/e).exists() for c in report['checks'] if c['status']=='PASS' for e in c['evidence'])
assert all(hashlib.sha256((ROOT/c['path']).read_bytes()).hexdigest()==c['sha256'] for c in report['captures'])
contract=json.loads((HERE/'source-contract.json').read_text(encoding='utf-8'))
assert all(hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==h for p,h in contract['sources'].items())
assert all(not any(w['missing'].values()) for w in contract['windows'].values())
assert not subprocess.check_output(['git','diff','--name-only','--','SAB/Cls_ViewTemplateGraphics','SAB/UI/Styles/SABFluentLight.xaml','SAB/UI/FluentLight','Build-Msi.bat'],cwd=ROOT,text=True).strip()
# SAB.csproj has a concurrent decoration SVG resource addition; it is not this preview's owner.
assert json.loads((doc/'STATUS.json').read_text(encoding='utf-8-sig'))['templateEditorPreview']['productionMigrationAuthorized'] is False
assert report['independentReview']['status']=='PASS'
assert report['independentReview']['hostSha256']==hashlib.sha256((OUT/'SAB.TemplateEditor.Preview.exe').read_bytes()).hexdigest()
launch=json.loads((doc/'screenshots-template-editor/revision-2/launch-verification.json').read_text(encoding='utf-8-sig'))
assert launch['singleInstance'] and launch['noRebuild']
print(f'PASS task packet: {len(ids)} registry IDs; {len(report["captures"])} capture hashes; evidence paths; baseline module hashes; original production untouched; reviewed host; singleton launch; migration false.')
