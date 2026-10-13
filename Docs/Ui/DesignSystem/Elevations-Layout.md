# «Развёртки»: Layout Specification для согласования

Дата: 2026-10-12. Scope: изолированный WPF Preview, **productionMigrationAuthorized=false для этого комплекта**. Общее утверждение Fluent Light не утверждает окна этого модуля. После показа требуется явное согласование пользователя. Макеты «Развёрток» не включаются в MSI до согласования. Отдельное изменение общего notification stack поставляется штатной цепочкой.

Источник функций: `SAB/Cls_InteriorElevations/Views`, ViewModels, Commands, selection/settings/report services; подробная карта — [Elevations-Inventory.md](Elevations-Inventory.md). Текущее незакоммиченное состояние351файла сохранено в единственном `outputs/ui-production-preview/elevations-before.zip`; это локальная защита изменений, не GitHub backup/commit.

## Исторические схемы первой редакции

Следующая таблица и её wireframe сохранены как история. Актуальные решения горизонтальной панели и единой формы оформления описаны ниже в разделе «Уточнения пользователя» и заменяют прежний выбор.

| Поверхность | Обязательная структура и действия | Выбор схемы / альтернатива |
|---|---|---|
| ElevationSettingsWindow | Источники:1/несколько помещений, host/link, selection status+pick.4вкладки: Развёртки (формулы+текстовый результат, тип/шаблон/масштаб/глубина/отступ, crop4границы+пример, оси); Лист (3режима, новый/существующий, placement/point, viewport/title); План-схема (create/place/manual position/point/формула/template/scale/crop/tag); Марки (2типа,состав,above/below). Footer Инструкция/Отмена/Создать. | Tabbed Workspace+Results & Preview. Выбрана постоянная узкая зона исходных данных слева,4capsule tabs и bounded scroll справа. Альтернатива: исходные данные сверху над tabs; отвергнута из-за потери вертикального пространства и прокрутки формы отдельно от выбора. На minimum боковая зона остаётся компактной, длинные подписи растут, все разделы достижимы. |
| ElevationDecorationWindow |3вкладки Состав/Типы марок/Каталог, все независимые flags и dependencies, spots/types/base/side/offset, dimensions3types/offsets, tags6types/leaders, catalog status+configure, validation, Оформить. | Tabbed Workspace, отдельные семантические карточки; внутри существующие responsive form grids. Альтернатива:6разделов в одном длинном scroll; отвергнута, т.к. типы и каталог редко меняются и скрывают основную задачу. |
| ElevationDecorationCatalogWindow | Role selector; host/linked add;2колонки type/source; remove selected/clear role/Done. | Data Manager. Настройки/selection actions выше своей bounded framed collection; без нового поиска или новых функций. Альтернатива master-detail создаёт лишнюю навигацию для8ролей; сохраняем реальный ComboBox. |
| MultiRoomSelectionWindow |7исходных колонок, row pick/plus/delete, реальная validation/summary, Transfer/Cancel. | Data Manager с таблицей в собственной1DIP рамке и footer. Минимум допускает horizontal scroll, последние действия достижимы. Plus не дублируется. |
| ElevationCropAdjustmentWindow |4signed numeric offsets, Apply/Cancel, validation. | Compact Dialog: четыре подписанных поля в2×2, без декоративного изображения границы. Отрицательные значения сохраняются. |
| LineGroupSelectionWindow | single/multiple action+cancel; реальные bool return semantics. | Compact Dialog. Два действующих выбора остаются кнопками: они завершают диалог, а не переключают независимый режим формы. |
| LineGroupNextActionWindow | group number status, next/finish/cancel. | Compact Dialog, статус в отдельном блоке; оба действия рядом, cancel в footer. |
| CropByExampleActionWindow | PickLine/CreateLine/None | Compact Dialog, два действия и закрытие. Обучающие абзацы вынесены в существующий HTML. |
| CropByExampleLineCreationWindow | modeless status, select/create/accept/cancel; enable gates lineSelected/sampleCreated | Compact Dialog modeless. Статус и действия видимы постоянно. Preview adapter имитирует только результат отсутствующего Revit/ExternalEvent, никогда не создаёт модель. |
| Shared progress/report/confirmation | Existing progress/close guard (без отмены); report full selectable text; confirmation choice retained | Preview showcases existing shared visual resources/template. Данные/текст из report services; подтверждение и подробный отчёт не превращать в toast. |

