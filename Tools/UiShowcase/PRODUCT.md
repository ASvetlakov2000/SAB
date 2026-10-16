# SAB UI Preview

<!-- impeccable:product-schema 1 -->

## Platform

Windows desktop / WPF, .NET Framework 4.8. Хост изолированного SAB/Revit 2023 использует установленные Autodesk DLL, но не передаёт Document и не требует запущенного Revit.

## Users

Пользователь SAB и разработчик оценивают два новых представления существующих функций.

## Product Purpose

Новый Fluent Light по пользовательскому референсу. «Параметризация» и «Создание видов и листов» берут поля, модели, VM, команды и обработчики из исходного плагина. Из картинки берётся оформление, а не выдуманная логика.

## Capabilities and Constraints

Host собирает actual production SAB и открывает реальные классы/ресурсы с synthetic DTO, без UI Compile substitutions и Document. Shared layer: SABFluentLight.xaml, FluentLight/{Presentation,FluentResources}.cs и parameter FluentTheme.cs. Main/secondary code-behind/geometry persistence, legacy Theme/global dictionary и ReportWindow сохранены; SmallWindows.Toast стал forwarding wrapper. Production-baseline/source-contract проверяют135/103 исходных bindings,10колонок и10business bodies с единственной helper-rename normalization; кодовая проверка не является runtime PASS.

## Evidence on Hand

Historical pre-migration source-contract.json и screenshots-v0.5 содержат27 PNG/75 PASS; они неизменяемы. Завершённый migration пакет screenshots-production-0.5:27PNG/81native PASS/25source checks и scoped independent ship; production-baseline/source-contract историчны относительно notification-изменений. Notification successor contract содержит47source checks; отдельный actual WPF пакет содержит35PASS/9PNG с bounded independent ship. Revit/Document/API, physical input/accessibility, OS DPI и внешний браузер не доказываются этим source/DTO host.

## UI authority and evidence

Единый стандарт — [Specification](../../Docs/Ui/DesignSystem/Specification.md); обязательные проверки — [ErrorRegistry](../../Docs/Ui/DesignSystem/ErrorRegistry.md), [Workflow](../../Docs/Ui/DesignSystem/Workflow.md) и [ReviewRoles](../../Docs/Ui/DesignSystem/ReviewRoles.md). Текущий STATUS:approved_for_production_migration,FluentLightSource,approvedAt2026-10-11,productionMigrationAuthorized=true,installationAuthorized=false. Новый запрос разрешил **ровно** макеты0.5 в обоих модулях: виды/листы main+secondary, параметризация все4вкладки+actual ParameterPicker/CategorySelectionControl, без новых вариантов. Остальные модули/установка не включены.

[Матрица](../../Docs/Ui/DesignSystem/component-coverage.csv):51тип=29current_example/11resource_only/11missing (до notification ProgressBar:28/11/12); состояния/прошлыеpreview evidence отделены от production source/runtime. [CoveragePlan](../../Docs/Ui/DesignSystem/CoveragePlan.md) содержит остаток галереи/gates. Workspace shared layer имеет4ownerfiles; общий notification-слой добавляет UI/Notifications и adapter ParameterNotifications. Исторические7generatorowners не активная цепочка. Documenter не запускает build/UI и не выдаёт прежние75 PASS за новые результаты.


## Уведомления и поставка —12.10.2026

Прямой запрос пользователя разрешил принятое уведомление параметризации, затем общий SAB-шаблон, затем правильный MSI. Общий шаблон уже выделен в SAB/UI/Notifications/{NotificationMessage,NotificationCard,NotificationStack}.cs; module adapter ParameterNotifications передаёт текст/тон из существующего командного контекста без Revit DB callbacks. Selectable summary, отдельные ID/полный support context, truthful copy feedback, independent close и10active seconds с паузой/остатком; newest-bottom gap12 и bounded overflow без потери. Подробный ReportWindow сохраняется. Transient notification не получает HTML-footer рабочих окон.

