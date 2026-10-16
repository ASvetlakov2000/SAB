# Независимый bounded review16.10.2026

Reviewer `/root/finish_reviewer`, read-only, отдельный контекст. Первичные [окно](reference/directions-window-user.png) и [выделения инструкции](reference/directions-instruction-user.png), актуальный канон и DirectionsWindow-Layout.md переданы до review. Проверен новый scope; аудит36окон не повторялся.

**Scoped ship: UI, код, HTML и MSI payload. Материальных blockers нет.**

Reviewer просмотрел все7actual captures: [протокол](screenshots-directions-window/verification.txt) содержит50WPF PASS. Окно «Направления» соответствует трём вертикальным blue/white/blue действиям, summary и видимая «Отмена» удалены. Empty/busy/error/restored и main footer regressions корректны. CancelRequested, X/Escape, busy guard и ExternalEvent cleanup сохранены. Самостоятельный запуск готового service test exe: exit0,66PASS.

HTML удаляет три отмеченных абзаца (Keep Readable, короткие участки, технические требования семейства), сохраняя запрет штатных move/rotate с реальным названием кнопки «Сменить сторону». Прочие шаги и оформление инструкции сохранены; упоминание удалённой кнопки «Отмена» в шаге завершения заменено существующими Escape/крестиком.

Read-only Property/Upgrade/LaunchCondition/InstallExecuteSequence таблицы MSI всех трёх лет: ProductVersion1.1.1; заменяются версии≤1.1.1 и ровно1.1.4, диапазоны(1.1.1,1.1.4) и>1.1.4 блокируются. RemoveExistingProducts присутствует. SHA256 MSI, current/extracted DLL, новый RFA25F8A49176F7111DEF0748E3AE00630560A75C11076903F9DE730343042C316D и HTML совпадают с verification.json. API refs22/23/24 согласованы. Builder дополнительно проверил27границ версии, SecureCustomProperties и порядок замены внутри транзакции перед InstallFiles по скомпилированным таблицам.

Publish script проверен статически: ReplaceTag ограничен latest mutable release, сохраняет старые assets и metadata, проверяет следующий patch, source commit и upload digests. Факт публикации проверяется отдельно после выполнения. Debug DLL actual captures: SHA2564C1C73DFF35409791B8D724742634B201FFBE537B13F7503BEA0CCD60BD45423.

NOT RUN: live Revit selection/transactions/RFA rendering, actual installation/downgrade replacement, physical keyboard/mouse, accessibility automation, OS DPI transitions. Этот verdict не даёт им PASS и не расширяется на неизменённые модули. [Report](DirectionsWindow-Report.json) · [Provenance](DirectionsWindow-Provenance.json) · [MSI](DirectionsWindow-Installer.json).
