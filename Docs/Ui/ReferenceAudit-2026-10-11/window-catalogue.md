# Каталог WPF-окон SAB

Дата: 11.10.2026. Commit: `565ebe7c2f3ced2611014b6e0363baa6ea5f1096`. Состав взят из исходников и явного списка Compile/Page/Content в SAB.csproj.

«Типы» — различные пользовательские контролы, контейнеры и модели колонок, объявленные в окне, его шаблонах, C# и вложенных компонентах. Повторы не увеличивают число. Служебные части общих шаблонов (Popup, Thumb, ContentPresenter и т. п.) вынесены в ресурсный реестр и не увеличивают это число. Набор объединяет условные ветви UI: все типы не обязаны одновременно присутствовать на экране. ToolTip через свойство считается возможностью, а не дополнительным типом для каждой строки.

| Окно | Модуль | Типов | Сложность | Балл /100 |
|---|---|---:|---|---:|
| ParameterTools.SettingsWindow | Cls_ParameterTools | 23 | высокая | 92.5 |
| CreateViewsAndSheetsWindow | Cls_CreateViewsAndSheets | 16 | высокая | 89.5 |
| ViewTemplateGraphicsWindow | Cls_ViewTemplateGraphics | 15 | высокая | 82.0 |
| ElevationSettingsWindow | Cls_InteriorElevations | 17 | высокая | 77.5 |
| MaterialRulesWindow | Cls_MaterialQuantity | 11 | средняя | 74.0 |
| DoorWindowViewsWindow | Cls_DoorWindowExplanations | 12 | высокая | 70.0 |
| ElevationDecorationWindow | Cls_InteriorElevations | 15 | высокая | 66.5 |
| CreateViewsAndSheetsSettingsWindow | Cls_CreateViewsAndSheets | 12 | средняя | 65.25 |
| CheckCategoriesWindow | Cls_ParameterTools | 12 | средняя | 64.0 |
| FilledRegion.SettingsWindow | Cls_FilledRegionFromMaterial | 11 | средняя | 63.75 |
| FrameModuleWindow | Cls_GklFrame | 10 | средняя | 56.75 |
| MultiRoomSelectionWindow | Cls_InteriorElevations | 8 | средняя | 56.5 |
| ReportWindow | Cls_ParameterTools | 9 | средняя | 56.25 |
| GraphicOverrideEditorWindow | Cls_ViewTemplateGraphics | 10 | средняя | 56.25 |
| DeleteViewsAndSheetsWindow | Cls_CreateViewsAndSheets | 9 | средняя | 54.25 |
| ElevationDecorationCatalogWindow | Cls_InteriorElevations | 11 | средняя | 54.25 |
| SyncReminderSettingsWindow | SyncReminder | 9 | средняя | 53.25 |
| SabStyledToastHost | Helpers | 9 | средняя | 51.75 |
| LibraryBuilderWindow | Cls_RevitLibraryBuilder | 9 | средняя | 50.75 |
| DoorWindowSelectionListWindow | Cls_DoorWindowExplanations | 8 | средняя | 50.5 |
| ModelSourceWindow | Cls_ParameterTools | 8 | средняя | 50.25 |
| AddFiltersWindow | Cls_ViewTemplateGraphics | 10 | средняя | 50.0 |
| RoomPlanSchemeSettingsWindow | Cls_InteriorElevations | 9 | средняя | 46.25 |
| DataSourceDialog | BimDashboard | 7 | низкая | 35.0 |
| ElevationCropAdjustmentWindow | Cls_InteriorElevations | 6 | низкая | 35.0 |
| CorpusChoiceWindow | Cls_ParameterTools | 6 | низкая | 32.5 |
| CreateViewsAndSheetsProgressWindow | Cls_CreateViewsAndSheets | 5 | низкая | 28.5 |
| MaterialQuantityProgressWindow | Cls_MaterialQuantity | 5 | низкая | 28.0 |
| CropByExampleActionWindow | Cls_InteriorElevations | 6 | низкая | 27.5 |
| CropByExampleLineCreationWindow | Cls_InteriorElevations | 6 | низкая | 27.5 |
| LineGroupNextActionWindow | Cls_InteriorElevations | 5 | низкая | 24.5 |
| LineGroupSelectionWindow | Cls_InteriorElevations | 5 | низкая | 24.5 |
| ConfirmViewCreationDialog | Cls_RevitLibraryBuilder | 4 | низкая | 23.75 |
| DuckReminderWindow | SyncReminder | 4 | низкая | 18.0 |
| PeekingAnimalReminderWindow | SyncReminder | 4 | низкая | 18.0 |
| PoopDropWindow | SyncReminder | 1 | низкая | 8.5 |

## ParameterTools.SettingsWindow

**Назначение:** Правила заполнения параметров.

**C#:** [SAB/Cls_ParameterTools/UI/SettingsWindow.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_ParameterTools/UI/SettingsWindow.cs:33). **XAML:** нет, интерфейс строится C#.

**Размер:** 1120 × 820 DIP; минимум 980 × 680 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** 4 вкладки: Правила, Источники, Значения и сопоставления, Категории; вложенные таблицы, закреплённый подвал. **Сложность:** высокая.

**Компоненты SAB:** CategorySelectionControl, ParameterPicker

