# Инструкции SAB

Применяются вместе с `C:\Users\svetl\Desktop\Codex\AGENTS.md` и применимыми вложенными инструкциями.

## Сохранение в Git

- Запрос пользователя «закоммить изменения» означает commit и push в его удалённый репозиторий. Локального коммита недостаточно.
- Проверять, что удалённая ветка содержит созданный коммит; если push не выполнен, прямо сообщать об этом.
- Сохранять исходники, ресурсы, семейства, тесты, инструкции и описание зависимостей, необходимые для сборки на другом ПК. Сборки, кэши и локальные снимки не включать в Git.
- Если пользователь требует сначала сохранить текущее состояние, не начинать последующую разработку до успешного push.
- Уточнение пользователя от12.10.2026: запрос «бэкап» означает сохранить старую версию в GitHub через commit + push, проверить наличие коммита в удалённой ветке и сообщить rollback commit. Это условная политика, не разрешение делать commit при каждой задаче. Локальный журнал сохранений вести в одном месте; не создавать отдельные Outputs для каждой резервной версии.

## Установочник и рабочие результаты

- Уточнение пользователя16.10.2026: публичные выпуски нумеровать последовательно. После1.1.0 «Редизайн» идёт1.1.1 «Обновление развёрток»; ошибочный опубликованный1.1.4 заменяется этим выпуском. Промежуточные локальные сборки не должны пропускать публичные patch-номера. Перед публикацией сверять последний опубликованный GitHub release, версию DLL и ProductVersion всех MSI.

- Любые поставляемые изменения SAB должны попадать в пакет, создаваемый `Build-Msi.bat`/его установочником. Перед завершением работы с установочником проверить состав пакета и включение изменённых файлов; сборка отдельной DLL не подтверждает правильность MSI. Проверка пакета не означает разрешение установки.
- Прямой запрос от12.10.2026 задаёт порядок: сначала фактическое уведомление «Параметризации», затем общий шаблон, потом правильный MSI. Текущая реализация уведомления не разрешает установку; этап MSI выполнять после уведомления в указанной последовательности.
- Повторяемые сборки и проверки используют один канонический рабочий/output-каталог: существующий `outputs/ui-production-preview`; текущие notification evidence — `Docs/Ui/DesignSystem/screenshots-notifications`. Не создавать новый timestamp output-каталог на каждый прогон. Исторические evidence сохранять; удаление/перемещение существующих Outputs требует отдельного основания и не входит в это правило.

## Инструкции пользователя

- Общая кнопка: SAB → Настройки → Инструкции. Локальные HTML устанавливаются вместе с SAB.
- Оформление брать из Universal_HTML_Template_Kit; исходный template.css сохранять.
  По последнему уточнению пользователя от 09.10.2026 assets/compact.css уменьшает
  только воздух внутри рамок и между разделами на 30% относительно исходного шаблона.
  Сохранять исходные размеры шрифтов, цвета, ширины, скругления, расположение блоков
  и одноколоночный каталог. Предыдущий вариант сильного уплотнения отменён.
