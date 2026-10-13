# Текущее состояние1.1.0

Production migration утверждена и выполнена; см. [STATUS](STATUS.json) и [актуальный отчёт](RedesignMigration-Verification.md). Ниже этапы с pending/migration=false описывают историческую очередь до утверждения и не задают текущий запрет переноса.

# Покрытие действующего SAB UI Preview

Это план недостающих доказательств и будущей работы, а не новый стандарт или разрешение менять UI. Визуальные правила находятся только в [Specification.md](Specification.md), обязательные проверки прошлых ошибок — в [ErrorRegistry.md](ErrorRegistry.md), процесс — в [Workflow.md](Workflow.md), контракты ролей — в [ReviewRoles.md](ReviewRoles.md). Статус утверждения определяет [STATUS.json](STATUS.json).

## Фактическая база

Историческое разрешение этапа0.5: `approved_for_production_migration`, FluentLightSource, approvedAt2026-10-11, productionMigrationAuthorized=true, installationAuthorized=false. Код0.5 перенесён ровно в два модуля; [отчёт миграции](ProductionMigration-Report.json) и [independent ship](ProductionMigration-Review.md) завершены в своём scope. Прямой запрос12.10 разрешил уведомление параметризации→общий шаблон→правильный MSI, без установки; это новый этап с отдельными доказательствами.

