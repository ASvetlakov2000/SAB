# Развёртки — актуальная проверка последних правок

12.10.2026: **635 PASS / 44 PNG / 0 FAIL / COMPLETED**. [Provenance](screenshots-elevations-preview/provenance.json) содержит SHA снимков, actual host/generated sources и DLL. Это WPF вне Revit: runtime96DPI,144DPI только offscreen render. Предыдущие565/41 сохранены в `outputs/ui-production-preview/elevations-annotations-before.zip`; прежние526/47 — в earlier snapshot. Текущий [отчёт70ID](Elevations-Report.json) не выдаёт историю за новые проверки.

Выбор оформления: три самостоятельные карточки, слева Высотные отметки над Размерами, справа Марки на общую высоту; normal/minimum geometry проверена. Все6типов марок сохраняют зазор12DIP от leader checkbox и вертикальную ось≤1DIP. [Карточки](screenshots-elevations-preview/ElevationDecorationWindow-normal.png), [minimum](screenshots-elevations-preview/ElevationDecorationWindow-minimum.png), [марки](screenshots-elevations-preview/decoration-tags.png).

[Границы](screenshots-elevations-preview/ElevationCropAdjustmentWindow-normal.png):4поля расположены пространственно, подписи/значения по центру, центральная рамка пунктирная без заливки. Сохранён original signed parser, проверено отрицательное значение. Окно по содержимому/NoResize. Прежние main4tabs/horizontal source panel, elastic room table,3compact helpers, old16DIPprogress и Cancel/reopen semantics сохранены; регрессии входят в текущий пакет.

## Реальный код плагина

Immutable baseline93: **85unchanged +8authorized exact deltas** по [контракту](../../../Tools/UiShowcase/elevations-functional-delta.json). Модель/хранение schema9/галочки/валидация/catalog roles/creation target gates связаны. Проверены8комбинаций сериализации и7валидных original UI-handler mappings, schema1–8→alltrue. Найден и исправлен реальный defect замены: ElevationAnnotationOwnershipService раньше удалял все Spot независимо от выбранной категории; теперь сохраняет выключенные категории и слои, включая Diagnostic roles. Gate проверен8масками. Реальная геометрия/transactions в Revit **NOT RUN**.

## Итог создания

Production ElevationCreationReportService больше не вызывает modal TaskDialog/final legacy toast: получает detached значения, строит общий NotificationMessage и вызывает NotificationStack. Локальный last-creation-report.txt сохранён. Manual-boundary проблема передаётся в общий warnings список. Summary содержит только положительные результаты; отдельный filled warning block сохраняет все причины. Green/amber/red определяются созданными видами и наличием проблем. Проверены неудачные виды, марки/план-схема, неполное размещение, fallback, единственный фактический лист продолжения и отсутствие ложной строки ручной расстановки при0viewports. Копирование полного warnings текста выполнено actual shared handler с injected writer.

[Успех](screenshots-elevations-preview/creation-notification-success.png), [предупреждения](screenshots-elevations-preview/creation-notification-warning.png), [ошибка](screenshots-elevations-preview/creation-notification-error.png), [длинный текст](screenshots-elevations-preview/creation-notification-long.png) показывают isolated416/16Bold proposal. [Actual production520/12](screenshots-elevations-preview/creation-notification-production.png) снят отдельно. Общий visual proposal не применён ко всем уведомлениям; optional Warnings section добавлен без изменения прежней геометрии других вызовов. Bottom-right/newest-bottom прежние. Старые35notification checks не засчитываются635текущими повторно.

## Поставка и границы

Ваш Build-Msi.bat Release завершён exit0 для2022/2023/2024. CAB извлечены, payload SHA/правильные API и compiled feature names проверены штатным Verify-MsiPayload. [Свежие пакеты](Installer-Payload.json), [проверка](Installer-Verification.md).8functional owners+2optional shared warning owners включены; новые макеты и416/16proposal не входят в MSI. Установки/commit не было.

Независимый reviewer просмотрел44PNG и сверил635PASS/hash/boundaries. Два findings про единственный фактический лист и ложную manual-placement строку закрыты source/runtime checks; bounded verdict **ship** в проверенном scope, без approval пользователя. [Review](Elevations-Review.md).

NOT RUN: actual Revit geometry/creation/transactions/native selection/ExternalEvent, physical input/accessibility, OS DPI/multi-monitor, полный popup matrix и внешний browser. Programmatic WPF focus/native captures не заменяют их. UI suite остаётся pending user approval по STATUS. [Запуск](../../../Tools/UiShowcase/Open-Elevations.cmd), [галерея](Elevations-Preview.html).

Свежий финальный launcher check: два запуска переиспользовали один PID13836, executable SHA не изменился, сборка не вызывалась. Physical foreground NOT RUN; evidence в outputs/ui-production-preview/elevations-launch-check.json.
