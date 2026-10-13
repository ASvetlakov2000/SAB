using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace SAB.UiShowcase
{
    // Standalone adaptation of SAB.ParameterTools ParameterPicker: editable ComboBox,
    // private collection view per instance, metadata and Enter/Escape. No Revit DTO/API.
    public sealed class ParameterPicker : StackPanel
    {
        public static readonly DependencyProperty OptionsProperty=DependencyProperty.Register(nameof(Options),typeof(IEnumerable),typeof(ParameterPicker),new PropertyMetadata(null,OptionsChanged));
        public static readonly DependencyProperty SelectedParameterProperty=DependencyProperty.Register(nameof(SelectedParameter),typeof(ParameterOption),typeof(ParameterPicker),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,SelectedChanged));
        public IEnumerable Options {get=>(IEnumerable)GetValue(OptionsProperty);set=>SetValue(OptionsProperty,value);}
        public ParameterOption SelectedParameter {get=>(ParameterOption)GetValue(SelectedParameterProperty);set=>SetValue(SelectedParameterProperty,value);}
        public ComboBox Editor {get;} public TextBlock Metadata{get;}
        ListCollectionView view; TextBox editor; bool syncing;
        public ParameterPicker()
        {
            VerticalAlignment=VerticalAlignment.Center;
            Editor=new ComboBox{IsEditable=true,IsTextSearchEnabled=false,IsSynchronizedWithCurrentItem=false,StaysOpenOnEdit=true,MaxDropDownHeight=340,HorizontalAlignment=HorizontalAlignment.Stretch,DisplayMemberPath="Name"};
            AutomationProperties.SetName(Editor,"Параметр назначения · поиск по имени");
            var root=new FrameworkElementFactory(typeof(StackPanel));
            var name=new FrameworkElementFactory(typeof(TextBlock));name.SetBinding(TextBlock.TextProperty,new Binding("Name"));root.AppendChild(name);
            var detail=new FrameworkElementFactory(typeof(TextBlock));detail.SetBinding(TextBlock.TextProperty,new Binding("Summary"));detail.SetValue(TextBlock.FontSizeProperty,12.0);detail.SetResourceReference(TextBlock.ForegroundProperty,"SabBrush.TextSecondary");root.AppendChild(detail);
            root.SetBinding(FrameworkElement.ToolTipProperty,new Binding("Details"));
            var identity=new FrameworkElementFactory(typeof(TextBlock));identity.SetBinding(TextBlock.TextProperty,new Binding("Guid"){StringFormat="GUID: {0}"});identity.SetValue(TextBlock.FontSizeProperty,11.0);identity.SetResourceReference(TextBlock.ForegroundProperty,"SabBrush.TextSecondary");root.AppendChild(identity);
            // ComboBox disallows DisplayMemberPath together with ItemTemplate.
            Editor.DisplayMemberPath="";Editor.ItemTemplate=new DataTemplate{VisualTree=root};Editor.TextSearchTextPath();
            Metadata=new TextBlock{FontSize=11,Margin=new Thickness(2,3,0,0),TextTrimming=TextTrimming.CharacterEllipsis};Metadata.SetResourceReference(TextBlock.ForegroundProperty,"SabBrush.TextSecondary");
            Children.Add(Editor);Children.Add(Metadata);
            Editor.SelectionChanged+=(s,e)=>{if(!syncing&&Editor.SelectedItem is ParameterOption p)Commit(p);};
            Editor.Loaded+=(s,e)=>{
                Editor.ApplyTemplate();var next=Editor.Template.FindName("PART_EditableTextBox",Editor) as TextBox;
                if(editor!=null)editor.TextChanged-=TextChanged;editor=next;if(editor!=null)editor.TextChanged+=TextChanged;
                SyncSelection();
            };
            AddHandler(Keyboard.PreviewKeyDownEvent,new KeyEventHandler((s,e)=>{ LastKey=e.Key.ToString();
                if(e.Key==Key.Escape){view?.Refresh();Editor.IsDropDownOpen=false;SyncSelection();e.Handled=true;}
                else if(e.Key==Key.Enter&&(Editor.IsDropDownOpen||view?.Filter!=null)){var p=Editor.SelectedItem as ParameterOption;if(p==null||view?.Contains(p)!=true)p=view?.Cast<ParameterOption>().FirstOrDefault();if(p!=null)Commit(p);Editor.IsDropDownOpen=false;SyncSelection();e.Handled=true;}
            }),true);
        }
        static void OptionsChanged(DependencyObject d,DependencyPropertyChangedEventArgs e){var p=(ParameterPicker)d;var list=(e.NewValue as IEnumerable)?.Cast<object>().ToList();p.view=list==null?null:new ListCollectionView(list);p.Editor.ItemsSource=p.view;p.SyncSelection();}
        static void SelectedChanged(DependencyObject d,DependencyPropertyChangedEventArgs e)=>((ParameterPicker)d).SyncSelection();
        void Commit(ParameterOption value){SetCurrentValue(SelectedParameterProperty,value);GetBindingExpression(SelectedParameterProperty)?.UpdateSource();}
        void SyncSelection(){syncing=true;if(view!=null){view.Filter=null;view.Refresh();}Editor.SelectedItem=SelectedParameter;Editor.Text=SelectedParameter?.Name??"";Metadata.Text=SelectedParameter?.Summary??"Выберите параметр";ToolTip=SelectedParameter?.Details;syncing=false;}
        void TextChanged(object sender,TextChangedEventArgs e)
        {
            if(syncing||!Editor.IsKeyboardFocusWithin||view==null)return;
            var text=editor.Text; if(text==SelectedParameter?.Name)return;
            syncing=true;view.Filter=o=>((ParameterOption)o).Name.IndexOf(text,StringComparison.CurrentCultureIgnoreCase)>=0;view.Refresh();Editor.IsDropDownOpen=true;Editor.Text=text;editor.Text=text;editor.CaretIndex=text.Length;syncing=false;
        }
        public int VisibleOptionCount=>view?.Count??0;
        public string LastKey {get;private set;}
    }
    static class ComboExtensions { public static void TextSearchTextPath(this ComboBox c)=>TextSearch.SetTextPath(c,"Name"); }

    // Adapted from CategorySelectionControl: filtered multi-selection, count,
    // select/clear visible, Space. Chips use the shared ToggleButton template.
    public sealed class CategorySelectionControl : Grid
    {
        public static readonly DependencyProperty ItemsProperty=DependencyProperty.Register(nameof(Items),typeof(IEnumerable),typeof(CategorySelectionControl),new PropertyMetadata(null,ItemsChanged));
        public IEnumerable Items{get=>(IEnumerable)GetValue(ItemsProperty);set=>SetValue(ItemsProperty,value);}
        public TextBox Search{get;} public ListBox List{get;} public TextBlock CountLabel{get;}
        ListCollectionView view;
        public CategorySelectionControl()
        {
            RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});RowDefinitions.Add(new RowDefinition());RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            var title=new TextBlock{Text="Категории элементов",FontSize=15,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12)};Children.Add(title);
            var toolbar=new DockPanel{Margin=new Thickness(0,0,0,12)};SetRow(toolbar,1);Children.Add(toolbar);
            var actions=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Bottom};DockPanel.SetDock(actions,Dock.Right);toolbar.Children.Add(actions);
            var all=new Button{Content="Выбрать найденные"};all.Click+=(s,e)=>SetVisible(true);actions.Children.Add(all);
            var clear=new Button{Content="Снять выбор",Margin=new Thickness(0)};clear.Click+=(s,e)=>SetVisible(false);actions.Children.Add(clear);
            Search=new TextBox{MaxWidth=420,HorizontalAlignment=HorizontalAlignment.Left,MinWidth=240,Margin=new Thickness(0,0,16,0),ToolTip="Поиск по названию категории"};AutomationProperties.SetName(Search,"Поиск категорий");var searchField=new StackPanel();searchField.Children.Add(new TextBlock{Text="Поиск категорий",Margin=new Thickness(0,0,0,6)});searchField.Children.Add(Search);toolbar.Children.Add(searchField);
            List=new ListBox{SelectionMode=SelectionMode.Extended,BorderThickness=new Thickness(0),Background=System.Windows.Media.Brushes.Transparent};SetRow(List,2);Children.Add(List);
            var item=new FrameworkElementFactory(typeof(ToggleButton));item.SetResourceReference(FrameworkElement.StyleProperty,"SabUi.CategoryToggle");item.SetBinding(ContentControl.ContentProperty,new Binding("Display"));item.SetBinding(ToggleButton.IsCheckedProperty,new Binding("Selected"){Mode=BindingMode.TwoWay});item.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Stretch);
            List.ItemTemplate=new DataTemplate{VisualTree=item};
            var style=new Style(typeof(ListBoxItem));style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(0)));List.ItemContainerStyle=style;
            Search.TextChanged+=(s,e)=>{if(view!=null){view.Filter=o=>((CategoryChoice)o).Name.IndexOf(Search.Text,StringComparison.CurrentCultureIgnoreCase)>=0;view.Refresh();UpdateCount();}};
            List.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Space&&e.OriginalSource is ListBoxItem){var targets=List.SelectedItems.Cast<CategoryChoice>().ToArray();bool next=targets.Any(c=>!c.Selected);foreach(var c in targets)c.Selected=next;e.Handled=true;}};
            CountLabel=new TextBlock{FontSize=12,Margin=new Thickness(0,12,0,0)};CountLabel.SetResourceReference(TextBlock.ForegroundProperty,"SabBrush.TextSecondary");SetRow(CountLabel,3);Children.Add(CountLabel);
        }
        static void ItemsChanged(DependencyObject d,DependencyPropertyChangedEventArgs e)
        {
            var c=(CategorySelectionControl)d;
            if(e.OldValue is IEnumerable old)foreach(var x in old.Cast<CategoryChoice>())x.PropertyChanged-=c.ChoiceChanged;
            var list=(e.NewValue as IEnumerable)?.Cast<CategoryChoice>().ToList()??new System.Collections.Generic.List<CategoryChoice>();
            c.view=new ListCollectionView(list);c.List.ItemsSource=c.view;foreach(var x in list)x.PropertyChanged+=c.ChoiceChanged;c.UpdateCount();
        }
        void ChoiceChanged(object s,PropertyChangedEventArgs e)=>UpdateCount();
        void SetVisible(bool selected){if(view!=null)foreach(CategoryChoice c in view)c.Selected=selected;UpdateCount();}
        void UpdateCount(){var all=Items?.Cast<CategoryChoice>().ToArray()??new CategoryChoice[0];CountLabel.Text=$"Выбрано {all.Count(c=>c.Selected)} из {all.Length} · найдено {view?.Count??0}. Пробел — переключить; Ctrl/Shift — выделить несколько.";}
    }
}
