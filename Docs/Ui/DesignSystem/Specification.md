# SAB Fluent Light — единая актуальная спецификация

Базовая визуальная версия0.5.0-source-review; текущий production-этап1.1.0 «Редизайн». [STATUS.json](STATUS.json) — единственный источник утверждения: **approved_for_production_migration; approvedConcept=FluentLightSource; approvedAt=2026-10-11; productionMigrationAuthorized=true; installationAuthorized=false**. Новый прямой запрос разрешил перенести ровно макеты0.5 в production двух модулей; это новое разрешение, а не ретроспективное утверждение исторических протоколов. Код перенесён; подтверждение выполнения, новые тесты и независимый verdict фиксируются отдельным отчётом миграции.

Прямое утверждение пользователя12.10.2026 разрешило весь показанный комплект «Развёрток», общий notification416/status16 и tabs40/text14, production migration, штатный MSI и commit+push/release1.1.0 «Редизайн». Установка не разрешена. Исторический pending suite/proposal завершён этим новым разрешением; старые preview/review результаты не становятся production PASS. Фактические исходники перенесены, текущие build/MSI/review/release результаты фиксирует builder отдельно.

Обязательная цепочка: [repo skill](../../../.agents/skills/sab-wpf-ui/SKILL.md) → STATUS → эта спецификация → [реестр70 проверок](ErrorRegistry.md) → [Workflow](Workflow.md) → [существующие роли](ReviewRoles.md). DESIGN/JSON описывают фактические ресурсы, не задают второй стандарт. Новое прямое требование пользователя имеет приоритет; конфликтующие нормы переносить в явную историю.

## Функции и автономность

Из картинки брать оформление и композиционные приёмы. Поля, значения, зависимости и команды — из кода SAB/задачи. Не выдумывать поиск, флажки строк или preview, которых нет в модуле. Разрешённое дополнение: Все/Выбранные и сохранение/загрузка категорий. Сохранять Binding, Command, DataContext, x:Name, handlers, VM, selection semantics и контекст Revit/ExternalEvent; дизайн не меняет алгоритмы. Проверять конфигурации .NET/Revit по проекту, не обновлять библиотеки ради оформления. Проверки001–005/053/054.

Один общий визуальный каркас, несколько подходящих функции [Layout Patterns](LayoutPatterns.md). До UI-кода записать [Layout Specification](LayoutSpecification.md); для сложного окна сравнить2 схемы. Пользователь описывает функции; Codex выбирает обычную компоновку без повторных вопросов о цветах, отступах и контейнерах. Уточнять критический пробел функции или существенное отклонение от принятой системы.008/056.

Разрешён текущий перенос **обоих** модулей: виды/листы с main и secondary settings; параметризация со всеми4 вкладками и настоящими ParameterPicker/CategorySelectionControl. Переносить ровно принятую композицию0.5, без новых вариантов. Это исторический scope11.10;12.10 отдельно разрешён весь показанный комплект «Развёрток». Остальные окна и установка не разрешены автоматически. Обычные цвета/отступы/контейнеры больше не требуют нового согласования.006/007; [остаток проверок](CoveragePlan.md).

Прямой запрос12.10.2026 разрешил следующий этап: фактическое уведомление «Параметризации» по принятому дизайну, затем общий SAB-шаблон, затем правильный установочник через Build-Msi.bat. Установка не разрешена. Принятый дизайн11.10 и актуальная композиция/источники записаны в [Notification-Layout.md](Notification-Layout.md); notification-проверки059–064 дополняют нормы0.5, не изменяют прошлые approvals и доказательства миграции.

## Ресурсы и токены

Текущие владельцы: `SAB/UI/Styles/SABFluentLight.xaml`, `SAB/UI/FluentLight/Presentation.cs`, `SAB/UI/FluentLight/FluentResources.cs`, `SAB/Cls_ParameterTools/UI/FluentTheme.cs`. Main/secondary views XAML подключают scoped SABFluentLight; SettingsWindow параметров использует отдельный FluentTheme. Старые `SAB/UI/Styles/SABWindowStyles.xaml` и Theme в SmallWindows.cs сохранены; общие ресурсы Revit Application не переопределяются. CategorySelectionControl имеет локальный словарь, ParameterPicker сохраняет собственную view. Новый компонент добавлять в этот слой и [coverage](component-coverage.csv).003/007/048.

Общий notification-слой — `SAB/UI/Notifications/{NotificationMessage,NotificationCard,NotificationStack}.cs` и notification-only styles в SABFluentLight; адаптер параметризации — `SAB/Cls_ParameterTools/UI/ParameterNotifications.cs`. SmallWindows.Toast теперь forwarding wrapper к нему; остальная legacy Theme и ReportWindow сохраняются. Общий компонент хранит строки/тон, без Revit DB objects/callbacks. Он не заменяет остальные notification-системы SAB автоматически.