- Уточнение от 09.10.2026: во всех инструкциях кнопки выделять капсулой со светло-серой
  заливкой (#f5f5f7); вкладки, панели, поля, папки и важные значения — контурной капсулой
  без заливки. Одиночные и составные списки шагов имеют одинаковые отступы, без
  минимальной высоты и дублирующих внутренних полей. Не менять расположение блоков.
- Писать для начального и среднего уровня: подготовка, точные русские названия кнопок,
  последовательность действий, результат, пробная проверка и понятные причины ошибок.
  Вариативные сценарии размещать в отдельных раскрывающихся разделах, без объяснений Revit API.
- Пользователь 09.10.2026 проверил инструкцию параметров и явно разрешил переработку
  остальных инструкций действующих инструментов SAB в том же формате.
- Название плагина — SAB. Инструкции, настройки, названия файлов и пользовательские
  тексты привязывать только к SAB, без брендов работодателей или сторонних организаций.
  Старые сетевые пути организаций не использовать. Официальные названия программы Revit,
  форматов, компонентов и технические пути установки сохранять точными.

## Текущий выпуск1.1.0 «Редизайн» —12.10.2026

- Пользователь явно утвердил весь показанный комплект «Развёрток» и общие notification416DIP/status16Bold/summary outline, tabs40DIP/text14. Разрешены production migration, штатный Build-Msi.bat/MSI и commit+push/release1.1.0 «Редизайн». Установка по-прежнему не разрешена. Это новое разрешение; старые pending/proposal/review документы сохраняют исторический scope.
- Текущий перенос включает9actual Elevations views/classes, VM null-Document guard (реальные collectors при Document), catalog raw draft round trip; owners/proof — Tools/UiShowcase/elevations-production-migration.json. Actual production captures через RedesignProduction.cs отделены от isolated ElevationPreview/generated sessions. Не считать635PASS/44PNG исторического preview production evidence.
- Результаты Revit/API/physical input/OS DPI/installation не выдумывать. Предыдущий MSI payload PASS не доказывает пакет1.1.0; до выпуска нужны свежая штатная сборка/payload proof, bounded independent review, commit+push и проверка remote/release по текущему разрешению.

## Обязательный workflow интерфейсов SAB

- До каждой UI-задачи читать `.agents/skills/sab-wpf-ui/SKILL.md`, `Docs/Ui/DesignSystem/STATUS.json`, `Specification.md`, `ErrorRegistry.md`, `Workflow.md` и `ReviewRoles.md` в той же папке. Это единый канонический пакет: Specification содержит текущие нормы; реестр — источники/ID/критерии; Workflow — обязательный процесс; ReviewRoles — постоянные контракты существующих builder/reviewer/documenter. Не создавать параллельный стандарт или дубли ролей.
- Текущее состояние:0.5.0-source-review, approved_for_production_migration, approvedConcept=FluentLightSource, approvedAt=2026-10-11, productionMigrationAuthorized=true, installationAuthorized=false. Новый прямой запрос разрешил ровно перенос0.5 в два модуля; исторические preview-вердикты не меняются задним числом. После явного утверждения системы обычные цвета/отступы/контейнеры и компоновка решаются автономно, без согласования каждого окна. Существенные изменения функций и scope соблюдать отдельно.
- Текущий разрешённый перенос включает оба модуля вместе: «Создание видов и листов» main+secondary settings и «Параметризацию» со всеми4 вкладками/настоящими ParameterPicker/CategorySelectionControl. Сохранить ровно макеты0.5 без нового выбора вариантов. Исторический scope11.10;12.10 отдельно утверждён комплект «Развёрток». Установка и остальные неоговорённые модули не разрешены. Production-ресурсы общие: SABFluentLight.xaml, UI/FluentLight/{Presentation,FluentResources}.cs, parameter FluentTheme.cs; глобальный SABWindowStyles и SmallWindows Theme сохранены.
- Дополнительный прямой запрос12.10 разрешил notification-этап параметризации по принятому дизайну11.10, затем общий шаблон, затем MSI. Общие owners: UI/Notifications/{NotificationMessage,NotificationCard,NotificationStack}.cs и notification-only styles SABFluentLight; module adapter ParameterNotifications.cs. Следовать Specification/ErrorRegistry059–064 и Notification-Layout; legacy ReportWindow и API/transactions сохранять. Передача данных в модель без Revit DB references; modeless callbacks только clipboard/close/scroll. Дополнительно утверждён elevation final result adapter; другие уведомления не мигрировать автоматически. Установка не разрешена.
- Пользователь описывает функции. До XAML/C# изучить реальные поля/команды/VM/API boundary, выбрать Layout Pattern и записать Layout Specification, для сложного окна сравнить2 схемы. Назначить применимость каждого ID реестра. Общие ресурсы и несколько функциональных patterns, без утраты логики ради одинаковой геометрии.
- Сохранять Binding/Command/DataContext/x:Name/handlers/VM/ExternalEvent и совместимость .NET/Revit. Новый компонент и повторяющуюся ошибку исправлять в общем слое, добавлять в реестр/coverage и проверку. Не плодить локальные стили. Применять подходящие UI UX Pro Max/Impeccable/frontend-design и Revit skills по фактической задаче.
- После UI-изменения: целевая сборка, запуск фактического окна, реальные снимки, применимые ID/состояния/minimum/long Russian text/keyboard, независимый bounded review и документирование по коду. Отчёт каждого ID: PASS/FAIL/NOT RUN/N/A с evidence/scope. Компиляция не visual-pass;144dpi render не OS150%; preview не Revit. Документационная правка без UI не требует тяжёлой пересборки, но использует прошлый протокол явно как существующий.
- Самые поздние общие размеры: TabItem Auto/min40DIP, text14/padding20,8; notification stack≤416DIP, outcome16Bold, summary outline1/radius4/padding10. Mode capsules min34/30 сохраняются. Самые поздние приоритеты: никаких описаний назначения/инструкций в окнах; одна footer «Инструкция»→local module HTML; рабочие metadata/errors/results сохранять. Капсулы minimum34 с ростом по тексту, не fixed34. Table scrollbar только ниже header/верхней группы, без пустого gutter. Полные текущие значения/критерии — в Specification/ErrorRegistry.
- Существующий аудит36 окон не повторять. Active host csproj компилирует SourcePreview.cs, NotificationPreview.cs, RedesignProduction.cs и отдельно ElevationPreview.cs/generated copies; исторические Gallery/ReferenceView/CompositeControls0.1 исключены. Проверять component-coverage.csv и CoveragePlan.md; наличие старого файла не current coverage. Старые WindowStyle/UI-Lab/Redesign_Backlog/Refinement-планы — история и источники, не конкурирующие нормы.
- `python Tools/UiShowcase/ValidateUiDocs.py` проверяет документы/ID/ссылки/coverage/существующие evidence, не качество UI. При новой ошибке обновлять канон/общий компонент/критерий и заполненный отчёт задачи, не обещать отсутствие всех будущих дефектов.

- Текущий Build.ps1 собирает настоящие production-исходники без SourcePreview.targets/Compile substitutions. GenerateSourceViews.py вызывает successor VerifyNotificationSource.py и не переписывает UI; старый transform в history. Старые source-contract.json/75 PASS/27 screenshots-v0.5 — неизменяемая история. Текущие production-baseline/production-source-contract и screenshots-production-0.5 оцениваются отдельно; код source check не доказывает работу в Revit.
- Завершённый migration proof описывает исходники до notification-правок; новый notification source contract/report не перезаписывает его. Native notification fixtures и screenshots-notifications доказывают только перечисленные WPF/clipboard состояния вне Revit, не actual module execution/API22/OS monitor transitions. Для transient notification HTML-footer N/A; отсутствие среды для Revit/physical/OS проверки — NOT RUN.
- Для Revit2022 использовать `Tools/Build-Revit2022.ps1` с локальными API из `C:\Users\svetl\Desktop\Codex\lib\Revit2022`; исходник DLL предоставлен пользователем в `Yandex.Disk\For_Revit\RVT22_dll`. Общие WPF-окна одинаковы для2022/2023/2024; отдельные копии интерфейса для2022 не создавать. `-Capture` собирает host против API22 и сохраняет отдельные native evidence вне Revit, не меняя исторический production-пакет2023.

- Актуальное дополнение12.10: host также компилирует ElevationPreview.cs +14 generated C# только для изолированного комплекта «Развёрток». STATUS.elevations требует явного утверждения всего комплекта перед production migration. Общие notifications: bottom-right16DIP, новые снизу, рост вверх; прежнее top-right/newest-first отменено. Current evidence: Elevations-Report/Review и Notification-Verification; общая галерея51тип=31current/11resource/9missing.
