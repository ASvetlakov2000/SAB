# Материалы → типы областей заливки

Пользователь16.10.2026 утвердил интерактивный макет и разрешил реализацию в основном модуле, commit+push, обновление дистрибутивов и следующий последовательный выпуск1.1.3 после опубликованного1.1.2. Последнее уточнение: убрать класс; обычный текст чёрный; «Создать» синий, «Обновить» зелёный, «Нет штриховки» красный. Это разрешение на данный модуль; установка не разрешена. Доказательства сборки MSI/публикации фиксируются отдельно от UI.

## Layout Specification

Data Manager: SAB chrome → «Выбор материалов» → карточка «Материалы документа» → Показать (Все/Выбранные/Невыбранные) и Выделение (Выделить все/Включить/Выключить) → подписанный поиск → DataGrid → счётчики → footer status → Инструкция/Отмена/Создать и обновить.

Master–Detail отклонён: редактирования материала нет; боковая панель отнимает место у длинных имён. Четыре колонки: В работу / Материал / Штриховка сечения / Действие. Расширяется имя, остальные колонки имеют обоснованные минимумы. Таблица ограничена строкой Grid *, footer не прокручивается. Native window 900×780, minimum760×620; long names доступны через tooltip. Shared SABFluentLight/Presentation: row44/header38, outline1/radius6, поля/actions34; группы переносятся целиком. Локальная явная чёрная кисть текста — прямое пользовательское требование, не глобальная смена цветов SAB.

Выделение Extended (native Shift/Ctrl) отдельно от Included. Выделить все — только видимые строки. Включить/Выключить/Space/тумблер выделенной строки работают по snapshot выделенного пакета. Все/Выбранные/Невыбранные фильтруют Included. Поиск только по имени (класс удалён); фильтры/поиск сохраняют Included, скрытые включённые строки учитываются при подтверждении. Все включения на старте выключены, чтобы прежняя автоматическая массовая операция не выполнялась без явного выбора. Нулевой набор и набор только пропускаемых строк отключают выполнение.

Коллектор всех Material активного документа выполняется внутри существующего Execute/Run до окна; настройки IncludeUnused/IncludePainted больше не ограничивают каталог ручного выбора. DTO содержит UniqueId и строки/флаги, без DB objects. Execute после modal ShowDialog пересекает выбранные UniqueId с собранными Material и передаёт только их существующей Synchronize. Отмена не открывает транзакцию. Существующие mappings/metadata/обновление instances и subtransactions сохраняются. SettingsCommand передаёт owner HWND в общий Run.

«Нет штриховки» — отсутствие обоих cut-pattern IDs; красный текст. Пропуск остаётся под существующей настройкой SkipMaterialsWithoutCutPattern. Если она выключена, материал без штриховки может создать/обновить пустой тип; tooltip и итоговые счётчики должны это отражать. Материалы с не-Drafting pattern не фильтруются молча: прежняя валидация Synchronize/EnsureDraftingPattern сохранена.

## Проверки и ownership

Окно/DTO: Cls_FilledRegionFromMaterial/MaterialSelection{Item,Window}. Shared presentation/styles не копируются. Command: CreateFilledRegionTypesCommand.Run; SettingsCommand.RunRequested. Evidence: фактический production-класс в существующем WPF host, normal/minimum/selected/unselected/search/empty/long names/status colours/footer; проверки пакетного изменения snapshot, выключения в selected-only, сохранения скрытого выбора, отмены, отсутствия класса, column widths, нормального/выделенного foreground. Совместимость: сборки2022/2023/2024; модельные операции/Revit runtime, physical input и OS DPI отдельно NOT RUN без реального запуска.

Применимость всех текущих ID фиксируется в [MaterialSelection-Report.json](MaterialSelection-Report.json); первоначальная матрица подготовлена builder до UI-кода, результаты заполнены по фактическому запуску. Новый локальный критерий [SAB-UI-084](ErrorRegistry.md#sab-ui-084): четыре колонки и измеренные чёрный/синий/зелёный/красный foreground в обычном/selected состоянии. Общая система не изменяется. Существующие профили Save/Load категорий не переносятся как скрытая новая функция материалов.

## Фактические значения и доказательства

| Элемент | Production-значение |
|---|---|
| Обычный текст строк / действие | #000000 / Create #0F6CBD, Update #067647, NoPattern #B42318 |
| Колонки | inclusion82; material */min190; cut pattern230/min180; action142DIP |
| Типографика | Segoe UI13; shared title24SemiBold; labels/counts12; shared section16SemiBold |
| Таблица / switch | row44/header38; outline1/radius6; switch38×22DIP |
| Группы / footer | Whole-group WrapPanel; page24; group-to-search12; status separate row, margin-bottom8; Cancel/Run gap8 |
| Ресурсы | Typed FluentResourceDictionary → FluentResources.CreateDictionary → внешний UI/Styles/SABFluentLight.xaml; никаких копий/изменений общей палитры |

Actual SAB.dll MaterialSelectionWindow снято существующим MaterialSelectionEvidence.cs: [verification.txt](screenshots-material-selection/verification.txt), COMPLETED42PASS/9PNG; synthetic80rows, clean Application resources. Состояния: normal, selected-colours, included-only, search-hidden-selection, empty-search, no-cut-pattern, minimum, minimum-last-row, empty-document. RenderTargetBitmap96dpi — значение offscreen render; runtime/OS DPI в этом fixture не измерены. Проверены automation toggle, routed mouse/Space и modal confirm/cancel; реальный Shift/Ctrl/полный physical keyboard, Revit-модель и OS monitor transitions NOT RUN. Независимый [review](MaterialSelection-Review.md) имеет только UI/code scope.