Build.ps1 собирает настоящие production-файлы без SourcePreview.targets/Compile-подмен; хост компилирует SourcePreview.cs, NotificationPreview.cs, ElevationPreview.cs/generated copies и RedesignProduction.cs. RedesignProduction открывает actual SAB.dll классы «Развёрток»; isolated session copies остаются отдельным scope. GenerateSourceViews.py — compatibility entry point к VerifyNotificationSource.py: проверяет исходники и записывает контракт, не генерирует UI. Старый трансформер хранится в [history](history/pre-migration-GenerateSourceViews.py.txt). Source-contract.json историчен; production-baseline.json/production-source-contract.json в Tools/UiShowcase фиксируют этап миграции до уведомлений и не переписываются под новые исходники. В том контракте десять бизнес-тел сравнивались по hash с единственной нормализацией FluentTheme.→Theme.; два тела имеют только это переименование, поэтому не называются побайтово неизменёнными. Последующие presentation call changes проверяются новым bounded notification proof отдельно. Code-behind main/secondary, их geometry persistence и VM/service layout не заменены.

| Токен | Значение |
|---|---|
| Фон / поверхность | #F6F7FA / #FFFFFF |
| Рамка / разделитель | #D6DEED / #E4E9F0 |
| Основной / вторичный текст | #202B3C / #5A6780 |
| Акцент / hover / selection | #0F6CBD / #095B9F / #E8F2FF |
| Общие error / success / warning | #B42318 / #067647 / #8A4B08 |
| Шрифт | Native Segoe UI24/16/13/12 DIP; source metadata11 |
| Поля / текстовые кнопки |34 DIP, radius4; button paddingX12 |
| Контейнер / коллекция | radius6, outline1 DIP |
| Интервалы |8 действия;12 информационные блоки/под вкладками;16 карточки;20 колонки;24 страница |

Вторичные кнопки нейтральные, основная синяя; текст по вертикальному центру. Не уменьшать шрифт ради сокрытия обрезки. Исходные локальные metadata brushes и цвета/символы VM-валидации сохраняются для точности, но не канонизируются как новые shared tokens.012/035/044.

## Карточки, формы и инструкции

- Отдельная карточка на смысловой раздел с header+content; без декоративных иконок/глифов заголовков и без карточек каждой строки. Сохранить условную Visibility.026/027.
- Поля вертикальной формы имеют общие левый/правый края с labels/соседними рамками. Horizontal margin4 устраняется также в динамической фабрике ParameterPicker; интервалы horizontal toolbar/table editors сохраняются. Во вложенных рядах labels в Grid row0, fields34/actions28 в row1 с общим vertical center.033/034.
- **В окнах нет описания назначения модуля или обучающих инструкций, даже коротких.** Не оставлять пустые рамки вместо текста. Labels, реальные metadata/errors/results/status сохранять. Metadata/status — светлый блок с рамкой; ready text «Настройки готовы.» без обучения.042/044.
- Ровно1 footer «Инструкция» в каждом текущем/следующем окне, включая secondary settings → поставляемый локальный HTML модуля. Catalog fallback только для окна без собственного файла. Параметры: `SAB_HTML_Zapolnenie_parametrov.html`; виды/листы: `SAB_HTML_Sozdat_vidy_i_listy.html`.043.
- Это правило footer относится к рабочим/настроечным окнам. Принятая transient notification-карточка содержит результат, копирование и закрытие; footer «Инструкция» в неё не добавлять.043 для этой поверхности N/A с такой причиной.
- Сохранять имеющийся текстовый результат; не возвращать декоративные рисунки листа и не выдумывать preview в модуле где его нет.054.

## Таблицы, списки и прокрутка

