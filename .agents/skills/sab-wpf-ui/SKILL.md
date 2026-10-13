---
name: sab-wpf-ui
description: Автономно проектировать, реализовывать и проверять WPF-окна SAB для Revit, включая общие компоненты, secondary settings и preview. Не для backend-only или редактирования HTML-инструкций.
---

# SAB WPF UI

Корень: `C:/Users/svetl/Desktop/Codex/Projects/SAB`, не старый OneDrive cwd. Прочитать рабочие/вложенные AGENTS. Перед UI-задачей обязательны документы ниже; не повторять аудит36 окон, концепции и уже созданный Preview.

Канонический пакет (пути относительно этой папки):

1. [STATUS.json](../../../Docs/Ui/DesignSystem/STATUS.json) — единственный источник approval/migration authority.
2. [Specification.md](../../../Docs/Ui/DesignSystem/Specification.md) — одна актуальная визуальная редакция; история не конкурирует с ней.
3. [ErrorRegistry.md](../../../Docs/Ui/DesignSystem/ErrorRegistry.md) — устойчивые ID известных ошибок, критерии/источники/статусы.
4. [Workflow.md](../../../Docs/Ui/DesignSystem/Workflow.md) — обязательные до/после checklist и отчёт каждого ID PASS/FAIL/NOT RUN/N/A.
5. [ReviewRoles.md](../../../Docs/Ui/DesignSystem/ReviewRoles.md) — существующие builder/reviewer/documenter, ownership и независимый bounded review.

До XAML/C#: разобрать реальные функции/команды/VM/API boundary; выбрать [Layout Pattern](../../../Docs/Ui/DesignSystem/LayoutPatterns.md), записать [Layout Specification](../../../Docs/Ui/DesignSystem/LayoutSpecification.md), для сложного окна сравнить2 схемы; назначить applicability всех ID и общий resource owner. Обычные design decisions принимать самостоятельно. Пользователь описывает функции, не размещение каждой кнопки.

Прямой approval12.10.2026: весь показанный комплект «Развёрток» утверждён; разрешены production migration, общие notification416/status16Bold/outlined summary и tabs40/text14, штатный MSI и commit+push/release1.1.0 «Редизайн». Installation false. Historical pending/proposal не текущий gate; предыдущие review/MSI результаты не становятся новым production proof.

Базовый STATUS: approved_for_production_migration, FluentLightSource, approvedAt2026-10-11, productionMigrationAuthorized=true, installationAuthorized=false. Новый прямой запрос разрешил ровно макеты0.5 в обоих модулях: виды/листы(main+secondary) и параметры(4 tabs+настоящие ParameterPicker/CategorySelectionControl). Это исторический scope11.10;12.10 отдельно утверждён комплект «Развёрток». Новый выбор дизайна/установка/неоговорённые модули не разрешены. Если будущий STATUS снова pending/false, соблюдать его границу; исторические pending-протоколы не переписывать.

Дополнительный запрос12.10.2026: сначала фактическое notification-окно параметризации по принятому дизайну11.10, затем общий SAB-шаблон, потом правильный MSI. [Notification-Layout](../../../Docs/Ui/DesignSystem/Notification-Layout.md) и Specification/ErrorRegistry059–064 — канон новой части. Shared owners: SAB/UI/Notifications/{NotificationMessage,NotificationCard,NotificationStack}.cs + notification-only styles SABFluentLight; module adapter ParameterNotifications.cs. Сохранять ReportWindow и transactions/API boundary, передавать в modeless слой только строки/тон. Transient card не получает HTML-footer; это N/A по отсутствию функции, не разрешение убирать footer рабочих окон.

Использовать общие resources/components, сохранять Binding/Command/DataContext/x:Name/handlers/VM/ExternalEvent. Не добавлять функции из визуального референса. Применять подходящий Revit skill, только если затронут его реальный API-контекст. Новую ошибку пополнять ID/общим исправлением/критерием; не плодить локальные варианты.