**Типы (23):** `Border`, `Button`, `CategorySelectionControl`, `CheckBox`, `ComboBox`, `ContentControl`, `DataGrid`, `DataGridCheckBoxColumn`, `DataGridTemplateColumn`, `DataGridTextColumn`, `DockPanel`, `Grid`, `GroupBox`, `ListBox`, `ParameterPicker`, `ScrollViewer`, `StackPanel`, `TabControl`, `TabItem`, `TextBlock`, `TextBox`, `ToggleButton`, `WrapPanel`.

**Состояния/нестандартное оформление:** ParameterPicker, фильтрация категорий, проверка профиля, выбор строки, редактирование; источники и таблицы перестраиваются динамически.

**Оценки И/К/Д/С/В/А:** [9.5, 9.5, 10, 8.5, 9, 7]; итог 92.5/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## CreateViewsAndSheetsWindow

**Назначение:** Пакетное создание видов и листов.

**C#:** [SAB/Cls_CreateViewsAndSheets/Views/CreateViewsAndSheetsWindow.xaml.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_CreateViewsAndSheets/Views/CreateViewsAndSheetsWindow.xaml.cs:23). **XAML:** [SAB/Cls_CreateViewsAndSheets/Views/CreateViewsAndSheetsWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_CreateViewsAndSheets/Views/CreateViewsAndSheetsWindow.xaml).

**Размер:** 1280 × 760 DIP; минимум 1100 × 640 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Панель настроек, основная таблица, группировка, закреплённый подвал. **Сложность:** высокая.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (16):** `Border`, `Button`, `CheckBox`, `ComboBox`, `DataGrid`, `DataGridTemplateColumn`, `DataGridTextColumn`, `Expander`, `Grid`, `ItemsControl`, `RadioButton`, `ScrollViewer`, `StackPanel`, `TextBlock`, `TextBox`, `ToggleButton`.

**Состояния/нестандартное оформление:** Множественное выделение, массовое редактирование, drag-and-drop, разные режимы, валидация; колонки параметров браузера генерируются C#.

**Оценки И/К/Д/С/В/А:** [9, 8.5, 10, 9, 9, 7]; итог 89.5/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## ViewTemplateGraphicsWindow

**Назначение:** Пакетное редактирование графики шаблонов видов.

**C#:** [SAB/Cls_ViewTemplateGraphics/Views/ViewTemplateGraphicsWindow.xaml.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_ViewTemplateGraphics/Views/ViewTemplateGraphicsWindow.xaml.cs:22). **XAML:** [SAB/Cls_ViewTemplateGraphics/Views/ViewTemplateGraphicsWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_ViewTemplateGraphics/Views/ViewTemplateGraphicsWindow.xaml).

**Размер:** 1380 × 820 DIP; минимум 1120 × 650 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Список шаблонов слева, 8 вкладок справа, несколько таблиц. **Сложность:** высокая.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (15):** `Border`, `Button`, `CheckBox`, `ComboBox`, `DataGrid`, `DataGridTemplateColumn`, `DataGridTextColumn`, `Grid`, `ListBox`, `StackPanel`, `TabControl`, `TabItem`, `TextBlock`, `TextBox`, `WrapPanel`.

**Состояния/нестандартное оформление:** Трёхсостояние флажков, изменено/исходное, readonly/редактируемые значения; иерархия категорий в DataGrid, не TreeView.

**Оценки И/К/Д/С/В/А:** [8, 8, 10, 9, 8, 3]; итог 82.0/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## ElevationSettingsWindow

**Назначение:** Создание развёрток стен.

**C#:** [SAB/Cls_InteriorElevations/Views/ElevationSettingsWindow.xaml.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_InteriorElevations/Views/ElevationSettingsWindow.xaml.cs:30). **XAML:** [SAB/Cls_InteriorElevations/Views/ElevationSettingsWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_InteriorElevations/Views/ElevationSettingsWindow.xaml).

**Размер:** 980 × 720 DIP; минимум 420 × 540 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Вкладки, адаптивные группы, текстовый предпросмотр, прокрутка. **Сложность:** высокая.

**Компоненты SAB:** AdaptiveSettingsGrid

**Типы (17):** `AdaptiveSettingsGrid`, `Border`, `Button`, `CheckBox`, `ComboBox`, `Grid`, `GroupBox`, `ItemsControl`, `RadioButton`, `RichTextBox`, `ScrollViewer`, `StackPanel`, `TabControl`, `TabItem`, `TextBlock`, `TextBox`, `WrapPanel`.

**Состояния/нестандартное оформление:** RichTextBox с Run для имени, динамический предпросмотр, предупреждения, зависимые настройки.

**Оценки И/К/Д/С/В/А:** [8, 9.5, 4, 9, 9, 4]; итог 77.5/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## MaterialRulesWindow

**Назначение:** Единицы и правила подсчёта материалов.

**C#:** [SAB/Cls_MaterialQuantity/MaterialRulesWindow.xaml.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_MaterialQuantity/MaterialRulesWindow.xaml.cs:11). **XAML:** [SAB/Cls_MaterialQuantity/MaterialRulesWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_MaterialQuantity/MaterialRulesWindow.xaml).