Отдельное RoomPlanSchemeSettingsWindow — без call sites, не входит в утверждаемый active suite; встроенная вкладка сохраняет настоящую функцию создания планов-схем.

```text
Основное: SAB header → title → [источники | capsule tabs / scroll карточек / текстовый результат] → status/actions footer
Оформление: SAB header → title → capsule tabs → scroll отдельных карточек → validation/actions footer
Каталог/помещения: title → настройки/действия → framed collection viewport → status/actions footer
Помощники: title → реальный status/поля/выбор → pinned Инструкция + отмена/основные действия
```

## Общие владельцы и граница проверки

Единственные визуальные ресурсы — существующие `SAB/UI/Styles/SABFluentLight.xaml` и `UI/FluentLight/Presentation.cs`; preview не изменяет их или production views. Source-derived копии с отдельными namespaces и XAML path генерируются только в существующий рабочий `outputs/ui-production-preview/elevations-source`/`plugin/ElevationPreview`. Сохранены имена/Binding/handlers; любые адаптации code-behind явно перечисляются генератором и source contract. Document collectors заменяются fixture lists; реальная валидация, formulas/naming и модели переиспользуются. Preferences не пишутся в пользовательские файлы. Modeless API requests заменены явно помеченным preview session adapter. Синтетические ID не становятся реальными объектами Document.

Токены/отступы/рамки/capsules — Specification0.5: page24, cards16, groups12, actions8, controls34, row44/header38, actions28. Capsules растут при длинном тексте; tabs-content gap12. Списки имеют собственный контур/rounded clip и native scroll ниже header. Footer содержит ровно1 Инструкция → module HTML, без описания назначения/обучающих абзацев. Рабочие status/validation/results сохраняются.

## Матрица проверки и применимость

Все9active windows: normal/minimum native capture; длинные labels/values, no prose/header icons, footer bounds/help, control heights, table outlines/headers/actions/scroll, source-preservation contract. Main: все4tabs, mode dependencies, formulas update actual naming preview, invalid scale, pick→group→next→return, crop cancel→same settings VM; accepted crop→отдельная modeless session, accept сохраняет результат для следующего запуска команды. Decoration: все3tabs, dependency flags/types/leaders, catalog missing/ready, configure→catalog→return preserving input, validation. Rooms: add/delete/lastblank/duplicates/transfer/pick return. Helpers: cancel/positive outputs; modeless gates. Keyboard: programmatic focus traversal/Escape/default action separate from physical keyboard. DPI96 runtime;144render отдельный, не OS150%.

Каждый SAB-UI-001…064 получает отдельный статус в Elevations-Report.json. План applicability:001–035,038–039,042–048,050–051,053–058 применимы к соответствующим формам/таблицам/процессу;036поиск только existing-sheet editable ComboBox (новый поиск не выдумывать);037/040/041N/A — нет category-selection toolbar/presets;049N/A — DockablePane отсутствует;052применяется к реальному lastblank room-row;059–064общий notification-template reuse с отдельными synthetic fixtures, без claims миграции production toast calls. Revit/API/transactions/model line creation, physical accessibility/input, actual OS DPI/multi-monitor и внешний browser — NOT RUN. Невыполненная среда не N/A.

Роли продолжаются: root builder; feedback05_documenter inventory/docs; feedback05_finish_reviewer independent read-only bounded review. Ship reviewer не заменяет approval пользователя.

Уточнения реализации: catalog — shared DataGrid с прежними2полями; временные экземпляры main имеют собственный scope GroupName до DataContext, что предотвращает влияние закрытых окон. Existing-sheet fixture adapter задаёт ID/name выбранной план-схемы и координаты, исходный validator не отключён; Cancel сохраняет предыдущий выбор. Подробный отчёт/confirmation — synthetic WPF, без claims actual TaskDialog. Финальная применимость заменяет предварительный план в [Elevations-Report.json](Elevations-Report.json).


## Уточнения пользователя: горизонтальная форма и оформление

Источник: десять последних изображений и дополнительный снимок прилипших галочек марок (9aba2add). Предыдущие47PNG/526PASS относятся к прошлой редакции; свежие доказательства обязательны.