Host Compile:SourcePreview.cs + NotificationPreview.cs + ElevationPreview.cs с изолированными generated copies; actual production SAB.dll/synthetic result fixtures. [Notification review](../../Docs/Ui/DesignSystem/Notification-Review.md): scoped ship,35native PASS/9PNG; это не actual Revit execution/API22, physical input/accessibility или OS-DPI transitions. Source successor проверяет47условий, старые migration/preview evidence неизменяемы.

Build-Msi.bat Release завершён для2022/2023/2024; автоматический Verify-MsiPayload.ps1 извлёк CAB и сверил SHA256 SAB.dll/styles/Toolkit/23HTML с текущими сборками, API references и отсутствие test/host API artifacts. Пакеты в Installer/output, verification в existing outputs/ui-production-preview/msi-check. Это package verification, не установка; installationAuthorized=false. Все поставляемые изменения включать в этот pipeline. Повторные прогоны не создают timestamp Outputs, историю не удаляют. Запрос «бэкап» отдельно запускает старую версию commit+push+remote verify с rollback commit и единым локальным журналом.


Актуальное уточнение12.10: Elevations native packet565PASS/41PNG; one decoration form, tag grid3×2 with12DIP leader gap, horizontal source panel, fixed content-sized helpers, elastic text columns, original progress. Generated source now14files (11views/VM +3notification proposal copies); production UI migration pending. Immutable93baseline reconciled through exact5authorized functional deltas/88unchanged; schema9categories included in normal2022–24MSI. Notification visual proposal416DIP pending user approval; current shared production notification remains520DIP. See Elevations-Verification/Report for current evidence; previous526/47 references are historical.


Актуальные последние правки:635PASS/44PNG,70ID;3selection cards, spatial dashed crop, positive-only creation notification with separate warnings. Independent category creation/storage and selected-role replacement are in production;8Elevation deltas+2optional shared warning files packaged for2022–24.85baseline unchanged. UI suite/416-16proposal migration pending. Previous565/41 and526/47 references describe history. See Elevations-Verification/Report and current Installer-Payload.


Текущий bounded hotfix1.1.1: [компоновка/порядокfooter](../../Docs/Ui/DesignSystem/ElevationHotfix-Layout.md), [actual captures](../../Docs/Ui/DesignSystem/ElevationHotfix.html), [verification](../../Docs/Ui/DesignSystem/ElevationHotfix-Verification.md). `Build.ps1 -ElevationHotfix` запускает actual классы с чистыми Application resources;17actualPNG,9shared notificationPNG и44source-derived sessionPNG сохраняются отдельно от1.1.0. Production включает новый read-only ElevationSelectionListWindow и immutable snapshot;2mode capsules/mainMin800, listMin740×420; footer status row0, actions row1. Установка и Revitexecution NOT RUN.


Текущие исправления параметризации1.1.2: [layout](../../Docs/Ui/DesignSystem/ParameterCheck-Layout.md), [verification](../../Docs/Ui/DesignSystem/ParameterCheck-Verification.md), [actualcaptures](../../Docs/Ui/DesignSystem/ParameterCheck.html). `Build.ps1 -ParameterChecks` — actual90/14, separateactualregression81/27 сcleanresources; source71boundedchecks. ParameterPicker gates actualedit events, CheckCategoriesWindow Fluentfooter/noaccentstub/min680×760, fullcatalog with independentCheckCategoryIds. ExistingCheckcommand/ribbonID retained; newToolkitCheckSettingsCommand; highlightingreports removed, onlyactivationnotice. NativeRevit/physical/OS/installation NOTRUN.

Нумерация развёрток15.10: actual ElevationSettingsWindow содержит буквальные поля префикса/суффикса в карточке наименований. Общий суффикс группы устраняет коллизии без изменения source index; настройки сохраняютсяschema15. [Scope/evidence](../../Docs/Ui/DesignSystem/ElevationNumbering-Report.json).


Direction preview15.10: actual settings entry and compact modeless controller surface; the drawing remains in Revit. [Bounded scope and lifecycle](../../Docs/Ui/DesignSystem/ElevationDirectionPreview-Layout.md); live Revit/RFA NOT RUN.


Preview runtime1.1.3: [source identity, fixed footer and virtual closure](../../Docs/Ui/DesignSystem/ElevationPreviewRuntime-Layout.md). Live Revit, physical focus/picking and installation NOT RUN.