**Размер:** 1100 × 700 DIP; минимум 920 × 560 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Поиск/фильтр, массовое правило, таблица, нижняя панель прогресса. **Сложность:** средняя.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (11):** `Border`, `Button`, `ComboBox`, `DataGrid`, `DataGridTemplateColumn`, `DataGridTextColumn`, `Grid`, `ProgressBar`, `StackPanel`, `TextBlock`, `TextBox`.

**Состояния/нестандартное оформление:** Множественный выбор, изменённые/неполные правила, busy, скрытый/видимый прогресс и счётчики.

**Оценки И/К/Д/С/В/А:** [6.5, 6.5, 8.5, 9, 9, 6]; итог 74.0/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## DoorWindowViewsWindow

**Назначение:** Создание видов дверей и окон.

**C#:** [SAB/Cls_DoorWindowExplanations/Views/DoorWindowViewsWindow.xaml.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_DoorWindowExplanations/Views/DoorWindowViewsWindow.xaml.cs:16). **XAML:** [SAB/Cls_DoorWindowExplanations/Views/DoorWindowViewsWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_DoorWindowExplanations/Views/DoorWindowViewsWindow.xaml).

**Размер:** 1180 × 780 DIP; минимум 980 × 680 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Левая форма, раскрываемые секции, предпросмотр. **Сложность:** высокая.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (12):** `Border`, `Button`, `CheckBox`, `ComboBox`, `Expander`, `Grid`, `RadioButton`, `ScrollViewer`, `StackPanel`, `TextBlock`, `TextBox`, `ToggleButton`.

**Состояния/нестандартное оформление:** Режимы вида/размещения, валидация, отключённые поля, условный предпросмотр.

**Оценки И/К/Д/С/В/А:** [7.5, 8, 3, 8.5, 8, 5]; итог 70.0/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## ElevationDecorationWindow

**Назначение:** Оформление развёрток: марки, размеры, отметки.

**C#:** [SAB/Cls_InteriorElevations/Views/ElevationDecorationWindow.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_InteriorElevations/Views/ElevationDecorationWindow.cs:21). **XAML:** [SAB/Cls_InteriorElevations/Views/ElevationDecorationWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_InteriorElevations/Views/ElevationDecorationWindow.xaml).

**Размер:** 900 × 720 DIP; минимум 420 × 540 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Вкладки, AdaptiveSettingsGrid, группы и прокрутка. **Сложность:** высокая.

**Компоненты SAB:** AdaptiveSettingsGrid

**Типы (15):** `AdaptiveSettingsGrid`, `Border`, `Button`, `CheckBox`, `ComboBox`, `Grid`, `GroupBox`, `RadioButton`, `ScrollViewer`, `StackPanel`, `TabControl`, `TabItem`, `TextBlock`, `TextBox`, `WrapPanel`.

**Состояния/нестандартное оформление:** Условные группы и поля, числовая проверка, выбранные типы семейств.

**Оценки И/К/Д/С/В/А:** [7, 9, 2, 8, 8, 2]; итог 66.5/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## CreateViewsAndSheetsSettingsWindow

**Назначение:** Дополнительные настройки создания видов и листов.

**C#:** [SAB/Cls_CreateViewsAndSheets/Views/CreateViewsAndSheetsSettingsWindow.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_CreateViewsAndSheets/Views/CreateViewsAndSheetsSettingsWindow.cs:13). **XAML:** [SAB/Cls_CreateViewsAndSheets/Views/CreateViewsAndSheetsSettingsWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_CreateViewsAndSheets/Views/CreateViewsAndSheetsSettingsWindow.xaml).

**Размер:** 1040 × 700 DIP; минимум 940 × 620 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Прокручиваемые секции Expander, связанные поля. **Сложность:** средняя.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (12):** `Border`, `Button`, `CheckBox`, `ComboBox`, `Expander`, `Grid`, `ItemsControl`, `RadioButton`, `ScrollViewer`, `StackPanel`, `TextBlock`, `TextBox`.

**Состояния/нестандартное оформление:** Раскрытие, режимы размещения, условная доступность, динамические параметры браузера.

**Оценки И/К/Д/С/В/А:** [6.5, 7.5, 3, 8, 8, 6]; итог 65.25/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## CheckCategoriesWindow

**Назначение:** Категории для проверки параметров.

**C#:** [SAB/Cls_ParameterTools/UI/CategorySelectionControl.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_ParameterTools/UI/CategorySelectionControl.cs:75). **XAML:** нет, интерфейс строится C#.

**Размер:** 470 × 550 DIP; минимум 420 × 430 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** CategorySelectionControl, сообщение ошибки и действия. **Сложность:** средняя.

**Компоненты SAB:** CategorySelectionControl

**Типы (12):** `Border`, `Button`, `CategorySelectionControl`, `DockPanel`, `Grid`, `GroupBox`, `ListBox`, `StackPanel`, `TextBlock`, `TextBox`, `ToggleButton`, `WrapPanel`.

**Состояния/нестандартное оформление:** Поиск, тумблеры, множественный выбор, ошибка при нулевом наборе.

**Оценки И/К/Д/С/В/А:** [6, 6.5, 6, 7, 7, 7]; итог 64.0/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## FilledRegion.SettingsWindow

**Назначение:** Штриховка по материалу: настройки соответствий.

**C#:** [SAB/Cls_FilledRegionFromMaterial/SettingsWindow.xaml.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_FilledRegionFromMaterial/SettingsWindow.xaml.cs:11). **XAML:** [SAB/Cls_FilledRegionFromMaterial/SettingsWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_FilledRegionFromMaterial/SettingsWindow.xaml).

