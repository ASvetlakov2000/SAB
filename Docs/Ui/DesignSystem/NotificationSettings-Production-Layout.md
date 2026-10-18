# Окно «Уведомления»: первый этап реализации18.10.2026

Прямое разрешение: пользователь попросил увеличить главный переключатель на приложенном снимке редакции2 и начать реализацию окна в плагине. Дополнение18.10: при первой установке модуль выключен, включение только пользователем в настройках. Сохранённый выбор schema2 сохраняется между запусками/обновлениями; старые blanket opt-in не мигрируют. Это разрешение на исходники и сборку; установка/публикация не запрашивались.

Pattern: master/detail settings. Сохраняем согласованную редакцию2: Правила и Журнал; три контекстных галочки над поиском; полный текст трёх действий; смысловые цветные метки отдельно от подтверждения. Альтернатива — редактирование действий в строках таблицы — отклонена: полное сообщение и статус подтверждения должны быть видны вместе в правой панели.

Общий owner: SABFluentLight.xaml. Новый именованный SabLargeSwitchTemplate: track56×32, knob24, hit64×40, visible keyboard focus. Старый размер38×22 сохраняется для других окон. Новый shared badge style исключает локальную фабрику стиля. Окно1160×820/min1040×700, shared tabs40/14, table header38/rows44, page24, columns20, detail336.

Production boundary: Options.BuildWindow сохраняется как entry point, открывает настоящий SettingsWindow из SAB.dll, owner HWND передаётся только при создании. ViewModel содержит строки и снимки журнала, без Document/UIApplication. Изменения — черновик до Save, Cancel/Escape не пишут файл. Persistence атомарный, schema2, старые blanket flags/DialogResults не становятся согласованными правилами. Пользовательские файлы не меняются тестовым хостом.

Первый этап не заявляет проверенных OK capabilities: выбранные действия сохраняются как ожидающие проверки, счётчик отличает настроенные правила от действующих. Ошибки, неизвестные окна и сообщения без OK остаются в Revit. Старые глобальные DeleteWarning/ResolveFailure/rollback и произвольные OverrideResult удаляются из активного обработчика. Наблюдение только записывает исходные сообщения; не вызывает исправление модели. Журнал содержит реальные записи, не историю кликов по черновику.

Проверки: сборки API22/23/24; actual SettingsWindow из SAB.dll вне Revit; normal/minimum/long text/dropdown/empty/protected/pending/delete consent/validation/journal/save/cancel; schema migration/atomic failure/draft separation/policy fail-open. Реальный Revit/кнопки OK/physical input/OS DPI — NOT RUN. Все registry ID сопоставить в Production-Report; исходные preview verdicts не расширять.
