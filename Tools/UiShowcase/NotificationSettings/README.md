# Настройки уведомлений SAB — рабочее окно

Текущий запуск actual окна из SAB.dll: [Open-Production.cmd](Open-Production.cmd) или [Open.cmd](Open.cmd). Обычный запуск использует готовую сборку; при её отсутствии выполняется Build-Production.ps1. Хост добавляет в заголовок «просмотр» и использует отдельный профиль `outputs/ui-production-preview/notification-settings/production/interactive-profile`, без записи настроек пользователя.

Явная сборка actual SAB для API2022/2023/2024 и проверка окна:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/UiShowcase/NotificationSettings/Build-Production.ps1 -Capture -AllVersions
```

Выполнять из корня SAB. Target net48/x64; MSBuild Visual Studio, API22 в корневом `lib/Revit2022`, API23/24 из соответствующих установок Revit. ProductionHarness ссылается на собранную SAB.dll; фикстуры и captures используют отдельные test/visual profiles в `outputs/ui-production-preview/notification-settings/production`. Настоящий пользовательский профиль `%LOCALAPPDATA%/RevitQuiet` использует вход плагина Options: SAB → Настройки → Уведомления.

Первый этап реализует рабочее окно с двумя вкладками «Правила» и «Журнал», черновик до «Сохранить», атомарную schema2 и чтение/HTML-экспорт настоящего журнала. Главный общий switch: track56×32DIP, knob24, hit64×40; semantic badge и поле «Подтверждение» разделены.

Три действия: «Не подавлять», «Подавлять с уведомлением SAB», «Подавлять с записью в журнал». Выбранные способы сохраняются, но ожидают проверки; доступно к подавлению0. Пока включение модуля запускает только наблюдение/журнал. Старые blanket suppression/auto-resolve/rollback/auto-OK удалены из активного обработчика.

При первой установке модуль выключен; отсутствующие/старые/повреждённые настройки также OFF. Пользователь включает его только в окне и нажимает «Сохранить». Намеренно сохранённый schema2 выбор восстанавливается при последующих запусках/обновлениях. Cancel/Escape/close не пишут draft. Журнал HTML экспортируется в сохранённую папку, а не по несохранённому пути.

Свежий [native протокол](../../../Docs/Ui/DesignSystem/screenshots-notification-settings/production/verification.txt):56PASS/21PNG actual SAB.dll, isolated fixtures вне Revit. Проверены defaultOFF/legacy/corrupt recovery, draft/save/atomic failure, journal, размеры/focus/routed Space/disabled switch и перечисленные состояния окна. Это client RenderTargetBitmap96dpi; popup04 — реальный Popup.Child. Native routed input не является physical input. Revit/реальные OK capabilities, полный accessibility, OS DPI, browser launch и MSI/установка не проверены. [Report91ID](../../../Docs/Ui/DesignSystem/NotificationSettings-Production-Report.json) и [independent review](../../../Docs/Ui/DesignSystem/NotificationSettings-Production-Review.md): ship_ui_source_scope. Первый этап завершён в source/WPF границе, движок подавления pending; protocol не означает полный backend PASS.

[Production layout](../../../Docs/Ui/DesignSystem/NotificationSettings-Production-Layout.md), [пользовательская инструкция](../../../Docs/PluginInstructions/SAB_HTML_Uvedomleniya.html). Scope: исходники и сборки первого этапа окна; MSI, установка и публикация не входят в текущий запрос.

История preview сохранена отдельно: редакция1 —35PASS/16PNG, [Report1](../../../Docs/Ui/DesignSystem/NotificationSettings-Report.json), исходные evidence и `revision-1-source`; редакция2 —40PASS/16PNG и отдельные3 boundary/3 launcher PASS, [Report2](../../../Docs/Ui/DesignSystem/NotificationSettings-Revision2-Report.json), [provenance2](../../../Docs/Ui/DesignSystem/screenshots-notification-settings/revision-2/provenance.json). Это isolated preview evidence, не текущая production-проверка. Готовый revision2 exe остаётся в `outputs/ui-production-preview/notification-settings/revision-2`; прежний launcher сохранён в `pre-production-source/Tools__UiShowcase__NotificationSettings__Open.cmd`.


Текущий установочный выпуск после исправления footer: [версия1.1.4 / installer record](../../../Docs/Ui/DesignSystem/NotificationSettings-Installer.json). Build-Msi.bat Release и PrepareOnly exit0; MSI2022/2023/2024 проверены по extracted DLL/actual XAML/styles/guides/API refs, default OFF подтверждён. [Выпуск1.1.4 опубликован](https://github.com/ASvetlakov2000/SAB/releases/tag/v1.1.4-notifications); [commit/tag/4assetSHA подтверждены](../../../Docs/Ui/DesignSystem/Release-1.1.4-notifications.json). Установка не выполнялась. [Независимый footer/payload review](../../../Docs/Ui/DesignSystem/TemplateEditorFooter-Review.md): ship_ui_code_payload;103PASS/6PNG и7source checks. Это отдельный delivery proof после первого этапа окна; native56/21 и preview1/2 сохраняют свой исходный scope, автоматическое подтверждение ещё не реализовано.
