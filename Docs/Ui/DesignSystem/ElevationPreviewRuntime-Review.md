# Независимый review runtime1.1.3 —15.10.2026

Reviewer `/root/finish_reviewer`, read-only, отдельный контекст. Новый bounded full review по фактическому user feedback: ложный wrong-document Create, недостижимый pick, новый footer и виртуальное замыкание. Первичные требования и свежий Layout переданы до review; прежний Verdict Pass079 не перенесён автоматически.

**Scoped ship UI/code. Материальных findings нет.**

- Document.Equals последовательно используется в source-document guards контроллера и continuation, ID плана сравнивается по значению. Reviewer отдельно read-only reflection подтвердил Document override Equals в локальных API2022/2023. Before-log воспроизводит дефект на doubles с разными managed wrappers одной native identity; это не live Revit reproduction.
- Перед PickObjects окно скрывается и предусмотрен foreground Revit; owned selection filter, preselection и Escape сохраняются. Reviewer самостоятельно запустил готовый test exe без rebuild и записи evidence: exit0,62PASS. Проверены fresh-wrapper Create/Flip/Save/SaveAs/Sync/Close, другой document с теми же title/IDs и другой план.
- Footer совпадает с прямым запросом: Инструкция / Список помещений / Показать превью / Выбрать линии / Создать развёртки. Первые2white, последние3sharedprimaryblue; main Cancel удалён, modeless cleanup Cancel сохранён. Шесть actual WPF capture и41PASS просмотрены, включая minimum/long-validation/modal settings round trip.
- Virtual closure соединяет свободные topology endpoints, проверяет area/crossing/overlap, учитывает virtual boundary в clearance. Actual open flag/mandatory preview остаются, дополнительная линия/вид/номер не появляются. Форма является предположением, а не доказанным восстановлением помещения.

Финальный Verdict Pass после исправления metadata punctuation и размера normal fixture1100×880: новый normal capture корректен, остальные5PNG сохранили проверенные SHA256. Сверены6capture hashes,10source hashes, Debug DLL, immutable parent proof и3лога. Проверенный fresh Debug DLL SHA256:18EAD519A754B5E66FA0404E4FCF2F48D2E5C075B2FF7A6FE70EF69B39726612.

MSI evidence отдельно сверено:2022/2023 version1.1.3, MSI/SAB.dll hashes соответствуют записанным payload/current Release bin,45payload entries для каждого, installation=false. Состав DLL/XAML/instructions/Toolkit/RFA проверен extracted payload verifier.

Live Revit, physical focus/selection, RFA placement, accessibility/OS DPI, установка, аварийное завершение процесса/отказ cleanup в noncancellable shutdown — **NOT RUN**. Этот verdict не доказывает прохождение сценария на реальной модели.

[Layout](ElevationPreviewRuntime-Layout.md) · [WPF](screenshots-preview-runtime/verification.txt) · [Provenance](screenshots-preview-runtime/provenance.json) · [Report](ElevationPreviewRuntime-Report.json) · [MSI](ElevationPreviewRuntime-Installer.json).
