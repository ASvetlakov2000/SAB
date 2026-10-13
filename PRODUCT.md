# SAB

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
