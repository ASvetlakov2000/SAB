"""Bounded successor to the immutable 0.5 migration proof; never writes UI or old evidence."""
from pathlib import Path
import hashlib, json
from VerifyProductionSource import body, inventory
from collections import Counter
from SourceHash import matches

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
def sha(data): return hashlib.sha256(data).hexdigest()

def verify():
    historical = json.loads((HERE/'production-source-contract.json').read_text(encoding='utf8'))
    baseline = json.loads((HERE/'notification-baseline.json').read_text(encoding='utf8'))
    checks = []
    def check(ok, name):
        if not ok: raise RuntimeError(name)
        checks.append(name)
    for name, expected in historical['files'].items():
        if name not in baseline['authorizedFiles']:
            check(matches(ROOT/name, expected), 'Unchanged migration source: '+name)
    for name, rules in baseline['presentationChanges'].items():
        current = (ROOT/name).read_text(encoding='utf8')
        for rule in rules['replacements']:
            check(current.count(rule['new']) == 1, 'Exact authorized presentation change: '+rule['name'])
            current = current.replace(rule['new'], rule['old'], 1)
        check(sha(current.encode()) == rules['originalNormalizedSha256'], 'All other source bytes preserved after exact UI reversal: '+name)
    small = (ROOT/'SAB/Cls_ParameterTools/UI/SmallWindows.cs').read_text(encoding='utf8')
    toast = 'internal static class Toast'; start = small.index(toast)
    stripped = small[:start] + small[start:].replace(toast+ '\n    '+body(small, toast), '', 1)
    check(sha(stripped.encode()) == baseline['smallWindowsWithoutToastSha256'], 'ReportWindow and every non-Toast class preserved exactly')
    project = (ROOT/'SAB/SAB.csproj').read_text(encoding='utf8')
    for entry in baseline['compileEntries']:
        check(project.count(entry) == 1, 'New component compiled once: '+entry.strip())
        project = project.replace(entry, '', 1)
    check(sha(project.encode()) == baseline['projectNormalizedSha256'], 'No other project changes')
    resources = (ROOT/'SAB/UI/Styles/SABFluentLight.xaml').read_text(encoding='utf8')
    original = resources[:resources.index('    <!-- Shared notification template:')] + '</ResourceDictionary>'
    # Exact successor reversal of approved 1.1.0 tab presentation; historical proof stays intact.
    original = original.replace('<Style TargetType="TabItem">\n    <Setter Property="Height" Value="Auto" />\n    <Setter Property="MinHeight" Value="40" />\n    <Setter Property="FontSize" Value="14" />\n    <Setter Property="Padding" Value="20,8" />', '<Style TargetType="TabItem">\n    <Setter Property="Height" Value="30" />\n    <Setter Property="Padding" Value="16,0" />', 1)
    check(sha(original.encode()) == baseline['resourcesPrefixNormalizedSha256'], 'Existing shared styles unchanged; only explicit notification styles added')
    original_contract = json.loads((HERE/'production-baseline.json').read_text(encoding='utf8'))
    for name, expected in original_contract['bindingInventories'].items():
        check(not (Counter(expected)-inventory(ROOT/name)), 'All original bindings still retained: '+name)
    settings = (ROOT/'SAB/Cls_ParameterTools/UI/SettingsWindow.cs').read_text(encoding='utf8')
    for method in original_contract['methods']:
        check(sha(body(settings, method['method']).replace('FluentTheme.', 'Theme.').encode()) == method['sha256'], 'Settings business method preserved: '+method['method'])
    for name in ('NotificationMessage.cs', 'NotificationCard.cs', 'NotificationStack.cs'):
        text = (ROOT/'SAB/UI/Notifications'/name).read_text(encoding='utf8')
        check('Autodesk.Revit' not in text, 'Generic notification has no Revit dependency: '+name)
    files = list(historical['files']) + ['SAB/Cls_ParameterTools/Workflow.cs','SAB/Cls_ParameterTools/Commands.cs','SAB/Cls_ParameterTools/UI/ParameterNotifications.cs','SAB/UI/SabWindowBehaviorService.cs'] + ['SAB/UI/Notifications/'+n for n in ('NotificationMessage.cs','NotificationCard.cs','NotificationStack.cs')]
    proof = {'version':'notification-production-2026-10-12','authorization':'Direct user request; Notification-Layout.md','historicalProof':'production-source-contract.json','uiSourceOverrides':False,'checks':checks,'files':{name:sha((ROOT/name).read_bytes()) for name in files}}
    (HERE/'notification-source-contract.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    print('PASS notification source contract:', len(checks), 'bounded checks; old migration proof retained')
if __name__ == '__main__': verify()