Выбрана горизонтальная исходная панель над вкладками: два равных поля режимов и справа действие «Выбрать линии» со статусом. Вариант с отдельной левой колонкой отклонён пользователем. Вкладки40DIP/14pt, общая зона содержимого через12DIP.

Оформление: единый scroll без вкладок. «Каталог» справа в заголовке. Карточка «Что создавать»: слева категории отметок и размеры, справа марки. Ниже настройки отметок, размеров, марок;6 типов марок сеткой3×2. У каждой пары ComboBox/CheckBox отдельная колонка зазора12DIP; общий transform не должен уничтожать этот зазор. Проверять реальную геометрию шести пар и общий вертикальный центр.

Компактные выборы: размер по содержимому, NoResize, центрирование группы действий. Формулы: контур остаётся поверх содержимого, RoundedClip по внутреннему краю. Таблица помещений: номер/имя растут (звёздные), служебные колонки фиксированы; Действия центрируются вместе с заголовком, последняя колонка совпадает с правой границей viewport. Прогресс сохраняет исходный XAML16DIP. Отчёт отделяет итоги (контур без фона) от предупреждений (светлосерый фон).

Уведомления: только изолированное предложение416DIP, жирный статус16, summary в контуре, предупреждения/копируемый текст с фоном. Общий production-шаблон изменяется после согласования. Bottom-right/newest-bottom остаются.

Функциональное уточнение: независимые Полы/Потолки/Перекрытия требуют моделей, хранения и сервиса; старые настройки должны сохранять прежнее включение всех категорий. Production UI комплекта пока не мигрируется без утверждения. Applicable IDs: прежняя матрица Elevations-Report плюс035 для зазора ComboBox/leader,013–019 для гибкости колонок,059 для предложения notification. Реальные Revit/OS проверки остаются NOT RUN.


## Уточнение: три карточки, пространственные границы, итоговое уведомление

Источник: пользовательские снимки5e15426c/8fe64366/93369f32/205a3cc4. Operate, существующий FluentLight. Верхний выбор: слева отдельная карточка «Высотные отметки», под ней «Размеры», справа «Марки» на высоту двух строк; разрыв16DIP. Существующая общая карточка отвергнута прямым запросом. Имена/галочки и зависимости сохраняются; master checkbox становится заголовком собственной карточки.

Границы: четыре поля вокруг центра, подписи и значения по центру. Центральный прямоугольник с прозрачным фоном и пунктирным контуром; сверху/снизу150DIP, боковые130DIP, зазоры12DIP. Инструкционный абзац/иконка из старого снимка не возвращаются.

Результат: вместо modal report — общий bottom-right notification. Положительные счётчики и листы в summary; FailedViews, все накопленные warnings, неразмещённые виды, отсутствующее ожидаемое размещение и fallback — в отдельном warning block, без потери причин. Цветной outcome: зелёный «Завершено», янтарный «Завершено с предупреждениями», красный «Не удалось создать развёртки». Success не содержит нулевых строк/описаний отключённых действий. Диагностический текст сохраняется в прежнем локальном журнале; modeless UI получает только строки и тон. Прямой запрос разрешает этот production notification adapter, но не утверждает весь комплект UI и общий visual proposal.

Проверки: geometry3cards normal/min; spatial4fields+dashed unfilled shape; success/partial/allfailed/markwarning/unplaced/fallback/long notification text; реальные категории8комбинаций, UI→model и schema migration. Прежний полный пакет переснимается без перепроверки аудита36окон. ID001–069 по прежней применимости; новый070 для clean result vs warnings. Revit geometry/transactions и физические/OS проверки остаются NOT RUN.


## Production migration 1.1.0 — утверждение 12.10.2026

Перенести утверждённые девять окон без нового проектирования: исходные имена/Binding/handlers/VM/ExternalEvent остаются. Одно горизонтальное поле исходных данных над четырьмя вкладками; три карточки выбора (отметки/размеры слева, марки справа); пространственные границы; компактные helper dialogs; единая форма оформления и каталог. Общие ресурсы SABFluentLight. Фактический progress сохраняется. Общие уведомления416DIP/status16Bold/контур summary/заливка предупреждений; снизу вверх. Прежние635/44 — история isolated preview. Новые проверки actual production classes и MSI отдельно; без установки/Revit geometry claim.
