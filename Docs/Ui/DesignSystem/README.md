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

## Подсказки выбора1.1.2

[Layout](SelectionGuidance-Layout.md), [33guide+35shared WPF checks/15PNG](SelectionGuidance-Provenance.json), [83-ID Report](SelectionGuidance-Report.json), [MSI](SelectionGuidance-Installer.json). Руководство выбора использует существующие shared notifications; inline инструкции в окна не добавлены.

[Независимый review подсказок](SelectionGuidance-Review.md): scoped ship source/UI/payload, physical/Revit/installation NOT RUN.

## Панель SAB17.10

Настройки заливок вторым пунктом сплит-кнопки; единый RGB каждого модуля, включая вложенные/составные значки; Каркас скрыт с сохранёнными командами. [Layout](RibbonModules-Layout.md), [85-ID report](RibbonModules-Report.json), [source provenance](RibbonModules-Provenance.json), [118 checks](screenshots-ribbon-modules/verification.txt), [production icons](screenshots-ribbon-modules/production-icons.png), [independent review](RibbonModules-Review.md), [MSI2022/2023/2024](RibbonModules-Installer.json). Asset render вне Revit; native ribbon/popup/input/OS DPI и установка NOT RUN.

Последующее уточнение: у «Оформить развертки» собственная розовая vector-иконка; отдельная ribbon-кнопка настроек удалена, вход внутри окна — «Каталог элементов». [Layout](DecorationRibbon-Layout.md), [88-ID report](DecorationRibbon-Report.json), [actual WPF10checks](screenshots-decoration-ribbon/verification.txt), [current assets121checks](screenshots-ribbon-modules/verification.txt), [review](DecorationRibbon-Review.md), [fresh MSI](DecorationRibbon-Installer.json). Ранее указанные118checks/85-ID относятся к сохранённому этапу до новой иконки; текущий native asset log121. Revit/установка NOT RUN.

17.10: [рабочий редактор шаблонов](TemplateEditor-Production-Layout.md), [actual DLL WPF/90ID](TemplateEditor-Production-Report.json), [MSI22/23/24 payload](TemplateEditor-Production-Installer.json). Preview558/52 остаётся историей; текущие624/54 не доказывают live Revit.

## Настройки уведомлений: реализация окна18.10.2026

По прямому запросу завершён в границе исходников и проверенного WPF [первый production-этап окна](NotificationSettings-Production-Layout.md): actual SAB.Notifications.SettingsWindow, две вкладки, сохранение draft по кнопке и реальный журнал. Общий главный switch56×32/hit64×40 — SAB-UI-091; прежний малый template сохранён. [Инструкция](../../PluginInstructions/SAB_HTML_Uvedomleniya.html) описывает фактический этап.

При первой установке модуль выключен; отсутствующие/старые/повреждённые настройки также OFF. Включение только пользователем через окно и Save; сохранённый schema2 выбор восстанавливается между запусками/обновлениями. Сейчас включение запускает наблюдение/журнал, все правила ожидают проверки и реально доступно к подавлению0. Старые blanket исправления/скрытие/rollback/автоответы удалены из активного обработчика. [Новый native протокол](screenshots-notification-settings/production/verification.txt):56PASS/21PNG actual SAB.dll с isolated fixtures вне Revit, включая defaultOFF, draft/save/cancel, журнал и размеры/focus большого switch. [Отчёт91ID](NotificationSettings-Production-Report.json) и [независимый review](NotificationSettings-Production-Review.md): ship_ui_source_scope. Это завершение первого этапа окна; движок подавления pending, preview40PASS/16PNG остаются историей. MSI/установка/релиз вне запроса; Revit/OK/physical/OS DPI NOT RUN.

## Редактор шаблонов: воздух внутри footer18.10.2026

По новому пользовательскому снимку реализовано узкое исправление статуса: [Layout до кода](TemplateEditorFooter-Layout.md), SAB-UI-092. Именованные shared panel/text styles подключаются opt-in, сохраняют общий card→status16; внутренний padding12,10 и Auto/min44 не дают длинному статусу прижаться к рамке. Локальная сумма body4+footer12 не является общей нормой остальных окон. [Свежий native протокол](screenshots-template-editor-footer/verification.txt):103PASS/6PNG actual SAB.dll с detached DTO; text layout/glyph ink bounds, gap16, long/validation и7 tabs проверены вне Revit. [Source contract](TemplateEditorFooter-SourceContract.json) сохраняет7 исходных контрактов; [Report92ID](TemplateEditorFooter-Report.json) и [независимый review](TemplateEditorFooter-Review.md): ship_ui_code_payload; footer detail recapture принят. [Штатные MSI1.1.4 после fix](NotificationSettings-Installer.json): Build-Msi Release и PrepareOnly exit0, extracted DLL/BAML/guides/API references проверены; установка не выполнялась. Прежние624/54 не подтверждают новую правку. После исправления возобновляется исходный запрос GitHub/обновления установочника; Git и публикацию подтверждает builder отдельно; текущий MSI подтверждён указанным installer record. Установка не проводится.

Текущая поставка после footer fix — prepared current candidate1.1.4: [installer record](NotificationSettings-Installer.json), штатный Release build и PrepareOnly exit0, все3 extracted MSI/DLL/actual XAML/styles/guides/API refs проверены, initial notifications OFF. Git/публикация планируемого v1.1.4-notifications pending и подтверждаются builder отдельно; установка не выполнялась. [Review текущего кода/payload](TemplateEditorFooter-Review.md): ship_ui_code_payload;103 native PASS/6PNG и7source checks. Это новый cumulative delivery proof, прежние UI counts/installer proofs сохранены историей; движок автоматического подавления остаётся pending.