**Размер:** 1120 × 700 DIP; минимум 880 × 610 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Форма выбора и редактируемая таблица. **Сложность:** средняя.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (11):** `Border`, `Button`, `CheckBox`, `ComboBox`, `DataGrid`, `DataGridTemplateColumn`, `Grid`, `ScrollViewer`, `StackPanel`, `TextBlock`, `TextBox`.

**Состояния/нестандартное оформление:** Флажки, условные поля, выбор материалов/штриховок, табличные ComboBox.

**Оценки И/К/Д/С/В/А:** [5.5, 6.5, 8, 7, 7, 4]; итог 63.75/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## FrameModuleWindow

**Назначение:** Расчёт/создание каркаса ГКЛ.

**C#:** [SAB/Cls_GklFrame/Views/FrameModuleWindow.xaml.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_GklFrame/Views/FrameModuleWindow.xaml.cs:13). **XAML:** [SAB/Cls_GklFrame/Views/FrameModuleWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_GklFrame/Views/FrameModuleWindow.xaml).

**Размер:** 980 × 690 DIP; минимум 880 × 620 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Две колонки настроек в ScrollViewer, ссылка справки и действия. **Сложность:** средняя.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (10):** `Border`, `Button`, `CheckBox`, `ComboBox`, `Grid`, `Hyperlink`, `ScrollViewer`, `StackPanel`, `TextBlock`, `TextBox`.

**Состояния/нестандартное оформление:** Генерация/расчёт, валидация, доступность полей, Hyperlink.

**Оценки И/К/Д/С/В/А:** [6.5, 6.5, 1, 7, 7, 4]; итог 56.75/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## MultiRoomSelectionWindow

**Назначение:** Сопоставление помещений для нескольких развёрток.

**C#:** [SAB/Cls_InteriorElevations/Views/MultiRoomSelectionWindow.xaml.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_InteriorElevations/Views/MultiRoomSelectionWindow.xaml.cs:21). **XAML:** [SAB/Cls_InteriorElevations/Views/MultiRoomSelectionWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_InteriorElevations/Views/MultiRoomSelectionWindow.xaml).

**Размер:** 1040 × 560 DIP; минимум 1000 × 420 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Редактируемая таблица строк и кнопки. **Сложность:** средняя.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (8):** `Border`, `Button`, `DataGrid`, `DataGridTemplateColumn`, `DataGridTextColumn`, `Grid`, `StackPanel`, `TextBlock`.

**Состояния/нестандартное оформление:** Вставка/удаление строки, текстовые/шаблонные ячейки.

**Оценки И/К/Д/С/В/А:** [4.5, 5, 7, 6.5, 7, 7]; итог 56.5/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## ReportWindow

**Назначение:** Отчёт проблем заполнения параметров.

**C#:** [SAB/Cls_ParameterTools/UI/SmallWindows.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_ParameterTools/UI/SmallWindows.cs:196). **XAML:** нет, интерфейс строится C#.

**Размер:** 1120 × 680 DIP; минимум 980 × 540 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Поиск, DataGrid и действия перехода/выделения. **Сложность:** средняя.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (9):** `Border`, `Button`, `DataGrid`, `DataGridTextColumn`, `DockPanel`, `Grid`, `StackPanel`, `TextBlock`, `TextBox`.

**Состояния/нестандартное оформление:** Фильтрация, выбранная проблема; переход к Revit зависит от UIApplication.

**Оценки И/К/Д/С/В/А:** [4.5, 6, 7, 7, 6, 3]; итог 56.25/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## GraphicOverrideEditorWindow

**Назначение:** Редактор переопределений графики.

**C#:** [SAB/Cls_ViewTemplateGraphics/Views/GraphicOverrideEditorWindow.xaml.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_ViewTemplateGraphics/Views/GraphicOverrideEditorWindow.xaml.cs:20). **XAML:** [SAB/Cls_ViewTemplateGraphics/Views/GraphicOverrideEditorWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_ViewTemplateGraphics/Views/GraphicOverrideEditorWindow.xaml).

**Размер:** 760 × 520 DIP; минимум 680 × 340 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Прокручиваемая форма графических настроек. **Сложность:** средняя.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (10):** `Border`, `Button`, `CheckBox`, `ComboBox`, `Grid`, `ScrollViewer`, `Slider`, `StackPanel`, `TextBlock`, `TextBox`.

**Состояния/нестандартное оформление:** Slider прозрачности, цвет/толщина/образцы линий и штриховок, флажки.

**Оценки И/К/Д/С/В/А:** [6.5, 5.5, 2, 7, 7, 4]; итог 56.25/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## DeleteViewsAndSheetsWindow

**Назначение:** Выбор созданных видов и листов для удаления.

**C#:** [SAB/Cls_CreateViewsAndSheets/Views/DeleteViewsAndSheetsWindow.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_CreateViewsAndSheets/Views/DeleteViewsAndSheetsWindow.cs:15). **XAML:** [SAB/Cls_CreateViewsAndSheets/Views/DeleteViewsAndSheetsWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_CreateViewsAndSheets/Views/DeleteViewsAndSheetsWindow.xaml).

