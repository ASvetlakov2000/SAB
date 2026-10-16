# Независимая проверка выбора материалов1.1.3

16.10.2026, independent reviewer `/root/material_production_reviewer`, read-only bounded pass. **Ship — UI/source и локальный release packet.** Это scoped verdict, не доказательство выполнения внутри Revit или установки.

Проверены diff/production owners, исходный утверждённый HTML-макет с последующими прямыми правками пользователя, actual production PNG normal/selected-colours/minimum/empty-search/no-cut-pattern/minimum-last-row, harness и лог42PASS/9PNG. Четыре требуемые колонки; обычный текст чёрный, Создать синий, Обновить зелёный, точное Нет штриховки красное, включая выделенные строки. Extended highlight отдельно от Included; snapshot batch, hidden inclusion, оба command routes и scoped dictionary соответствуют запросу. Материальных findings/recapture нет.

Release packet pass: ProductVersion1.1.3 трёх MSI; SHA256 MSI/built DLL/45 extracted payload entries каждого target совпадают с отчётом; host RevitAPI DLL не поставляются. Source/host DLL/log/9PNG соответствуют provenance; Synchronize tail неизменён. Verifier проверяет type metadata и compiled BAML. Release notes и HTML описывают текущую функцию и настройку пропуска корректно.

Evidence: [Provenance](MaterialSelection-Provenance.json), [Installer](MaterialSelection-Installer.json), [Report](MaterialSelection-Report.json), [native log](screenshots-material-selection/verification.txt).

Ограничения: actual SAB.dll WPF с synthetic detached80rows, RenderTargetBitmap96dpi, automation Toggle и routed Space/mouse. Physical Shift/Ctrl, full accessibility traversal, OS DPI, Revit collectors/model transactions и установка NOT RUN. Remote commit/tag/release/assets проверяет root после публикации отдельно.

Documenter agent начал канонические дополнения, но прервался по usage limit; root завершил документацию по фактическим owners/evidence. Это не дополнительный независимый doc verdict; существующий UI/packet reviewer ship сохранён.
