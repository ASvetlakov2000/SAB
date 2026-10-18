# Редактор шаблонов — этап 1, WPF-макет

**Историческая ревизия1.** После прямых правок пользователя действует [Layout2](TemplateEditor-Revision2-Layout.md): вертикальная карточка слева,7вкладок, capability-aware cells и новое дерево. Нижеследующая компоновка верхней карточки сохранена как история и не является текущим вариантом для утверждения.

Дата: 17.10.2026. Scope: preview. Production migration **не разрешён** до отдельного явного утверждения этого комплекта. Другие approvals STATUS не распространяются на него. Git baseline чистый; существующих незакоммиченных изменений нет.

## Функциональный инвентарь

Источник: `SAB/Cls_ViewTemplateGraphics/{Views,ViewModels,Models,Services,Commands}`. Главное окно загружает loose XAML, назначает ViewTemplateGraphicsViewModel и routed handlers. Команда Execute открывает modal window; только DialogResult=true передаёт DTO/target IDs в ApplyService. Транзакции и чтение Document находятся в production services/command, вне макета.

- Шаблоны: тип, поиск по кнопке/Enter, Выбрать все/Снять все для видимых; Extended highlight отделён от IsTarget. Выбранные шаблоны агрегируют настройки; разные значения сохраняют mixed-state. Dirty блокирует изменение состава целей/источника.
- Свойства вида: поиск; текст, список значений, ElementId, числовое/строковое значение, read-only, navigation в графические разделы; включение отдельного параметра; связанная пара режима масштаба/значения.
- Модель, Аннотации, Аналитика, Импорт: отдельные CategoryTabData; включение раздела, видимость группы; поиск; дерево/раскрытие; Extended rows; видимость, проекция линии/штриховки, прозрачность, сечение линии/штриховки, полутона, детализация, modified status; mixed comparison и ограничения Supports*.
- Фильтры: включение раздела; добавление через отдельный modal picker; удаление из шаблона; порядок вверх/вниз; поиск; enabled/visibility/halftone; все пять графических редакторов; mixed и modified. Отсутствующий в шаблоне фильтр остаётся кандидатом добавления.
- Рабочие наборы: включение раздела, поиск, Extended selection, трёхвариантная видимость и mixed-state; empty state.
- Связанные файлы: включение раздела; поддержка API, загруженность/тип/экземпляр, поиск, отображение/связанный вид с CanSelectLinkedView; status и empty state.
- GraphicOverrideEditor: пять sectional editors и All; копия данных до подтверждения; line weight/pattern/color, передняя/задняя штриховка/color/visibility, transparency, reset отдельного значения, дополнительные halftone/detail; условные панели. Cancel не переносит копию.
- AddFilters: поиск, несколько галочек, already/partially included; проверка пустого выбора; Add возвращает только выбранные IDs, Cancel не меняет таблицу.
- Главный footer: StatusText отдельно от Close/Apply; CanApply требует цели и dirty. Apply завершает modal диалог, а не выполняет Revit внутри окна.

Контракты Binding/Command/x:Name/Tag и исходные hashes сохраняются в `Tools/UiShowcase/TemplateEditor/source-contract.json`. Макет использует исходные модели и обработчики в изолированном namespace; адаптация Document/TaskDialog/geometry persistence явно отделена от production.

## Две компоновки

А. Master–Detail: шаблоны слева 310 DIP, настройки справа. Список постоянно высок, но отнимает ширину у восьми вкладок и таблиц с 10–11 колонками; на минимуме почти всегда нужна горизонтальная прокрутка.

Б. Tabbed Workspace с верхним блоком выбора: компактные тип/поиск/массовые действия слева в карточке; ограниченный список шаблонов справа. Настройки и все вкладки ниже занимают всю ширину окна. Выбран вариант Б: приоритет — сравнение графики, читаемые колонки и доступность всех разделов. Длинный список шаблонов прокручивается внутри своей рамки.

Wireframe: заголовок «Редактор шаблонов» + маркировка тестовых данных → карточка выбора шаблонов → восемь вкладок общей капсулой → локальная панель действий/поиск → таблица с собственным контуром → status/validation отдельной строкой → Инструкция слева, Закрыть/Применить справа.

Normal 1440×940, minimum 1120×760 DIP. Верхняя карточка196DIP; рабочая область `*`; списки и таблицы прокручиваются независимо. На узости разрешена горизонтальная прокрутка таблиц; fixed flags/status имеют MinWidth=Width (WPF иначе сжимает их ради star minimum), текстовые колонки используют star widths; последняя колонка у края. Общий TabControl допускает перенос headers при недостатке ширины; каждый header min40/text14/padding20,8; до содержимого ровно12. Footer неподвижен. Secondary редактор bounded scroll; default lines420, patterns560, transparency380, All720, minimum680×340; AddFilters680×620/min560×460 — отдельная bounded таблица.

## Ресурсы и проверки

Единственный визуальный owner — существующие `SAB/UI/Styles/SABFluentLight.xaml` и `SAB/UI/FluentLight`. Нет копии словаря/новой темы; каждое окно подключает исходный словарь scoped, Application.Resources пуст. Один нижний «Инструкция» каждого рабочего окна открывает существующий `Docs/PluginInstructions/SAB_HTML_Redaktor_shablonov.html`.

Матрица: все8tabs; normal/minimum/stretch; long Russian names; empty/selected/mixed/dirty/disabled/validation; все5graphics sections/All; actual open dropdown; search; focus/Enter/Escape; filter add/remove/order; template batch guard; footer/header/scrollbar bounds. Native screenshots запущенного preview отдельно от Revit. OS DPI, Revit operations/transactions, installer и installation — NOT RUN на этапе1.

Применимость каждого ID фиксируется до реализации в `TemplateEditor-Report.json`, результаты дополняются после запуска. Независимый bounded reviewer назначается по существующему ReviewRoles.md; review не является пользовательским approval.

## Фактические границы проверки

После прерывания Computer Use пользователь явно поручил продолжить без него. Финальный прогон использует WPF Keyboard.Focus/routed KeyEventArgs/AccessKeyManager и запущенные живые controls. Popup снят вместе с живым Popup.Child по измеренному screen-to-DIP offset; это WPF visual capture, не физический ввод или OS screenshot. Root Grid имеет canonical background, поэтому Content snapshots не содержат прозрачных чёрных промежутков. Браузерное отображение инструкции остаётся NOT RUN; существование точного локального HTML и footer wiring/count подтверждены. Повторный Open.cmd переиспользует процесс и не пересобирает занятый exe.

В ходе задачи появились параллельные изменения в MainPanel.cs, ParameterToolsIcons.cs и Ribbon.cs вне этого scope. Они не редактировались и не включены в утверждение макета. Модуль шаблонов и общие production FluentLight resources проверяются отдельно по baseline hashes/git diff.
