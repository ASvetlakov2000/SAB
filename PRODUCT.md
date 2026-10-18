# SAB

17.10.2026: по прямому утверждению пользователя [дизайн редактора шаблонов перенесён в рабочие3окна](Docs/Ui/DesignSystem/TemplateEditor-Production-Layout.md). Две колонки345.6/20/*,7вкладок с общим переносом в2ряда, capability-aware дерево и batch guards; нижний зазор16DIP. [Actual SAB.dll WPF report](Docs/Ui/DesignSystem/TemplateEditor-Production-Report.json), [MSI22/23/24 payload](Docs/Ui/DesignSystem/TemplateEditor-Production-Installer.json). Preview558/52 — история; текущая проверка624/54 относится к production UI с detached DTO, не к операциям Revit. Установка/публикация/commit не выполнялись; physical/OS DPI/browser/live Revit отдельно NOT RUN. Без Computer Use.

<!-- impeccable:product-schema 1 -->

## Platform

Windows desktop, WPF. Это расширение схемы контекста для Windows; не web, iOS или Android.

## Stack

C#, .NET Framework 4.8, WPF; Autodesk Revit 2022/2023/2024 по SAB.csproj. Текущий host0.5 собирает actual production SAB без presentation substitutions; используются библиотеки Revit 2023 и синтетические DTO, без Document и процесса Revit. Существующие WPF-UI 4.3.0 и ресурсы SAB можно переиспользовать; новые зависимости без необходимости не добавлять.

## Users

Пользователи Revit, которые настраивают и выполняют инженерные операции: виды и листы, развёртки, параметры, материалы и графику шаблонов. Русскоязычные названия и длинные значения — обычные рабочие данные.

## Product Purpose

Сократить ручные операции в модели через понятные настройки, проверку исходных данных и предсказуемый результат. Пользователь задаёт функциональность; Codex самостоятельно проектирует и проверяет интерфейс.

## Operating Context

Окна работают в контексте Revit; API-вызовы сохраняют допустимый контекст и существующие ExternalEvent. Интерфейсы настраивают операции, но оформление не меняет алгоритмы и правила записи в модель.

## Capabilities and Constraints

Сохранять Binding, Command, DataContext, x:Name, handlers, VM/API/ExternalEvent и реальные зависимости. Один SAB Fluent Light, несколько функциональных компоновок. Native Segoe UI, общие таблицы44/header38, mode capsules min34/30, текущие tabs Auto/min40/text14. Описания/обучение только через одну footer «Инструкция»→локальный HTML; metadata/errors/results остаются. Для transient notification и исходного progress действуют явные исключения канона.

## UI authority and current release

Канон: [STATUS](Docs/Ui/DesignSystem/STATUS.json), [Specification](Docs/Ui/DesignSystem/Specification.md), [ErrorRegistry70ID](Docs/Ui/DesignSystem/ErrorRegistry.md), [Workflow](Docs/Ui/DesignSystem/Workflow.md), [ReviewRoles](Docs/Ui/DesignSystem/ReviewRoles.md). Первые два модуля перенесены по approval11.10. Прямое утверждение12.10 разрешило весь показанный комплект «Развёрток», общие notification416/status16Bold/summary outline и tabs40/text14, production migration, штатный MSI и commit+push/release**1.1.0 «Редизайн»**. Установка не разрешена. Старые pending/proposal — история, не текущий gate; неоговорённые модули не мигрировать автоматически.

9actual Elevations views/classes используют общие resources. Main VM null-Document guard сохраняет настоящие collectors при Document; catalog draft round trip сохраняет unfinished raw input. Production manifest — Tools/UiShowcase/elevations-production-migration.json. Standalone RoomPlanSchemeSettingsWindow unreachable, функция плана-схемы в main tab сохранена. Настоящие model operations/ExternalEvent/transactions не заменяются synthetic host sessions.

## Common notifications

SAB/UI/Notifications/{NotificationMessage,NotificationCard,NotificationStack}.cs + SABFluentLight, parameter adapter ParameterNotifications и elevation result adapter. Stack≤416DIP, справа снизу/newest-bottom/grows-up/gap12; status16Bold, selectable summary с прозрачным контуром, отдельные warnings/ID/support/copy с truthful feedback.10active seconds, pause/resume remainder, independent close/overflow без потери. Parameter ReportWindow сохраняется; elevation final summary positive-only со всеми проблемами в отдельном warning block и сохранённым диагностическим журналом. Modeless слой получает строки/тон без Revit DB callbacks.

## Evidence and limits

[Coverage](Docs/Ui/DesignSystem/component-coverage.csv)/[CoveragePlan](Docs/Ui/DesignSystem/CoveragePlan.md):51тип=31current/11resource/9missing; прежние29/11/11 и28/11/12 — история. Current example не all-states PASS. Host Compile: SourcePreview/NotificationPreview/RedesignProduction и отдельно isolated ElevationPreview/generated copies; Gallery0.1 исключена. Historical75/27, migration81/27, final elevation preview635/44 не становятся новым production proof.

Fresh COMPLETED native logs: [actual elevations104/21](Docs/Ui/DesignSystem/screenshots-redesign-production/verification.txt), [shared notifications35/9](Docs/Ui/DesignSystem/screenshots-redesign-notifications/verification.txt), [два прежних модуля81/27](Docs/Ui/DesignSystem/screenshots-redesign-modules/verification.txt), [isolated sessions635/44](Docs/Ui/DesignSystem/screenshots-redesign-sessions/verification.txt). WPF-host вне Revit использует fixtures. Revit/API execution, physical input/full accessibility, OS monitor/DPI transitions и установка NOT RUN;144dpi render не OS150%. Текущие independent review/build/MSI/release подтверждаются builder отдельно.

## Delivery and workspace

Все поставляемые изменения входят в штатный Build-Msi.bat для2022/2023/2024; нужен свежий payload proof1.1.0, предыдущий Installer PASS не распространяется на новый выпуск. Commit+push/release сейчас разрешены; завершение и remote verification не заявлять до фактов. Установка отдельно не разрешена.

Повторяемые прогоны используют existing outputs/ui-production-preview и Installer/output без timestamp Outputs на каждый запуск. Историю evidence сохранять. «Бэкап» отдельно означает старую версию commit+push+remote verify с rollback commit и единым локальным журналом; не автоматический backup каждой задачи.


15.10: [Предпросмотр направлений развёрток](Docs/Ui/DesignSystem/ElevationDirectionPreview-Layout.md) на текущем плане Revit с временными аннотациями и подтверждением сторон. Поставка для2022/2023; [отчёт](Docs/Ui/DesignSystem/ElevationDirectionPreview-Report.json). Live Revit/RFA и установка NOT RUN.


1.1.3: [Исправление preview runtime, новый footer и виртуальное замыкание](Docs/Ui/DesignSystem/ElevationPreviewRuntime-Layout.md). Целевые MSI2022/2023; live Revit и установка проверяются отдельно.

## Настройки уведомлений — первый этап18.10.2026

По разрешению пользователя завершён первый этап в границе исходников и проверенного WPF: рабочее окно и увеличенный главный переключатель. Actual owners: SAB/Notifications/{NotificationController,NotificationSettingsStore,NotificationSettingsModel,SettingsWindow.xaml,SettingsWindow.xaml.cs}; общие SabLargeSwitchTemplate56×32/hit64×40 и SabNotificationTypeBadgeStyle — SABFluentLight. [Layout](Docs/Ui/DesignSystem/NotificationSettings-Production-Layout.md), [инструкция](Docs/PluginInstructions/SAB_HTML_Uvedomleniya.html), SAB-UI-091. Предыдущий isolated preview остаётся историей.

Первый запуск без действующих настроек — OFF; legacy/invalid settings также OFF. Пользователь включает модуль только в окне и сохраняет; сознательно сохранённое Enabled=true schema2 восстанавливается между запусками/обновлениями. Реализованы draft/save/cancel, атомарная schema2, чтение и HTML-экспорт настоящего журнала. Все выбранные способы подавления пока pending; доступно0, включение запускает только наблюдение. Старый blanket backend удалён: ни auto-OK, ни DeleteWarning/ResolveFailure/rollback в активных обработчиках нет. Native Revit/OK/physical/OS DPI не проверены; scope исходников/сборок без MSI/установки/релиза.

[Отчёт91ID](Docs/Ui/DesignSystem/NotificationSettings-Production-Report.json) и [независимый review](Docs/Ui/DesignSystem/NotificationSettings-Production-Review.md): ship_ui_source_scope;56PASS/21PNG actual SAB.dll с isolated fixtures. Это завершение первого этапа окна; движок подавления pending, Revit/OK/physical/OS DPI не подтверждены.

## Подготовленный установочник1.1.4 —18.10.2026

Текущий cumulative выпуск1.1.4 после исправления footer: [NotificationSettings-Installer.json](Docs/Ui/DesignSystem/NotificationSettings-Installer.json). Штатный Build-Msi.bat Release1.1.4 и PrepareOnly завершились exit0; все3MSI для2022/2023/2024 проверены по извлечённым DLL, actual XAML/BAML, styles, guides и API references. Первый запуск без действующих настроек уведомлений OFF; автоматическое подтверждение по-прежнему не реализовано. [Выпуск1.1.4 опубликован](https://github.com/ASvetlakov2000/SAB/releases/tag/v1.1.4-notifications), [контроль публикации](Docs/Ui/DesignSystem/Release-1.1.4-notifications.json). Исходный commit c3b7a322b87a2519226899300068aedd3312fc34 проверен в origin/master; новый tag v1.1.4-notifications сохраняет исторический v1.1.4. Установка не выполнялась. [Независимый footer/payload review](Docs/Ui/DesignSystem/TemplateEditorFooter-Review.md): ship_ui_code_payload;103PASS/6PNG,7source checks и3freshMSI. Исходный запрос GitHub/обновления установочника возобновлён после footer fix; старые native counts/proofs не переписываются.
