"""Render the evidence catalogue after inventory.py; editorial notes are explicit."""
import collections, csv, json, re
from pathlib import Path
OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[2]
d=json.loads((OUT/'inventory.json').read_text(encoding='utf8'))
# purpose, structure, complexity, notable states, six qualitative scores (0..10)
meta={
'DataSourceDialog': ('Источник CSV для BIM dashboard','Карточки профиля и файла, подвал действий','низкая','Недоступный профиль, выбранный/невыбранный путь; системный файловый диалог',[3,5,0,4,5,7]),
'CreateViewsAndSheetsProgressWindow': ('Ход создания видов и листов','Вертикальные этапы и индикатор прогресса','низкая','Этапы, процент, счётчики и предупреждения создаются/обновляются из C#',[1,3,0,7,5,7]),
'CreateViewsAndSheetsSettingsWindow': ('Дополнительные настройки создания видов и листов','Прокручиваемые секции Expander, связанные поля','средняя','Раскрытие, режимы размещения, условная доступность, динамические параметры браузера',[6.5,7.5,3,8,8,6]),
'CreateViewsAndSheetsWindow': ('Пакетное создание видов и листов','Панель настроек, основная таблица, группировка, закреплённый подвал','высокая','Множественное выделение, массовое редактирование, drag-and-drop, разные режимы, валидация; колонки параметров браузера генерируются C#',[9,8.5,10,9,9,7]),
'DeleteViewsAndSheetsWindow': ('Выбор созданных видов и листов для удаления','Таблица выбора и подтверждение','средняя','Выбранные строки, флажки, доступность действия удаления',[4.5,5,7,6,6,6]),
'DoorWindowSelectionListWindow': ('Список дверей/окон для формирования видов','Таблица с флажками и подвал','средняя','Выбор строк, DataGridCheckBoxColumn, пустой список',[4,5,6.5,5.5,6,5]),
'DoorWindowViewsWindow': ('Создание видов дверей и окон','Левая форма, раскрываемые секции, предпросмотр','высокая','Режимы вида/размещения, валидация, отключённые поля, условный предпросмотр',[7.5,8,3,8.5,8,5]),
'FilledRegion.SettingsWindow': ('Штриховка по материалу: настройки соответствий','Форма выбора и редактируемая таблица','средняя','Флажки, условные поля, выбор материалов/штриховок, табличные ComboBox',[5.5,6.5,8,7,7,4]),
'FrameModuleWindow': ('Расчёт/создание каркаса ГКЛ','Две колонки настроек в ScrollViewer, ссылка справки и действия','средняя','Генерация/расчёт, валидация, доступность полей, Hyperlink',[6.5,6.5,1,7,7,4]),
'CropByExampleActionWindow': ('Выбор действия обрезки по примеру','Набор крупных кнопок и пояснения','низкая','Разные действия, длинные пояснения Run',[2,4,0,3,5,6]),
'CropByExampleLineCreationWindow': ('Режим создания линий обрезки по примеру','Кнопки режимов и пояснения','низкая','Варианты действия, WrapPanel/Run',[2,4,0,3,5,6]),
'ElevationCropAdjustmentWindow': ('Числовая корректировка границ развёртки','Компактная сетка числовых полей','низкая','Ошибочный ввод, подтверждение, обычный/недоступный ввод',[3,4,0,6,5,5]),
'ElevationDecorationCatalogWindow': ('Каталог элементов оформления развёрток','GroupBox и ListView с GridView','средняя','Тип и источник записи, выделение, операции с каталогом',[4.5,6.5,6,6,6,3]),
'ElevationDecorationWindow': ('Оформление развёрток: марки, размеры, отметки','Вкладки, AdaptiveSettingsGrid, группы и прокрутка','высокая','Условные группы и поля, числовая проверка, выбранные типы семейств',[7,9,2,8,8,2]),
'ElevationSettingsWindow': ('Создание развёрток стен','Вкладки, адаптивные группы, текстовый предпросмотр, прокрутка','высокая','RichTextBox с Run для имени, динамический предпросмотр, предупреждения, зависимые настройки',[8,9.5,4,9,9,4]),
'LineGroupNextActionWindow': ('Дальнейшее действие после группы линий','Пояснение и кнопки действий','низкая','Переход к следующей операции',[2,3,0,3,4,6]),
'LineGroupSelectionWindow': ('Выбор способа работы с группой линий','Компактный диалог действия','низкая','Варианты выбора',[2,3,0,3,4,6]),
'MultiRoomSelectionWindow': ('Сопоставление помещений для нескольких развёрток','Редактируемая таблица строк и кнопки','средняя','Вставка/удаление строки, текстовые/шаблонные ячейки',[4.5,5,7,6.5,7,7]),
'RoomPlanSchemeSettingsWindow': ('Настройки плана-схемы помещений','Прокручиваемые раскрываемые секции','средняя','Выбор типов, числовые настройки, раскрытие секций',[4.5,6,1,6,6,4]),
'MaterialQuantityProgressWindow': ('Ход подсчёта материалов','Короткое окно этапа и прогресса','низкая','Определённый/неопределённый прогресс и текст этапа',[1,3,0,7,5,6]),
'MaterialRulesWindow': ('Единицы и правила подсчёта материалов','Поиск/фильтр, массовое правило, таблица, нижняя панель прогресса','средняя','Множественный выбор, изменённые/неполные правила, busy, скрытый/видимый прогресс и счётчики',[6.5,6.5,8.5,9,9,6]),
'CheckCategoriesWindow': ('Категории для проверки параметров','CategorySelectionControl, сообщение ошибки и действия','средняя','Поиск, тумблеры, множественный выбор, ошибка при нулевом наборе',[6,6.5,6,7,7,7]),
'ParameterTools.SettingsWindow': ('Правила заполнения параметров','4 вкладки: Правила, Источники, Значения и сопоставления, Категории; вложенные таблицы, закреплённый подвал','высокая','ParameterPicker, фильтрация категорий, проверка профиля, выбор строки, редактирование; источники и таблицы перестраиваются динамически',[9.5,9.5,10,8.5,9,7]),
'ModelSourceWindow': ('Выбор источника значения модели','Поиск TextBox и ListBox, подвал','средняя','Отфильтрованный/пустой список, выбранное значение',[4.5,5,5,5,6,7]),
'CorpusChoiceWindow': ('Ручной выбор корпуса','Прокручиваемый набор кнопок значений','низкая','Выбор доступного корпуса',[2,4,2,4,5,7]),
'ReportWindow': ('Отчёт проблем заполнения параметров','Поиск, DataGrid и действия перехода/выделения','средняя','Фильтрация, выбранная проблема; переход к Revit зависит от UIApplication',[4.5,6,7,7,6,3]),
'ConfirmViewCreationDialog': ('Подтверждение создания видов библиотеки','Сообщение и две кнопки','низкая','Default/Cancel, клавиатурное подтверждение',[1.5,3,0,3,4,8]),
'LibraryBuilderWindow': ('Операции библиотеки Revit и просмотр CSV','Навигация кнопками режимов, панели операций, прокрутка и прогресс','средняя','Выбранные режимы и действия, условная доступность, прогресс; режимы имитируют вкладки без TabControl',[4.5,6,2,7,7,5]),
'AddFiltersWindow': ('Добавление фильтров к шаблонам видов','Поиск и таблица выбора','средняя','Фильтр, флажки, выделенные/пустые строки',[4,5,6,6,6,4]),
'GraphicOverrideEditorWindow': ('Редактор переопределений графики','Прокручиваемая форма графических настроек','средняя','Slider прозрачности, цвет/толщина/образцы линий и штриховок, флажки',[6.5,5.5,2,7,7,4]),
'ViewTemplateGraphicsWindow': ('Пакетное редактирование графики шаблонов видов','Список шаблонов слева, 8 вкладок справа, несколько таблиц','высокая','Трёхсостояние флажков, изменено/исходное, readonly/редактируемые значения; иерархия категорий в DataGrid, не TreeView',[8,8,10,9,8,3]),
'SabStyledToastHost': ('Хост уведомлений SAB','Стопка уведомлений, детали в Expander, ссылки и действия','средняя','Info/Success/Warning/Error, таймер исчезновения, кнопки закрытия/папки, развёрнутые детали',[4.5,6,2,9,6,3]),
'DuckReminderWindow': ('Анимированное напоминание о синхронизации','Прозрачный Canvas со спрайтом и декоративными фигурами','низкая','Кадры анимации, движение, декоративное дочернее окно',[0,3,0,5,2,5]),
'PoopDropWindow': ('Декоративный эффект напоминания','Малое прозрачное Canvas-окно','низкая','Движение/исчезновение фигур; не рабочая форма',[0,1,0,2,1,5]),
'PeekingAnimalReminderWindow': ('Анимированное напоминание выглядывающим персонажем','Прозрачный Canvas, Image, подпись','низкая','Последовательность кадров и положение окна',[0,3,0,5,2,5]),
'SyncReminderSettingsWindow': ('Настройки напоминания о синхронизации','Компактная форма интервала и вида анимации','средняя','Slider и TextBox синхронизированы, флажок включения, тестовое напоминание',[6.5,5,0,7,7,6]),
}
weights=[.35,.20,.15,.15,.10,.05]
def key(r):
    if r['name']=='SettingsWindow': return 'ParameterTools.SettingsWindow' if 'Cls_ParameterTools' in r['path'] else 'FilledRegion.SettingsWindow'
    return r['name']
