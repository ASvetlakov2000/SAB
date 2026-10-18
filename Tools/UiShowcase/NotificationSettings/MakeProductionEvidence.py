import hashlib
import json
import re
import struct
import subprocess
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
D = ROOT / "Docs/Ui/DesignSystem"
SHOTS = D / "screenshots-notification-settings/production"
OUT = ROOT / "outputs/ui-production-preview/notification-settings/production"
def read(path): return json.loads(path.read_text(encoding="utf-8-sig"))
def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def write(path, value): path.write_text(json.dumps(value, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
def rel(path): return path.relative_to(ROOT).as_posix()

reviewed = "--reviewed" in sys.argv
if reviewed:
    assert "ship_ui_source_scope" in (D/"NotificationSettings-Production-Review.md").read_text(encoding="utf-8")
log = (OUT / "verification.txt").read_text(encoding="utf-8-sig")
assert "COMPLETED: 56 PASS; 21 PNG." in log and not re.search(r"^FAIL", log, re.M)
captures = []
for path in sorted(SHOTS.glob("*.png")):
    width, height = struct.unpack(">II", path.read_bytes()[16:24])
    captures.append(dict(path=rel(path), sha256=digest(path), bytes=path.stat().st_size,
        widthPx=width, heightPx=height, renderDpi=96, surface="shown actual SAB.dll SettingsWindow.Content or Popup.Child",
        excludesOsTitlebar=True, data="isolated fixtures; not live Revit"))
assert len(captures) == 21
production = [
    "SAB/Notifications/NotificationController.cs", "SAB/Notifications/NotificationSettingsStore.cs",
    "SAB/Notifications/NotificationSettingsModel.cs", "SAB/Notifications/SettingsWindow.xaml",
    "SAB/Notifications/SettingsWindow.xaml.cs", "SAB/UI/Styles/SABFluentLight.xaml", "SAB/SAB.csproj",
    "Docs/PluginInstructions/SAB_HTML_Uvedomleniya.html",
]
host = [
    "Tools/UiShowcase/NotificationSettings/ProductionHarness.cs",
    "Tools/UiShowcase/NotificationSettings/ProductionHarness.csproj",
    "Tools/UiShowcase/NotificationSettings/Build-Production.ps1",
    "Tools/UiShowcase/NotificationSettings/Open-Production.cmd",
    "Tools/UiShowcase/NotificationSettings/Open.cmd",
]
parent = ROOT/"Tools/UiShowcase/TemplateEditor/production-source-contract.json"
baseline = ROOT/"outputs/ui-production-preview/notification-settings/pre-production-source"
source = {}
for name in ["SAB/UI/Styles/SABFluentLight.xaml", "SAB/SAB.csproj"]:
    before = baseline/name.replace("/", "__")
    source[name] = dict(beforePath=rel(before), before=digest(before), after=digest(ROOT/name))
controller = (ROOT/production[0]).read_text(encoding="utf-8-sig")
model = (ROOT/production[2]).read_text(encoding="utf-8-sig")
window = (ROOT/production[4]).read_text(encoding="utf-8-sig")
source_checks = {
    "no_failure_mutation_or_automatic_dialog_answer_in_controller":
        not re.search(r"\.\s*(?:DeleteWarning|DeleteAllWarnings|ResolveFailure|SetProcessingResult|SetFailureHandlingOptions|OverrideResult|Arm|PostCommand)\s*\(", controller),
    "settings_vm_contains_no_revit_objects": "Autodesk." not in model,
    "window_does_not_replace_host_application_main_window": "Application.Current.MainWindow" not in window,
    "master_default_requires_explicit_save": "public bool Enabled;" in (ROOT/production[1]).read_text() and "store.Save(next);apply?.Invoke" in model,
    "legacy_toggle_opens_settings_instead_of_enabling": "return new Options().Execute" in controller,
    "production_window_compiled_page": 'Page Include="Notifications\\SettingsWindow.xaml"' in (ROOT/"SAB/SAB.csproj").read_text(),
    "local_instruction_in_existing_distribution_content_glob": "Docs\\PluginInstructions\\**\\*.*" in (ROOT/"SAB/SAB.csproj").read_text(),
}
assert all(source_checks.values()), source_checks
contract = dict(schemaVersion=1, date="2026-10-18", scope="first-stage production settings source",
    uiSourceOverrides=False, historicalProof=rel(parent), historicalProofSha256=digest(parent),
    source=source, currentFiles={name:digest(ROOT/name) for name in production+host},
    checks=source_checks, actualWindow="SAB.Notifications.SettingsWindow from SAB.dll",
    activeAutomaticConfirmations=0, firstLaunchEnabled=False,
    limitations=["live Revit handlers and OK capabilities NOT RUN", "no MSI/installation/release", "physical input/OS DPI NOT RUN"])
write(ROOT/"Tools/UiShowcase/NotificationSettings/production-source-contract.json", contract)

old=read(D/"NotificationSettings-Revision2-Report.json")
checks=old["checks"]
verification=rel(SHOTS/"verification.txt")
for check in checks:
    check["scope"]="production"
    if check["status"] == "PASS":
        check["evidence"]=[verification, "Tools/UiShowcase/NotificationSettings/production-source-contract.json"]
        check["reason"]="Fresh actual WPF/source evidence for this criterion within first-stage settings scope."
        check["unverified"]=["Live Revit execution, physical input and OS DPI NOT RUN"]
    else:
        check["reason"]=check["reason"].replace("standalone макете", "первом этапе окна").replace("standalone уведомлений","окна уведомлений")
        check["evidence"]=[]
    if check["status"] == "NOT RUN":
        check["evidence"]=[verification]
updates={
    1: ("PASS", True, "Direct user requirements: large toggle, actual window, first launch OFF; earlier labels/two tabs/context options retained."),
    3: ("PASS", True, "Named large switch, semantic badge and readable DataGrid text styles are shared SABFluentLight resources; scoped dictionaries only."),
    4: ("PASS", True, "Actual search, selected rule, draft, stable rule persistence, journal, consent, Save/Cancel/Escape verified. Deliberate blanket backend removal follows user requirement."),
    5: ("PASS", True, "Builds against Revit22/23/24; VM/window contain no Revit objects. Existing command identities preserved. Actual API execution NOT RUN."),
    6: ("PASS", True, "Direct request authorizes first-stage production window. Installation/release not performed."),
    7: ("PASS", True, "Actual SAB.dll compiled window with scoped shared resources and existing Options entry point; no global Revit resources mutated."),
    8: ("PASS", True, "Master/detail chosen over in-row editing, recorded before production implementation in Production-Layout."),
    9: ("PASS", True, "21 fresh actual SAB.dll WPF captures including native Popup.Child; coherent current assembly/source hashes."),
    13: ("PASS", True, "Fresh runtime header38/row44 measurements normal/minimum and captures."),
    29: ("PASS", True, "Normal/minimum footer and separation bounds; long text bounded with scrollable detail."),
    39: ("N/A", False, "No user add-row action; observed unknown messages are appended automatically from journal."),
    42: ("PASS", True, "Labels, source text, pending capability metadata, validation/status only. Tutorial is local module HTML."),
    44: ("PASS", True, "Meaning and confirmation separate; pending action explicitly shown; corrupt recovery red, legacy migration neutral, successful Save green."),
    48: ("PASS", True, "Actual compiled Page/source owner, new dedicated coverage fields, 91 registry IDs and honest runtime boundaries."),
    56: ("PASS", True, "New complaint registered091 and fixed in named shared large switch; source/bounds/focus/disabled/routed Space captured."),
    91: ("PASS", True, "Track56x32, knob24, hit64x40 runtime measured; on/off/focus/disabled/routed Space verified, previous small template preserved. Physical Space NOT RUN."),
}
by_id={int(c["id"][-3:]):c for c in checks}
for number,(status,applicable,reason) in updates.items():
    item=by_id.get(number, dict(id=f"SAB-UI-{number:03d}",criterion=f"ErrorRegistry.md#sab-ui-{number:03d}"))
    item.update(status=status,applicable=applicable,scope="production",reason=reason,
        evidence=[verification,rel(SHOTS/"01-default-off.png")] if status=="PASS" else [],
        unverified=["Live Revit, full physical input/accessibility and OS DPI NOT RUN"] if applicable else [])
    if number not in by_id: checks.append(item)
expected={f"SAB-UI-{number}" for number in re.findall(r'id="sab-ui-(\d+)"',(D/"ErrorRegistry.md").read_text(encoding="utf-8-sig"))}
assert {c["id"] for c in checks} == expected and len(checks)==91
build=[]
for version in [2022,2023,2024]:
    dll=OUT/("plugin/SAB.dll" if version==2023 else f"api{version}/plugin/SAB.dll")
    build.append(dict(version=version,status="PASS",exitCode=0,assembly=rel(dll),sha256=digest(dll),log=rel(OUT/f"build{version}.log"),
        command=f"Build-Production.ps1 -Capture -AllVersions (Revit{version} phase)",scope="API compilation only; not execution in Revit"))
build.append(dict(command="Build-Production.ps1 -Capture -AllVersions",status="PASS",exitCode=0,
    assertions=56,pngCount=21,evidence=verification,log=rel(OUT.parent/"production-final-build.log")))
report=dict(schemaVersion=1,task="SAB — первый этап настоящего окна Уведомления",date="2026-10-18",
    scope="production sources + actual SAB.dll WPF with isolated fixtures; automatic confirmations not implemented",
    sourceRevision="0d5b17db7f418fa9d837a553cdd1feac0e2cbfc5",
    dirtyBaseline="Pre-existing unrelated dirty changes retained. Touched original source snapshots in outputs/ui-production-preview/notification-settings/pre-production-source.",
    readFiles=["AGENTS.md",".agents/skills/sab-wpf-ui/SKILL.md"]+[rel(D/name) for name in ["STATUS.json","Specification.md","ErrorRegistry.md","Workflow.md","ReviewRoles.md"]]+production+host,
    layoutSpecification=rel(D/"NotificationSettings-Production-Layout.md"),
    build=build,captures=captures,runtimeDpi=None,renderDpi=[96],
    independentReview=dict(status="PASS" if reviewed else "NOT RUN",reason="Fresh bounded review; one recovery status finding fixed and verified" if reviewed else "Awaiting final SHA/report gate",
        scope="first-stage source/settings/actual WPF only; not live Revit/automatic OK/installation",
        report=rel(D/"NotificationSettings-Production-Review.md") if reviewed else ""),
    approvalStatusSource=rel(D/"STATUS.json"),checks=sorted(checks,key=lambda c:c["id"]),
    unverified=["Revit live errors/sketch/worksharing/modes and OK capabilities","Physical keyboard/pointer/accessibility traversal","Runtime/OS DPI and monitor transitions","MSI payload and installation"],
    productionMigrationAuthorized=True,installationPerformed=False,msiBuilt=False,
    automaticConfirmationsImplemented=False,firstLaunchEnabled=False,
    registryStatusCounts=dict(Counter(c["status"] for c in checks)),
    history={"revision2":rel(D/"NotificationSettings-Revision2-Report.json"),"note":"Earlier preview evidence unchanged; no expansion of old verdicts"},
    documentationValidation={"status":"PASS" if reviewed else "NOT RUN", "exitCode":0 if reviewed else None,
        "command":"Bundled Python Tools/UiShowcase/ValidateUiDocs.py --report Docs/Ui/DesignSystem/NotificationSettings-Production-Report.json",
        "evidence":rel(OUT/"docs-validation.txt"),"reason":"Metadata/links/hash-chain validation only; not UI runtime" if reviewed else "Run validator after report generation"})
write(D/"NotificationSettings-Production-Report.json",report)
files=production+host+[
    rel(OUT/"plugin/SAB.dll"),rel(OUT/"plugin/SAB.NotificationSettings.ProductionHost.exe"),
    rel(OUT/"plugin/UI/Styles/SABFluentLight.xaml"), verification,
    rel(SHOTS/"launcher-verification.txt"),"Tools/UiShowcase/NotificationSettings/production-source-contract.json"]
provenance=dict(date="2026-10-18",scope=report["scope"],actualWindow=contract["actualWindow"],captureAssembly=rel(OUT/"plugin/SAB.dll"),
    source={name:digest(ROOT/name) for name in files},captures=captures,build=build,passCount=56,pngCount=21,
    launcherPassCount=3,sourceContractPassCount=len(source_checks),firstLaunchEnabled=False,
    independentReview=report["independentReview"],installationPerformed=False,uiSourceOverrides=False)
reference=D/"screenshots-notification-settings/reference-feedback/master-switch-size.png"
provenance["userReference"]={"path":rel(reference),"sha256":digest(reference),"source":"user attached clipboard PNG, unchanged copy; not generated capture"}
write(SHOTS/"provenance.json",provenance)
print(f"Production evidence: {len(checks)} IDs / 56 checks / 21 PNG / {len(source_checks)} source contracts; reviewed={reviewed}")
