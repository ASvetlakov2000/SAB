## Параметризация1.1.2 —13.10.2026

Исправлено самораскрытие исходного параметра. Полный каталог категорий проверки и отдельные команды: Проверка параметров / Настройки проверок / Снять подсветку. По последнему уточнению отчёты подсветки удалены, только уведомление активации. [Layout](ParameterCheck-Layout.md), [снимки](ParameterCheck.html), [verification](ParameterCheck-Verification.md), [report77ID](ParameterCheck-Report.json), [review](ParameterCheck-Review.md), [MSI1.1.2](ParameterCheck-Installer.json). Предыдущие этапы ниже — история.

## Исправления развёрток1.1.1 —13.10.2026

Последний запрос: точная кнопка «Список помещений», все пять действий внизу. [Компоновка](ElevationHotfix-Layout.md), [реальные снимки](ElevationHotfix.html), [протокол](ElevationHotfix-Verification.md), [отчёт73ID](ElevationHotfix-Report.json), [review](ElevationHotfix-Review.md), [MSI1.1.1](ElevationHotfix-Installer.json). История и опубликованный выпуск1.1.0 ниже сохранены отдельно.

# Текущий выпуск SAB1.1.0 — Редизайн

Пользователь утвердил перенос12.10.2026. [Actualproductionпроверки](RedesignMigration-Verification.md), [снимки](RedesignMigration.html), [independentreview](RedesignMigration-Review.md), [MSIpayload1.1.0](Installer-Payload.json). Общие tabs40/text14 и notification416/status16Bold применены в рабочие окна. Ниже сохранена история предыдущих этапов; её pending/неперенесённые статусы не являются текущимauthority.

# SAB UI — Fluent Light 0.5 и уведомления

[STATUS.json](STATUS.json): approved_for_production_migration, FluentLightSource, approvedAt2026-10-11, productionMigrationAuthorized=true; installationAuthorized=false. Новый прямой запрос разрешил ровно разработанные макеты в двух модулях: виды/листы с main+secondary settings; параметризация со всеми4 вкладками и настоящими ParameterPicker/CategorySelectionControl. Остальные модули и установка не включены.

Канон: [Specification](Specification.md), [ErrorRegistry](ErrorRegistry.md), [Workflow](Workflow.md), [ReviewRoles](ReviewRoles.md). [CoveragePlan](CoveragePlan.md) и [матрица51типов](component-coverage.csv) описывают оставшуюся галерею/проверки, а не отдельный стандарт.

Прямой запрос12.10 разрешил уведомление «Параметризации», затем общий SAB-шаблон, потом правильный MSI через Build-Msi.bat. Принятая композиция/источники — [Notification-Layout](Notification-Layout.md), новые обязательные критерии059–064 — в реестре. Установка не разрешена. Общий runtime: SAB/UI/Notifications/{NotificationMessage,NotificationCard,NotificationStack}.cs + notification-only styles SABFluentLight; module adapter ParameterNotifications.cs. Он не переводит остальные модули SAB автоматически.

Отдельные notification evidence: [9PNG](screenshots-notifications/) и [native WPF-log](screenshots-notifications/verification.txt). Actual production SAB.dll, synthetic result fixtures вне Revit; реальные Windows clipboard success/выделение текста и injected clipboard failure, независимые lifetimes/close, bounded long text/stack/overflow. Свежий лог26PASS/COMPLETED и [scoped independent ship](Notification-Review.md) относятся к notification integration/показанным WPF-сценариям; successor notification-source-contract.json содержит47source checks. Task report ведёт builder; прошлые81/25/75 PASS не относятся к уведомлениям. Physical hover/full keyboard/accessibility, Revit execution/API22 и реальные monitor/OS-DPI transitions NOT RUN;144dpi PNG — offscreen render. Source integration ReportWindow сохранена, его реальные Revit save/selection/show здесь не проверены.

Дополнение12.10.2026: [сборка тех же окон для Revit2022 прошла](Revit2022-Verification.md) с локальными API22.0.2.392. Первоначальное ограничение «API2022 отсутствует» ниже относится к предыдущему этапу; теперь не подтверждён только запуск с native runtime2022, которого нет на этом ПК. Снимки/81PASS этапа2023 не выдаются за проверки2022.

Текущие owners находятся в SAB: SABFluentLight.xaml, UI/FluentLight/Presentation.cs и FluentResources.cs, parameter FluentTheme.cs. [Хост и сборка](../../../Tools/UiShowcase/README.md) используют actual production source без generated presentation substitutions. [DESIGN](../../../Tools/UiShowcase/DESIGN.md)/[sidecar](../../../Tools/UiShowcase/.impeccable/design.json) сохраняют токены0.5.