def link(p,ln=None): return '['+p+']('+str(ROOT/p).replace('\\','/')+((':'+str(ln)) if ln else '')+')'
skip={'Window','UserControl','ContentPresenter','ItemsPresenter','ScrollContentPresenter','Thumb','RepeatButton','ScrollBar','Track','Path','Ellipse','Run','SymbolIcon','Popup','ComboBoxItem'}
byname={key(r):r for r in d['classes'] if r['scope']=='active'}
rows=[]
for r in d['classes']:
    if r['scope']!='active' or not r['base'].startswith('Window'): continue
    k=key(r); m=meta[k]; types=set(r['types'])
    match=next((x for x in d['xaml'] if x.get('scope')=='active' and x['path']==re.sub(r'(\.xaml)?\.cs$','.xaml',r['path'])),None)
    if match: types.update(match['all'])
    text=(ROOT/r['path']).read_text(encoding='utf-8-sig')
    if 'Cls_ParameterTools/UI/' in r['path']:
        if 'Theme.Frame' in text: types.update('Grid StackPanel Border TextBlock Button'.split())
        if 'Theme.Card' in text: types.add('GroupBox')
        if 'Theme.Button' in text: types.add('Button')
        if 'Theme.Text' in text: types.add('TextBlock')
    if 'ParameterPicker' in types: types.update(byname['ParameterPicker']['types'])
    if 'CategorySelectionControl' in types: types.update(byname['CategorySelectionControl']['types']); types.update(['TextBlock','Button'])
    meaningful=sorted(types-skip)
    dims=collections.OrderedDict()
    for prop,value in r['dimensions']:
        if prop not in dims: dims[prop]=value
    if match: dims={prop:match.get(prop[0].lower()+prop[1:],'—') for prop in ['Width','Height','MinWidth','MinHeight']}
    rows.append(dict(r,key=k,purpose=m[0],layout=m[1],complexity=m[2],states=m[3],scores=m[4],score=round(sum(a*b for a,b in zip(m[4],weights))*10,2),effective_types=meaningful,dimensions_summary=dims,xaml=match['path'] if match else None))