**Размер:** 1120 × 720 DIP; минимум 920 × 560 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Таблица выбора и подтверждение. **Сложность:** средняя.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (9):** `Border`, `Button`, `CheckBox`, `DataGrid`, `DataGridTemplateColumn`, `DataGridTextColumn`, `Grid`, `StackPanel`, `TextBlock`.

**Состояния/нестандартное оформление:** Выбранные строки, флажки, доступность действия удаления.

**Оценки И/К/Д/С/В/А:** [4.5, 5, 7, 6, 6, 6]; итог 54.25/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## ElevationDecorationCatalogWindow

**Назначение:** Каталог элементов оформления развёрток.

**C#:** [SAB/Cls_InteriorElevations/Views/ElevationDecorationCatalogWindow.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_InteriorElevations/Views/ElevationDecorationCatalogWindow.cs:24). **XAML:** [SAB/Cls_InteriorElevations/Views/ElevationDecorationCatalogWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_InteriorElevations/Views/ElevationDecorationCatalogWindow.xaml).

**Размер:** 880 × 620 DIP; минимум 760 × 520 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** GroupBox и ListView с GridView. **Сложность:** средняя.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (11):** `Border`, `Button`, `ComboBox`, `Grid`, `GridView`, `GridViewColumn`, `GroupBox`, `ListView`, `StackPanel`, `TextBlock`, `WrapPanel`.

**Состояния/нестандартное оформление:** Тип и источник записи, выделение, операции с каталогом.

**Оценки И/К/Д/С/В/А:** [4.5, 6.5, 6, 6, 6, 3]; итог 54.25/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## SyncReminderSettingsWindow

**Назначение:** Настройки напоминания о синхронизации.

**C#:** [SAB/SyncReminder/SyncReminderSettingsWindow.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/SyncReminder/SyncReminderSettingsWindow.cs:11). **XAML:** нет, интерфейс строится C#.

**Размер:** 560 × 540 DIP; минимум 560 × 540 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Компактная форма интервала и вида анимации. **Сложность:** средняя.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (9):** `Border`, `Button`, `CheckBox`, `ComboBox`, `Grid`, `Slider`, `StackPanel`, `TextBlock`, `TextBox`.

**Состояния/нестандартное оформление:** Slider и TextBox синхронизированы, флажок включения, тестовое напоминание.

**Оценки И/К/Д/С/В/А:** [6.5, 5, 0, 7, 7, 6]; итог 53.25/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## SabStyledToastHost

**Назначение:** Хост уведомлений SAB.

**C#:** [SAB/Helpers/Notifications/ToastNotifications/SabStyledToastNotifier.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Helpers/Notifications/ToastNotifications/SabStyledToastNotifier.cs:129). **XAML:** нет, интерфейс строится C#.

**Размер:** 464 × 24 DIP; минимум — × 86 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Стопка уведомлений, детали в Expander, ссылки и действия. **Сложность:** средняя.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (9):** `Border`, `Button`, `DockPanel`, `Expander`, `Grid`, `ScrollViewer`, `StackPanel`, `TextBlock`, `TextBox`.

**Состояния/нестандартное оформление:** Info/Success/Warning/Error, таймер исчезновения, кнопки закрытия/папки, развёрнутые детали.

**Оценки И/К/Д/С/В/А:** [4.5, 6, 2, 9, 6, 3]; итог 51.75/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## LibraryBuilderWindow

**Назначение:** Операции библиотеки Revit и просмотр CSV.

**C#:** [SAB/Cls_RevitLibraryBuilder/UI/LibraryBuilderWindow.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_RevitLibraryBuilder/UI/LibraryBuilderWindow.cs:23). **XAML:** [SAB/Cls_RevitLibraryBuilder/UI/LibraryBuilderWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_RevitLibraryBuilder/UI/LibraryBuilderWindow.xaml).

**Размер:** 1080 × 760 DIP; минимум 920 × 640 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Навигация кнопками режимов, панели операций, прокрутка и прогресс. **Сложность:** средняя.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (9):** `Border`, `Button`, `DockPanel`, `Grid`, `ProgressBar`, `ScrollViewer`, `StackPanel`, `TextBlock`, `ToolTip`.

**Состояния/нестандартное оформление:** Выбранные режимы и действия, условная доступность, прогресс; режимы имитируют вкладки без TabControl.

**Оценки И/К/Д/С/В/А:** [4.5, 6, 2, 7, 7, 5]; итог 50.75/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## DoorWindowSelectionListWindow

**Назначение:** Список дверей/окон для формирования видов.

**C#:** [SAB/Cls_DoorWindowExplanations/Views/DoorWindowSelectionListWindow.xaml.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_DoorWindowExplanations/Views/DoorWindowSelectionListWindow.xaml.cs:23). **XAML:** [SAB/Cls_DoorWindowExplanations/Views/DoorWindowSelectionListWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_DoorWindowExplanations/Views/DoorWindowSelectionListWindow.xaml).

**Размер:** 980 × 620 DIP; минимум 900 × 500 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Таблица с флажками и подвал. **Сложность:** средняя.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (8):** `Border`, `Button`, `DataGrid`, `DataGridCheckBoxColumn`, `DataGridTextColumn`, `Grid`, `StackPanel`, `TextBlock`.

**Состояния/нестандартное оформление:** Выбор строк, DataGridCheckBoxColumn, пустой список.