Завершённый production-пакет миграции0.5 до notification-изменений: `screenshots-production-0.5`, production-baseline.json и production-source-contract.json в Tools/UiShowcase; [просмотр 27 снимков](ProductionReview.html), [проверки](ProductionMigration-Verification.md), [отчёт 58 ID](ProductionMigration-Report.json) и [независимый review: ship](ProductionMigration-Review.md). На том этапе production-сборки Revit2023/2024 прошли, native WPF-протокол содержит81 PASS, source contract —25 PASS. Более поздний Revit2022 build указан выше; запуск внутри Revit/с native runtime2022 не проверен. Documenter обновлял документацию; runtime-результаты получены отдельно builder. Migration source contract сохраняет135/103 исходных bindings и10 колонок;10business bodies проверялись hash с единственным helper rename FluentTheme.→Theme. Main/secondary code-behind и persistence сохранены. Последующие notification-правки получают новый bounded source proof, старый не перезаписывается.

[Исторические27 PNG](screenshots-v0.5/), [75 PASS](screenshots-v0.5/verification.txt), [Verification](Verification.md), [FinishReview0.5](FinishReview-0.5.md) и source-contract.json относятся к isolated preview **до** переноса. Они не заменяют текущий production review и не переписываются задним числом. Gallery/ReferenceView/CompositeControls0.1 исключены; MakeReview.py пишет только исторические Review-v0.1.html/component-coverage-v0.1.csv.

Учёт:29 current_example /11 resource_only /11 missing из51типов. До уведомлений было28/11/12; настоящий determinate ProgressBar теперь снят в NotificationCard. Current_example не означает all-states или in-Revit PASS. Historical preview/migration и notification source/runtime evidence разделены в CSV. Revit Document/API, physical input/accessibility, OS DPI и внешний браузер не доказываются offscreen WPF-снимками.

Build-Msi.bat Release завершён для2022/2023/2024; Verify-MsiPayload.ps1 автоматически извлёк CAB и сверил DLL/styles/Toolkit/23HTML по SHA256 с текущими сборками, API references и отсутствие test/host API artifacts. Verification.json в outputs/ui-production-preview/msi-check/{year}:PASS/installationPerformed=false; итоговый Installer-Verification ведёт builder. Установка и installed Revit runtime NOT RUN, установка требует отдельного разрешения. Повторные прогоны используют outputs/ui-production-preview, уведомления — screenshots-notifications; исторические evidence не удалять, timestamp Outputs на каждый прогон не плодить. «Бэкап» — отдельный запрос на старую версию commit+push+remote verify и выдачу rollback commit, не автоматический commit этой задачи.


Актуальные последние правки:635PASS/44PNG,70ID;3selection cards, spatial dashed crop, positive-only creation notification with separate warnings. Independent category creation/storage and selected-role replacement are in production;8Elevation deltas+2optional shared warning files packaged for2022–24.85baseline unchanged. UI suite/416-16proposal migration pending. Previous565/41 and526/47 references describe history. See Elevations-Verification/Report and current Installer-Payload.

Нумерация развёрток15.10: [компоновка](ElevationNumbering-Layout.md), [20actual WPF checks/3PNG](screenshots-elevation-numbering/provenance.json), [78-ID отчёт](ElevationNumbering-Report.json), [независимый bounded review](ElevationNumbering-Review.md), [MSI2022–24](ElevationNumbering-Installer.json). [Исследование ориентации и обрезки](../../InteriorElevationOrientation_Research.md) отделяет подтверждённые code defects от исторического Revit bug; геометрические исправления пока не внесены. Revit/physical/OS/installation NOT RUN.


Предпросмотр направлений15.10: [компоновка](ElevationDirectionPreview-Layout.md), [свежий протокол](screenshots-direction-preview/verification.txt), [79-ID отчёт](ElevationDirectionPreview-Report.json). Текущий план Revit, временные annotation instances, shared modeless Compact Dialog; live Revit/RFA NOT RUN.


Runtime fix1.1.3: [layout](ElevationPreviewRuntime-Layout.md), [протокол](screenshots-preview-runtime/verification.txt), [отчёт](ElevationPreviewRuntime-Report.json). Main footer обновлён прямым запросом; same-native Document wrappers и virtual closure проверяются отдельно от live Revit.


16.10,1.1.4: [кончик превью на source line](ElevationPreviewTip-Implementation.md), [отчёт](ElevationPreviewTip-Report.json). Изменены backend placement и RFA22/23; WPF captures не переснимались.

## Окно «Направления»16.10

Три вертикальных действия blue/white/blue, без summary и видимой Cancel; X/Escape сохраняют cleanup. [Layout](DirectionsWindow-Layout.md), [50actual WPF checks/7PNG](screenshots-directions-window/verification.txt), [Provenance](DirectionsWindow-Provenance.json), [82-ID Report](DirectionsWindow-Report.json), [Review](DirectionsWindow-Review.md), [MSI1.1.1](DirectionsWindow-Installer.json). Новый публичный1.1.1 следует заRedesign1.1.0; старые1.1.4 proofs являются историей.
