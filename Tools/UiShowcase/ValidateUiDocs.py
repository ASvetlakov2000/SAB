"""Read-only validation of SAB UI documentation; never a UI or Revit test."""
from pathlib import Path
from urllib.parse import unquote
import argparse
import csv
import hashlib
import json
import re
import sys
import subprocess
from SourceHash import matches, matches_bytes

ROOT = Path(__file__).resolve().parents[2]
D = ROOT / 'Docs/Ui/DesignSystem'
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--report', type=Path, help='Validate a completed per-task report as well')
args = parser.parse_args()
errors = []

def require(ok, message):
    if not ok:
        errors.append(message)

def read_json(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

registry = (D / 'ErrorRegistry.md').read_text(encoding='utf-8-sig')
ids = re.findall(r'<a id="sab-ui-\d{3}"></a>(SAB-UI-\d{3})', registry)
require(bool(ids) and len(ids) == len(set(ids)), 'Registry IDs must exist and be unique')
expected = set(ids)
files = [ROOT / 'AGENTS.md', ROOT / '.agents/skills/sab-wpf-ui/SKILL.md', ROOT / 'PRODUCT.md']
files += [D / p for p in ('Specification.md', 'ErrorRegistry.md', 'Workflow.md', 'ReviewRoles.md', 'LayoutPatterns.md', 'LayoutSpecification.md', 'README.md', 'CoveragePlan.md', 'WorkflowVerification.md', 'WorkflowReview.md')]
files += [ROOT / 'Tools/UiShowcase' / p for p in ('README.md', 'PRODUCT.md', 'DESIGN.md')]
for path in files:
    text = path.read_text(encoding='utf-8-sig')
    for target in re.findall(r'!?\[[^\]]*\]\(([^)\n]+)\)', text):
        target = unquote(target.strip('<>'))
        if re.match(r'(?:https?|mailto|app|codex)\:', target):
            continue
        name, _, anchor = target.partition('#')
        resolved = (path.parent / name).resolve() if name else path
        require(resolved.exists(), f'Broken link: {path.relative_to(ROOT)} -> {target}')
        if anchor.startswith('sab-ui-'):
            require(anchor.upper() in expected, f'Unknown registry anchor: {target}')
    for mentioned in re.findall(r'SAB-UI-\d{3}', text):
        require(mentioned in expected, f'Unknown ID: {mentioned} in {path.name}')

template = read_json(D / 'WorkflowReport.template.json')
require({c['id'] for c in template['checks']} == expected and len(template['checks']) == len(ids), 'Report template must cover each registry ID exactly once')
require(all(c['status'] == 'NOT RUN' and c['applicable'] is None and not c['evidence'] for c in template['checks']), 'Unfilled template must not claim PASS')

with (D / 'component-coverage.csv').open(encoding='utf-8-sig', newline='') as stream:
    rows = list(csv.DictReader(stream))
require(len(rows) == len({r['type'] for r in rows}), 'Coverage types must be unique')
counts = {}
for row in rows:
    state = row['coverage_status']
    counts[state] = counts.get(state, 0) + 1
    require(state in {'current_example', 'resource_only', 'missing'}, f'Unsupported coverage status: {state}')
    for evidence in row['current_evidence'].split(';'):
        if evidence.strip():
            require((ROOT / evidence.strip()).exists(), f'Missing coverage evidence: {evidence}')
    for ident in re.findall(r'SAB-UI-\d{3}', ';'.join(row.values())):
        require(ident in expected, f'Unknown coverage ID: {ident}')

status = read_json(D / 'STATUS.json')
sidecar = read_json(ROOT / 'Tools/UiShowcase/.impeccable/design.json')
authority = sidecar['extensions']['approval']
for key in ('status', 'version', 'approvedConcept', 'approvedAt', 'productionMigrationAuthorized'):
    require(authority[key] == status[key], f'Sidecar/STATUS disagree: {key}')
if status['status'] == 'pending_user_approval':
    require(status['approvedConcept'] is None and status['approvedAt'] is None and not status['productionMigrationAuthorized'], 'Pending status must not manufacture approval')

shots = D / 'screenshots-v0.5'
proof = read_json(shots / 'provenance.json')['captures']
require(len(proof) == len(list(shots.glob('*.png'))) == 27, 'Existing0.5 evidence must retain27 captures')
for shot in proof:
    path = ROOT / shot['path']
    require(path.exists() and hashlib.sha256(path.read_bytes()).hexdigest() == shot['sha256'], f'Capture hash mismatch: {path}')
log = (shots / 'verification.txt').read_text(encoding='utf-8-sig')
require(sum(s.startswith('PASS ') for s in log.splitlines()) == 75 and 'COMPLETED' in log and not any(s.startswith('FAIL') for s in log.splitlines()), 'Existing0.5 protocol differs from75 PASS/COMPLETED')
contract = read_json(ROOT / 'Tools/UiShowcase/source-contract.json')
require(contract['sourceBindings'] == 135 and contract['secondarySettingsBindings'] == 103 and not contract['missingBindings'] and not contract['secondaryMissingBindings'], 'Existing0.5 source bindings changed')
require(contract['sourceColumns'] == contract['newColumns'] == 10 and len(contract['parameterBehaviorMethodsUnchanged']) == 10 and not contract['productionFilesWritten'], 'Existing0.5 source contract changed')

# Production proof is separate: never reuse historical PASS or rewrite approved screenshots.
production = ROOT / 'Tools/UiShowcase/production-source-contract.json'
if production.exists():
    active = read_json(production)
    require(active['uiSourceOverrides'] is False, 'Actual production evidence must not use UI source overrides')
    successor = ROOT / 'Tools/UiShowcase/notification-source-contract.json'
    if successor.exists():
        active = read_json(successor)
        require(active['uiSourceOverrides'] is False and active['historicalProof'] == production.name, 'Notification successor must retain original production proof')
    # A bounded successor records before/after hashes without rewriting historical proof.
    numbering_path = D/'screenshots-elevation-numbering/provenance.json'
    numbering = read_json(numbering_path) if numbering_path.exists() else None
    direction_path = D/'screenshots-direction-preview/provenance.json'
    direction = read_json(direction_path) if direction_path.exists() else None
    runtime_path = D/'screenshots-preview-runtime/provenance.json'
    runtime = read_json(runtime_path) if runtime_path.exists() else None
    tip_path = D/'ElevationPreviewTip-Provenance.json'
    tip = read_json(tip_path) if tip_path.exists() else None
    directions_path = D/'DirectionsWindow-Provenance.json'
    directions = read_json(directions_path) if directions_path.exists() else None
    guidance_path = D/'SelectionGuidance-Provenance.json'
    guidance = read_json(guidance_path) if guidance_path.exists() else None
    material_path = D/'MaterialSelection-Provenance.json'
    material = read_json(material_path) if material_path.exists() else None
    ribbon_path = D/'RibbonModules-Provenance.json'
    ribbon = read_json(ribbon_path) if ribbon_path.exists() else None
    template_path = ROOT/'Tools/UiShowcase/TemplateEditor/production-source-contract.json'
    template_migration = read_json(template_path).get('migrationSuccessor', {}) if template_path.exists() else {}
    notification_settings_path = ROOT/'Tools/UiShowcase/NotificationSettings/production-source-contract.json'
    notification_settings = read_json(notification_settings_path) if notification_settings_path.exists() else {}
    footer_path = D/'TemplateEditorFooter-SourceContract.json'
    footer = read_json(footer_path) if footer_path.exists() else {}
    for name, sha in active['files'].items():
        source_bytes = (ROOT/name).read_bytes()
        if name in footer.get('source', {}):
            delta = footer['source'][name]
            before_path = ROOT/delta['beforePath']
            parent_path = ROOT/footer['historicalProof']
            authorized = (status.get('templateEditorFooter', {}).get('productionMigrationAuthorized') is True and
                footer.get('uiSourceOverrides') is False and
                footer.get('historicalProof') == 'Tools/UiShowcase/NotificationSettings/production-source-contract.json' and
                parent_path.exists() and hashlib.sha256(parent_path.read_bytes()).hexdigest() == footer.get('historicalProofSha256') and
                before_path.exists() and hashlib.sha256(before_path.read_bytes()).hexdigest() == delta.get('before') and
                hashlib.sha256(source_bytes).hexdigest() == delta.get('after'))
            require(authorized, 'Invalid template footer source successor: '+name)
            if authorized: source_bytes = before_path.read_bytes()
        if name in notification_settings.get('source', {}):
            delta = notification_settings['source'][name]
            before_path = ROOT/delta['beforePath']
            parent_path = ROOT/notification_settings['historicalProof']
            authorized = (status.get('notificationSettingsProduction', {}).get('productionMigrationAuthorized') is True and
                notification_settings.get('uiSourceOverrides') is False and
                notification_settings.get('historicalProof') == 'Tools/UiShowcase/TemplateEditor/production-source-contract.json' and
                parent_path.exists() and hashlib.sha256(parent_path.read_bytes()).hexdigest() == notification_settings.get('historicalProofSha256') and
                before_path.exists() and hashlib.sha256(before_path.read_bytes()).hexdigest() == delta.get('before') and
                hashlib.sha256(source_bytes).hexdigest() == delta.get('after'))
            require(authorized, 'Invalid notification settings source successor: '+name)
            if authorized: source_bytes = before_path.read_bytes()
        if name in template_migration.get('source', {}):
            delta = template_migration['source'][name]
            before_path = ROOT/template_migration['baseline']/name
            parent_path = ROOT/template_migration['historicalProof']
            authorized = (status.get('templateEditorProduction', {}).get('productionMigrationAuthorized') is True and
                template_migration.get('uiSourceOverrides') is False and
                template_migration.get('historicalProof') == 'Docs/Ui/DesignSystem/RibbonModules-Provenance.json' and
                parent_path.exists() and hashlib.sha256(parent_path.read_bytes()).hexdigest() == template_migration.get('historicalProofSha256') and
                before_path.exists() and hashlib.sha256(before_path.read_bytes()).hexdigest() == delta.get('before') and
                hashlib.sha256(source_bytes).hexdigest() == delta.get('after'))
            require(authorized, 'Invalid template editor source successor: '+name)
            if authorized: source_bytes = before_path.read_bytes()
        if ribbon is not None and name in ribbon.get('source', {}):
            git = Path(r'C:\Users\svetl\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe')
            original = subprocess.run([str(git), 'show', ribbon['sourceRevision']+':'+name], cwd=ROOT, capture_output=True)
            candidates = [original.stdout, original.stdout.replace(b'\r\n', b'\n'), original.stdout.replace(b'\r\n', b'\n').replace(b'\n', b'\r\n')]
            before = next((data for data in candidates if hashlib.sha256(data).hexdigest() == ribbon['sourceBefore'].get(name)), None)
            authorized = (ribbon.get('uiSourceOverrides') is False and original.returncode == 0 and
                ribbon.get('historicalProof') == 'Docs/Ui/DesignSystem/MaterialSelection-Provenance.json' and
                ribbon.get('historicalProofSha256') == hashlib.sha256(material_path.read_bytes()).hexdigest() and
                hashlib.sha256(original.stdout).hexdigest() == ribbon['sourceBeforeGitBlobSha256'].get(name) and
                hashlib.sha256(source_bytes).hexdigest() == ribbon['source'].get(name) and before is not None)
            require(authorized, 'Invalid ribbon source successor: '+name)
            if authorized: source_bytes = before
        if material is not None and name in material.get('sourceBeforeGitBlobSha256', {}):
            git = Path(r'C:\Users\svetl\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe')
            original = subprocess.run([str(git), 'show', material['sourceRevision']+':'+name], cwd=ROOT, capture_output=True)
            authorized = (material.get('uiSourceOverrides') is False and original.returncode == 0 and
                material.get('historicalProof') == 'Docs/Ui/DesignSystem/SelectionGuidance-Provenance.json' and
                material.get('historicalProofSha256') == hashlib.sha256(guidance_path.read_bytes()).hexdigest() and
                hashlib.sha256(source_bytes).hexdigest() == material['source'].get(name) and
                hashlib.sha256(original.stdout).hexdigest() == material['sourceBeforeGitBlobSha256'][name])
            require(authorized, 'Invalid material selection source successor: '+name)
            if authorized:
                prior_expected = guidance.get('source', {}).get(name, {}).get('after', sha) if guidance else sha
                candidates = [original.stdout, original.stdout.replace(b'\r\n', b'\n'), original.stdout.replace(b'\r\n', b'\n').replace(b'\n', b'\r\n')]
                source_bytes = next((data for data in candidates if hashlib.sha256(data).hexdigest() == prior_expected), original.stdout)
        if guidance is not None and name in guidance.get('source', {}):
            delta = guidance['source'][name]
            before = ROOT/'outputs/ui-production-preview/selection-guidance-baseline/files'/name
            authorized = (guidance.get('uiSourceOverrides') is False and
                guidance.get('historicalProof') == 'Docs/Ui/DesignSystem/DirectionsWindow-Provenance.json' and
                guidance.get('historicalProofSha256') == hashlib.sha256(directions_path.read_bytes()).hexdigest() and
                hashlib.sha256(source_bytes).hexdigest() == delta['after'] and before.exists() and
                hashlib.sha256(before.read_bytes()).hexdigest() == delta['before'])
            require(authorized, 'Invalid selection guidance source successor: '+name)
            if authorized: source_bytes = before.read_bytes()
        source_sha = hashlib.sha256(source_bytes).hexdigest()
        valid = matches_bytes(ROOT/name, source_bytes, sha)
        if not valid and numbering is not None:
            delta = numbering.get('source', {}).get(name, {})
            valid = (numbering.get('uiSourceOverrides') is False and
                     numbering.get('historicalProof') == 'Tools/UiShowcase/notification-source-contract.json' and
                     delta.get('before') == sha and
                     source_sha == delta.get('after'))
        if not valid and direction is not None:
            delta = direction.get('source', {}).get(name, {})
            previous = numbering.get('source', {}).get(name, {}) if numbering else {}
            before_proof = previous.get('after') if previous.get('before') == sha else sha
            baseline = ROOT/'outputs/ui-production-preview/direction-preview-baseline/files'/name
            valid = (direction.get('uiSourceOverrides') is False and
                     direction.get('historicalProof') == 'Docs/Ui/DesignSystem/screenshots-elevation-numbering/provenance.json' and
                     delta.get('beforeProofHash') == before_proof and baseline.exists() and
                     hashlib.sha256(baseline.read_bytes()).hexdigest() == delta.get('before') and
                     matches_bytes(ROOT/name, baseline.read_bytes(), before_proof) and
                     source_sha == delta.get('after'))
        if not valid and runtime is not None and direction is not None:
            current_delta = runtime.get('source', {}).get(name, {})
            parent_delta = direction.get('source', {}).get(name, {})
            previous = numbering.get('source', {}).get(name, {}) if numbering else {}
            before_proof = previous.get('after') if previous.get('before') == sha else sha
            original = ROOT/'outputs/ui-production-preview/direction-preview-baseline/files'/name
            baseline = ROOT/'outputs/ui-production-preview/direction-preview-runtime-baseline/files'/name
            parent_valid = (parent_delta.get('beforeProofHash') == before_proof and original.exists() and
                            hashlib.sha256(original.read_bytes()).hexdigest() == parent_delta.get('before') and
                            matches_bytes(ROOT/name, original.read_bytes(), before_proof))
            parent_hash = parent_delta.get('after')
            if not parent_delta:
                # A first change to a previously untouched owner (for example the product version).
                parent_hash = current_delta.get('before')
                parent_valid = baseline.exists() and matches_bytes(ROOT/name, baseline.read_bytes(), before_proof)
            runtime_authorized = (runtime.get('uiSourceOverrides') is False and parent_valid and
                     runtime.get('historicalProof') == 'Docs/Ui/DesignSystem/screenshots-direction-preview/provenance.json' and
                     runtime.get('historicalProofSha256') == hashlib.sha256(direction_path.read_bytes()).hexdigest() and
                     current_delta.get('before') == parent_hash and baseline.exists() and
                     hashlib.sha256(baseline.read_bytes()).hexdigest() == current_delta.get('before'))
            valid = runtime_authorized and source_sha == current_delta.get('after')
            if not valid and runtime_authorized and tip is not None:
                tip_delta = tip.get('source', {}).get(name, {})
                tip_baseline = ROOT/'outputs/ui-production-preview/preview-tip-baseline/files'/name
                tip_authorized = (tip.get('uiSourceOverrides') is False and
                         tip.get('historicalProof') == 'Docs/Ui/DesignSystem/screenshots-preview-runtime/provenance.json' and
                         tip.get('historicalProofSha256') == hashlib.sha256(runtime_path.read_bytes()).hexdigest() and
                         tip_delta.get('before') == current_delta.get('after') and tip_baseline.exists() and
                         hashlib.sha256(tip_baseline.read_bytes()).hexdigest() == tip_delta.get('before'))
                valid = tip_authorized and source_sha == tip_delta.get('after')
                if not valid and tip_authorized and directions is not None:
                    delta = directions.get('source', {}).get(name, {})
                    before = ROOT/'outputs/ui-production-preview/directions-window-baseline/files'/name
                    valid = (directions.get('uiSourceOverrides') is False and
                             directions.get('historicalProof') == 'Docs/Ui/DesignSystem/ElevationPreviewTip-Provenance.json' and
                             directions.get('historicalProofSha256') == hashlib.sha256(tip_path.read_bytes()).hexdigest() and
                             delta.get('before') == tip_delta.get('after') and before.exists() and
                             hashlib.sha256(before.read_bytes()).hexdigest() == delta.get('before') and
                             source_sha == delta.get('after'))
        require(valid, 'Stale production source proof: '+name)
    fresh = D/'screenshots-production-0.5'
    if (fresh/'provenance.json').exists():
        provenance = read_json(fresh/'provenance.json')
        require(len(provenance['captures']) == len(list(fresh.glob('*.png'))) == 27, 'Production capture count differs')
        for capture in provenance['captures']:
            require(hashlib.sha256((ROOT/capture['path']).read_bytes()).hexdigest() == capture['sha256'], 'Stale production PNG: '+capture['path'])
        runtime=(fresh/'verification.txt').read_text(encoding='utf-8-sig')
        require('COMPLETED' in runtime and not any(s.startswith('FAIL') for s in runtime.splitlines()), 'Production runtime capture incomplete')
        require(provenance['passCount']==sum(s.startswith('PASS ') for s in runtime.splitlines()), 'Production PASS count disagrees with log')

if args.report:
    report = read_json(args.report)
    checks = report['checks']
    require(len(checks) == len(ids) and {c['id'] for c in checks} == expected, 'Completed report needs every ID exactly once')
    for key in ('task', 'date', 'scope', 'readFiles', 'sourceRevision', 'dirtyBaseline'):
        require(bool(report.get(key)), f'Completed report needs {key}')
    for c in checks:
        require(c['status'] in {'PASS', 'FAIL', 'NOT RUN', 'N/A'}, f'Invalid status: {c["id"]}')
        require(c['scope'] in {'documentation', 'preview', 'production'}, f'Invalid scope: {c["id"]}')
        require(type(c['applicable']) is bool, f'Applicability missing: {c["id"]}')
        require(c['applicable'] == (c['status'] != 'N/A'), f'N/A/applicability conflict: {c["id"]}')
        require(bool(c['evidence']) if c['status'] == 'PASS' else bool(c['reason']), f'Evidence/reason missing: {c["id"]}')
        require(c['reason'] != 'Форма ещё не заполнена', f'Report still unfilled: {c["id"]}')

for error in errors:
    print('FAIL', error)
if errors:
    sys.exit(1)
print(f'PASS documentation structure: {len(ids)} registry IDs, {len(files)} linked documents, {len(template["checks"])} unclaimed report checks')
print(f'PASS coverage accounting: {len(rows)} types, {counts}')
print('PASS existing evidence integrity:27 PNG hashes,75 prior runtime PASS,135/103 bindings,10 columns/methods; no new UI execution')
print('PASS authority consistency:', status['version'], status['status'], 'productionMigrationAuthorized=' + str(status['productionMigrationAuthorized']))
if args.report:
    print('PASS completed-report structure; evidence truth still requires review')