**Оценки И/К/Д/С/В/А:** [4, 5, 6.5, 5.5, 6, 5]; итог 50.5/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## ModelSourceWindow

**Назначение:** Выбор источника значения модели.

**C#:** [SAB/Cls_ParameterTools/UI/SmallWindows.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_ParameterTools/UI/SmallWindows.cs:159). **XAML:** нет, интерфейс строится C#.

**Размер:** 530 × 420 DIP; минимум 430 × 320 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Поиск TextBox и ListBox, подвал. **Сложность:** средняя.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (8):** `Border`, `Button`, `DockPanel`, `Grid`, `ListBox`, `StackPanel`, `TextBlock`, `TextBox`.

**Состояния/нестандартное оформление:** Отфильтрованный/пустой список, выбранное значение.

**Оценки И/К/Д/С/В/А:** [4.5, 5, 5, 5, 6, 7]; итог 50.25/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## AddFiltersWindow

**Назначение:** Добавление фильтров к шаблонам видов.

**C#:** [SAB/Cls_ViewTemplateGraphics/Views/AddFiltersWindow.xaml.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_ViewTemplateGraphics/Views/AddFiltersWindow.xaml.cs:18). **XAML:** [SAB/Cls_ViewTemplateGraphics/Views/AddFiltersWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_ViewTemplateGraphics/Views/AddFiltersWindow.xaml).

**Размер:** 680 × 620 DIP; минимум 560 × 460 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Поиск и таблица выбора. **Сложность:** средняя.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (10):** `Border`, `Button`, `CheckBox`, `DataGrid`, `DataGridTemplateColumn`, `DataGridTextColumn`, `Grid`, `StackPanel`, `TextBlock`, `TextBox`.

**Состояния/нестандартное оформление:** Фильтр, флажки, выделенные/пустые строки.

**Оценки И/К/Д/С/В/А:** [4, 5, 6, 6, 6, 4]; итог 50.0/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## RoomPlanSchemeSettingsWindow

**Назначение:** Настройки плана-схемы помещений.

**C#:** [SAB/Cls_InteriorElevations/Views/RoomPlanSchemeSettingsWindow.xaml.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_InteriorElevations/Views/RoomPlanSchemeSettingsWindow.xaml.cs:16). **XAML:** [SAB/Cls_InteriorElevations/Views/RoomPlanSchemeSettingsWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_InteriorElevations/Views/RoomPlanSchemeSettingsWindow.xaml).

**Размер:** 820 × 560 DIP; минимум 760 × 540 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Прокручиваемые раскрываемые секции. **Сложность:** средняя.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (9):** `Border`, `Button`, `ComboBox`, `Expander`, `Grid`, `ScrollViewer`, `StackPanel`, `TextBlock`, `TextBox`.

**Состояния/нестандартное оформление:** Выбор типов, числовые настройки, раскрытие секций.

**Оценки И/К/Д/С/В/А:** [4.5, 6, 1, 6, 6, 4]; итог 46.25/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## DataSourceDialog

**Назначение:** Источник CSV для BIM dashboard.

**C#:** [SAB/BimDashboard/UI/DataSourceDialog.xaml.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/BimDashboard/UI/DataSourceDialog.xaml.cs:15). **XAML:** нет, интерфейс строится C#.

**Размер:** 700 × 470 DIP; минимум 680 × 450 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Карточки профиля и файла, подвал действий. **Сложность:** низкая.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (7):** `Border`, `Button`, `ComboBox`, `DockPanel`, `Grid`, `StackPanel`, `TextBlock`.

**Состояния/нестандартное оформление:** Недоступный профиль, выбранный/невыбранный путь; системный файловый диалог.

**Оценки И/К/Д/С/В/А:** [3, 5, 0, 4, 5, 7]; итог 35.0/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## ElevationCropAdjustmentWindow

**Назначение:** Числовая корректировка границ развёртки.

**C#:** [SAB/Cls_InteriorElevations/Views/ElevationCropAdjustmentWindow.xaml.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_InteriorElevations/Views/ElevationCropAdjustmentWindow.xaml.cs:13). **XAML:** [SAB/Cls_InteriorElevations/Views/ElevationCropAdjustmentWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_InteriorElevations/Views/ElevationCropAdjustmentWindow.xaml).

**Размер:** 540 × 438 DIP; минимум 540 × 438 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Компактная сетка числовых полей. **Сложность:** низкая.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (6):** `Border`, `Button`, `Grid`, `StackPanel`, `TextBlock`, `TextBox`.

**Состояния/нестандартное оформление:** Ошибочный ввод, подтверждение, обычный/недоступный ввод.

**Оценки И/К/Д/С/В/А:** [3, 4, 0, 6, 5, 5]; итог 35.0/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## CorpusChoiceWindow

**Назначение:** Ручной выбор корпуса.

**C#:** [SAB/Cls_ParameterTools/UI/SmallWindows.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_ParameterTools/UI/SmallWindows.cs:178). **XAML:** нет, интерфейс строится C#.

**Размер:** 400 × auto DIP; минимум — × 230 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Прокручиваемый набор кнопок значений. **Сложность:** низкая.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (6):** `Border`, `Button`, `Grid`, `ScrollViewer`, `StackPanel`, `TextBlock`.

**Состояния/нестандартное оформление:** Выбор доступного корпуса.

