# SAB UI host — production1.1.0 «Редизайн»

Канон: [STATUS](../../Docs/Ui/DesignSystem/STATUS.json), [Specification](../../Docs/Ui/DesignSystem/Specification.md), [ErrorRegistry70ID](../../Docs/Ui/DesignSystem/ErrorRegistry.md), [Workflow](../../Docs/Ui/DesignSystem/Workflow.md), [ReviewRoles](../../Docs/Ui/DesignSystem/ReviewRoles.md). [DESIGN](DESIGN.md) описывает фактические ресурсы, не задаёт второй стандарт.

Прямое утверждение12.10 разрешило весь показанный комплект «Развёрток», общий notification416/status16Bold/summary outline и tabs40/text14, production migration, штатный MSI и commit+push/release1.1.0 «Редизайн». Установка не разрешена. Два первоначальных модуля сохраняют approval11.10; остальные модули автоматически не мигрируют. Historical pending suite/proposal больше не текущий gate.

## Фактический Compile и owners

Build.ps1 собирает обычный production SAB.csproj в existing outputs/ui-production-preview/plugin без SourcePreview.targets/Compile substitutions. Sab.UiShowcase.csproj компилирует SourcePreview.cs, NotificationPreview.cs, RedesignProduction.cs и отдельно ElevationPreview.cs/generated elevations-source/*.cs. Первые три используют actual SAB.dll; отдельные generated copies обслуживают isolated session adapter и не доказывают production Revit workflow. Gallery/ReferenceView/CompositeControls0.1 исключены; MakeReview.py пишет исторические0.1 артефакты.

Общие owners: SAB/UI/Styles/SABFluentLight.xaml, FluentLight/{Presentation,FluentResources}.cs, parameter FluentTheme.cs; NotificationMessage/Card/Stack и module adapters.9actual Elevations views/classes, VM null-Document guard и catalog CaptureDraft/RestoreDraft round trip перенесены; [migration manifest](elevations-production-migration.json) содержит before/after. Реальные collectors работают при Document, host передаёт fixture options. Global SABWindowStyles/SmallWindows Theme/parameter ReportWindow сохранены.

GenerateSourceViews.py — compatibility entry к read-only source verifier, не UI generator. GenerateElevationPreview.py отдельно обслуживает isolated clones; прежние migration/notification contracts историчны относительно последующих разрешённых изменений. Новый manifest/proof не перезаписывает их.

## Открытие и evidence

[Open.cmd](Open.cmd) открывает готовый module host; [Open-Elevations.cmd](Open-Elevations.cmd) — source-derived интерактивные elevation sessions. LaunchPreview.ps1 повторно использует существующий process нужного режима без rebuild/перезаписи занятого exe; при отсутствии artifact вызывает явную сборку. Из корня: `Build.ps1 -Elevations -Open`; `-Elevations -Capture` снимает isolated session packet, `-Capture` — module packet, `-Notifications` — общий notification packet. Fresh actual Elevations proof создаёт RedesignProduction.Run (`--capture-redesign --out <path>`), который открывает actual production классы/XAML, не clones. Documenter эти команды не запускал.

| Свежий COMPLETED-пакет | Программные PASS / PNG | Граница |
|---|---:|---|
| [screenshots-redesign-production](../../Docs/Ui/DesignSystem/screenshots-redesign-production/verification.txt) |109 /23 | Actual9Elevations classes SAB.dll, fixture options вне Revit. |
| [screenshots-redesign-notifications](../../Docs/Ui/DesignSystem/screenshots-redesign-notifications/verification.txt) |35 /9 | Actual shared416/status16 template, WPF/clipboard fixtures. |
| [screenshots-redesign-modules](../../Docs/Ui/DesignSystem/screenshots-redesign-modules/verification.txt) |81 /27 | Regression двух прежних модулей, actual SAB.dll с synthetic DTO. |
| [screenshots-redesign-sessions](../../Docs/Ui/DesignSystem/screenshots-redesign-sessions/verification.txt) |635 /44 | Isolated source-derived session adapters, не actual API/ExternalEvent. |

[Inventory](../../Docs/Ui/DesignSystem/Elevations-Inventory.md)/[Layout](../../Docs/Ui/DesignSystem/Elevations-Layout.md)/[historical verification](../../Docs/Ui/DesignSystem/Elevations-Verification.md) описывают происхождение suite;635/44 старого screenshots-elevations-preview — historical approval proof. RoomPlanSchemeSettingsWindow unreachable и не десятое active окно; main сохраняет функцию плана-схемы. Historical75/27 preview0.5 и предыдущий migration/notification scoped ship не новый release verdict.

WPF RenderTargetBitmap и программный focus не physical input;144dpi render не OS150%. Revit Document/transactions/native selection/ExternalEvent, фактический persistence/runtime установленного плагина, physical keyboard/accessibility, OS monitor/DPI transitions и browser launch NOT RUN. Current independent review/MSI/release оформляет builder отдельно; наличие COMPLETED не означает all70ID PASS или ship.

## Поставка и покрытие

Текущий shared template: tabs Auto/min40DIP/text14/padding20,8; modes min34/30/inset2; notification≤416DIP/status16Bold/outlined selectable summary, bottom-right/newest-bottom/grows-up/gap12,10active seconds с паузой/остатком. Positive-only elevation result и optional filled warning block не заменяют parameter ReportWindow.

Build-Msi.bat должен включать текущие DLL/XAML/styles/Toolkit/HTML для2022/2023/2024. Текущий1.1.0 MSI требует нового payload verification; старый Installer PASS не использовать как новый. Commit+push/release разрешены, результат не объявлять до remote verification. Установка не разрешена. Канонический рабочий output — outputs/ui-production-preview/Installer/output, без timestamp каталогов на каждый прогон и без удаления исторических evidence.

[Coverage](../../Docs/Ui/DesignSystem/component-coverage.csv):51тип=31current/11resource/9missing;29/11/11 и28/11/12 — прежние этапы. Current example не all-states/in-Revit PASS. [CoveragePlan](../../Docs/Ui/DesignSystem/CoveragePlan.md) сохраняет детали и оставшиеся среды; historical pending в прежних документах относится к моменту до нового approval12.10.
