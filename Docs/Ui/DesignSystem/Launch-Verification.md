# Исправление запуска макетов —12.10.2026

Прямое evidence пользователя: codex-clipboard-2a697dde-19f1-4e54-bc35-f7f9639d9e83.png и codex-clipboard-ac9978ed-6968-4107-a067-f67a461c8e5b.png. Ошибка воспроизведена через настоящий Open-Elevations.cmd: MSB3027/MSB3021, занятый процессом38824 SAB.UI.Preview.exe. Старый diagnostics/error.txt от11.10 к этой ошибке не относится.

Open-Elevations.cmd и Open.cmd теперь вызывают общий LaunchPreview.ps1. Build.ps1 -Open также делегирует launch-only. Готовый exe открывается без сборки; активный экземпляр того же режима возвращается через native HWND. Mutex предотвращает параллельное создание дубликатов. Если exe отсутствует, предусмотрена первоначальная сборка. Для обновления исходников явная сборка выполняется отдельно при закрытом Preview.

Фактически проверено из C:\Windows\System32: повторный Elevations exit0/PID38824; первый Modules exit0/PID34268; повторный Modules exit0/тот же PID; один экземпляр Modules; SHA256 exe до/после совпадает. Тестовый Modules закрыт через CloseMainWindow; пользовательский Elevations сохранён. [Результаты](../../../outputs/ui-production-preview/launcher-verification.json), [лог воспроизведения](../../../outputs/ui-production-preview/elevations-launch.log).

UI/DLL/XAML и MSI не менялись. Предыдущий scoped visual verdict и47PNG сохраняют свой scope; это проверка launcher, не новый UI verdict. Independent review этой небольшой script-only правки NOT RUN; missing-artifact build fallback, конкурирующие реальные клики, Windows foreground-policy/physical input не проверены. [Отчёт](Launch-Report.json). Скрипты до изменений сохранены в existing outputs/ui-production-preview/launcher-before.json, без новой папки результатов.