Единый текущий production-слой: SAB/UI/Styles/SABFluentLight.xaml, SAB/UI/FluentLight/{Presentation,FluentResources}.cs, SAB/Cls_ParameterTools/UI/FluentTheme.cs. Scoped dictionaries, без изменения Revit Application resources/legacy SmallWindows Theme/global SABWindowStyles. Build.ps1 использует actual production source, без SourcePreview.targets/Compile-подмен; host SourcePreview.cs/NotificationPreview.cs/RedesignProduction.cs, плюс отдельный isolated ElevationPreview/generated copies. GenerateSourceViews.py теперь запускает successor VerifyNotificationSource.py, UI не генерирует; старые семь preview owners — история. [Coverage](../../../Docs/Ui/DesignSystem/component-coverage.csv) учитывает active csproj: старые Gallery/ReferenceView/CompositeControls0.1 исключены. [Остаток галереи/переноса](../../../Docs/Ui/DesignSystem/CoveragePlan.md). MakeReview.py — исторический генератор0.1; не использовать для текущего coverage/стандарта.

RedesignProduction.cs проверяет9actual Elevations views/classes SAB.dll; main VM null-Document guard позволяет fixture options при сохранённых collectors для Document. Catalog raw draft сохранён через CaptureDraft/RestoreDraft и command round trip. Tools/UiShowcase/elevations-production-migration.json — текущий manifest; isolated source-derived session635/44 и прежний93baseline — история.

NotificationPreview.cs входит в actual host Compile. Настоящий determinate notification ProgressBar снят; это не покрывает indeterminate и все состояния. Предыдущие migration proofs историчны относительно последующих notification изменений; новый bounded source/runtime report отдельно. Native fixtures не доказывают actual Revit/API22/physical hover/full keyboard/accessibility/OS DPI transitions; эти границы писать NOT RUN, не N/A.

После UI-правок: целевая сборка, запуск фактической поверхности, настоящие captures, применимые ID/состояния/minimum/long Russian text/keyboard; исправить существенные findings пакетом и передать независимому reviewer по Workflow. Documenter фиксирует код/evidence, не выдумывает результаты. Для docs-only не повторять тяжёлую сборку; ссылаться на существующий протокол явно как существующий. Не запускать Revit/API/physical/browser проверки только в отчёте на словах.

Запуск: `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/UiShowcase/Build.ps1 -Capture`, интерактивно Open.cmd. Сохранность текущего переноса — production-baseline.json/production-source-contract.json;10business hashes допускают только FluentTheme.→Theme. normalization. Старый source-contract.json и Verification.md/screenshots-v0.5 историчны. Новые evidence — screenshots-production-0.5 и отчёт миграции; не переносить туда75 старых PASS.144dpi — offscreen render, не Windows150%. Новая UI-задача получает новый отчёт/доказательство;75 PASS0.5 не переносить на новые окна автоматически.

Текущие shared размеры: TabItem Auto/min40DIP, text14/padding20,8; NotificationStack≤416DIP, outcome16Bold, summary outline1/radius4/padding10; bottom-right/newest-bottom/gap12. Общие mode capsule min34/30 остаются.

Ключевые приоритеты: инструкции/описания модуля только через footer «Инструкция»→local HTML; metadata/errors/results остаются. Капсулы min34 с ростом по тексту, не fixed34. Native table scrollbar ниже header/верхней группы, без пустого gutter. Детальные числа/проверки только в каноне и реестре, не в накапливающихся версиях skill. Старые UI-Lab/WindowStyle/Redesign_Backlog и Refinement-планы — история. После утверждения системы не спрашивать снова о цветах/отступах; scope и существенные функциональные решения соблюдать.

Поставка: любые поставляемые изменения включать в Build-Msi.bat/MSI и проверять состав пакета до завершения установочника; установка отдельно не разрешена. Повторяемые прогоны используют existing outputs/ui-production-preview, notification evidence screenshots-notifications, без timestamp Outputs на каждый запуск и без удаления истории. Запрос «бэкап» отдельно запускает сохранение старой версии commit+push+remote verify с rollback commit и единым локальным журналом; сам по себе UI-task не разрешает автоматический backup commit.

- Актуальное дополнение12.10: host также компилирует ElevationPreview.cs +14 generated C# только для изолированного комплекта «Развёрток». STATUS.elevations требует явного утверждения всего комплекта перед production migration. Общие notifications: bottom-right16DIP, новые снизу, рост вверх; прежнее top-right/newest-first отменено. Current evidence: Elevations-Report/Review и Notification-Verification; общая галерея51тип=31current/11resource/9missing.

Свежие runtime proof paths: screenshots-redesign-production (actual classes), screenshots-redesign-notifications (shared template), screenshots-redesign-modules (regressions), screenshots-redesign-sessions (isolated session boundary). COMPLETED/counts брать из реальных логов. Current MSI/review/release подтверждать отдельными результатами; Revit/installation/OS/physical input не выводить из WPF/source проверки.
