## verdict

12.10.2026. Независимый reviewer `feedback05_finish_reviewer`. Verdict Pass после полного read-only review уведомлений: оценено прежнее P1 и возможные регрессии его исправления, без нового поиска. Reviewer не запускал сборку, WPF, Revit, browser или capture; изменён только этот отчёт по прямому назначению ownership.

- **P1 · SAB-UI-007/009 — resolved.** [SourcePreview.cs](../../../Tools/UiShowcase/SourcePreview.cs) теперь подключает legacy application dictionary и вызывает `SabWindowBehaviorService.Initialize()` до ветки `--notifications`. [SabWindowBehaviorService.cs](../../../SAB/UI/SabWindowBehaviorService.cs) явно исключает `SAB.UI.Notifications.` рядом с прежними notification namespaces. NotificationStack сохраняет собственные ресурсы, размещение и жизненный цикл; general window behavior не центрирует его и не добавляет общую оконную анимацию.
- Повторно открыты все девять actual native PNG по прежним путям в [screenshots-notifications](screenshots-notifications/): success, warning, selection/copy, cancellation, clipboard failure, long minimum,144dpi render, stack, overflow. Материальных регрессий исправления не обнаружено; принятые карточки, читаемые блоки данных, header/footer и bounded overflow сохранены.
- Свежий [verification.txt](screenshots-notifications/verification.txt) содержит **26 PASS / COMPLETED**, а не27. В нём есть проверки no activation, focused-card pause, disabled-owner pause, offscreen pause, независимого закрытия и фактического DispatcherTimer expiry после десяти активных секунд. Это результаты builder, прочитанные reviewer, не собственный повторный запуск.

Предыдущий полный проход сравнил девять снимков с принятым PNG и исходным документом дизайна, указанными в [Notification-Layout](Notification-Layout.md), прочитал NotificationMessage/Card/Stack, module adapter, presentation changes Commands/Workflow, Toast wrapper и shared styles. Единственным материальным finding был исправленный integration mismatch. Diff с сохранённым pre-change SmallWindows.cs ограничен Toast wrapper; ReportWindow не переработан. Timer/input callbacks общего компонента оперируют строками и WPF/clipboard, не Revit DB.

## remaining

clear — прежнее P1 закрыто, внесённых им материальных регрессий не обнаружено. Этот ship относится к оценённому исправлению notification integration и показанным WPF-сценариям вне Revit; он не является полной приёмкой всех состояний продукта.

NOT RUN: execution/transactions внутри Revit, physical hover/input, реальные переходы между мониторами и OS DPI, полный accessibility обход.144dpi — offscreen RenderTargetBitmap. Installer/MSI — следующий отдельный этап; этим review он не проверен. Установка не выполнялась и этим verdict не разрешается. Новый notification proof не переписывает исторические production0.5/2023 и Revit2022 evidence.

disposition: ship

## Bottom-up: дополнительный bounded review —12.10.2026

Reviewer feedback05_finish_reviewer прочитал NotificationStack.cs целиком и просмотрел все9 текущих native PNG. Append/newest-bottom, рост вверх, gap12, bottom-right inset16DIP, ScrollToBottom без чтения и сохранение offset при focus/hover соответствуют последнему требованию пользователя. Lifetime/offscreen/disabled-owner/cleanup сохранены.35 PASS/COMPLETED — текущий native96 протокол.

disposition: ship в указанном scope. Revit/install, physical multi-monitor/OS-DPI и144render как OS-scale не подтверждены. Этот verdict не утверждает макеты «Развёрток». Предыдущий26-PASS verdict выше относится к предыдущему этапу.
