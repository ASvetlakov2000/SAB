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
    parameter_path = HERE/'parameter-check-delta.json'
    parameter = json.loads(parameter_path.read_text(encoding='utf8')) if parameter_path.exists() else {'files':{}}
    checks = []
    def check(ok, name):
        if not ok: raise RuntimeError(name)
        checks.append(name)
    for name, expected in historical['files'].items():
        if name not in baseline['authorizedFiles']:
            if name in parameter['files']:
                check(parameter['files'][name]['before'] == expected, 'Parameter fix follows preserved migration: '+name)
                expected = parameter['files'][name]['after']
            check(matches(ROOT/name, expected), 'Unchanged migration source or exact parameter fix: '+name)
    for name, rules in baseline['presentationChanges'].items():
        current = (ROOT/name).read_text(encoding='utf8')
        if name in parameter.get('presentationReversals', {}):
            reversal = parameter['presentationReversals'][name]
            check(current.count(reversal['new']) == 1, 'Exact parameter check workflow change: '+name)
            current = current.replace(reversal['new'], reversal['old'], 1)
            check(sha(current.encode()) == reversal['beforeNormalizedSha256'], 'All other parameter workflow source preserved: '+name)
        for rule in rules['replacements']:
            check(current.count(rule['new']) == 1, 'Exact authorized presentation change: '+rule['name'])
            current = current.replace(rule['new'], rule['old'], 1)
        check(sha(current.encode()) == rules['originalNormalizedSha256'], 'All other source bytes preserved after exact UI reversal: '+name)
    small = (ROOT/'SAB/Cls_ParameterTools/UI/SmallWindows.cs').read_text(encoding='utf8')
    toast = 'internal static class Toast'; start = small.index(toast)
    stripped = small[:start] + small[start:].replace(toast+ '\n    '+body(small, toast), '', 1)
    check(sha(stripped.encode()) == baseline['smallWindowsWithoutToastSha256'], 'ReportWindow and every non-Toast class preserved exactly')
    project = (ROOT/'SAB/SAB.csproj').read_text(encoding='utf8')
    hotfix_path = HERE/'elevations-hotfix-delta.json'
    if hotfix_path.exists():
        hotfix = json.loads(hotfix_path.read_text(encoding='utf8'))
        for entry in hotfix['projectEntries']:
            check(project.count(entry) == 1, 'Explicit hotfix project entry compiled/copied once: '+entry.strip())
            project = project.replace(entry, '', 1)
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
    for name, change in parameter['files'].items():
        check(matches(ROOT/name, change['after']), 'Exact authorized parameter check delta: '+name)
    if parameter['files']:
        workflow = (ROOT/'SAB/Cls_ParameterTools/Workflow.cs').read_text(encoding='utf8')
        quick = body(workflow, 'internal static void Check(UIApplication app)')
        check(all(token not in quick for token in ('ShowDialog', 'CheckCategoriesWindow', 'ReportWindow', 'ShowResult', 'Targets(')), 'Immediate check has no settings/report or fill-scope enumeration')
        check(quick.count('Подсветка элементов активирована') == 1 and quick.index('ViewFilterService.Apply') < quick.index('UI.Toast.Show'), 'Only successful filter activation notifies with exact user text')
        settings = body(workflow, 'internal static void CheckSettings(UIApplication app)')
        check('profile.CheckCategoryIds = window.CategoryIds' in settings and 'profile.CategoryIds =' not in settings, 'Check settings save only independent check scope')
        check('settings.Commit() != TransactionStatus.Committed' in settings and settings.index('settings.Commit()') < settings.index('Check(app)'), 'Check settings require committed storage before activation')
        catalog = (ROOT/'SAB/Cls_ParameterTools/Catalog.cs').read_text(encoding='utf8')
        check('return CheckCategories(Categories(doc, profile), profile.CheckCategoryIds)' in catalog, 'Check and fill reuse exact full category catalog')
        categories = (ROOT/'SAB/Cls_ParameterTools/UI/CategorySelectionControl.cs').read_text(encoding='utf8')
        check(sha(categories.split('    internal sealed class CheckCategoriesWindow')[0].encode()) == parameter['categoryControlUnchangedPrefixSha256'], 'Existing shared category search/selection/import/export implementation unchanged')
        ribbon = (ROOT/'SAB/Cls_ParameterTools/ParameterToolsModule.cs').read_text(encoding='utf8')
        check(ribbon.count('Data<CheckParametersCommand>("Check",') == 1, 'Incumbent check command and ribbon identity retained for hotkeys')
        labels = ['"Проверка параметров"', '"Настройки проверок"', '"Снять подсветку"']
        check([ribbon.index(label) for label in labels] == sorted(ribbon.index(label) for label in labels), 'Three stacked ribbon buttons follow exact requested order')
        commands = (ROOT/'SAB/Cls_ParameterTools/Commands.cs').read_text(encoding='utf8')
        check('CheckSettingsCommand : Nice3point.Revit.Toolkit.External.ExternalCommand' in commands, 'New settings entry uses Toolkit command base')
    files = list(historical['files']) + ['SAB/Cls_ParameterTools/Workflow.cs','SAB/Cls_ParameterTools/Commands.cs','SAB/Cls_ParameterTools/UI/ParameterNotifications.cs','SAB/UI/SabWindowBehaviorService.cs'] + ['SAB/UI/Notifications/'+n for n in ('NotificationMessage.cs','NotificationCard.cs','NotificationStack.cs')]
    files = list(dict.fromkeys(files + list(parameter['files'])))
    proof = {'version':'notification-production-2026-10-12','authorization':'Direct user request; Notification-Layout.md','historicalProof':'production-source-contract.json','uiSourceOverrides':False,'checks':checks,'files':{name:sha((ROOT/name).read_bytes()) for name in files}}
    (HERE/'notification-source-contract.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    print('PASS notification source contract:', len(checks), 'bounded checks; old migration proof retained')
if __name__ == '__main__': verify()
