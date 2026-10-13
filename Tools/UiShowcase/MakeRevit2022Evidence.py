"""Record the separate API22 capture packet; never rewrite the reviewed API23 packet."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('--api-directory', required=True, type=Path)
parser.add_argument('--plugin-directory', required=True, type=Path)
parser.add_argument('--capture-directory', required=True, type=Path)
args = parser.parse_args()
capture = args.capture_directory.resolve()
assert capture == ROOT / 'Docs/Ui/DesignSystem/screenshots-production-2022', 'API22 packet must stay separate'
log = (capture / 'verification.txt').read_text(encoding='utf-8-sig').splitlines()
assert 'COMPLETED' in log and not any(line.startswith('FAIL') for line in log), 'Capture did not complete successfully'
loaded = [line for line in log if line.startswith('Loaded RevitAPI:')]
assert len(loaded) == 1 and loaded[0].startswith('Loaded RevitAPI:22.'), 'Host must load API22'
expected_api = (args.api_directory / 'RevitAPI.dll').resolve()
loaded_path = Path(loaded[0].split(';path:', 1)[1]).resolve()
assert loaded_path == expected_api, 'Loaded API must be the configured dependency'
images = sorted(capture.glob('*.png'))
assert len(images) == 27, 'Expected all 27 native capture scenarios'
def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()
contract = json.loads((ROOT / 'Tools/UiShowcase/production-source-contract.json').read_text(encoding='utf-8'))
assert all(sha(ROOT / path) == value for path, value in contract['files'].items()), 'Production source changed after migration evidence'
packet = {
    'version': '0.5-production-Revit2022',
    'origin': 'Actual production SAB classes; built against API22; synthetic DTOs outside Revit; no UI substitutions',
    'runtimeRevitApi': loaded[0],
    'apiFiles': {name: {'path': str(args.api_directory / name), 'sha256': sha(args.api_directory / name)} for name in ('RevitAPI.dll', 'RevitAPIUI.dll')},
    'assemblySha256': sha(args.plugin_directory / 'SAB.dll'),
    'sourceFiles': contract['files'],
    'hostSourceSha256': sha(ROOT / 'Tools/UiShowcase/SourcePreview.cs'),
    'passCount': sum(line.startswith('PASS') for line in log),
    'captures': [{'path': str(path.relative_to(ROOT)).replace('\\', '/'), 'sha256': sha(path)} for path in images],
    'limitations': ['No Revit execution/transactions/ExternalEvent', 'No physical input or system DPI transitions', '144dpi is offscreen rendering', 'No installation'],
}
(capture / 'provenance.json').write_text(json.dumps(packet, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f"PASS API22 evidence: {packet['passCount']} runtime checks; {len(images)} PNG hashes; loaded API22 verified")