[Sab.UiShowcase.csproj](../../../Tools/UiShowcase/Sab.UiShowcase.csproj) использует SourcePreview.cs, NotificationPreview.cs, ElevationPreview.cs и11 generated elevations-source/*.cs, без старых Gallery/ReferenceView/CompositeControls. Build.ps1 собирает обычный production SAB.csproj без SourcePreview.targets/Compile-подмен; SourcePreview/NotificationPreview открывают actual production классы и scoped ресурсы с синтетическими DTO вне Revit. ElevationPreview отдельно открывает source-derived clones и VM fixture branch; это не production migration. Текущие owners: SAB/UI/Styles/SABFluentLight.xaml, SAB/UI/FluentLight/Presentation.cs, FluentResources.cs и parameter FluentTheme.cs; notification-общий слой SAB/UI/Notifications и module adapter ParameterNotifications. Исторические семь generator owners больше не активная цепочка. Старый трансформер и targets сохранены в history; GenerateSourceViews.py теперь вызывает successor VerifyNotificationSource.py и не пишет UI.

Действуют два модуля и три основных оконных класса: параметр SettingsWindow(все4вкладки), CreateViewsAndSheetsWindow и CreateViewsAndSheetsSettingsWindow. CategorySelectionControl/ParameterPicker настоящие. Global SABWindowStyles и SmallWindows Theme сохранены, main/secondary code-behind и geometry persistence не подменяются. Gallery/ReferenceView/CompositeControls0.1 остаются исключёнными.

Исторический isolated preview до переноса:75 PASS/COMPLETED и27PNG в screenshots-v0.5, неизменяемый source-contract.json. Отдельная завершённая миграция:27PNG/81 native WPF PASS/25source checks и58-ID report; source proof и screenshots-production-0.5 относятся к своему этапу до notification-изменений. Revit2022 build подтверждён отдельным [протоколом](Revit2022-Verification.md), native runtime2022 не запущен. Notification evidence не получает эти PASS автоматически. Documenter не запускал build/UI; source check не заменяет WPF/Revit runtime или независимый review.

[component-coverage.csv](component-coverage.csv) сохраняет 51 тип исходного аудита:

| Статус | Типов | Что означает |
|---|---:|---|
| current_example | 31 | Тип используется в действующих примерах; строка указывает конкретное существующее доказательство и оставшиеся проверки. Это не полное покрытие состояний. |
| resource_only | 11 | Есть ресурс, часть нативного шаблона или настроенный источник данных; отдельного подтверждённого сценария компонента нет. Некоторые части видимы внутри работающих контролов. |
| missing | 9 | В действующих примерах нет подтверждённого сценария этого типа. Старый Gallery не закрывает пробел. |

Счётчик относится к типам, а не окнам, состояниям, ролям или production-тестам. Он не является процентом готовности всех окон SAB.

До notification-этапа учёт был28/11/12; determinate ProgressBar в9 notification PNG дал29/11/11. Изолированный elevation formula example добавил RichTextBox/Run: теперь31/11/9. Это текущие типы Preview, не approval/production readiness. Отдельного indeterminate-операционного сценария по-прежнему нет; shared elevation progress добавляет determinate operation fixture.

## Обязательные проверки перед следующим изменением

- Прочитать применимые пункты реестра и заранее указать, каким сценарием/снимком они закрываются. Реестр нельзя заменять фразой «сборка прошла»; результаты записывать по [Workflow.md](Workflow.md).
- [SAB-UI-003](ErrorRegistry.md#sab-ui-003): проверить четыре production resource owners, scoped dictionaries и реальные Compile/Content; старые генераторы не являются текущими владельцами. Новые компоненты должны входить в общий слой.
- [SAB-UI-006](ErrorRegistry.md#sab-ui-006), [SAB-UI-007](ErrorRegistry.md#sab-ui-007): отличать утверждение, объём задачи, isolated preview и рабочий плагин. Текущая миграция разрешена отдельным прямым запросом; покрытие/review не расширяют её на остальные окна или установку.
- [SAB-UI-008](ErrorRegistry.md#sab-ui-008): выбрать подходящую схему и записать композицию по каноническому процессу; не навязывать одну структуру всем задачам.
- [SAB-UI-009](ErrorRegistry.md#sab-ui-009), [SAB-UI-010](ErrorRegistry.md#sab-ui-010): использовать реальные актуальные снимки, проверять видимое содержимое и происхождение; 144-dpi RenderTargetBitmap не считать системным масштабом или DPI transition.
- [SAB-UI-011](ErrorRegistry.md#sab-ui-011): показывать применимые focus/disabled/error/empty/loading/success и клавиатурные состояния; неприменимость объяснять поведением реального компонента.
- [SAB-UI-048](ErrorRegistry.md#sab-ui-048): сверять реестр покрытия с фактическим Compile/targets и состояниями активного хоста. Сначала доказательство, затем статус строки.
- [SAB-UI-056](ErrorRegistry.md#sab-ui-056): сохранять этапы, результаты и роли по единому workflow; роль не считается выполненной по одному названию.

## Точное оставшееся наполнение галереи

Галерея ниже ещё не реализована. Следующая отдельно запрошенная UI-задача должна добавить сценарии в действующий host/shared layer и его реальный Compile. Не подключать старый стенд 0.1 целиком и не объявлять его старые компоненты актуальными.

| Недостающий тип | Требуемый сценарий и результат |
|---|---|
| AdaptiveSettingsGrid | Реальный существующий класс с тем же набором настроек при широкой и узкой рабочей области; показать переход, порядок полей, длинные подписи, общий левый/правый край и доступность footer. |
| Canvas | Сначала подтвердить актуальное применение в SAB. Если нужен для исходного UI — минимальный функциональный пример и resize/clipping/hit test. Если не нужен — сохранить явное исключение с причиной; не возвращать декоративную схему листа ради покрытия. |
| ListView | Настоящий ListView с актуальным набором данных: empty/one/many, selection/focus/keyboard, длинные значения, bounded scroll и последний элемент. ListBox категорий его не заменяет. |
| GridView | В том же ListView явно подключить GridView: шапки, несколько колонок, normal/minimum и горизонтальная прокрутка без потери последней колонки. |
| GridViewColumn | В GridView проверить ширины, длинный заголовок/значение, resize и оси данных/шапки; сохранить действие сортировки только если оно есть в исходной функции. |
| Hyperlink | Подтвердить реальное назначение ссылки; добавить keyboard focus, Enter, открытие целевого ресурса и обработку ошибки. Кнопка «Инструкция» не является примером Hyperlink. |
| Image | Подтвердить реальный исходный информационный asset; показать загрузку/отсутствие ресурса, Stretch, масштаб и отсутствие клиппинга. Не добавлять декоративные рисунки в текстовый preview. |
| Slider | Исходное диапазонное значение с min/max/current: клавиатура, disabled, focus, крайние значения и синхронизация подписи. Не выдумывать функциональный параметр модуля. |
| SymbolIcon | Сначала проверить наличие реального использования и совместимость текущей WPF-UI/.NET Framework 4.8. Проверить semantic action/automation name, focus/disabled и ресурс символа. Не объявлять Path или старый status glyph примером SymbolIcon и не добавлять header-icons ради галереи. |

Для каждого из 11 `resource_only` типов требуется проверить состояния в его владельце, а отдельный пример добавлять только если он помогает увидеть проблему. Приоритет: ComboBoxItem selection/highlight и открытый ToolTip; Thumb/Track/RepeatButton в обеих ориентациях с физическим page/drag; ContentControl/ContentPresenter/ItemsPresenter с заменой/пустым/условным содержимым; ScrollContentPresenter с viewport; Path/Ellipse в реальном действии/состоянии без нового декоративного языка. Источник и пределы перечислены построчно в CSV.

Для31 `current_example` типов закрыть колонку `remaining_checks`; уже существующий снимок не закрывает отсутствующие клавиатурные, физические, accessibility и OS-DPI проверки. ProgressBar имеет determinate notification lifetime и source-derived elevation operation-progress fixture; indeterminate не подтверждён. RichTextBox/Run имеют formula→naming update, long text и synthetic Enter suppression; physical caret/selection/copy и полная матрица inline states остаются непроверенными. Новое доказательство должно указывать реально открытое окно, состояние, размер, renderer/системный DPI и проверенную привязку/команду. Полный набор состояний определяется реальной функцией и [ErrorRegistry.md](ErrorRegistry.md).

## Текущий production-слой и оставшиеся gates

Новый прямой запрос уже разрешил перенос обоих модулей вместе: виды/листы main+secondary; параметризация4вкладки с actual ParameterPicker/CategorySelectionControl. Это не будущее намерение и не ретроактивное изменение истории preview. Код использует один SABFluentLight/FluentLight helper layer; новые варианты не проектируются.

Миграция завершена, её source/runtime/review evidence указано выше и в production-полях CSV. Эти поля описывают предыдущий проверенный этап; изменённый notification-код требует нового bounded contract/report, который ведёт builder. Установка не разрешена, in-Revit операции/Document/transactions/ExternalEvent, physical input/accessibility/OS DPI/browser требуют отдельных доказательств.

Проверить resource lookup вне Revit Application resources, работу native Escape/dropdown, сохранение geometry persistence и локальных HTML-assets, а также downstream reuse CategorySelectionControl в legacy CategoryCheckWindow: оболочка Theme сохранена, но control content обновился. Source-контракт сам по себе не доказывает отсутствие визуальных эффектов в этом зависимом окне.

Historical preview/migration evidence не удалять и не перехешировать под новые исходники. Остальные модули не мигрировать автоматически. Будущая установка и расширение module scope требуют отдельного разрешения пользователя.

## Текущий notification-этап и следующий MSI

Источник/композиция: [Notification-Layout](Notification-Layout.md); критерии [059](ErrorRegistry.md#sab-ui-059)–[064](ErrorRegistry.md#sab-ui-064). Реальные NotificationCard/NotificationStack, immutable NotificationMessage и module adapter уже входят в production SAB.csproj; host NotificationPreview использует synthetic fixtures actual SAB.dll, не отдельную UI-копию.

| Сценарий | Текущее доказательство / граница |
|---|---|
| Успех / пропуски / отмена |01-success,02-warning,04-error;9PNG в screenshots-notifications. Исходы демонстрационные, не выполненные транзакции Revit. |
| Selectable summary/ID/support, clipboard success/failure |03-selected-copied/05-clipboard-failure; [лог](screenshots-notifications/verification.txt) проверяет real Windows clipboard и injected failure. |
| Остаток времени / фокус / owner availability / expiry | Только фактически перечисленные assertions свежего COMPLETED-лога; physical hover/minimize/full keyboard/accessibility отдельно NOT RUN. |
| Long Russian / bounded body / render DPI |06-long-minimum/07-long-144dpi-render; render144dpi не OS150% и не monitor transition. |
| Newest-first / gap12 / independent close / overflow |08-stack/09-overflow; все карточки в bounded scroll, неполностью видимые paused; нет отдельной destructive dequeue-очереди. |
| Реальный module flow / подробный ReportWindow | Source integration сохранена; actual Revit/API/ReportWindow save+selection+show NOT RUN. |

Notification scope добавлен отдельными CSV-полями; прежние evidence сохранены. [Независимый Verdict Pass](Notification-Review.md): scoped ship исправлению интеграции,35native assertions/9PNG; successor source contract47checks. Применимость всех64ID оформляет builder; этот verdict не означает all-states или in-Revit PASS. Table/capsule/category/picker/HTML-footer критерии N/A для карточки по отсутствию функции; unavailable Revit/OS tests — NOT RUN.

После уведомлений завершена сборка Build-Msi.bat Release2022/2023/2024. Автоматический Verify-MsiPayload.ps1 извлёк CAB и сверил SHA256 SAB.dll/двух styles/Toolkit/23HTML с текущими сборками; API references и отсутствие host API/test artifacts проверены. Существующие verification.json в outputs/ui-production-preview/msi-check/{year}:PASS/installationPerformed=false. Итоговый Installer-Verification ведёт builder. Это package verification; установка и установленный Revit runtime NOT RUN, установка не разрешена. Использовать existing outputs/ui-production-preview и единое место журнала; evidence — screenshots-notifications, без нового timestamp output на каждый прогон. Историю не удалять. «Бэкап» запускает commit+push+remote verification/rollback commit только по отдельному запросу пользователя.

## «Развёртки»: исторический isolated suite и approval gate до12.10

Прямой запрос12.10 разрешил интерактивный Preview **всего действующего комплекта**, но productionMigrationAuthorized=false для этого модуля. [Inventory](Elevations-Inventory.md) содержит9 active+unreachable RoomPlanSchemeSettingsWindow; [Layout](Elevations-Layout.md) — композицию/64-ID applicability; [Verification](Elevations-Verification.md) — actual Compile, source/fixture boundary и существующий526PASS/47PNG COMPLETED-log. Новые CSV-поля elevations_* отделены от исторических preview0.5/production/notification evidence.

Сейчас доступны main4tabs, decoration3tabs, catalog/rooms,4 компактных выбора/корректировки и modeless crop helper; отдельные progress/report/confirmation fixtures и reuse общего notification stack. Generated copies сохраняют исходные bindings/handlers и реальные settings validation/naming; preference writes исключены. Document collectors/type lists/selection/ExternalEvent ограничены явными fixtures/adapters. DataGrid каталога с теми же2полями не даёт current coverage ListView/GridView/GridViewColumn. Standalone RoomPlanSchemeSettingsWindow — N/A active suite по отсутствию call sites, а не потерянная функция основной вкладки.

Preview evidence: независимый bounded review и заполненный per-ID отчёт builder, показ интерактивного9-window suite пользователю и явное утверждение всего комплекта. Существующий COMPLETED-log подтверждает только перечисленные assertions/снимки, не все состояния. Physical input/accessibility, full keyboard, Revit Document/transactions/native selection/ExternalEvent, actual settings persistence, OS DPI/monitor transitions/browser — NOT RUN. Дополнительные unavailable среды нельзя отмечать N/A.

После approval — отдельный production-перенос9 active окон и shared surfaces в общий слой без смены функций/VM/API, включая сохранность raw input catalog round trip, реальные crop line/create/accept/cancel/return, settings/geometry/columns, regressions параметров и видов/листов. Затем необходимые builds2022/2023/2024, фактические Revit сценарии по доступности и включение изменённых DLL/XAML/resources в Build-Msi.bat payload. Исторический installer PASS двух прежних модулей не закрывает будущий MSI «Развёрток»; install/commit отдельно не разрешены.

Последующее прямое пожелание пользователя: после макетов изменить **существующий общий notification stack на bottom-up**. Fix завершён отдельно:35 PASS/9PNG и scoped ship; новые карточки снизу, рост вверх. Это не разрешение переноса «Развёрток» и не новая система уведомлений. Прежний26-PASS review сохраняет историю первого этапа; текущие notification captures обновлены по тем же путям.

Исторический остаток до утверждения12.10: явное утверждение целого9-window комплекта пользователем, затем production migration; Revit/physical/OS-DPI проверки остаются NOT RUN. Bottom-up notification fix уже завершён,35 native assertions/9PNG, normal MSI rebuild/payload PASS.


## Production1.1.0

[Actualmigrationevidence](RedesignMigration-Verification.md) заменяет isolatedevidence для поставленных окон. Новые redesign_*CSVколонки отделяют225actualassertions/59PNG от635source-derivedsessionassertions/44PNG. Учёт51типа31current/11resource/9missing не означаетпокрытиевсехсостояний; missingexamples/fullphysical/Revit/OSchecks остаются.