**Оценки И/К/Д/С/В/А:** [2, 4, 2, 4, 5, 7]; итог 32.5/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## CreateViewsAndSheetsProgressWindow

**Назначение:** Ход создания видов и листов.

**C#:** [SAB/Cls_CreateViewsAndSheets/Views/CreateViewsAndSheetsProgressWindow.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_CreateViewsAndSheets/Views/CreateViewsAndSheetsProgressWindow.cs:13). **XAML:** [SAB/Cls_CreateViewsAndSheets/Views/CreateViewsAndSheetsProgressWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_CreateViewsAndSheets/Views/CreateViewsAndSheetsProgressWindow.xaml).

**Размер:** 520 × auto DIP; минимум 500 × — DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Вертикальные этапы и индикатор прогресса. **Сложность:** низкая.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (5):** `Border`, `Grid`, `ProgressBar`, `StackPanel`, `TextBlock`.

**Состояния/нестандартное оформление:** Этапы, процент, счётчики и предупреждения создаются/обновляются из C#.

**Оценки И/К/Д/С/В/А:** [1, 3, 0, 7, 5, 7]; итог 28.5/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## MaterialQuantityProgressWindow

**Назначение:** Ход подсчёта материалов.

**C#:** [SAB/Cls_MaterialQuantity/MaterialQuantityProgressWindow.xaml.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_MaterialQuantity/MaterialQuantityProgressWindow.xaml.cs:8). **XAML:** [SAB/Cls_MaterialQuantity/MaterialQuantityProgressWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_MaterialQuantity/MaterialQuantityProgressWindow.xaml).

**Размер:** 560 × auto DIP; минимум 520 × — DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Короткое окно этапа и прогресса. **Сложность:** низкая.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (5):** `Border`, `Grid`, `ProgressBar`, `StackPanel`, `TextBlock`.

**Состояния/нестандартное оформление:** Определённый/неопределённый прогресс и текст этапа.

**Оценки И/К/Д/С/В/А:** [1, 3, 0, 7, 5, 6]; итог 28.0/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## CropByExampleActionWindow

**Назначение:** Выбор действия обрезки по примеру.

**C#:** [SAB/Cls_InteriorElevations/Views/CropByExampleActionWindow.xaml.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_InteriorElevations/Views/CropByExampleActionWindow.xaml.cs:17). **XAML:** [SAB/Cls_InteriorElevations/Views/CropByExampleActionWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_InteriorElevations/Views/CropByExampleActionWindow.xaml).

**Размер:** 850 × 410 DIP; минимум 850 × 410 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Набор крупных кнопок и пояснения. **Сложность:** низкая.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (6):** `Border`, `Button`, `Grid`, `StackPanel`, `TextBlock`, `WrapPanel`.

**Состояния/нестандартное оформление:** Разные действия, длинные пояснения Run.

**Оценки И/К/Д/С/В/А:** [2, 4, 0, 3, 5, 6]; итог 27.5/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## CropByExampleLineCreationWindow

**Назначение:** Режим создания линий обрезки по примеру.

**C#:** [SAB/Cls_InteriorElevations/Views/CropByExampleLineCreationWindow.xaml.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_InteriorElevations/Views/CropByExampleLineCreationWindow.xaml.cs:12). **XAML:** [SAB/Cls_InteriorElevations/Views/CropByExampleLineCreationWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_InteriorElevations/Views/CropByExampleLineCreationWindow.xaml).

**Размер:** 815 × 455 DIP; минимум 815 × 455 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Кнопки режимов и пояснения. **Сложность:** низкая.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (6):** `Border`, `Button`, `Grid`, `StackPanel`, `TextBlock`, `WrapPanel`.

**Состояния/нестандартное оформление:** Варианты действия, WrapPanel/Run.

**Оценки И/К/Д/С/В/А:** [2, 4, 0, 3, 5, 6]; итог 27.5/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## LineGroupNextActionWindow

**Назначение:** Дальнейшее действие после группы линий.

**C#:** [SAB/Cls_InteriorElevations/Views/LineGroupNextActionWindow.xaml.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_InteriorElevations/Views/LineGroupNextActionWindow.xaml.cs:10). **XAML:** [SAB/Cls_InteriorElevations/Views/LineGroupNextActionWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_InteriorElevations/Views/LineGroupNextActionWindow.xaml).

**Размер:** 680 × 270 DIP; минимум 640 × 270 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Пояснение и кнопки действий. **Сложность:** низкая.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (5):** `Border`, `Button`, `Grid`, `StackPanel`, `TextBlock`.

**Состояния/нестандартное оформление:** Переход к следующей операции.

**Оценки И/К/Д/С/В/А:** [2, 3, 0, 3, 4, 6]; итог 24.5/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## LineGroupSelectionWindow

**Назначение:** Выбор способа работы с группой линий.

**C#:** [SAB/Cls_InteriorElevations/Views/LineGroupSelectionWindow.xaml.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_InteriorElevations/Views/LineGroupSelectionWindow.xaml.cs:10). **XAML:** [SAB/Cls_InteriorElevations/Views/LineGroupSelectionWindow.xaml](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_InteriorElevations/Views/LineGroupSelectionWindow.xaml).

**Размер:** 620 × 270 DIP; минимум 620 × 270 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Компактный диалог действия. **Сложность:** низкая.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (5):** `Border`, `Button`, `Grid`, `StackPanel`, `TextBlock`.