- Все таблицы как виды/листы, включая «Правила»: header38, row44, horizontal separators1; без зебры/вертикальной сетки/постоянных cell outlines. При вводе одна focus-рамка.013/015.
- Actions28×28, gap8/inset8, общий vertical center; «Действия» и точные доступные имена source ToolTip. Последний delete доступен на minimum.014/018/051.
- Header/видимый text-editor — общая ось, left inset10 один раз. Numeric header/видимый editable text центрировать по одной оси (допуск1 DIP), проверять PART_EditableTextBox, не скрытый Label.016/017/058.
- Каждая видимая вложенная DataGrid/ListBox/bound ItemsControl имеет1 contour1/radius6. Table frame inset16 от карточки; ItemsControl padding8. Переиспользовать category frame; у строк этажей только нижние разделители без второго полного frame.031/032.
- Оболочка связанного ItemsControl без видимого содержимого не оставляет полоску18 DIP: CollectionChrome снимает её border/padding, сохраняя измерение дочернего содержимого/восстановление после заполнения. Это не правило скрывать рамку пустой DataGrid/ListBox: они сохраняют структурный контур и свой empty state. ClearRows возвращает исходную1 blank row, не0.030/052.
- Все4 угла отсекать настоящей RectangleGeometry с учётом рамки; ClipToBounds недостаточен.028.
- Длинное содержимое — в bounded region с неподвижной внешней рамкой; footer не перекрыт, продолжение доступно прокруткой, для длинных стеков fade12. Категории скроллят список, не всё окно.029/049/050.
- Вертикальный DataGrid ScrollBar только в row area, ниже header и видимой верхней группы («Без раздела»). Header до края таблицы; group gutter получает фон/нижнюю линию группы без белого канала. Сохранить native parts/bindings/scrolling/group collapse; sticky groups не обещаны.045/046.
- Обычные колонки помещаются на старте без лишнего horizontal bar; minimum/dynamic columns допускают прокрутку с доступными действиями. Текущий пример PlanKind150/ViewName min130 сохраняет полный «План стандартный».019.

## Капсулы, вкладки и действия

- Взаимоисключающие modes — capsule с движущимся индикатором; RadioButton/TabItem/GroupName/bindings/focus сохраняются. Независимые Enabled/Required/multiple selection не превращать в radio; категории — switches.020/053.
- Outer min34, inner min30, inset2. Однострочная группа компактна как соседние кнопки; wrapped text растёт по содержимому с paddingY6. Сегменты общей высоты, indicator/radius по измерению. Скрытая опция не оставляет пустое место, одна опция занимает track−4.021/022.
- Вкладки — capsule только по ширине headers, content не окрашивать. Текущий общий TabItem: Auto height, min40DIP, текст14, padding20,8; track и indicator измеряются по содержимому. Mode capsules сохраняют min34/30/inset2. До рабочей поверхности12 DIP во всех вкладках без двойного padding.023/024.
- Движение160ms CubicEaseOut учитывает ClientAreaAnimation; выбор обновляется сразу. Static capture после settle (сейчас220ms), animation clock проверять отдельно.025.
- Подписанный поиск непосредственно перед своей коллекцией; настройки/группы действий выше/рядом. Показать; Выборки:Сохранить/Загрузить; Выделение:Выделить все/Включить/Выключить. На узости переносить группу целиком.036/037.
- Импорт/Экспорт конфигурации в footer рядом с Инструкцией, сгруппированы без описательного абзаца. Статус отдельной строкой, кнопки одного ряда одной высоты/gap8.038/055.
- Не дублировать row plus второй кнопкой того же добавления; для фактически пустой коллекции один доступно названный plus.039.
- Multiple selection:Все/Выбранные, Save/Load stable IDs +Format/Version; unknown IDs с отчётом, invalid/cancel без изменения выбора. Highlight не равен Selected.040/041.

## Уведомления: результат и отдельные данные

- Заголовок «SAB · Параметризация», текстовый исход «Завершено»/«Завершено с пропусками»/«Операция отменена»; для исходных служебных сообщений — «Сообщение». Цвет дополняет текст. Сводка — selectable read-only TextBox; нет обучающего prose. Крестик28 и Escape закрывают только свою карточку.059.
- Только при наличии данных показывать подписанные блоки «ID элементов» и «Ошибка для поддержки». «Копировать ID» передаёт только положительные уникальные ID; «Копировать» — полный подготовленный контекст операции/параметра/элементов/причины/технических деталей. «Скопировано» только после успешного Clipboard.SetText; при исключении «Не скопировано» и причина в tooltip; feedback2секунды. Текст остаётся выделяемым.060/064.
- Карточка: white/background/border из текущего словаря, outline1/radius6/padding16, ширина стека416DIP с ограничением work area. Title16Semibold, outcome16Bold, summary13 с line height20; code Consolas12/line height19.2, radius4/padding10,12. Это параметры общего notification-компонента, не новая глобальная типографика. Summary имеет прозрачный контур1/radius4/padding10; optional Warnings — отдельный selectable блок с серой заливкой/копированием. ID/support сохраняют neutral code frames. Header и footer остаются снаружи bounded scroll body; countdown12 и determinate ProgressBar высотой2.059/063.
-10секунд активной полной видимости для каждой карточки. Stopwatch/monotonic delta; hover/focus/copy, невидимость, disabled/minimized owner приостанавливают расход. Снятие всех причин продолжает остаток, без reset после copy. Видимая строка показывает оставшиеся секунды; progress.Value отражает тот же остаток.061.
- Один owner-bound немодальный stack Window, исключённый из общего SabWindowBehaviorService: новые снизу, рост стека вверх, gap12, независимые close/lifetimes, ShowActivated=false. Overflow сохраняет все карточки в bounded scrolling stack; частично/полностью невидимые не расходуют время. При добавлении во время чтения сохраняется позиция читаемой части. Закрытие последней/owner освобождает timer/events/static reference.062/063.
- Позиционирование справа снизу work area монитора owner HWND, inset16; новые карточки у нижнего края, существующие поднимаются вверх. MonitorFromWindow/GetMonitorInfo/GetDpiForWindow и пересчёт каждые250ms. Наличие этой логики не доказывает реальные OS DPI/multi-monitor transitions: они NOT RUN без соответствующего окружения.063.
- Адаптер берёт существующие counters/commit state/Issue данные синхронно в команде и формирует immutable текстовый DTO. Timer/input callbacks общего слоя выполняют clipboard/close/scroll, без Revit API/transactions. Исходный ReportWindow с сохранением/копированием/selection/show остаётся; краткое уведомление не подменяет полный отчёт.064.

