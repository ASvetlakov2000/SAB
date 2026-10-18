# Независимая проверка макета настроек уведомлений

17.10.2026. Reviewer: отдельный read-only контекст `notification_settings_reviewer`, роль из ReviewRoles.md. Scope: standalone WPF-макет, его источник, запуск, сохранение,16PNG и протокол; не production, Revit или пользовательское approval.

## Первый bounded review

Disposition: **fix**. Просмотрены все16PNG, verification.txt, XAML/model/host/capture/build/launcher/help и канон. Существенных визуальных замечаний нет; снимки валидны. Пересъёмка визуального решения не требуется для следующих исправлений:

| Finding | Проблема | Исправление builder | Проверка |
|---|---|---|---|
| F1 · P2 · локальный функциональный риск | OpenJournal записывал файл непосредственно во введённый Folder вопреки границе изолированного макета | WriteDemoJournal использует только Program.Output/Журнал; Folder сохраняется как демонстрационный параметр | boundary-verification.txt:3PASS, внешний путь не создаётся |
| F2 · P2 · SAB-UI-065 | Повторный запуск создавал новый экземпляр | Per-root namedMutex/EventWaitHandle; существующее окно Show/Restore/Activate через Dispatcher, capture/verify отдельно | launcher-verification.txt:3PASS, один прежний PID, запуск из TEMP, exe hash без изменений |
| Gate · SAB-UI-009 | Не было manifest с source/exe/capture hashes | Report/Provenance разделяют первоначальный capture-owner и текущий build после I/O/launcher fix | provenance.json, capture-build.json; visual owners не менялись |

Исходный reviewer подтвердил соответствие требованиям показа без OK, трёх действий, защищённых контуров/владения/неизвестных окон, consent на зависимое удаление, global pause и общей Fluent Light-компоновки.35PASS — протокол builder; reviewer его повторно не запускал.

## Verdict Pass

Окончательный disposition: **ship_preview_scope**. Ограничен F1/F2 и документарным gate; новый поиск косметики не выполнялся. F1, F2 и provenance gate оценены как **resolved**. Reviewer независимо сверил SHA256 всех16PNG, сохранённых исходников и обеих версий exe; расхождений нет. Хеши Window.xaml, Window.xaml.cs и SABFluentLight.xaml совпадают с capture-build; пересъёмка не требуется.

Вердикт разрешает показать пользователю изолированный интерактивный макет для согласования. Протоколы35+3+3PASS прочитаны reviewer, независимо не перезапускались. Пользовательское согласование макета пока не получено.

## Границы

Revit events/API/model transactions, реальное наличие и эффект OK, физический ввод, полный accessibility traversal, OS DPI и browser launch: **NOT RUN**. Это не разрешение переносить макет в production, собирать MSI, устанавливать или публиковать изменение.
