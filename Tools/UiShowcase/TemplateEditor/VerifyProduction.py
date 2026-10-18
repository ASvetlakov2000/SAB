"""Read-only integrity of the reviewed actual-DLL/installer packet."""
from Prepare import *
from PIL import Image

DOC=ROOT/'Docs/Ui/DesignSystem'
report=json.loads((DOC/'TemplateEditor-Production-Report.json').read_text(encoding='utf-8-sig'))
contract=json.loads((HERE/'production-source-contract.json').read_text(encoding='utf-8-sig'))
ids={'SAB-UI-'+x for x in re.findall(r'<a id="sab-ui-(\d+)"',(DOC/'ErrorRegistry.md').read_text(encoding='utf-8-sig'))}
assert {c['id'] for c in report['checks']}==ids
assert all(hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==h for p,h in contract['currentOwners'].items())
assert all(not any(w['missing'].values()) for w in contract['windows'].values())
assert all((ROOT/e).exists() for c in report['checks'] if c['status']=='PASS' for e in c['evidence'])
for shot in report['captures']:
    p=ROOT/shot['path'];assert hashlib.sha256(p.read_bytes()).hexdigest()==shot['sha256']
    # WPF antialias compositing can round alpha to254; reject any actual transparent region.
    with Image.open(p) as im:assert im.convert('RGBA').getchannel('A').getextrema()[0]>=254,p.name
provenance=json.loads((DOC/'screenshots-template-editor-production/provenance.json').read_text(encoding='utf-8-sig'))
assert hashlib.sha256((OUT/'production/SAB.dll').read_bytes()).hexdigest()==provenance['sha256']
assert hashlib.sha256((OUT/'production/SAB.TemplateEditor.ProductionCheck.exe').read_bytes()).hexdigest()==provenance['hostSha256']
assert b'SAB.TemplateEditorPreview' not in (OUT/'production/SAB.dll').read_bytes()
for build in report['build']:
    assert hashlib.sha256((ROOT/build['assembly']).read_bytes()).hexdigest()==build['sha256']
installer=json.loads((DOC/'TemplateEditor-Production-Installer.json').read_text(encoding='utf-8-sig'))
for target in installer['targets']:
    assert hashlib.sha256(Path(target['msi']).read_bytes()).hexdigest().upper()==target['sha256'].upper()
    assert target['status']=='PASS' and target['installationPerformed'] is False
assert json.loads((DOC/'STATUS.json').read_text(encoding='utf-8-sig'))['templateEditorProduction']['productionMigrationAuthorized'] is True
review=report['independentReview']
assert review['status']=='PASS', 'Independent production review is not completed'
assert review['assemblySha256']==provenance['sha256'] and review['hostSha256']==provenance['hostSha256']
print(f'PASS actual production packet: {len(ids)} registry IDs; {len(report["captures"])} opaque PNG/hashes; current source owners and contracts; reviewed DLL/host;3 API builds/3 MSI payloads; migration authority; no installation.')