**Состояния/нестандартное оформление:** Варианты выбора.

**Оценки И/К/Д/С/В/А:** [2, 3, 0, 3, 4, 6]; итог 24.5/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## ConfirmViewCreationDialog

**Назначение:** Подтверждение создания видов библиотеки.

**C#:** [SAB/Cls_RevitLibraryBuilder/UI/Dialogs/ConfirmViewCreationDialog.xaml.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_RevitLibraryBuilder/UI/Dialogs/ConfirmViewCreationDialog.xaml.cs:11). **XAML:** нет, интерфейс строится C#.

**Размер:** 420 × 190 DIP; минимум 380 × 170 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Сообщение и две кнопки. **Сложность:** низкая.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (4):** `Button`, `Grid`, `StackPanel`, `TextBlock`.

**Состояния/нестандартное оформление:** Default/Cancel, клавиатурное подтверждение.

**Оценки И/К/Д/С/В/А:** [1.5, 3, 0, 3, 4, 8]; итог 23.75/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## DuckReminderWindow

**Назначение:** Анимированное напоминание о синхронизации.

**C#:** [SAB/SyncReminder/DuckReminderWindow.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/SyncReminder/DuckReminderWindow.cs:16). **XAML:** нет, интерфейс строится C#.

**Размер:** 430 × 372 DIP; минимум 220 × 40 DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Прозрачный Canvas со спрайтом и декоративными фигурами. **Сложность:** низкая.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (4):** `Border`, `Canvas`, `Image`, `TextBlock`.

**Состояния/нестандартное оформление:** Кадры анимации, движение, декоративное дочернее окно.

**Оценки И/К/Д/С/В/А:** [0, 3, 0, 5, 2, 5]; итог 18.0/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## PeekingAnimalReminderWindow

**Назначение:** Анимированное напоминание выглядывающим персонажем.

**C#:** [SAB/SyncReminder/PeekingAnimalReminderWindow.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/SyncReminder/PeekingAnimalReminderWindow.cs:14). **XAML:** нет, интерфейс строится C#.

**Размер:** 900 × 560 DIP; минимум — × — DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Прозрачный Canvas, Image, подпись. **Сложность:** низкая.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (4):** `Border`, `Canvas`, `Image`, `TextBlock`.

**Состояния/нестандартное оформление:** Последовательность кадров и положение окна.

**Оценки И/К/Д/С/В/А:** [0, 3, 0, 5, 2, 5]; итог 18.0/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## PoopDropWindow

**Назначение:** Декоративный эффект напоминания.

**C#:** [SAB/SyncReminder/DuckReminderWindow.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/SyncReminder/DuckReminderWindow.cs:740). **XAML:** нет, интерфейс строится C#.

**Размер:** 46 × 38 DIP; минимум — × — DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.

**Структура:** Малое прозрачное Canvas-окно. **Сложность:** низкая.

**Компоненты SAB:** Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.

**Типы (1):** `Canvas`.

**Состояния/нестандартное оформление:** Движение/исчезновение фигур; не рабочая форма.

**Оценки И/К/Д/С/В/А:** [0, 1, 0, 2, 1, 5]; итог 8.5/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.

## Окна без отдельного класса

| Место | Назначение | Размер | Учёт |
|---|---|---|---|
| [SAB/Cls_ParameterTools/UI/SmallWindows.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Cls_ParameterTools/UI/SmallWindows.cs:139) | Toast параметров | 540 × auto; MaxHeight 420 | отдельная видимая поверхность |
| [SAB/Notifications/NotificationController.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Notifications/NotificationController.cs:297) | История/список уведомлений | 520 × 580 | отдельная видимая поверхность |
| [SAB/Notifications/NotificationController.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Notifications/NotificationController.cs:359) | Настройки уведомлений | 620 × 760 | отдельная видимая поверхность |
| [SAB/Helpers/Notifications/ToastNotifications/SabStyledToastNotifier.cs](C:/Users/svetl/Desktop/Codex/Projects/SAB/SAB/Helpers/Notifications/ToastNotifications/SabStyledToastNotifier.cs:100) | _dummyWindow, служебный владелец хоста | техническое окно | исключено из числа пользовательских интерфейсов |

Настройки уведомлений состоят из StackPanel, TextBlock, TextBox, Button, ToggleButton, Border/Ellipse в шаблоне; имеют длительность, переключатели уровней, readonly-путь и недоступную кнопку папки без модели. История строится динамически как ScrollViewer/StackPanel/Border с текстом/ссылками. Toast параметров — Border/StackPanel/TextBlock с таймером. Их узкая задача не делает их кандидатами на основное демонстрационное окно.

## Архив и тестовые интерфейсы

В SAB/Archive находятся 2 XAML-окна: PlanCornerMarkAlignmentWindow и RoomGeometryToolsWindow. Они не включены в действующий SAB.csproj и не участвуют в рейтинге. Исходники прочитаны без изменений. В тестовых/служебных проектах дополнительно найдены MainWindow прототипа ModPlusInspiredElevation, ReviewWindow (Tools/UiReview), SettingsWindow и DuckReminderWindow (SyncReminderTest). Они также исключены из рабочего набора. Резервные копии Backups, bin/obj/packages и outputs исключены как дубликаты/результаты сборки.
