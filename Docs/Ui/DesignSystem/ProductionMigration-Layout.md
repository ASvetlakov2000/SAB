# Перенос проверенного Fluent Light в SAB

Дата:2026-10-11. Прямой новый запрос пользователя разрешает ровно перенос макетов0.5 обоих модулей. Установка/публикация не запрошена. Backup: outputs/production-migration-backup.txt.

Operate, без нового выбора концепции. Виды/листы: Data Manager, sidebar отдельных карточек структуры/образца/размещения/копирования → исходная таблица10колонок → исходная валидация → footer помощи/обмена/Отмена/Создать. Secondary: отдельные карточки тех же исходных настроек, footer помощи/Закрыть. Параметры: Tabbed Workspace,4вкладки Правила/Источники/Значения/Категории → исходные поля и таблицы → фактический статус → footer помощи/обмена/Отмена/Сохранить.

Сравнение структур: (A) сохранить старую геометрию и обновить стили; (B) применить проверенную композицию0.5. Выбрана B по прямому требованию точного совпадения с макетами. Новых полей/поиска/демоданных в production нет. Единственное функциональное дополнение уже разрешено ранее: Все/Выбранные + JSON сохранение/загрузка stable category IDs.

Исходные VM/models/services/commands/API boundary сохраняются. XAML binding inventory135/103,10колонок и10защищённых методов параметров сверяются с backup. Main RestoreWindowLayout/SaveWindowLayout и secondary WindowSizeSettingsService.Apply остаются действующими. Ни Document, ни API/ExternalEvent не вызываются в native testhost.

Общий production owner: SAB/UI/Styles/SABFluentLight.xaml и SAB/UI/FluentLight/Presentation.cs. Словарь локален окнам, общий SABWindowStyles остальных модулей сохраняется. SettingsWindow использует отдельную FluentTheme, legacy Theme остальных параметрических диалогов сохраняется. Preview компилирует те же production UI-классы без визуальных подмен.

Матрица:27сценариев baseline0.5 normal/minimum/4tabs/dropdown/edit/empty/validation/modes/secondary/selection JSON/144dpi render + scoped resource isolation + production geometry preservation. Minimum параметры980×820, виды1100×760, secondary1040×760 соответствуют макетам; wrapped capsule растёт, footer неподвижен, scrollbar ниже header/group, последние actions доступны.

Все58ID применимы к переносу и/или проверке процесса. До проверки статус NOT RUN; полный keyboard/physical input, Revit model operations, OS DPI transitions и реальный внешний browser не считать проверенными. Findings закрываются одним пакетом; независимый reviewer read-only, documenter фиксирует фактические owners/evidence.
