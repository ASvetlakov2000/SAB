# Проверка исправлений развёрток1.1.1

13.10.2026; базаc5ce558fdae37e90259328ac11005af8982a5ef2, до задачи исходники чистые. Последнее название — **Список помещений**. Actual классы SAB.dll используют штатные production исходники. Исторические пакеты1.1.0 не изменены.

| Проверка | Фактический результат |
| --- | --- |
| Clean Application до исправления | Воспроизведён ResourceReferenceKeyNotFoundException: SabBrush.DisabledBackground. |
| Clean Application после исправления | PASS; success/warning/error, warning/ID/support создаются с собственным scoped словарём. |
| Actual production WPF |282PASS/17PNG; normal/minimum800,4tabs,100long namingcontexts,18-line validation и readonlylist0/3/100. |
| Shared notifications |35PASS/9PNG; clipboard/timer/stack/overflow, чистые Application resources. |
| Source-derived sessions |635PASS/44PNG; это отдельные session adapters, не запуск команд Revit. |
| Source contract |49boundedchecks;66baselinehashes unchanged,8functional deltas и9historical migrationviews сохранены;10hotfixowners отдельным manifest. |
| Portable hashes |116files: LF/CRLF allowed, content mutation rejected. |
| Safe result delivery | Actual helper: success, injected primary UI failure, injected fallback/logfailure; исключения не уходят в обработчик отказа команды. |
| Native readonly list | Actual footer→owned modal child→sameVM; detachedimmutable snapshot, noedit/add/delete; fullheaderwidth иlastrowbounds при740×420. |
| Build-Msi.bat | Release2022/2023/2024, exit0; Assembly1.1.1.0, ProductVersion1.1.1. |
| MSI payload | ИзвлечённыйCAB: актуальнаяDLL, всеlooseXAML inclновыйсписок, styles,Toolkit,23HTML; SHA256 matching, APIreferences22/23/24, nohostlibraries/testartifacts. |
| Upgrade identity | ProductVersion1.1.1, stableyearUpgradeCode соответствует неизменённому installer source. |
| Independent review | Fullreview обнаружил1P2; послеfix boundedVerdictPass resolved/ship дляUI/code. MSIgate также PASS для всех трёх пакетов. |

[Снимки](ElevationHotfix.html), [точный provenance](screenshots-elevation-hotfix/provenance.json), [actuallog](screenshots-elevation-hotfix/verification.txt), [notifications](screenshots-elevation-hotfix/notifications/verification.txt), [sessions](screenshots-elevation-hotfix/sessions/verification.txt), [73-IDreport](ElevationHotfix-Report.json), [independentreview](ElevationHotfix-Review.md), [MSIproof](ElevationHotfix-Installer.json).

Команды: `Tools/UiShowcase/Build.ps1 -ElevationHotfix`; корневой `Build-Msi.bat` без аргументов; `Tools/UiShowcase/MakeElevationHotfixInstallerEvidence.ps1`; `Tools/UiShowcase/SourceHash.py`; `Tools/UiShowcase/ValidateUiDocs.py --report Docs/Ui/DesignSystem/ElevationHotfix-Report.json`. Повторные сборки используют outputs/ui-production-preview и Installer/output.

Runtime96DPI;144DPI только offscreen notification render. **NOT RUN**: реальные операции/транзакции/откат/нативный выбор/ExternalEvent вRevit, nativeAPI22runtime, physicalkeyboard/accessibility, OSmonitor/DPItransitions, установка. Локальные MSI собраны и проверены; публикация нового GitHubrelease не выполнялась. Опубликованный1.1.0 остаётся историческим выпуском.
