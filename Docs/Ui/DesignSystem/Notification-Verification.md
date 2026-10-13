# Проверка уведомлений — 12.10.2026

Реализовано фактическое уведомление «Параметризации», затем выделены общие `NotificationMessage`, `NotificationCard`, `NotificationStack`. Принятый [скриншот](Notification-Accepted.png) сохранён как первичный reference; [Layout](Notification-Layout.md) описывает компоновку и границы задачи.

Production `Workflow` передаёт реальные счётчики, commit state, структурированные ID и поля Issue в модульный адаптер. Общий компонент получает только строки. Отмена и исключения команд также используют новый шаблон; журнал ошибок сохранён. Исходный `ReportWindow`, его сохранение отчёта и API-действия не изменены. Общие обработчики окон SAB исключают namespace уведомлений, чтобы сохранялось собственное размещение.

[Нативный протокол](screenshots-notifications/verification.txt):35 PASS / COMPLETED. Проверены независимые десять активных секунд, сохранение остатка после паузы, фактическое DispatcherTimer-закрытие, focus/disabled-owner/offscreen pause, Windows clipboard для ID и полной диагностики, честный отказ копирования, выбор текста, bounded длинный текст, очередь без потери карточек, новое уведомление снизу с ростом стека вверх, gap12, индивидуальное закрытие и отсутствие начального захвата native activation. Инъекция clipboard failure является контролируемым тестом; остальные операции копирования выполнялись через настоящий буфер Windows.

Сняты9 actual WPF PNG с production-классами и теми же startup-обработчиками, что использует SAB. Данные результатов синтетические, Revit Document не подключён. Runtime96DPI;144DPI — отдельный RenderTargetBitmap, не системный переход150%. [Provenance](screenshots-notifications/provenance.json) содержит размер, DPI, SHA256 каждого PNG, фактической DLL и исходников хоста.

[Source contract](../../../Tools/UiShowcase/notification-source-contract.json):47 ограниченных проверок. Точные presentation-only замены обратимо сопоставлены с pre-change текстом; всё остальное в Workflow/Commands сохранено. Все non-Toast классы SmallWindows, включая ReportWindow, совпадают. Прежние bindings, business methods и общие стили сохранены. Исторический production proof не переписан; новый successor имеет собственные хеши.

[Независимый review](Notification-Review.md):ship в проверенном WPF scope после исправления единственного material P1 — host/production initialization mismatch. [Отчёт64ID](Notification-Report.json) разделяет PASS, NOT RUN и N/A.

Release для Revit2022/2023/2024 собран обычным `Build-Msi.bat`, состав MSI проверен отдельно в [Installer-Verification](Installer-Verification.md). Actual Revit execution/transactions, physical hover/input, OS DPI/multi-monitor transitions и native API22 runtime остаются NOT RUN. Установка не выполнялась.

Дополнительный прямой запрос: уведомления снизу, новые у нижнего края, существующие поднимаются вверх. Native HWND bounds проверены после добавления и удаления: bottom/right inset16DIP; overflow открывается внизу, focused reader сохраняет offset. Отдельный bounded review всех9PNG и NotificationStack дал scoped ship; прежний26-PASS review ниже сохранён как история.
