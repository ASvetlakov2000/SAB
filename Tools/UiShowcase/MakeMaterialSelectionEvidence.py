"""Record actual material-picker WPF and extracted MSI proof without changing UI."""
from pathlib import Path
import hashlib
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
DOC = ROOT / 'Docs/Ui/DesignSystem'
SHOTS = DOC / 'screenshots-material-selection'
OUT = ROOT / 'outputs/ui-production-preview'
GIT = r'C:\Users\svetl\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe'

def read(path): return path.read_text(encoding='utf-8-sig')
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def rel(path): return path.relative_to(ROOT).as_posix()
def save(path, value): path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')

revision = subprocess.check_output([GIT, 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
command = ROOT / 'SAB/Cls_FilledRegionFromMaterial/CreateFilledRegionTypesCommand.cs'
before = subprocess.check_output([GIT, 'show', f'{revision}:{rel(command)}'], cwd=ROOT).decode('utf-8-sig').replace('\r\n', '\n')
after = read(command).replace('\r\n', '\n')
boundary = '        private static SyncReport Synchronize('
assert before[before.index(boundary):] == after[after.index(boundary):], 'Existing synchronization and transaction bodies changed'
assert 'MaterialScanner.Scan(document, true, true)' in after
assert 'scan.Materials.Where(material => selectedIds.Contains(material.UniqueId))' in after
for name in ('MaterialSelectionItem.cs', 'MaterialSelectionWindow.xaml.cs'):
    assert 'Autodesk.Revit' not in read(command.parent / name), 'UI retains Revit dependency'
log = read(SHOTS / 'verification.txt')
assert 'COMPLETED; assertions=42; captures=9' in log and not re.search(r'^FAIL', log, re.M)
captures = [{'path': rel(p), 'sha256': sha(p)} for p in sorted(SHOTS.glob('*.png'))]
assert len(captures) == 9
owners = [command, command.parent/'MaterialSelectionItem.cs', command.parent/'MaterialSelectionWindow.xaml',
          command.parent/'MaterialSelectionWindow.xaml.cs', command.parent/'SettingsCommand.cs', command.parent/'SettingsWindow.xaml',
          ROOT/'SAB/Cls_MainPanel/MainPanel.cs', ROOT/'SAB/UI/FluentLight/FluentResources.cs', ROOT/'SAB/Properties/AssemblyInfo.cs', ROOT/'SAB/SAB.csproj']
source_before = {}
for owner in owners:
    original = subprocess.run([GIT, 'show', f'{revision}:{rel(owner)}'], cwd=ROOT, capture_output=True)
    if original.returncode == 0:
        source_before[rel(owner)] = hashlib.sha256(original.stdout).hexdigest()
installers = []
for year in (2022, 2023, 2024):
    item = json.loads(read(OUT / f'msi-check/{year}/verification.json'))
    assert item['status'] == 'PASS' and item['productVersion'] == '1.1.3' and item['assemblyVersion'] == '1.1.3.0'
    assert item['msiSha256'].lower() == sha(ROOT / f'Installer/output/SAB_Revit_{year}.msi')
    installers.append(item)
save(DOC/'MaterialSelection-Installer.json', {'status': 'PASS', 'version': '1.1.3',
    'command': 'Installer/Build-All-Msi.ps1 -Configuration Release', 'exitCode': 0,
    'additionalCheck': 'Publish-GitHubRelease.ps1 -Version 1.1.3 -PrepareOnly: exit0; all22guides/CSS/Toolkit; no host libraries',
    'compiledBaml': 'cls_filledregionfrommaterial/materialselectionwindow.baml verified in extracted SAB.g.resources',
    'installationPerformed': False, 'results': installers})
proof = {'date': '2026-10-16', 'version': '1.1.3', 'sourceRevision': revision,
    'dirtyBaseline': 'clean working tree at task start; this source revision is before material-picker commit',
    'source': {rel(p): sha(p) for p in owners}, 'sourceBeforeGitBlobSha256': source_before,
    'historicalProof': 'Docs/Ui/DesignSystem/SelectionGuidance-Provenance.json',
    'historicalProofSha256': sha(DOC/'SelectionGuidance-Provenance.json'),
    'actualAssembly': rel(OUT/'plugin/SAB.dll'), 'actualAssemblySha256': sha(OUT/'plugin/SAB.dll'),
    'command': 'Tools/UiShowcase/Build.ps1 -MaterialSelection', 'exitCode': 0,
    'nativeAssertions': 42, 'captures': captures, 'runtimeDpi': 'NOT RUN: actual OS monitor DPI transitions',
    'renderDpi': 96, 'surface': 'actual production MaterialSelectionWindow with synthetic detached rows outside Revit',
    'uiSourceOverrides': False, 'applicationResources': 'clean; shared dictionary scoped to window',
    'synchronizationUnchanged': True, 'synchronizationTailSha256': hashlib.sha256(after[after.index(boundary):].encode()).hexdigest(),
    'allDocumentCatalogueAndSelectedStableIds': 'source-contract PASS; native model execution NOT RUN',
    'uiApiDependency': 'no Autodesk.Revit reference in DTO or window',
    'logSha256': sha(SHOTS/'verification.txt'), 'installer': 'Docs/Ui/DesignSystem/MaterialSelection-Installer.json',
    'unverified': ['live Revit material collectors/transactions/model writes', 'physical Shift/Ctrl/keyboard/mouse input',
                   'full accessibility traversal', 'OS DPI transitions', 'installation']}
save(DOC/'MaterialSelection-Provenance.json', proof)
save(SHOTS/'provenance.json', proof)
print('PASS: 42 actual WPF assertions / 9 PNG; unchanged synchronization; three fresh MSI1.1.3 payload proofs')
