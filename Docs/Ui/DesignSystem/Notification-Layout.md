# Уведомления — утверждённый общий production template,12.10.2026

Первоначальное разрешение: сначала настоящее уведомление, затем правильный MSI через Build-Msi.bat. Позднее12.10 пользователь утвердил416DIP/status16Bold/outlined summary для общего production template и разрешил migration/MSI/commit+push/release1.1.0 «Редизайн». Установка не разрешена; старый520DIP дизайн — история принятого первого этапа. Новый скриншот codex-clipboard-a4572ea1-0245-4e33-af03-d4683e615021.png и переданный документ «SAB_Уведомления_Проект_дизайна.md» являются точным визуальным эталоном. Установка не запрошена. Автоматические GitHub backups не запускать без отдельного запроса «бэкап»; текущую рабочую копию сохранить в единственном служебном месте существующего ui-production-preview.

Сценарий: завершение заполнения/проверки/сохранения параметров или отмена → немодальное уведомление SAB · Параметризация. Успех/пропуски/отмена текстом, реальная сводка, только имеющиеся ID и технические подробности. Общий SAB-компонент уже выделен без Revit DB dependencies. Финальный результат создания «Развёрток» отдельно разрешён и использует его; остальные notification call sites не мигрировать автоматически.

Входы: текущие Workflow counters/commit state, Issue.ElementId/Reason/Technical/Parameter, ReportWindow.Format; ModuleDataAdapter формирует DTO внутри существующего API-контекста. Параметрические транзакции, запись, валидация и подробный ReportWindow сохраняются. Отдельный elevation result adapter передаёт positive-only summary и все проблемы в optional selectable Warnings section с серой заливкой и копированием; журнал диагностики сохраняется (070). Modeless callbacks выполняют только clipboard/close/scroll; API-вызовов из них нет.

Действия: крестик закрывает одну карточку; Копировать ID — только список положительных ID; Копировать — весь подписанный error context; исходный ReportWindow продолжает сохранять отчёт и выполнять свои действия в прежнем синхронном API-контексте. Clipboard failure выводится явно, текст всегда доступен для выделения.

Две структуры: A — отдельное Window на карточку с независимой геометрией; B — один owner-bound stack Window, вертикальный scroll и независимые карточки. Выбрана B: gap12 сохраняется, переполнение не уводит окна за экран, скрытые карточки не истекают, окно не активируется при новом сообщении. Карточка ограничивает прокручиваемое body, header/footer остаются видимыми.

Wireframe: title16Semibold+vector close28 → outcome16Bold → selectable summary13 в прозрачном контуре1/radius4/padding10 → optional divider/ID block12+copy28 → optional divider/support block12+copy28 → remaining12 → progress2. Width416DIP, outer border1/radius6/padding16, metadata background текущего словаря, gap12; на узком work area ширина ограничивается монитором. Stack справа снизу у рабочей области монитора Revit, margin16. Нативная DPI-конверсия через HWND, обновление позиции/размеров при смене DPI/монитора; фактические multi-monitor transitions отдельно NOT RUN без такого окружения.

Каждая карточка имеет10секунд активной видимости. Stopwatch/monotonic delta; hover/focus/copy/невидимость/disabled Revit owner ставят паузу. После снятия всех причин продолжается остаток; copy не сбрасывает длительность. Переполненные карточки сохраняются в scroll stack и стоят на паузе до полной видимости. Новые снизу; стек растёт вверх, индивидуальный close, закрытый host освобождает timer/events/static reference.

## Direction contract

THESIS: пользователь сразу понимает исход команды и отдельно копирует фактические ID или диагностику.

OWN-WORLD: принятый SAB Fluent Light, Segoe UI, border6/1, белая поверхность, синий thin progress; exact screenshot geometry без декоративных иконок.

STORY: прочитать исход → при проблемах выделить/скопировать отдельный блок → закрыть либо дать активному таймеру истечь.

FIRST VIEWPORT:416DIP справа, заголовок и крестик на одной оси, сводка сверху, данные в нейтральных рамках, footer с временем и двухпиксельным progress.

FORM: принятый code-led native stack из пользовательского эталона; случайный выбор/seed не применяются к явно заданной композиции. Signature interaction — пауза текущего остатка времени при чтении/копировании.

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance

Матрица evidence: success, warning+IDs+support, cancelled, independent stack/overflow, selected text, copied/failure, timer pause/resume/independent expiry, long diagnostic/compact work area, owner position/no activation, keyboard close,144dpi render. Production build2022/2023/2024; native WPF2023 вне Revit. API/transactions/physical DPI transitions отдельно NOT RUN. Все действующие registry IDs получить applicability в Notification-Report.json; table/capsule/selection-source IDs N/A там, где этих компонентов нет. Новые notification критерии добавляет documenter в единый реестр.

Общие owners после извлечения: SAB/UI/Notifications/{NotificationMessage,NotificationCard,NotificationStack}.cs, ресурсы в SABFluentLight; module adapter SAB/Cls_ParameterTools/UI/ParameterNotifications.cs. Независимые существующие reviewer/documenter проверяют production-класс и новые evidence. Installer обязан включить классы в SAB.dll и словарь в MSI; проверка Installer — следующий этап той же задачи.


Первичный PNG сохранён без изменений: [Notification-Accepted.png](Notification-Accepted.png). Исходный документ пользователя: `C:\Users\svetl\Documents\Codex\2026-10-11\referenced-chatgpt-conversation-this-is-an\outputs\SAB_Уведомления_Проект_дизайна.md`. Исходный HTML: `C:\Users\svetl\.codex\visualizations\2026\10\11\01a12ae0-874f-7ee3-bf5f-54119740f366\sab-parameter-notifications.html`.

## Проверенный scope текущего изменения

Production owners NotificationCard.cs/NotificationStack.cs и SABFluentLight применяют416DIP, status16Bold и summary outline. Bottom-right/newest-bottom/gap12,10active seconds/pause/remainder и bounded overflow сохранены. Общие workspace tabs отдельно получили min40DIP/text14/padding20,8 без смены semantics.

Свежий [COMPLETED-log](screenshots-redesign-notifications/verification.txt):35PASS/9PNG. Это actual SAB.dll WPF/clipboard fixtures вне Revit, не actual parameter transaction или installed-plugin runtime. Historical Notification-Review и screenshots-notifications относятся к своему этапу; current independent review/MSI proof оформляются builder отдельно. Revit/physical input/accessibility/OS monitor-DPI transitions/installation NOT RUN. Исходный transient card не получает HTML-footer; подробный parameter ReportWindow остаётся отдельной поверхностью.
