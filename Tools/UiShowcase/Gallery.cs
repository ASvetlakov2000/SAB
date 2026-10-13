using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SAB.UiShowcase
{
    public static class Gallery
    {
        public static UIElement Build(int page,DemoModel model)
        {
            var root=new StackPanel{Margin=new Thickness(24)};
            root.Children.Add(Text(page==1?"Компоненты SAB":page==2?"Состояния и обратная связь":"Схемы компоновки",23,true));
            root.Children.Add(Hint(page==1?"Реальные WPF-контролы. Основа — существующие шаблоны SAB, дополненные общими токенами.":page==2?"Состояния проверяются на настоящих контролах. Hover и нажатие доступны при наведении и удержании мыши.":"Выбор схемы зависит от задачи. У каждого примера своя структура и зона основного действия."));
            if(page==1)Components(root,model);else if(page==2)States(root,model);else Patterns(root,model);
            return new ScrollViewer{Content=root,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        }
        static TextBlock Text(string text,double size=13,bool strong=false)=>new TextBlock{Text=text,FontSize=size,FontWeight=strong?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};
        static TextBlock Hint(string text){var t=Text(text,12);t.SetResourceReference(TextBlock.ForegroundProperty,"SabBrush.TextSecondary");return t;}
        static StackPanel Row(params UIElement[] children){var p=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,14)};foreach(var c in children)p.Children.Add(c);return p;}
        static Button Button(string text,string style=null){var b=new Button{Content=text};if(style!=null)b.SetResourceReference(FrameworkElement.StyleProperty,style);return b;}
        static void Section(Panel p,string name){p.Children.Add(new Border{Height=1,Background=(Brush)Application.Current.FindResource("SabBrush.BorderWeak"),Margin=new Thickness(0,12,0,18)});p.Children.Add(Text(name,15,true));}
        static void Components(Panel p,DemoModel m)
        {
            Section(p,"Действия и ввод");
            var primary=Button("Основное действие","SabPrimaryButtonStyle");primary.Margin=new Thickness(0,0,12,0);
            var normal=Button("Второстепенное");var danger=Button("Удалить","SabDangerOutlineButtonStyle");
            p.Children.Add(Row(primary,normal,danger,new ToggleButton{Content="Выбрано",IsChecked=true,Style=(Style)Application.Current.FindResource("SabUi.CategoryToggle")}));
            var form=new Grid{Margin=new Thickness(0,0,0,16)};form.ColumnDefinitions.Add(new ColumnDefinition());form.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(24)});form.ColumnDefinitions.Add(new ColumnDefinition());
            var left=new StackPanel();left.Children.Add(Text("Название набора"));left.Children.Add(new TextBox{Text="Материалы · архитектурный раздел"});form.Children.Add(left);
            var right=new StackPanel();Grid.SetColumn(right,2);right.Children.Add(Text("Параметр с поиском и метаданными"));right.Children.Add(new ParameterPicker{Options=m.Parameters,SelectedParameter=m.Parameters[0]});form.Children.Add(right);p.Children.Add(form);
            p.Children.Add(Row(new CheckBox{Content="Включено",IsChecked=true,Margin=new Thickness(0,0,24,0)},new CheckBox{Content="Смешанное",IsThreeState=true,IsChecked=null,Margin=new Thickness(0,0,24,0)},new RadioButton{Content="Экземпляр",IsChecked=true,GroupName="scope",Margin=new Thickness(0,0,24,0)},new RadioButton{Content="Тип",GroupName="scope"}));
            p.Children.Add(Row(new Label{Content="Масштаб предпросмотра"},new Slider{Minimum=50,Maximum=200,Value=100,Width=240,VerticalAlignment=VerticalAlignment.Center,TickFrequency=25,IsSnapToTickEnabled=true}));
            Section(p,"Таблицы, списки и раскрытие");
            var grid=new DataGrid{ItemsSource=m.Rules,AutoGenerateColumns=false,CanUserAddRows=false,Height=194,RowHeight=38};grid.Columns.Add(new DataGridTextColumn{Header="Сущность",Binding=new Binding("Entity"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});grid.Columns.Add(new DataGridCheckBoxColumn{Header="Включено",Binding=new Binding("Enabled"),Width=100});var col=new DataGridTemplateColumn{Header="Источник",Width=new DataGridLength(1,DataGridLengthUnitType.Star)};var combo=new FrameworkElementFactory(typeof(ComboBox));combo.SetValue(ItemsControl.ItemsSourceProperty,m.Sources);combo.SetBinding(Selector.SelectedItemProperty,new Binding("Source"){Mode=BindingMode.TwoWay});col.CellTemplate=new DataTemplate{VisualTree=combo};grid.Columns.Add(col);p.Children.Add(grid);
            var expander=new Expander{Header="Дополнительные компоненты · ListView, RichTextBox, Hyperlink",Margin=new Thickness(0,18,0,0),IsExpanded=true};
            var more=new Grid();more.ColumnDefinitions.Add(new ColumnDefinition());more.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(24)});more.ColumnDefinitions.Add(new ColumnDefinition());
            var list=new ListView{Height=160,ItemsSource=m.Levels};var view=new GridView();view.Columns.Add(new GridViewColumn{Header="Уровень",DisplayMemberBinding=new Binding("Name"),Width=190});view.Columns.Add(new GridViewColumn{Header="Отметка, м",DisplayMemberBinding=new Binding("Elevation"),Width=110});list.View=view;more.Children.Add(list);
            var doc=new FlowDocument();var paragraph=new Paragraph(new Run("Результат проверки\n"));paragraph.Inlines.Add(new Bold(new Run("8 правил готовы к сохранению.")));paragraph.Inlines.Add(new LineBreak());var link=new Hyperlink(new Run("Открыть вкладку состояний"));link.Click+=(s,e)=>((MainWindow)Application.Current.MainWindow)?.ShowPage(2);paragraph.Inlines.Add(link);doc.Blocks.Add(paragraph);var rich=new RichTextBox{Document=doc,Height=160,IsReadOnly=true,BorderBrush=(Brush)Application.Current.FindResource("SabBrush.Border"),BorderThickness=new Thickness(1)};Grid.SetColumn(rich,2);more.Children.Add(rich);expander.Content=more;p.Children.Add(expander);
            Section(p,"Шаблоны и составные элементы");p.Children.Add(Hint("ParameterPicker и CategorySelectionControl адаптированы из SAB. GroupBox, Popup, ToolTip, ScrollBar, ContentControl и DataTemplate используются в этом стенде. Grid, DockPanel, StackPanel и WrapPanel — контейнеры, а не самостоятельные поля ввода. AdaptiveSettingsGrid и тосты показаны как отдельные образцы поведения; код производственных классов не заменён."));
            var group=new GroupBox{Header="Логическая группа",Content=new CheckBox{Content="Использовать только выбранные элементы",IsChecked=true,Margin=new Thickness(12)}};p.Children.Add(group);
            Section(p,"AdaptiveSettingsGrid, иконки и повторяющиеся элементы");
            var adaptive=new SAB.UI.AdaptiveSettingsGrid{Margin=new Thickness(0,0,0,16)};adaptive.ColumnDefinitions.Add(new ColumnDefinition());adaptive.ColumnDefinitions.Add(new ColumnDefinition());var a=Form("Основные параметры",m);a.Margin=new Thickness(0,0,20,0);adaptive.Children.Add(a);var b=Form("Дополнительные параметры",m);Grid.SetColumn(b,1);adaptive.Children.Add(b);p.Children.Add(adaptive);
            p.Children.Add(Hint("AdaptiveSettingsGrid подключён из рабочего SAB без изменений: перестраивается при ширине окна меньше 780 DIP. Ограничение эталонного окна 980 DIP сохраняется; адаптацию узких панелей проверять в целевом окне."));
            var icon=new Wpf.Ui.Controls.SymbolIcon{Symbol=Wpf.Ui.Controls.SymbolRegular.ChevronDown12,Width=24,Height=24};
            var canvas=new Canvas{Width=28,Height=24};canvas.Children.Add(new Rectangle{Width=18,Height=14,Stroke=(Brush)Application.Current.FindResource("SabBrush.Accent"),StrokeThickness=1});Canvas.SetLeft(canvas.Children[0],4);Canvas.SetTop(canvas.Children[0],5);
            var image=new Image{Width=24,Height=24,Source=new DrawingImage(new GeometryDrawing(Brushes.Transparent,new Pen((Brush)Application.Current.FindResource("SabBrush.Accent"),1),new RectangleGeometry(new Rect(3,3,16,16),2,2)))};
            var items=new ItemsControl{ItemsSource=new[]{"Текст","Длина","Площадь"},Margin=new Thickness(16,0,0,0)};var wrap=new FrameworkElementFactory(typeof(WrapPanel));items.ItemsPanel=new ItemsPanelTemplate(wrap);var item=new FrameworkElementFactory(typeof(TextBlock));item.SetBinding(TextBlock.TextProperty,new Binding());item.SetValue(FrameworkElement.MarginProperty,new Thickness(0,0,16,0));items.ItemTemplate=new DataTemplate{VisualTree=item};p.Children.Add(Row(icon,image,canvas,items));
        }
        static Border Message(string title,string body,string color,string background)
        {
            var stack=new StackPanel();var heading=Text(title,13,true);heading.SetResourceReference(TextBlock.ForegroundProperty,color);stack.Children.Add(heading);stack.Children.Add(Text(body));var b=new Border{Child=stack,Padding=new Thickness(12,10,12,2),Margin=new Thickness(0,0,0,12),CornerRadius=new CornerRadius(5)};b.SetResourceReference(Border.BackgroundProperty,background);return b;
        }
        static void States(Panel p,DemoModel m)
        {
            Section(p,"Доступность и клавиатура");
            var focus=Button("Перевести фокус в поле");var field=new TextBox{Text="Фокус: одна синяя рамка",Width=300};focus.Click+=(s,e)=>{field.Focus();System.Windows.Input.Keyboard.Focus(field);};
            var disabled=Button("Недоступно");disabled.IsEnabled=false;p.Children.Add(Row(focus,field,disabled));
            p.Children.Add(Row(new CheckBox{Content="Недоступный флажок",IsEnabled=false,IsChecked=true,Margin=new Thickness(0,0,24,0)},new ComboBox{ItemsSource=m.Sources,SelectedIndex=0,Width=250,IsEnabled=false}));
            Section(p,"Ошибка поля и результат операции");
            var invalid=new Rule{Entity="",Parameter=m.Parameters[0]};var input=new TextBox{Width=420,HorizontalAlignment=HorizontalAlignment.Left};input.SetBinding(TextBox.TextProperty,new Binding("Entity"){Source=invalid,Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged,ValidatesOnDataErrors=true});p.Children.Add(input);var err=Text("Введите название сущности.",12);err.SetResourceReference(TextBlock.ForegroundProperty,"SabBrush.Error");p.Children.Add(err);
            p.Children.Add(Message("Не удалось применить правила","У двух правил не выбран параметр. Выберите параметр назначения и повторите проверку.","SabBrush.Error","SabUi.ErrorBackground"));
            p.Children.Add(Message("Найдены одинаковые назначения","Два правила заполняют один параметр. Проверьте порядок применения.","SabBrush.Warning","SabUi.WarningBackground"));
            p.Children.Add(Message("Проверка завершена","8 правил готовы к работе. В стенде изменения остаются в памяти.","SabBrush.Success","SabUi.SuccessBackground"));
            Section(p,"Загрузка, пустой список и уведомление");
            var progress=new ProgressBar{Minimum=0,Maximum=100,Value=0,Height=6,Margin=new Thickness(0,0,0,10)};progress.SetResourceReference(Control.ForegroundProperty,"SabBrush.Accent");p.Children.Add(progress);
            var start=Button("Запустить тестовую проверку");var result=Text("Ожидание запуска",12);var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(80)};timer.Tick+=(s,e)=>{progress.Value+=5;if(progress.Value>=100){timer.Stop();start.IsEnabled=true;result.Text="Готово · 8 правил проверено";}else result.Text=$"Проверка правил · {progress.Value:0}%";};start.Click+=(s,e)=>{progress.Value=0;start.IsEnabled=false;result.Text="Подготовка…";timer.Start();};start.Unloaded+=(s,e)=>timer.Stop();p.Children.Add(Row(start,result));
            var empty=new Border{Padding=new Thickness(24),BorderThickness=new Thickness(1),Margin=new Thickness(0,0,0,14)};empty.SetResourceReference(Border.BorderBrushProperty,"SabBrush.BorderWeak");var ep=new StackPanel();ep.Children.Add(Text("Ничего не найдено",16,true));ep.Children.Add(Hint("Измените строку поиска или снимите фильтр."));empty.Child=ep;p.Children.Add(empty);
            var notify=Button("Показать уведомление");var toast=Message("Настройки сохранены","Демонстрационное уведомление; исчезнет через 4 секунды.","SabBrush.Success","SabUi.SuccessBackground");toast.Visibility=Visibility.Collapsed;var hide=new DispatcherTimer{Interval=TimeSpan.FromSeconds(4)};hide.Tick+=(s,e)=>{toast.Visibility=Visibility.Collapsed;hide.Stop();};notify.Click+=(s,e)=>{toast.Visibility=Visibility.Visible;hide.Stop();hide.Start();};notify.Unloaded+=(s,e)=>hide.Stop();p.Children.Add(notify);p.Children.Add(toast);
        }
        static void Patterns(Panel p,DemoModel m)
        {
            var names=new[]{"Compact Dialog","Settings Form","Data Manager","Master–Detail","Tabbed Workspace","Results & Preview","DockablePane Layout"};
            var captions=new[]{"Одно решение: краткий вопрос, одно поле, основное действие справа.","Последовательная настройка: подписи над полями, связанные параметры вместе.","Много однотипных строк: поиск и действия сверху, виртуализированная таблица в центре.","Коллекция объектов и подробности выбранного: список слева, редактор справа.","Несколько самостоятельных наборов настроек: вкладки и общий подвал.","Параметры и проверяемый результат рядом. Предпросмотр не заменяет таблицу результата.","Узкая закрепляемая панель: вертикальный поток, раскрываемые секции, действие снизу."};
            var select=new ComboBox{ItemsSource=names,SelectedIndex=0,Width=280,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,12,0,12)};p.Children.Add(select);var description=Hint(captions[0]);p.Children.Add(description);
            var host=new Border{Height=390,BorderThickness=new Thickness(1),Padding=new Thickness(20),Background=Brushes.White};host.SetResourceReference(Border.BorderBrushProperty,"SabBrush.Border");p.Children.Add(host);
            Action render=()=>{int i=select.SelectedIndex;description.Text=captions[i];host.Child=Pattern(i,m);};select.SelectionChanged+=(s,e)=>render();render();
            p.Children.Add(Hint("Это композиционные примеры на тестовых данных. DockablePane показан как макет содержимого; регистрация панели и контекст Revit здесь отсутствуют."));
        }
        static UIElement Pattern(int index,DemoModel m)
        {
            if(index==2){var g=new Grid();g.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});g.RowDefinitions.Add(new RowDefinition());g.Children.Add(Row(new TextBox{Text="Найти сущность…",Width=260},Button("Добавить"),Button("Удалить")));var d=new DataGrid{ItemsSource=m.Rules,AutoGenerateColumns=false,CanUserAddRows=false,RowHeight=38};d.Columns.Add(new DataGridTextColumn{Header="Сущность",Binding=new Binding("Entity"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});d.Columns.Add(new DataGridTextColumn{Header="Источник",Binding=new Binding("Source"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});Grid.SetRow(d,1);g.Children.Add(d);return g;}
            if(index==3||index==5){var g=new Grid();g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(index==3?0.8:1,GridUnitType.Star)});g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(20)});g.ColumnDefinitions.Add(new ColumnDefinition());UIElement left=index==3?(UIElement)new ListBox{ItemsSource=m.Rules,DisplayMemberPath="Entity",SelectedIndex=0}:new ScrollViewer{Content=Form("Параметры результата",m),VerticalScrollBarVisibility=ScrollBarVisibility.Auto};g.Children.Add(left);UIElement right=index==3?(UIElement)Form("Выбранное правило",m):WithFooter(new Border{Background=(Brush)Application.Current.FindResource("SabBrush.TableHeaderBackground"),Padding=new Thickness(20),Child=Text("Предпросмотр результата\n\nМатериал — Бетон B25\nПараметр — SAB_Материал_Имя\nЗначение — Бетон B25",15)},"Применить результат");Grid.SetColumn(right,2);g.Children.Add(right);return g;}
            if(index==4){var tabs=new TabControl();tabs.Items.Add(new TabItem{Header="Основные",Content=Form("Настройки",m)});tabs.Items.Add(new TabItem{Header="Дополнительные",Content=new CheckBox{Content="Заполнять только пустые значения"}});return WithFooter(tabs,"Сохранить настройки");}
            if(index==6){var panel=new DockPanel{MaxWidth=360,HorizontalAlignment=HorizontalAlignment.Left};var action=Button("Применить","SabPrimaryButtonStyle");action.Margin=new Thickness(0,12,0,0);DockPanel.SetDock(action,Dock.Bottom);panel.Children.Add(action);var content=new StackPanel();content.Children.Add(Text("Параметры элементов",17,true));content.Children.Add(new Expander{Header="Источник",IsExpanded=true,Content=Form("",m)});content.Children.Add(new Expander{Header="Область применения",Content=new CheckBox{Content="Только выбранные элементы"}});panel.Children.Add(new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,8,0)});return panel;}
            if(index==0){var panel=new StackPanel{MaxWidth=440,VerticalAlignment=VerticalAlignment.Center};panel.Children.Add(Text("Сохранить набор правил?",18,true));panel.Children.Add(Hint("Введите имя, чтобы найти этот набор при следующем запуске."));panel.Children.Add(new TextBox{Text="Материалы · АР",Margin=new Thickness(0,0,0,20)});var actions=Row(Button("Отмена"),Button("Сохранить","SabPrimaryButtonStyle"));actions.HorizontalAlignment=HorizontalAlignment.Right;panel.Children.Add(actions);return panel;}
            return WithFooter(Form("Настройки заполнения",m),"Сохранить настройки");
        }
        static UIElement WithFooter(UIElement content,string actionText)
        {
            var g=new Grid();g.RowDefinitions.Add(new RowDefinition());g.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            g.Children.Add(new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            var footer=Row(Button("Отмена"),Button(actionText,"SabPrimaryButtonStyle"));footer.HorizontalAlignment=HorizontalAlignment.Right;footer.Margin=new Thickness(0,12,0,0);Grid.SetRow(footer,1);g.Children.Add(footer);return g;
        }
        static StackPanel Form(string title,DemoModel m){var p=new StackPanel{MaxWidth=480,HorizontalAlignment=HorizontalAlignment.Stretch};if(title!="")p.Children.Add(Text(title,16,true));p.Children.Add(Text("Источник значения"));p.Children.Add(new ComboBox{ItemsSource=m.Sources,SelectedIndex=0,Margin=new Thickness(0,0,0,16)});p.Children.Add(Text("Параметр назначения"));p.Children.Add(new ParameterPicker{Options=m.Parameters,SelectedParameter=m.Parameters[0]});p.Children.Add(new CheckBox{Content="Заполнять только пустые значения",IsChecked=true,Margin=new Thickness(0,18,0,18)});return p;}
    }
}
