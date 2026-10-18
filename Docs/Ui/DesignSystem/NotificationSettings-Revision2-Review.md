# Настройки уведомлений — независимый review редакции2

17.10.2026. Disposition: **ship_preview_scope**. Отдельный read-only reviewer `notification_settings_reviewer` провёл свежий bounded full review по четырём дословным сообщениям пользователя, а не Verdict Pass старой версии.

Проверены current source, все16PNG, протоколы40PASS + boundary3PASS + launcher3PASS, Report2 и provenance. Материальных findings, требующих fix или recapture, в проверенном scope нет.

- Три действия имеют полные пользовательские названия в таблице, редакторе и журнале; читаются целиком в проверенных состояниях.
- Цветные смысловые метки отделены от поля «Подтверждение»; сведения о наличии OK помечены демонстрационными.
- Вкладка «Исключения» и справочные блоки удалены; остались «Правила» и «Журнал».
- Три независимые контекстные галочки находятся на «Правилах» над поиском.
- Normal/minimum, длинный текст, реальный dropdown, protected/no-OK/unknown, consent, validation и Save проверены в scope макета.
- Изоляция журнала и единичный повторный запуск сохранены.
- Reviewer независимо сверил SHA256 всех16PNG и18 исходников/exe/зависимостей/протоколов; совпадение полное. Report содержит90 уникальных registry ID.
- Production handlers/settings Revit не вызываются и не изменяются из preview-кода.

Runtime-протоколы builder reviewer независимо не перезапускал. Реальный Revit и OK capabilities, физический ввод, полный accessibility traversal, OS/runtime DPI и browser launch Help/журнала: **NOT RUN**.

Вердикт разрешает демонстрацию отдельного макета для согласования. Это не пользовательское approval, не production readiness и не разрешение на migration/MSI/installation/publication.
