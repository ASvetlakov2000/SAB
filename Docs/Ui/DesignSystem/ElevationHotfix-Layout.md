# Развёртки: исправления после реального запуска, 13.10.2026

Источник — три новых снимка dbbf8492 /6a1f6cf9 /4b9e0162 и последние текстовые уточнения пользователя. Они важнее прежнего top-status макета. Base commit c5ce558fdae37e90259328ac11005af8982a5ef2; исходники чистые до начала задачи, новый probe-host сохраняется отдельно.

Проблемы: notification warnings требуют отсутствующую scoped кисть; test Application legacy dictionary скрывал дефект. WarningInfoBorder стоит в Grid.Row4 двухстрочного footer и перекрывает actionrow1. Длинные сведения о выборе растягивают main window.

Последний утверждённый wireframe: header → title → две mode capsule (помещения/источник) →4tabs/scrolling settings → optional validation status (footer row0) →5buttons(row1): Инструкция | Отмена | Список помещений | Выбрать линии | Создать развёртки. Вверху больше нет selection action/status panel. MainMinWidth800, MainMinHeight650 подтвержденыactualbounds; списокMin740×420, номерная колонкаMin140 после independent finding. Source x:Name/bindings/selection command/API остаются.

Данные выбора читаются синхронно из actual selectionPackages в detached immutable DTO (номер/имя/контуры/линии/готовность). Main list button открывает отдельную modal read-only collection: header → bordered table → totals → Инструкция/Закрыть. Нет add/delete/edit/transfer кнопок и DB references в DTO. Empty/long/many rows ограничены собственным scrolling body, имя elastic, counts fixed. Это reuse collection pattern MultiRoomSelection, отдельная форма editing workflow сохраняется.

Сравнение: A — полный статус под режимами (отменён пользователем); B — сведения по запросу в отдельном read-only окне, все actions в footer (выбран пользователем). Не вводить новый visual direction.

Общий scoped dictionary должен полностью обеспечивать NotificationCard без Application legacy resources. После API commit итоговое UI delivery не выбрасывает presentation exception в command failure: диагностический журнал + безопасный fallback, API/transactions не подавляются. Tests: воспроизведение missingkey доfix; cleanresources success/warning/error/copy; injected primary/fallback errors; actual footer bounds/order at800/normal/longstatus; readonly list empty/normal/100rows/lastrow; snapshot detached; normalMSI2022–24/APIrefs/currentXAML/dll/version. Реальный Revit/physicalinput/OSDPI NOTRUN, если среда не выполнена.
