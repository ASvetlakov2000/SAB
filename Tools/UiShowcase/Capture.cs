using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SAB.UiShowcase
{
    public static class Capture
    {
        static readonly List<string> log=new List<string>();
        static string output;
        static void Pump(){var frame=new DispatcherFrame();var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(50)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
        static IEnumerable<T> Descendants<T>(DependencyObject o) where T:DependencyObject
        { for(int i=0;i<VisualTreeHelper.GetChildrenCount(o);i++){var c=VisualTreeHelper.GetChild(o,i);if(c is T t)yield return t;foreach(var x in Descendants<T>(c))yield return x;} }
        static void Check(bool condition,string name){log.Add((condition?"PASS ":"FAIL ")+name);if(!condition)throw new InvalidOperationException(name);}
        static void Shot(MainWindow w,string name,double dpi=96)
        {
            Pump();w.UpdateLayout();Pump();var root=w.RenderRoot;
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth*dpi/96),(int)Math.Ceiling(root.ActualHeight*dpi/96),dpi,dpi,PixelFormats.Pbgra32);bitmap.Render(root);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(output,name+".png")))encoder.Save(file);
            log.Add($"SHOT {name}.png | {root.ActualWidth:0}x{root.ActualHeight:0} DIP | render {dpi:0} dpi");
        }
        static void PopupShot(FrameworkElement root,string name)
        {
            Pump();root.UpdateLayout();Check(root.ActualWidth>0&&root.ActualHeight>0,"Popup has a rendered visual");
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth),(int)Math.Ceiling(root.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(Path.Combine(output,name+".png")))encoder.Save(f);log.Add("SHOT "+name+".png | popup visual, separate HWND");
        }
        public static void Run(MainWindow w,string[] args)
        {
            output=Path.GetFullPath(args.Contains("--out")?args[Array.IndexOf(args,"--out")+1]:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"captures"));Directory.CreateDirectory(output);
            try
            {
                w.Left=40;w.Top=40;w.ShowInTaskbar=false;w.Show();Pump();
                var actual=VisualTreeHelper.GetDpi(w);log.Add($"Native visual DPI {actual.PixelsPerInchX:0}x{actual.PixelsPerInchY:0}. 144 dpi shots are RenderTargetBitmap scaling, not an OS display-setting change.");
                string[] themes={"FluentLight","FluentCompact","MinimalSaas","Unified"};
                for(int i=0;i<themes.Length;i++){w.ApplyConcept(themes[i]);Shot(w,$"0{i}-{themes[i]}");}
                w.ApplyConcept("Unified");Pump();w.UpdateLayout();Pump();
                Check(w.Model.Rules.Count==8,"Identical reference dataset: 8 rules");
                Check((double)Application.Current.FindResource("SabUi.ControlHeight")==32,"Unified resources resolve at runtime");
                var inputs=Descendants<TextBox>(w.RenderRoot).ToList();var entity=inputs.First(x=>x.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path?.Path=="Entity");
                entity.Text="Материал — проверка привязки";Pump();Check(w.Model.Rules[0].Entity==entity.Text,"Actual TextBox edit reaches Rule through Binding");
                w.Model.Save.Execute(null);Check(w.Model.Status.Contains("в памяти"),"Save command validates and saves in memory");
                w.Model.Add.Execute(null);Check(w.Model.Rules.Count==9&&w.Model.SelectedRule==w.Model.Rules.Last(),"Add command selects new rule");w.Model.Remove.Execute(null);Check(w.Model.Rules.Count==8,"Remove command removes selected rule");w.Model.LoadRules();Pump();
                var pickers=Descendants<ParameterPicker>(w.Reference.RulesGrid).ToList();Check(pickers.Count>=2,"ParameterPicker controls instantiated in real grid");
                pickers[0].Editor.Focus();var edit=(TextBox)pickers[0].Editor.Template.FindName("PART_EditableTextBox",pickers[0].Editor);edit.Focus();edit.Text="Площадь";Pump();
                Check(pickers[0].VisibleOptionCount==1&&pickers[1].VisibleOptionCount==12,"Parameter search is local to each picker");Shot(w,"10-parameter-search");
                PopupShot((FrameworkElement)((Popup)pickers[0].Editor.Template.FindName("PART_Popup",pickers[0].Editor)).Child,"10b-parameter-popup");log.Add($"SEARCH open={pickers[0].Editor.IsDropDownOpen} text={edit.Text} selected={pickers[0].SelectedParameter} dc={pickers[0].DataContext}");
                pickers[0].Editor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(w),0,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});Pump();log.Add($"KEY {pickers[0].LastKey} param={pickers[0].SelectedParameter} model={((Rule)pickers[0].DataContext).Parameter}");Check(((Rule)pickers[0].DataContext).Parameter.Name.Contains("Площадь"),"Enter commits the filtered parameter through binding");
                edit.Text="Длина";Pump();pickers[0].Editor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(w),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});Pump();Check(pickers[0].VisibleOptionCount==12&&((Rule)pickers[0].DataContext).Parameter.Name.Contains("Площадь"),"Escape resets filter and preserves the committed parameter");w.Model.LoadRules();Pump();
                for(int i=1;i<4;i++){w.Reference.WorkspaceTabs.SelectedIndex=i;Shot(w,$"1{i}-tab-{i}");}
                var category=w.Reference.CategorySelector;category.Search.Text="Стены";Pump();Check(category.List.Items.Count==1,"Category search filters visible categories");category.Search.Text="";
                category.List.SelectedItem=w.Model.Categories[0];Pump();var categoryItem=(ListBoxItem)category.List.ItemContainerGenerator.ContainerFromIndex(0);categoryItem.Focus();bool before=w.Model.Categories[0].Selected;categoryItem.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(w),0,Key.Space){RoutedEvent=Keyboard.PreviewKeyDownEvent});Check(w.Model.Categories[0].Selected!=before,"Space toggles the selected category");w.Model.Categories[0].Selected=before;
                w.Reference.WorkspaceTabs.SelectedIndex=0;w.Model.Rules[0].Entity="";w.Model.Save.Execute(null);Pump();Check(Validation.GetHasError(Descendants<TextBox>(w.Reference.RulesGrid).First(x=>x.DataContext==w.Model.Rules[0]&&x.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path?.Path=="Entity")),"WPF validation error appears from IDataErrorInfo");Shot(w,"20-validation-error");w.Model.LoadRules();
                w.Reference.SaveButton.IsEnabled=false;Shot(w,"21-disabled");w.Reference.SaveButton.IsEnabled=true;
                w.Model.Rules.Clear();Shot(w,"22-empty");Check(w.Reference.EmptyState.Visibility==Visibility.Visible,"Empty table offers an add action");w.Model.LoadRules();Pump();w.UpdateLayout();Pump();
                entity=Descendants<TextBox>(w.Reference.RulesGrid).First(x=>x.DataContext==w.Model.Rules[0]&&x.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path?.Path=="Entity");entity.Focus();Keyboard.Focus(entity);Shot(w,"23-focus");Check(entity.IsKeyboardFocused,"Keyboard focus reaches the editable entity field");
                w.Width=980;w.Height=680;w.Model.Rules[0].Entity="Длинное русское название сущности для заполнения параметров материалов архитектурного раздела";w.Model.Rules[0].Parameter=new ParameterOption{Name="SAB_Материал_ОченьДлинноеНаименованиеПараметраДляПроверкиПрокрутки",DataType="Текст",Identity="Общий · экземпляр",Guid="00000000-0000-4000-8000-000000000099"};Shot(w,"24-minimum-long-text");Shot(w,"25-minimum-144dpi",144);
                Check(w.Reference.SaveButton.ActualWidth>100&&w.Reference.SaveButton.IsVisible,"Primary action remains visible at minimum size");
                w.Width=1200;w.Height=840;w.Model.LoadRules();
                for(int p=1;p<=3;p++){w.ShowPage(p);Shot(w,$"3{p}-gallery-{p}");Shot(w,$"4{p}-gallery-{p}-144dpi",144);}
                var patterns=Descendants<ComboBox>(w.PageHost).First(c=>c.Items.Count==7);for(int i=1;i<7;i++){patterns.SelectedIndex=i;Pump();if(i==6){Descendants<ScrollViewer>(w.PageHost).First(v=>v.Content is StackPanel stack&&stack.Children.OfType<Expander>().Any()).ScrollToBottom();log.Add("DockablePane capture: inner content scrolled to bottom; footer remains fixed.");}Shot(w,$"5{i}-pattern-{i}");}
                w.ShowPage(1);Pump();Descendants<ScrollViewer>(w.PageHost).First().ScrollToBottom();Shot(w,"60-components-bottom");
                w.ShowPage(2);Pump();var stateScroll=Descendants<ScrollViewer>(w.PageHost).First();stateScroll.ScrollToBottom();Shot(w,"61-states-bottom");var start=Descendants<Button>(w.PageHost).First(b=>Equals(b.Content,"Запустить тестовую проверку"));start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Shot(w,"62-loading");Check(!start.IsEnabled&&Descendants<ProgressBar>(w.PageHost).First().Value>0,"Loading state reports progress and disables repeat action");
                var notify=Descendants<Button>(w.PageHost).First(b=>Equals(b.Content,"Показать уведомление"));notify.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Shot(w,"63-toast");
                w.ShowPage(0);for(int i=0;i<1000;i++)w.Model.Rules.Add(new Rule{Entity="Правило "+i,Parameter=w.Model.Parameters[0],Source=w.Model.Sources[0]});Pump();w.UpdateLayout();Check(Descendants<DataGridRow>(w.Reference.RulesGrid).Count()<50,"DataGrid virtualizes 1008 rows");w.Model.LoadRules();Pump();
                w.ShowPage(0);Shot(w,"04-Unified-144dpi",144);
                var bindingErrors=new StringBuilder();
                log.Add("No Autodesk/Revit references or API calls. No production Window/ViewModel compiled into preview.");
                log.Add("Hover/pressed are interactive states in the templates; capture suite does not claim native mouse screenshots. OS multi-monitor DPI transition requires manual check.");
                log.Add("COMPLETED");
            }
            finally{File.WriteAllLines(Path.Combine(output,"verification.txt"),log,Encoding.UTF8);w.Close();}
        }
    }
}