## Проверка и доказательства

[Workflow](Workflow.md) обязателен для всех ролей. Каждый из65 ID получает applicability и PASS/FAIL/NOT RUN/N/A с доказательством и scope. Применимые состояния, длинный русский текст, minimum, последний столбец, keyboard проверять отдельно; один screenshot не закрывает все состояния.009–011/047/056–064.

Исторический isolated preview до переноса: [27 native PNG0.5](screenshots-v0.5/), [75 программных PASS/COMPLETED](screenshots-v0.5/verification.txt), [source-contract](../../../Tools/UiShowcase/source-contract.json):135 main/103 secondary bindings,10 columns/10 unchanged methods. Full review0.5 был fix; финальный ship — по3 исправлениям Verdict Pass. [Протокол](FinishReview-0.5.md). Этот исторический verdict не заменяет новый production review; текущее approval получено отдельным прямым запросом. [Verification](Verification.md): Revit/Document/transactions/ExternalEvent, physical input/drag-drop/full keyboard/accessibility, actual browser launch, OS DPI transitions NOT RUN;144dpi только RenderTargetBitmap.

Текущий production-хост использует actual SAB classes/resources с синтетическими DTO вне Revit. Новый пакет расположен в `screenshots-production-0.5`; его итоговые числа, provenance и независимый verdict сообщает отчёт миграции, а не исторические75 PASS. Этот documenter не запускал build/UI/capture и не объявляет новые runtime PASS. Installation, Revit Document/transactions/ExternalEvent, physical input/accessibility и OS DPI остаются отдельными проверками. Новый пользовательский дефект включать в реестр/общую проверку.

Миграция двух модулей завершена отдельным [отчётом](ProductionMigration-Report.json)/[review](ProductionMigration-Review.md):27PNG/81native WPF PASS,25source checks относятся к тому этапу. Дополнение [Revit2022](Revit2022-Verification.md) подтверждает сборку, не native runtime/API22 execution. Эти proof/snapshot историчны относительно последующего notification-кода и не должны переписываться под него.

Уведомления проверяются отдельным [native WPF-логом](screenshots-notifications/verification.txt) и9PNG в screenshots-notifications: actual SAB.dll classes, synthetic fixtures, real Windows clipboard success и injected failure, selectable text, bounded long text, stack/overflow.144dpi — RenderTargetBitmap, не системные150%. Свежий лог содержит35 PASS/COMPLETED; [независимый Verdict Pass](Notification-Review.md) дал scoped ship исправлению интеграции и показанным WPF-сценариям. Successor notification-source-contract.json содержит47source checks; applicability всей задачи фиксирует builder отдельно. Documenter не запускал UI/сборки. Revit execution/API22, physical hover/full keyboard/accessibility и реальные OS monitor/DPI transitions остаются NOT RUN. Table/capsule/category/source-picker критерии N/A только для самой notification-поверхности, не для затронутого module flow.

Риск повторного использования: CategorySelectionControl также создаётся в legacy CategoryCheckWindow; внешняя legacy Theme-оболочка сохранена, но влияние нового контролa на это окно требует отдельной проверки. Не выдавать неизменность SmallWindows/global dictionary за доказательство отсутствия всех downstream-визуальных эффектов.

## История, не текущие нормы

