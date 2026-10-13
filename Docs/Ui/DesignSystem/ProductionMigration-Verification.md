# Проверка рабочего UI SAB после переноса0.5

2026-10-11. Пользователь прямо разрешил перенос ровно последних макетов обоих модулей. Scope: «Создание видов и листов» (main+secondary settings), «Параметризация» (4вкладки+ParameterPicker+CategorySelectionControl). Установка и публикация не выполнялись.

## Что перенесено

Shared production dictionary [SABFluentLight.xaml](../../../SAB/UI/Styles/SABFluentLight.xaml), [Presentation.cs](../../../SAB/UI/FluentLight/Presentation.cs), [FluentResources.cs](../../../SAB/UI/FluentLight/FluentResources.cs), [FluentTheme.cs](../../../SAB/Cls_ParameterTools/UI/FluentTheme.cs). Последний оформляет SettingsWindow; legacy SmallWindows/Theme и общий SABWindowStyles остальных модулей сохранены. Shared CategorySelectionControl используется также старым CheckCategoriesWindow: внешний диалог отдельно не переработан/не снят.

Карточки, капсулы с движением и ростом по тексту, таблицы/списки с собственным контуром, выравнивание полей, действия в строках, footer обмена и одна локальная Инструкция воспроизводят0.5. Production не содержит demo badge. Preview использует те же рабочие C#/XAML/resources без визуальных подмен. Страница снимков явно отмечает синтетические DTO и отсутствие Document.

## Сборка и источники

- `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/UiShowcase/Build.ps1 -Capture`: PASS, exit0. Обычный SAB.csproj Debug/RevitVersion2023/net48, затем host; никаких CompileRemove/Include overrides или post-build перезаписей XAML/styles.
- MSBuild SAB.csproj Debug/RevitVersion2024 с отдельным OutputPath/IntermediateOutputPath: PASS, exit0; прежние предупреждения устаревшего ElementId в других модулях.
- RevitVersion2022: попытка сборки exit1 из-за отсутствующего `C:/Program Files/Autodesk/Revit 2022/RevitAPI.dll`/RevitAPIUI. Локальный общий lib содержит API23.1, поэтому подмена им не выполнялась. Целевая2022 совместимость NOT RUN, а не PASS.
- [Production source contract](../../../Tools/UiShowcase/production-source-contract.json):25 проверок. Все исходные135/103 binding attributes,10колонок сохранены.10защищённых тел методов совпадают после единственного разрешённого визуального переименования квалификатора `FluentTheme.`→`Theme.`; исходный и текущий SHA каждого тела записаны отдельно. Это не утверждение побайтовой неизменности всех UI-методов.
- Main/secondary codebehind, VM, layout service, legacy SmallWindows.cs и глобальный SABWindowStyles.xaml побайтово совпадают с [зафиксированным baseline](../../../Tools/UiShowcase/production-baseline.json). API/транзакции/ExternalEvent не редактировались. Исходное незакоммиченное состояние сохранено в локальном backup по `outputs/production-migration-backup.txt`.
- Локальные HTML/assets входят в output сборки через SAB.csproj. Проверены существование файлов и разрешение ссылок; фактический внешний browser не запускался.

## Нативные проверки

[27 свежих PNG](screenshots-production-0.5/), [протокол81PASS/COMPLETED](screenshots-production-0.5/verification.txt), [SHA/provenance](screenshots-production-0.5/provenance.json), [страница просмотра](ProductionReview.html). Это новые проверки actual production UI-классов; старые27PNG/75PASS в screenshots-v0.5 остаются отдельным неизменным протоколом.

Проверены4вкладки, таблица правил и её реальная привязка, раскрытый picker с отрисованными SAB-именами, поле источника и его оси, валидация, поиск/пустое состояние/выбранные категории/JSON import/export, рамки всех видимых коллекций и отсутствие пустых оболочек, normal/minimum/secondary/режимы структуры, action/row commands, Create disabled, native scroll/group collapse, scrollbar ниже header/top group, footer, wrapped capsules и фактическое движение tab indicator. Программные routed Escape events проверяют открытый dropdown и закрытие окна; физические нажатия не заявлены.

Host регистрирует тот же SabWindowBehaviorService.Initialize и оставляет legacy application dictionary. Отдельная проверка доказывает отсутствие Fluent keys в Application.Resources. Production windows получают новый словарь локально. Main restore/save не отключаются в исходниках: host перенаправляет только путь экземпляра layout service, а отдельный сценарий запускает реальный restore и проверяет сохранение всех10ширин колонок. Secondary service выполняет настоящие save/restore; host сохраняет/восстанавливает единственный пользовательский файл secondary preferences в finally.

Runtime DPI96;144dpi — отдельный RenderTargetBitmap. Нет claims о системных150%/смене монитора. Снимки сделаны с синтетическими моделями без Revit Document, без создания видов/листов, записи параметров, транзакций или поднятия ExternalEvent.

## Ограничения и итог

[Отчёт всех58ID](ProductionMigration-Report.json) разделяет PASS/NOT RUN/N/A. [Независимый review](ProductionMigration-Review.md) имеет собственный scope. Непроверены: Revit execution/transactions/selection-point workflows, full physical keyboard/mouse/drag-drop/accessibility, OS DPI transitions, произвольно длинные реальные данные/все hover/pressed/loading состояния, реальный browser и Revit2022. Общий control в CheckCategoriesWindow требует downstream smoke. Эти ограничения не подменяются существующими75PASS или новой сборкой.