rows.sort(key=lambda r:(-r['score'],r['path']))
(OUT/'window-analysis.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf8')
with (OUT/'rankings.csv').open('w',encoding='utf-8-sig',newline='') as f:
    w=csv.writer(f);w.writerow(['window','module','path','interactive35','layout20','complex15','states15','demo10','standalone5','score100','type_count'])
    for r in rows:w.writerow([r['key'],r['path'].split('/')[1],r['path'],*r['scores'],r['score'],len(r['effective_types'])])
lines=['# Каталог WPF-окон SAB','', 'Дата: 11.10.2026. Commit: `'+d['commit']+'`. Состав взят из исходников и явного списка Compile/Page/Content в SAB.csproj.', '', '«Типы» — различные пользовательские контролы, контейнеры и модели колонок, объявленные в окне, его шаблонах, C# и вложенных компонентах. Повторы не увеличивают число. Служебные части общих шаблонов (Popup, Thumb, ContentPresenter и т. п.) вынесены в ресурсный реестр и не увеличивают это число. Набор объединяет условные ветви UI: все типы не обязаны одновременно присутствовать на экране. ToolTip через свойство считается возможностью, а не дополнительным типом для каждой строки.', '', '| Окно | Модуль | Типов | Сложность | Балл /100 |', '|---|---|---:|---|---:|']
for r in rows: lines.append('| '+r['key']+' | '+r['path'].split('/')[1]+' | '+str(len(r['effective_types']))+' | '+r['complexity']+' | '+str(r['score'])+' |')
for r in rows:
    dims=r['dimensions_summary']; dim=' × '.join(dims.get(k,'auto') for k in ['Width','Height']); mins=' × '.join(dims.get(k,'—') for k in ['MinWidth','MinHeight'])
    custom=', '.join(t for t in r['effective_types'] if t in {'ParameterPicker','CategorySelectionControl','AdaptiveSettingsGrid'}) or 'Отдельных вложенных классов SAB нет; используются стили/шаблоны и UI-механика окна.'
    lines+=['','## '+r['key'],'', '**Назначение:** '+r['purpose']+'.', '', '**C#:** '+link(r['path'],r['line'])+'. **XAML:** '+(link(r['xaml']) if r['xaml'] else 'нет, интерфейс строится C#')+'.', '', '**Размер:** '+dim+' DIP; минимум '+mins+' DIP. Для C# указаны первые присваивания размеров в классе; у декоративных/авторазмерных окон размеры могут вычисляться далее.', '', '**Структура:** '+r['layout']+'. **Сложность:** '+r['complexity']+'.', '', '**Компоненты SAB:** '+custom, '', '**Типы ('+str(len(r['effective_types']))+'):** '+', '.join('`'+t+'`' for t in r['effective_types'])+'.', '', '**Состояния/нестандартное оформление:** '+r['states']+'.', '', '**Оценки И/К/Д/С/В/А:** '+str(r['scores'])+'; итог '+str(r['score'])+'/100. Это экспертные оценки выбора демонстрационного окна, не результаты usability-теста.']
