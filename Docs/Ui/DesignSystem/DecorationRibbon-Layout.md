# Оформление развёрток — иконка и единый вход в каталог17.10

Прямой запрос пользователя: найти иконку «Оформить развертки», удалить дублирующую ribbon-кнопку «Настройки оформления», переименовать внутреннюю «Каталог» в «Каталог элементов». Scope: MainPanel, shared Ribbon icon factory, одна подпись actual ElevationDecorationWindow.xaml и соответствующая локальная инструкция. ConfigureElevationDecorationCatalogCommand/каталог/сервисы/параметры/handlers сохраняются.

Выбран контурный [Lucide pencil-ruler](https://lucide.dev/icons/pencil-ruler): карандаш/линейка обозначают оформление марок и размеров. Исходный SVG встроен в DLL; WPF DrawingImage масштабируется16/32 без raster dependencies, stroke1 на24grid, цвет прежних развёрток RGB251,152,152. ISC notice поставляется в существующем RibbonIcons-Credits.md. Другие розовые значки не меняются.

Ribbon: самостоятельная большая «Оформить\nразвертки» с прежней identity SAB_DecorateInteriorElevations; прежняя пара small stacked buttons заменяется одной, поэтому отдельная ribbon-кнопка настроек больше не создаётся. Window: прежний Settings Form, header [Оформление развёрток] [Каталог элементов], Grid */Auto и shared SabPrimaryButtonStyle. x:Name ConfigureCatalogButton и его OpenCatalogRequested/CaptureDraft/RestoreDraft остаются. Два сложных варианта N/A: меняются только label и одна ribbon-группа.

Инструкция указывает доступный путь SAB → Развертки → Оформить развертки → Каталог элементов. Для подготовки каталога нужен отдельный вид, а не лист (существующее ограничение команды). После возврата draft сохраняется; затем можно отменить окно и открыть нужную развертку/лист. На листе каталог не выбирать. HTML template/styles неизменны.

Проверка: actual production decoration window normal/minimum, полный caption/geometry и native routed catalog click без Document; actual vector16/32 и scaled48, pink/alpha/bounds; исходная цепочка catalog/draft и отсутствие ribbon registration; штатные MSI2022/23/24 с извлечённой DLL/XAML/HTML. Native Revit ribbon/input/catalog picking/OS DPI и установка NOT RUN. Независимый bounded review. Предыдущее dirty source/evidence сохранено outputs/ui-production-preview/ribbon-before-decoration.zip; параллельные TemplateEditor/NotificationsAudit не затрагиваются.

Применимость каждой ID — DecorationRibbon-Report.json: общие contracts/authority/evidence/label/layout/help и новый088 (086/087 заняты параллельной задачей); остальные table/notification/model features N/A по фактическому scope. Реальная Revit проверка NOT RUN, а не N/A.