[Отменённые правила](ErrorRegistry.md#отменённые-требования-и-история), [архив прежнего стандарта](history/Specification-through-0.5.txt). Redesign0.2/Refinement0.3–0.5/старые review сохраняют историю. Unified/UI-Lab, row86/sidebar300, жёсткие capsules34 и инструкции в рамках не применять. Прежнее согласование каждого окна отменено автономным workflow. Историческая gallery0.1 исключена из active csproj, её файлы не доказывают текущее coverage. Исторический Direction contract0.2 существует; нового approved comp/отдельного QUALITY BAR/five-block документа для0.5 нет.


## Последние уточнения макетов «Развёрток» (12.10.2026)

- Составное поле «тип марки + выноска»: структурный зазор12DIP, общий вертикальный центр≤1DIP. Generic field normalization не заменяет разделитель пары. Все6 пар проверяются normal/minimum, включая disabled/enabled. SAB-UI-066.
- Таблица при горизонтальном растяжении: первая/последняя колонки следуют краям viewport. Растут только текстовые поля, служебные числовые/статус/действия остаются фиксированными. При нехватке места разрешён horizontal scroll. Actions header и геометрический центр всей группы кнопок совпадают≤1DIP; trailing margin не сдвигает группу. SAB-UI-067 и016.
- Короткие диалоги выбора: NoResize, SizeToContent по кнопкам/заголовку, группа кнопок центрируется; лишней пустой карточки нет. SAB-UI-068.
- Новые вкладки макетов: минимум40DIP, текст14; capsule mode selectors по-прежнему min34 и растут при переносе текста. Это уточнение текущей цели, ещё не перенос всех production-окон.
- Независимые категории отметок Полы/Потолки/Перекрытия: прежний master switch остаётся. Schema9 хранит выбор,1–8 сохраняют прежнее включение всех категорий. Сервис отбирает цели/предупреждения по категориям; каталог требует только реальные роли выбранных операций. Горизонтальные размеры требуют FinishWall, вертикальные FinishFloor+Ceiling+StructuralSlab. SAB-UI-069.
- Прогресс сохраняет прежнее окно и16DIP bar по прямому указанию пользователя. Для исходного progress HTML-footer N/A; функциональный close guard сохраняется.
- Утверждённый общий notification template применён в production:416DIP вместо520, статус16Bold, summary в контуре без заливки, warning/copy секции с фоном; bottom-right/newest-bottom сохраняется. Прежний isolated proposal pending — история до approval12.10.

История до approval12.10: сначала были разрешены только functional deltas и их MSI; оформление suite/proposal оставалось isolated. Этот scope заменён прямым утверждением всего комплекта. Первоначальный baseline93файла и historical635PASS/44PNG не перезаписывать; текущий production delta описан в Tools/UiShowcase/elevations-production-migration.json.


## Уточнение развёрток: выбор, границы и итоговое уведомление

Три самостоятельные карточки выбора: Высотные отметки над Размерами слева, Марки справа на общую высоту. Границы задаются пространственно вокруг прозрачного прямоугольника с пунктирным контуром, подписи/значения центрируются. Прежние combined WhatCreate и crop2×2 отменены последним запросом.

Финальное создание развёрток выводит общий notification вместо modal report. Summary содержит только ненулевые успешные действия, листы с результатом; отдельный Warnings блок с серым фоном содержит все накопленные причины, FailedViews, неразмещённые виды и fallback. Цветные статусы: Success/Warning/Error по наличию результата и проблем. Исходный локальный диагностический журнал сохраняется. API objects читаются до открытия modeless UI; notification получает только строки. SAB-UI-070.

Независимые категории высотных отметок управляют не только target creation, но и заменой прежних owned annotations: выключенная категория не удаляется при частичном оформлении. Проверять8комбинаций хранения/role gates и7валидных UI→model mappings. Показанный комплект утверждён12.10 и перенесён в9actual production views/classes; текущая VM имеет null-Document guard для host, реальные collectors остаются при наличии Document. Catalog CaptureDraft/RestoreDraft и command round trip сохраняют незаконченный raw input. Общие notification416/status16 и tabs40/text14 применены в production. Это не доказательство actual Revit/API/persistence runtime.

## Текущий production-пакет1.1.0 «Редизайн»

Владельцы:9 активных views/classes SAB/Cls_InteriorElevations, ElevationSettingsViewModel, catalog draft round trip в DecorateInteriorElevationsCommand; общий SABFluentLight/FluentLight и NotificationCard/Stack. [Production migration manifest](../../../Tools/UiShowcase/elevations-production-migration.json) фиксирует before/after файлов, отдельные прежние functional deltas не скрываются. Unreachable RoomPlanSchemeSettingsWindow не мигрируется как десятый active workflow; функция плана-схемы сохранена в main tab.

Свежие COMPLETED native WPF пакеты: [actual production](screenshots-redesign-production/verification.txt)104PASS/21PNG; [shared notifications](screenshots-redesign-notifications/verification.txt)35PASS/9PNG; [прежние два модуля](screenshots-redesign-modules/verification.txt)81PASS/27PNG; [isolated sessions](screenshots-redesign-sessions/verification.txt)635PASS/44PNG. Их scope раздельный: sessions не actual production, DTO/Document-null host не Revit. Historical635/44 в screenshots-elevations-preview остаётся прежним approval packet. Build/MSI/independent review/release проверяются отдельно; не переносить предыдущий Installer PASS на1.1.0 без нового payload proof. Installation и Revit/physical input/OS transitions здесь NOT RUN.


## Исправления после runtime feedback13.10

Текущая композиция «Развёрток»: две modeкапсулы сверху, без длинного inline selectionsummary; footer exactorder Инструкция/Отмена/Список помещений/Выбрать линии/Создать развёртки. Сведения выбора — отдельная readonlycollection на detachedsnapshot, безadd/delete. Optionalfooterstatus толькоrow0, actionsrow1, обе строки проверяются при непустом тексте. Notificationresources полностьюscoped: чистый Application безlegacyfallback обязателен; presentationfailureне превращает completedoperation в commandfailure. Критерии071–073; прежняяtopstatus композиция отменена последними текстовыми указаниями.


## Параметризация: актуальное уточнение13.10

Проверка заполненности использует полный Catalog.Categories как main parameterization, собственные CheckCategoryIds независимы отfillprofile/visibleelements. Existing check command/hotkey запускает подсветку напрямую, отдельные настройки доступны соседней кнопкой. Ribbonorder сверху вниз: Проверка параметров / Настройки проверок / Снять подсветку. Последний прямой ответ отменяет отчёты подсветки и кнопку отчёта: только успешное уведомление «Подсветка элементов активирована». Отчёты заполнения не затронуты. WhatCheck имеет запрошенный explanatoryfooter вfilledframe надCancel/Check (узкое явное исключение прежнего общего запрета inlineinstructions), no bluestub. EditableParameterPicker открывает popup только от пользовательского ввода/явного раскрытия; template/load/обновлениеSources не меняет выбраннуюGUID/ID identity. Критерии074–077.

## Дополнение15.10: нумерация развёрток

В карточке «Наименование вида» после существующих наименований располагаются «Номер вида», поля «Префикс» / «Суффикс» и пример первых трёх номеров. Существующие scoped field styles и две равные адаптивные колонки; введённый текст сохраняется буквально. При коллизии суффикс всей группы изменяется согласованно, числовая последовательность берётся из исходных индексов линий. SAB-UI-078; [компоновка](ElevationNumbering-Layout.md).

Предпросмотр направлений использует текущий план Revit и временные GenericAnnotation instances. Кнопка «Показать превью» — в карточке «Параметры вида»; фиксированный footer настроек сохраняется. Modeless Compact Dialog использует общие scoped styles, metadata/status и действия «Сменить сторону», «Отмена», «Создать развёртки» с одной footer «Инструкция». Направление и точка меняются совместно, подтверждение использует тот же draft и номера; X/Escape ожидают cleanup. Сохранение/SaveAs/синхронизация завершают preview перед записью модели. Масштаб плана настраивает пользователь. SAB-UI-079; [контракт](ElevationDirectionPreview-Layout.md).


## Актуальное runtime уточнение15.10

Прямой запрос отменяет прежнее расположение Preview в Parameters card и main Cancel. Новый fixed main footer: Инструкция / Список помещений / Показать превью / Выбрать линии / Создать развёртки; первые2white/outline, последние3sharedprimaryblue. Одна строка наminimum800; close chrome остаётся. Modeless preview Cancel сохраняется для cleanup. SAB-UI-073/080; [компоновка и сценарии](ElevationPreviewRuntime-Layout.md).

Document identity во всех preview lifecycle callbacks и continuation определяется Document.Equals, план — значением ElementId. Виртуальное замыкание открытой цепочки соединяет только свободные topology endpoints и используется только после проверки simple polygon/nonzero area; без Revit element/view/number. Virtual clearance учитывается; preview остаётся обязательным для открытых цепочек. SAB-UI-081.


## Привязка preview16.10

По прямому запросу annotation origin находится на кончике стрелки и вставляется в середину source line. Flip вращает annotation на месте, real elevation MarkerPoint отдельно меняет сторону с прежним MarkerDistance. Новый RFA2022 применяется в пакетах22/23; существующее в модели семейство автоматически не перезагружается. Читаемость подписей — native Keep Readable каждой метки, без обещания постоянно горизонтального текста. WPF/footer не менялись; предыдущие captures относятся к1.1.3. [Контракт и evidence](ElevationPreviewTip-Implementation.md).

## Окно «Направления»: уточнение16.10

Прямое пользовательское указание заменяет широкую компоновку и видимую Cancel предыдущих preview-протоколов. Заголовок «Направления», без статистики марок/контуров. Три равные по ширине кнопки строго вертикально: синяя «Сменить сторону», белая «Инструкция», синяя «Создать развёртки»; общие scoped styles, min34, gap12, padding24. Width240/NoResize/SizeToContent.Height. Пустой статус полностью Collapsed; operational error/busy остаются в bounded180 scroll. Крестик/Escape сохраняют CancelRequested/ExternalEvent/cleanup. HTML не содержит выделенных на пользовательском скриншоте абзацев Keep Readable, коротких участков и технических требований семейства; предупреждение о запрете штатного перемещения/поворота временных марок сохранено с точным названием «Сменить сторону». SAB-UI-082; [компоновка](DirectionsWindow-Layout.md).

## Подсказки выбора16.10

Последний запрос явно разрешает учебные подсказки в transient notifications: общий обзор линий→Escape→помещения сразу после «Выбрать линии», guide для каждой multiple group и точное «Линии выбраны, выберите помещение.» перед room picker. После группы сначала выбирается следующая группа либо завершение; прежний алгоритм не переставляется ради подсказки. Shared NotificationCard/Stack416 и10active seconds, безHTML-footer; только прежняя guide заменяется, остальные уведомления и native focus сохраняются. Inline instructions в рабочих окнах по-прежнему запрещены. SAB-UI-083; [контракт](SelectionGuidance-Layout.md).

## Выбор материалов16.10

Пользователь утвердил макет ручного выбора материалов и явно разрешил реализацию, обновление дистрибутивов, commit+push и следующий последовательный выпуск1.1.3 после опубликованного1.1.2. Установка не разрешена. Это отдельное разрешение на данный модуль; исторические scopes выше сохраняются. Текущий этап и фактические доказательства — `STATUS.materialSelection`, [компоновка](MaterialSelection-Layout.md), [отчёт](MaterialSelection-Report.json) и [независимый review](MaterialSelection-Review.md).

Data Manager показывает ровно четыре колонки: «В работу», «Материал», «Штриховка сечения», «Действие»; «Класс» отсутствует. Обычный текст строк чёрный #000000, «Создать» использует общий Accent #0F6CBD, «Обновить» Success #067647, «Нет штриховки» Error #B42318; обычное и выделенное состояния сохраняют эти foreground. Чёрный текст — локальное прямое требование, не новый глобальный токен. Строки44/header38, collection outline1/radius6, обычные поля/кнопки34 и остальные shared styles сохраняются. Окно900×780, minimum760×620; имя расширяется, pattern230/min180, action142, inclusion82DIP.

Каталог содержит все материалы активного документа, включая неиспользуемые. Extended highlight отличается от Included: фильтры «Все/Выбранные/Невыбранные» работают по Included, поиск — по имени, массовые действия применяют snapshot выделенного пакета, «Выделить все» охватывает только видимые строки. Скрытые включённые строки сохраняются и учитываются при подтверждении. На старте Included=false; нулевой набор и набор только пропускаемых строк отключают выполнение. Коллекторы и синхронизация остаются внутри существующего Execute/Run; modal UI получает detached DTO (UniqueId/строки/флаги), при подтверждении передаёт только включённые UniqueId в прежнюю Synchronize. Отмена не начинает её транзакцию. Настройка SkipMaterialsWithoutCutPattern сохраняется: красное «Нет штриховки» само по себе не означает пропуск, когда настройка выключена; tooltip раскрывает это поведение.

Главная ribbon identity `SAB_FilledRegionFromMaterial_Settings` теперь вызывает CreateFilledRegionTypesCommand; отдельная кнопка настроек — SettingsCommand. Старые IncludeUnused/IncludePainted поля настроек скрыты, действие настроек называется «Выбрать материалы». Единственная footer «Инструкция» открывает локальный `SAB_HTML_Oblasti_zalivki.html`. Shared `FluentResourceDictionary` в `FluentResources.cs` подключает тот же внешний SABFluentLight из compiled BAML; словарь не клонируется, Application resources не изменяются. Локальный критерий SAB-UI-084 проверяет четыре колонки, цвета и границу ручного выбора. Actual WPF fixture вне Revit проверяется отдельно от Revit/physical input/OS DPI/MSI/publication.

## Лента: группировка и цвета17.10

Прямой запрос17.10: «Настройки областей заливки» — второй пункт сплит-кнопки «Области заливки из материала», создание остаётся основным действием независимо от последнего выбранного пункта. Все иконки модуля, включая вложенные и нарисованные элементы составных значков, имеют один RGB: Настройки0,0,0; Библиотека128,128,128; Альбом и графика92,109,187; Материалы246,169,80; Параметры134,162,147. Развертки и Экспликации сохраняют исходные цвета. Каркас скрыт через Visible=false панели, модуль и обе команды сохранены.

Общий владелец палитры — `SAB/Helpers/Ribbon/Ribbon.cs`; окна и их semantic tokens не меняются. Обычная перекраска сохраняет альфа-канал. У заполненного значка Библиотеки белые внутренние прорези преобразуются в прозрачный negative space с сохранением сглаживания; заливка одним RGB не должна уничтожать детали.16/32DIP и нормализация96DPI сохраняются. SAB-UI-085; [контракт и пределы проверки](RibbonModules-Layout.md), [отчёт](RibbonModules-Report.json). Native asset sheet вне Revit не доказывает actual ribbon/popup/input/OS DPI.

## Оформление развёрток: один вход в каталог17.10

Последующий прямой запрос удаляет ribbon-кнопку «Настройки оформления». Команда ConfigureElevationDecorationCatalogCommand и вся функциональность каталога сохраняются; вход — «Каталог элементов» (прежнее «Каталог») в header окна оформления. x:Name/handler/OpenCatalogRequested/draft roundtrip не меняются. «Оформить развертки» становится самостоятельной большой ribbon-кнопкой с прежней command identity и контурной иконкой Lucide pencil-ruler в прежнем розовом RGB251,152,152; SVG embedded, vector16/32, stroke1 на24grid, ISC attribution в поставляемом RibbonIcons-Credits.md. Остальные иконки/цвета/окна не меняются.

Инструкция отражает новый путь и существующее ограничение выбора образцов на отдельном виде, не на листе. SAB-UI-088; [layout](DecorationRibbon-Layout.md), [отчёт](DecorationRibbon-Report.json). Actual WPF/null Document click/draft/render не заменяют native Revit selection/ribbon/input.

## Настройки уведомлений: первый этап18.10

Прямой запрос разрешает начать реализацию рабочего окна и увеличить его главный переключатель; последующее уточнение требует выключенный модуль при первой установке. Это scope исходников/сборки, без MSI, установки и публикации. [Production layout](NotificationSettings-Production-Layout.md); первичные preview1/2 остаются отдельной историей и не доказывают production.

Окно SAB.Notifications.SettingsWindow сохраняет две вкладки «Правила»/«Журнал», три независимых контекста над поиском, точные действия «Не подавлять»/«Подавлять с уведомлением SAB»/«Подавлять с записью в журнал». Смысловая текстовая метка использует общий SabNotificationTypeBadgeStyle; сведения о подтверждении отдельны. Именованный SabLargeSwitchTemplate: track56×32DIP, knob24/margin4, hit64×40 и видимая keyboard focus-рамка. Обычный SabSwitchTemplate38×22 сохраняется. SAB-UI-091; measured runtime и полная матрица состояний фиксируются новым протоколом, не выводятся из setters.

Отсутствующие, старые и повреждённые настройки дают Enabled=false; включение только через окно и «Сохранить». Намеренно сохранённое Enabled=true schema2 восстанавливается при следующих запусках/обновлениях. Draft отделён от committed settings, Cancel/Escape/close не сохраняют; schema2 записывается атомарно, прежние blanket flags/DialogResults не становятся разрешёнными правилами.

Первый этап реализует окно, сохранение и чтение/экспорт реального журнала. Выбранные действия ожидают проверки; «Доступно к подавлению: 0». Общий переключатель пока включает только наблюдение и журнал. Активные обработчики не удаляют предупреждения, не выбирают исправление, не откатывают транзакции и не подтверждают диалоги. Revit получает все сообщения штатно. Контексты и длительность уведомления сохраняются для будущих проверенных правил. UI содержит только строки/снимки журнала; Document/UIApplication в VM не передаются. Revit/реальные OK/physical input/OS DPI — NOT RUN; текущий этап не означает завершённое подавление.

## Внутренние отступы footer статуса18.10

Уточнение пользователя на первичном снимке редактора шаблонов: текст не должен прилегать к нижней рамке. SAB-UI-092, [новая компоновка](TemplateEditorFooter-Layout.md) имеет приоритет для этого footer. Opt-in shared SabFooterStatusPanelStyle на основе SabValidationPanelStyle: Padding12,10DIP / MinHeight44 / HeightAuto; SabFooterStatusTextStyle на основе SabStatusTextStyle: Font13 / LineHeight20 / BlockLineHeight / Wrap / VerticalCenter / Margin0 / Padding0. Старые SabValidationPanelStyle/SabStatusTextStyle не меняются; новую геометрию не навязывать всем окнам.

Для ViewTemplateGraphicsWindow сохранить card→status16DIP±1 локальным распределением body bottom4 + footer top12. Это реализация суммарного прежнего gap16 только в данном окне. Без validation между статусом и действиями12; при видимой validation между status/validation и validation/actions по12. Нижний footer padding12; длинные status/validation увеличивают Auto-footer. Проверять actual text bounds и воздух внутри всех четырёх сторон панели, внешние gaps и доступность нижнего края/кнопок normal/minimum/tall; setters не заменяют измерения. Новые captures/report/review отдельно от прежнего production proof; Revit/physical/OS DPI не выводить из WPF.