lines+=['','## Окна без отдельного класса','', '| Место | Назначение | Размер | Учёт |','|---|---|---|---|', '| '+link('SAB/Cls_ParameterTools/UI/SmallWindows.cs',139)+' | Toast параметров | 540 × auto; MaxHeight 420 | отдельная видимая поверхность |', '| '+link('SAB/Notifications/NotificationController.cs',297)+' | История/список уведомлений | 520 × 580 | отдельная видимая поверхность |', '| '+link('SAB/Notifications/NotificationController.cs',359)+' | Настройки уведомлений | 620 × 760 | отдельная видимая поверхность |', '| '+link('SAB/Helpers/Notifications/ToastNotifications/SabStyledToastNotifier.cs',100)+' | _dummyWindow, служебный владелец хоста | техническое окно | исключено из числа пользовательских интерфейсов |', '', 'Настройки уведомлений состоят из StackPanel, TextBlock, TextBox, Button, ToggleButton, Border/Ellipse в шаблоне; имеют длительность, переключатели уровней, readonly-путь и недоступную кнопку папки без модели. История строится динамически как ScrollViewer/StackPanel/Border с текстом/ссылками. Toast параметров — Border/StackPanel/TextBlock с таймером. Их узкая задача не делает их кандидатами на основное демонстрационное окно.', '', '## Архив и тестовые интерфейсы','', 'В SAB/Archive находятся 2 XAML-окна: PlanCornerMarkAlignmentWindow и RoomGeometryToolsWindow. Они не включены в действующий SAB.csproj и не участвуют в рейтинге. Исходники прочитаны без изменений. В тестовых/служебных проектах дополнительно найдены MainWindow прототипа ModPlusInspiredElevation, ReviewWindow (Tools/UiReview), SettingsWindow и DuckReminderWindow (SyncReminderTest). Они также исключены из рабочего набора. Резервные копии Backups, bin/obj/packages и outputs исключены как дубликаты/результаты сборки.', '']
(OUT/'window-catalogue.md').write_text('\n'.join(lines),encoding='utf8')
with (OUT/'resources.csv').open('w',encoding='utf-8-sig',newline='') as f:
    w=csv.writer(f); w.writerow(['scope','path','kind','key','target','source'])
    for r in d['xaml']:
        for x in r.get('definitions',[]): w.writerow([r.get('scope'),r['path'],x['kind'],x['key'],x['target'],x['source']])
with (OUT/'types.csv').open('w',encoding='utf-8-sig',newline='') as f:
    w=csv.writer(f); w.writerow(['type','evidence_paths'])
    for t,paths in d['type_sources'].items(): w.writerow([t,' | '.join(paths)])
compiled={n.attrib['Include'].replace('\\','/') for n in __import__('xml.etree.ElementTree',fromlist=['ElementTree']).parse(ROOT/'SAB/SAB.csproj').iter() if n.tag.endswith('}Compile') and 'Include' in n.attrib}
with (OUT/'dynamic-resources.csv').open('w',encoding='utf-8-sig',newline='') as f:
    w=csv.writer(f);w.writerow(['path','line','kind','definition'])
    for p in sorted(compiled):
        source=ROOT/'SAB'/p
        if not source.exists(): continue
        for line_number,line in enumerate(source.read_text(encoding='utf-8-sig').splitlines(),1):
            for match in re.finditer(r'new\s+(DataTemplate|ControlTemplate|ResourceDictionary)\b|<(ControlTemplate)(?=[\s>])',line):
                w.writerow(['SAB/'+p,line_number,match[1] or match[2],line.strip()])
print('Top 8:',[(r['key'],r['score'],len(r['effective_types'])) for r in rows[:8]])
print('Rows:',len(rows),'active XAML',sum(r.get('scope')=='active' for r in d['xaml']),'total source XAML',len(d['xaml']))
