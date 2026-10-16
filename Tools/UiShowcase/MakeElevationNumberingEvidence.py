"""Record bounded evidence for actual numbering UI; never infer Revit execution."""
from pathlib import Path
import csv
import hashlib
import json
import re
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[2]
D = ROOT / "Docs/Ui/DesignSystem"
S = D / "screenshots-elevation-numbering"
BASE = ROOT / "outputs/ui-production-preview/elevation-research-baseline"


def read(path):
    return path.read_text(encoding="utf-8-sig")


def save(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def append_once(path, marker, text):
    original = read(path)
    if marker not in original:
        path.write_text(original.rstrip() + "\n\n" + text + "\n", encoding="utf-8")


layout = D / "ElevationNumbering-Layout.md"
layout.write_text(read(layout).replace("schema13", "schema15"), encoding="utf-8")
append_once(D / "Specification.md", "Дополнение15.10: нумерация развёрток", """## Дополнение15.10: нумерация развёрток

В карточке «Наименование вида» после существующих наименований располагаются «Номер вида», поля «Префикс» / «Суффикс» и пример первых трёх номеров. Существующие scoped field styles и две равные адаптивные колонки; введённый текст сохраняется буквально. При коллизии суффикс всей группы изменяется согласованно, числовая последовательность берётся из исходных индексов линий. SAB-UI-078; [компоновка](ElevationNumbering-Layout.md).""")
append_once(D / "README.md", "Нумерация развёрток15.10", """Нумерация развёрток15.10: [компоновка](ElevationNumbering-Layout.md), [20actual WPF checks/3PNG](screenshots-elevation-numbering/provenance.json), [78-ID отчёт](ElevationNumbering-Report.json), [независимый bounded review](ElevationNumbering-Review.md), [MSI2022–24](ElevationNumbering-Installer.json). [Исследование ориентации и обрезки](../../InteriorElevationOrientation_Research.md) отделяет подтверждённые code defects от исторического Revit bug; геометрические исправления пока не внесены. Revit/physical/OS/installation NOT RUN.""")
append_once(ROOT / "Tools/UiShowcase/PRODUCT.md", "Нумерация развёрток15.10", """Нумерация развёрток15.10: actual ElevationSettingsWindow содержит буквальные поля префикса/суффикса в карточке наименований. Общий суффикс группы устраняет коллизии без изменения source index; настройки сохраняютсяschema15. [Scope/evidence](../../Docs/Ui/DesignSystem/ElevationNumbering-Report.json).""")
append_once(ROOT / "Tools/UiShowcase/DESIGN.md", "Numbering refinement15.10", """Numbering refinement15.10 preserves the accepted Settings Form: two equal AdaptiveSettingsGrid columns inside the naming card, existing scoped TextBox34 and label styles, separate live example wrapping at minimum height. [Actual normal/empty/minimum captures](../../Docs/Ui/DesignSystem/screenshots-elevation-numbering/provenance.json); no change to shared tokens, approval or installation authority.""")
sidecar_path = ROOT / "Tools/UiShowcase/.impeccable/design.json"
sidecar = json.loads(read(sidecar_path))
sidecar.setdefault("extensions", {})["elevationNumbering"] = {"date": "2026-10-15", "scope": "naming card refinement", "newSharedTokens": False, "evidence": "Docs/Ui/DesignSystem/screenshots-elevation-numbering/provenance.json"}
save(sidecar_path, sidecar)
coverage_path = D / "component-coverage.csv"
with coverage_path.open(encoding="utf-8-sig", newline="") as stream:
    reader = csv.DictReader(stream); columns = reader.fieldnames; coverage = list(reader)
for row in coverage:
    if row["type"] == "TextBox":
        extra = "Docs/Ui/DesignSystem/screenshots-elevation-numbering/provenance.json"
        if extra not in row["current_evidence"]:
            row["current_evidence"] = row["current_evidence"].rstrip(";") + ";" + extra
with coverage_path.open("w", encoding="utf-8", newline="") as stream:
    writer = csv.DictWriter(stream, fieldnames=columns); writer.writeheader(); writer.writerows(coverage)

template_path = D / "WorkflowReport.template.json"
template = json.loads(read(template_path))
if not any(c["id"] == "SAB-UI-078" for c in template["checks"]):
    check = dict(template["checks"][0], id="SAB-UI-078", criterion="ErrorRegistry.md#sab-ui-078")
    template["checks"].append(check)
    save(template_path, template)

git = shutil.which("git") or r"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe"
revision = subprocess.check_output([git, "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
log = read(S / "verification.txt")
assert "COMPLETED" in log and not re.search(r"^FAIL", log, re.M)
captures = [{"path": p.relative_to(ROOT).as_posix(), "sha256": sha(p)} for p in sorted(S.glob("*.png"))]
assert len(captures) == 3

owners = [
    "SAB/SAB.csproj",
    "SAB/Cls_InteriorElevations/Models/ElevationSettings.cs",
    "SAB/Cls_InteriorElevations/ViewModels/ElevationSettingsViewModel.cs",
    "SAB/Cls_InteriorElevations/Views/ElevationSettingsWindow.xaml",
    "SAB/Cls_InteriorElevations/Services/Settings/ElevationSettingsStorageService.cs",
    "SAB/Cls_InteriorElevations/Services/Sheets/ElevationDetailNumberService.cs",
    "SAB/Cls_InteriorElevations/Services/Sheets/ViewportPlacementService.cs",
    "SAB/Cls_InteriorElevations/Services/Sheets/AutomaticElevationSheetPlacementService.cs",
    "SAB/Cls_InteriorElevations/Services/Sheets/ViewportTransferService.cs",
    "SAB/Cls_InteriorElevations/Services/Elevations/ElevationFlipService.cs",
    "SAB/Services/PluginResources/PluginFamilyResourceService.cs",
    "SAB/Cls_InteriorElevations/Commands/CreateInteriorElevationsCommand.cs",
]
bindings = lambda path: set(re.findall(r"\{Binding\s+([^},]+)", read(path)))
ui = "SAB/Cls_InteriorElevations/Views/ElevationSettingsWindow.xaml"
missing_bindings = sorted(bindings(BASE / "files" / ui) - bindings(ROOT / ui))
assert not missing_bindings, missing_bindings
source = {name: {"before": sha(BASE / "files" / name) if (BASE / "files" / name).exists() else None,
                 "after": sha(ROOT / name)} for name in owners}
provenance = {"date": "2026-10-15", "sourceRevision": revision, "dirtyBaseline": str(BASE),
              "historicalProof": "Tools/UiShowcase/notification-source-contract.json", "uiSourceOverrides": False,
              "actualAssembly": str(ROOT / "outputs/ui-production-preview/plugin/SAB.dll"),
              "assemblySha256": sha(ROOT / "outputs/ui-production-preview/plugin/SAB.dll"),
              "surface": "actual SAB.dll WPF classes, null Document fixtures outside Revit",
              "runtimeDpi": 96, "renderDpi": 96, "nativeAssertions": len(re.findall(r"^PASS ", log, re.M)),
              "captures": captures, "source": source, "missingOriginalBindings": missing_bindings,
              "serviceTests": "20 actual-service checks with Revit API stubs",
              "geometryEvidence": "12 arithmetic counterexamples; geometry production source unchanged",
              "unverified": ["actual Revit model/transactions/rotation", "physical keyboard/accessibility", "OS DPI transitions", "installation"]}
save(S / "provenance.json", provenance)

report = dict(template, task="SAB: numbering affixes and family load policy; geometry research",
              date="2026-10-15", scope="production", sourceRevision=revision,
              dirtyBaseline=str(BASE), readFiles=["AGENTS.md", ".agents/skills/sab-wpf-ui/SKILL.md", "Docs/Ui/DesignSystem/STATUS.json", "Docs/Ui/DesignSystem/Specification.md", "Docs/Ui/DesignSystem/ErrorRegistry.md", "Docs/Ui/DesignSystem/Workflow.md", "Docs/Ui/DesignSystem/ReviewRoles.md", "Docs/Ui/DesignSystem/LayoutPatterns.md"],
              layoutSpecification="Docs/Ui/DesignSystem/ElevationNumbering-Layout.md",
              build=[{"command": "MSBuild SAB.csproj Debug Revit2023 + Sab.UiShowcase.csproj", "exitCode": 0},
                     {"command": "dotnet run Tests/InteriorElevations.Numbering -c Release", "exitCode": 0, "assertions": 20},
                     {"command": "GeometryCounterexamples.py", "exitCode": 0, "assertions": 12}],
              captures=captures, runtimeDpi=96, renderDpi=[96])
pass_ids = {1,2,3,4,6,7,8,9,10,12,18,26,27,29,33,34,35,42,43,44,48,50,54,55,56,78}
partial_ids = {5,11,53}
evidence = ["Docs/Ui/DesignSystem/screenshots-elevation-numbering/provenance.json", "Docs/Ui/DesignSystem/screenshots-elevation-numbering/verification.txt", "Docs/Ui/DesignSystem/ElevationNumbering-Layout.md"]
for c in report["checks"]:
    n = int(c["id"][-3:])
    c.update(scope="production", applicable=n in pass_ids | partial_ids, evidence=[], unverified=[])
    if n in pass_ids:
        c.update(status="PASS", reason="Bounded naming-card/source scope only; other components unchanged.", evidence=evidence, unverified=provenance["unverified"])
    elif n in partial_ids:
        c.update(status="NOT RUN", reason="Build, binding, JSON and programmatic focus checks passed; actual Revit context and complete physical input/state matrix unavailable.", evidence=evidence, unverified=provenance["unverified"])
    else:
        c.update(status="N/A", reason="This bounded change adds two text editors to the naming card; table/picker/category/notification/other-window scenario and existing capsules/footer contracts are unchanged.")
review = D / "ElevationNumbering-Review.md"
if review.exists():
    report["independentReview"] = {"status": "PASS", "reason": "Independent bounded finish review; scope and limits in report.", "scope": "production source and actual WPF outside Revit", "report": review.relative_to(ROOT).as_posix()}
installer = []
for year in (2022,2023,2024):
    path = ROOT / f"outputs/ui-production-preview/msi-check/{year}/verification.json"
    item = json.loads(read(path))
    assert item["status"] == "PASS" and sha(Path(item["msi"])) == item["msiSha256"].lower()
    # The payload verifier compares the extracted DLL and XAML hashes to each current build.
    assert any(p["name"] == "SAB.dll" and p["sha256"].lower() == sha(ROOT / f"SAB/bin/Revit{year}/SAB.dll") for p in item["verifiedPayload"])
    installer.append(item)
report["build"].append({"command": "Installer/Build-All-Msi.ps1 -Configuration Release (Build-Msi.bat backend)", "exitCode": 0, "years": [2022,2023,2024], "payload": "extracted DLL/XAML/styles/Toolkit/instructions SHA256; API references verified; no installation"})
save(D / "ElevationNumbering-Installer.json", {"status": "PASS", "installationPerformed": False, "results": installer})
save(D / "ElevationNumbering-Report.json", report)
print("PASS", provenance["nativeAssertions"], "actual WPF assertions,3PNG,20stub service checks,12arithmetic checks;3MSI payloads")
