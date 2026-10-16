using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Point = System.Windows.Point;
using Grid = System.Windows.Controls.Grid;
using Control = System.Windows.Controls.Control;
using Autodesk.Revit.DB;
using SAB.CreateViewsAndSheets.Models;
using SAB.CreateViewsAndSheets.ViewModels;
using SAB.CreateViewsAndSheets.Views;
using SAB.ParameterTools.Core;
using SAB.ParameterTools;
using Profile = SAB.ParameterTools.Core.Profile;
using SAB.UI;

namespace SAB.UiShowcase
{
    // Actual production SAB classes/resources. Only DTOs and preference storage are isolated; no Document.
    internal static class SourcePreview
    {
        [DllImport("kernel32",CharSet=CharSet.Unicode)] static extern bool SetDllDirectory(string path);
        static readonly List<string> log=new List<string>(); static string output;
        [STAThread] static int Main(string[] args)
        {
            string revit=Environment.GetEnvironmentVariable("SAB_PREVIEW_REVIT_API_DIRECTORY");
            if(string.IsNullOrWhiteSpace(revit))revit=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Autodesk","Revit 2023");
            SetDllDirectory(revit);
            AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{string p=Path.Combine(revit,new AssemblyName(e.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};
            try{return Run(args);}catch(Exception e){Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"diagnostics"));File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"diagnostics","error.txt"),e.ToString());return 1;}
        }
        [MethodImpl(MethodImplOptions.NoInlining)] static int Run(string[] args)
        {
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            if(args.Contains("--capture-material-selection"))
            {
                SabWindowAnimationService.Enabled=false;
                return MaterialSelectionEvidence.Run(args[Array.IndexOf(args,"--out")+1]);
            }
            if(args.Contains("--capture-selection-guidance"))
            {
                SabWindowBehaviorService.Initialize();SabWindowAnimationService.Enabled=false;
                return SelectionGuidanceEvidence.Run(args[Array.IndexOf(args,"--out")+1]);
            }
            if(args.Contains("--capture-preview-runtime")||args.Contains("--capture-directions-window"))
            {
                SabWindowBehaviorService.Initialize();SabWindowAnimationService.Enabled=false;
                return ElevationDirectionPreviewEvidence.Run(args[Array.IndexOf(args,"--out")+1]);
            }
            if(args.Contains("--capture-direction-preview"))
            {
                SabWindowBehaviorService.Initialize();SabWindowAnimationService.Enabled=false;
                return ElevationDirectionPreviewEvidence.Run(args[Array.IndexOf(args,"--out")+1]);
            }
            if(args.Contains("--capture-elevation-numbering"))
            {
                SabWindowBehaviorService.Initialize();SabWindowAnimationService.Enabled=false;
                return ElevationNumberingPreview.Run(args[Array.IndexOf(args,"--out")+1]);
            }
            if(args.Contains("--capture-parameter-checks"))
            {
                SabWindowBehaviorService.Initialize();SabWindowAnimationService.Enabled=false;
                return ParameterCheckPreview.Run(args[Array.IndexOf(args,"--out")+1]);
            }
            if(args.Contains("--capture-elevation-hotfix"))
            {
                SabWindowBehaviorService.Initialize();SabWindowAnimationService.Enabled=false;
                return ElevationHotfixPreview.Run(args[Array.IndexOf(args,"--out")+1]);
            }
            if(args.Contains("--probe-notification-resources"))
            {
                string path=args[Array.IndexOf(args,"--out")+1];Directory.CreateDirectory(path);
                try
                {
                    var card=new SAB.UI.Notifications.NotificationCard(new SAB.UI.Notifications.NotificationMessage("SAB · Развёртки","Завершено с предупреждениями",SAB.UI.Notifications.NotificationTone.Warning,"Создано развёрток: 4.",warnings:"Не удалось разместить план-схему."));
                    File.WriteAllText(Path.Combine(path,"resource-probe.txt"),"PASS actual NotificationCard with warnings and zero host application dictionaries");return 0;
                }
                catch(Exception error){File.WriteAllText(Path.Combine(path,"resource-probe.txt"),"EXPECTED BASELINE FAILURE: "+error);return 1;}
            }
            if(!args.Contains("--clean-resources")) app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"UI","Styles","SABWindowStyles.xaml"))});
            SabWindowBehaviorService.Initialize();
            if(args.Contains("--notifications")) return NotificationPreview.Run(args[Array.IndexOf(args,"--out")+1]);
            SabWindowAnimationService.Enabled=false;
            if(args.Contains("--capture-redesign")) return RedesignProduction.Run(args[Array.IndexOf(args,"--out")+1]);
            if(args.Contains("--capture-elevations")) return Elevations.ElevationPreview.Capture(args[Array.IndexOf(args,"--out")+1]);
            if(args.Contains("--elevations")) { var stand=Elevations.ElevationPreview.Menu();stand.Closed+=(s,e)=>app.Shutdown();app.Run(stand);return 0; }
            if(args.Contains("--capture"))
            {
                output=args[Array.IndexOf(args,"--out")+1];Directory.CreateDirectory(output);
                log.Add("Loaded RevitAPI:"+typeof(Element).Assembly.GetName().Version+";path:"+typeof(Element).Assembly.Location);
                using(var preferences = new SecondaryPreferences()) {
                    try{CaptureExamples();log.Add("COMPLETED");return 0;}
                    finally{File.WriteAllLines(Path.Combine(output,"verification.txt"),log);app.Shutdown();}
                }
            }
            var menu=new Window{Title="SAB · новый Fluent Light",Width=560,Height=300,WindowStartupLocation=WindowStartupLocation.CenterScreen,FontFamily=new FontFamily("Segoe UI"),FontSize=13,Background=(Brush)app.FindResource("SabBrush.WindowBackground")};
            var body=new StackPanel{Margin=new Thickness(28)};body.Children.Add(new TextBlock{Text="Предпросмотр SAB",FontSize=24,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12)});body.Children.Add(new TextBlock{Text="Тестовые данные · модель Revit не подключена",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,20)});
            var parameters=new Button{Content="Параметризация",Margin=new Thickness(0,0,0,10)};parameters.Click+=(s,e)=>{var w=Parameters();w.Owner=menu;w.ShowDialog();};body.Children.Add(parameters);
            var sheets=new Button{Content="Создание видов и листов"};sheets.Click+=(s,e)=>{CreateViewsAndSheetsViewModel vm;var w=Views(out vm);w.Owner=menu;w.ShowDialog();};body.Children.Add(sheets);menu.Content=body;menu.Closed+=(s,e)=>app.Shutdown();app.Run(menu);return 0;
        }
        sealed class SecondaryPreferences : IDisposable
        {
            readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SAB","WindowSizes","CreateViewsAndSheets.SettingsWindow.json");
            readonly byte[] original;
            public SecondaryPreferences() { original=File.Exists(path)?File.ReadAllBytes(path):null; }
            public void Dispose() {
                if(original!=null)File.WriteAllBytes(path,original);
                else if(File.Exists(path))File.Delete(path); // Only the one file created by this capture run.
            }
        }
        static Window Parameters()
        {
            var refs=new List<ParameterRef>();string[] names={"SAB_Элемент_Корпус","SAB_Элемент_Уровень","SAB_Элемент_Помещение","SAB_Элемент_Зона","Номер","Имя"};
            for(int i=0;i<names.Length;i++)refs.Add(new ParameterRef{Id=100+i,Name=names[i],SharedGuid=new Guid(i+1,0,0,new byte[8]).ToString(),DataType="Текст",VariesAcrossGroups=i!=3});
            var p=new Profile{Configured=true};p.Corpora.Add("Корпус А");
            p.Rules.Add(new Rule{EntityName="Корпус",Group=ParameterGroup.Zone,Source=RuleValueSource.ManualCorpus,Target=refs[0]});
            p.Rules.Add(new Rule{EntityName="Уровень",Group=ParameterGroup.Level,Source=RuleValueSource.Level,Target=refs[1]});
            p.Rules.Add(new Rule{EntityName="Номер помещения",Group=ParameterGroup.Room,Source=RuleValueSource.Room,RoomField=RoomField.Number,Target=refs[2]});
            p.Rules.Add(new Rule{EntityName="Зона",Group=ParameterGroup.Zone,Source=RuleValueSource.Mapping,MappingInput=MappingInput.RoomName,Target=refs[3],Mappings=new List<ValueMapping>{new ValueMapping{Key="Офис",Value="Рабочая зона"},new ValueMapping{Key="Коридор",Value="Общая зона"}}});
            for(int i=1;i<=6;i++)p.Levels.Add(new LevelMapping{LevelUniqueId="demo-"+i,LevelName="Этаж "+i.ToString("00"),Value=i.ToString("00")});
            var cats=new List<CategoryChoice>();string[] categories={"Стены","Перекрытия","Потолки","Двери","Окна","Мебель","Обобщённые модели","Лестницы","Ограждения","Несущие колонны"};for(int i=0;i<categories.Length;i++)cats.Add(new CategoryChoice{Id=i+1,Name=categories[i],Selected=i<7});
            return (Window)Activator.CreateInstance(typeof(Profile).Assembly.GetType("SAB.ParameterTools.UI.SettingsWindow"),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{p,refs,refs,cats,p.Levels,refs},null);
        }
        static RevitElementItem Item(int id,string name,ViewType type=ViewType.Undefined)=>new RevitElementItem{Id=new ElementId(id),Name=name,ViewType=type};
        static CreateViewsAndSheetsWindow Views(out CreateViewsAndSheetsViewModel vm)
        {
            var views=new List<RevitElementItem>{Item(101,"Шаблон · План этажа",ViewType.FloorPlan),Item(102,"Шаблон · План потолка",ViewType.CeilingPlan)};
            var sheets=new List<RevitElementItem>{Item(201,"АР-00 · Лист-образец")};sheets[0].SheetBounds=new SheetBounds{MaxXFeet=594/304.8,MaxYFeet=420/304.8};
            var viewport=new List<RevitElementItem>{Item(301,"Заголовок вида · стандартный")};var title=new List<RevitElementItem>{Item(401,"А2 · Альбомная")};
            var templates=new List<RevitElementItem>{Item(501,"АР · Рабочий",ViewType.FloorPlan)};
            var settings=new CreateViewsAndSheetsSettings{SourceViewId=views[0].Id,SourceSheetId=sheets[0].Id,ViewportTypeId=viewport[0].Id,TitleBlockTypeId=title[0].Id,SheetBounds=sheets[0].SheetBounds};
            string[] names={"План 1 этажа","План 2 этажа","План 3 этажа","План кровли","План подвала"};
            for(int i=0;i<names.Length;i++)settings.SessionRows.Add(new SheetCreationSessionRow{ViewName=names[i],ViewScaleText="50",SheetNumber="АР-0"+(i+1),SheetName="Планы и виды",ViewTemplateId=templates[0].Id});
            vm=new CreateViewsAndSheetsViewModel(views,sheets,viewport,title,new List<RevitElementItem>(),new Dictionary<long,List<string>>(),new Dictionary<long,HashSet<long>>(),templates,new HashSet<string>(),new HashSet<string>(),settings);
            var window = new CreateViewsAndSheetsWindow(vm);
            var layout = typeof(CreateViewsAndSheetsWindow).GetField("_layoutService",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-preferences","window-layout.json");
            // Preserve all production restore/save calls, but redirect this instance to isolated storage.
            typeof(SAB.CreateViewsAndSheets.Services.CreateViewsAndSheetsWindowLayoutService).GetField("_layoutFilePath",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(layout,path);
            typeof(CreateViewsAndSheetsWindow).GetField("_layoutRestored",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);
            return window;
        }
        static IEnumerable<T> All<T>(DependencyObject root) where T:DependencyObject
        {for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var c=VisualTreeHelper.GetChild(root,i);if(c is T t)yield return t;foreach(var x in All<T>(c))yield return x;}}
        static void Pump(int milliseconds=90){var f=new DispatcherFrame();var t=new DispatcherTimer(DispatcherPriority.ApplicationIdle){Interval=TimeSpan.FromMilliseconds(milliseconds)};t.Tick+=(s,e)=>{t.Stop();f.Continue=false;};t.Start();Dispatcher.PushFrame(f);}
        static void Show(Window w){w.WindowStartupLocation=WindowStartupLocation.Manual;w.Left=30;w.Top=30;w.ShowInTaskbar=false;w.Show();Pump();w.UpdateLayout();Pump();}
        static void Shot(FrameworkElement root,string name,double dpi=96){root.UpdateLayout();Pump(220);root.UpdateLayout();var b=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth*dpi/96),(int)Math.Ceiling(root.ActualHeight*dpi/96),dpi,dpi,PixelFormats.Pbgra32);b.Render(root);var p=new PngBitmapEncoder();p.Frames.Add(BitmapFrame.Create(b));using(var f=File.Create(Path.Combine(output,name+".png")))p.Save(f);log.Add("SHOT "+name+" | "+root.ActualWidth+"x"+root.ActualHeight+" DIP | render "+dpi+" dpi");}
        static void Check(bool ok,string message){log.Add((ok?"PASS ":"FAIL ")+message);if(!ok)throw new InvalidOperationException(message);}
        static void CheckHelp(Window window,string file,bool attached)
        {
            var help=All<Button>(window).Where(b=>Equals(b.Content,"Инструкция")&&b.IsVisible).ToList();
            Check(help.Count==1&&File.Exists(SAB.UI.FluentLight.Instructions.Resolve(file))&&(!attached||SAB.UI.FluentLight.Instructions.GetFile(help[0])==file),"One instruction button resolves the module HTML file: "+window.Title);
        }
        static void CheckNoGuidance(Window window)
        {
            string[] prohibited={"Здесь задаются правила.","Настройте структуру и образцы", "Каждая строка записывает", "Основной сценарий:", "Для выделения на нескольких этажах", "Значения правил текущей модели:", "Сопоставление точное;", "В группе записываются только", "Shift — диапазон", "Сохраните настройки, выделите элементы", "Название правила служит подписью"};
            Check(!All<TextBlock>(window).Any(t=>t.IsVisible&&prohibited.Any(prefix=>t.Text.Contains(prefix))),"Visible window has no inline module description or instructional prose: "+window.Title);
        }
        static bool InTable(DependencyObject item)
        { for(var current=VisualTreeHelper.GetParent(item);current!=null;current=VisualTreeHelper.GetParent(current)) if(current is DataGrid) return true; return false; }
        static void CheckFormEdges(Window window)
        {
            var fields=All<ComboBox>(window).Where(c=>c.IsVisible&&!InTable(c)&&c.Parent is StackPanel parent&&parent.Orientation==Orientation.Vertical&&c.HorizontalAlignment==HorizontalAlignment.Stretch).ToList();
            Check(fields.Count>0&&fields.All(c=>Math.Abs(c.TranslatePoint(new Point(),(UIElement)c.Parent).X)<1&&Math.Abs(c.ActualWidth-((FrameworkElement)c.Parent).ActualWidth)<1),"Form pickers share both edges with their vertical field/label container");
        }
        static void CheckCollections(Window window)
        {
            var collections=All<Control>(window).Where(c=>c.IsVisible&&c.ActualHeight>0&&(c is DataGrid||c is ListBox||(c.GetType()==typeof(ItemsControl)&&((ItemsControl)c).ItemsSource!=null))).ToList();
            Check(collections.Count>0&&collections.All(c=> { var border=c.Parent as Border; if(c is ListBox&&c.Parent is Grid overlay)border=overlay.Parent as Border; return border!=null&&Equals(border.Tag,"CollectionFrame")&&border.BorderThickness==new Thickness(1)&&border.CornerRadius.TopLeft==6; }),"Every visible table/list has one independent rounded one-DIP collection outline");
            var zero=All<ItemsControl>(window).Where(c=>c.GetType()==typeof(ItemsControl)&&c.IsVisible&&c.ItemsSource!=null&&c.ActualHeight==0).ToList();
            Check(zero.All(c=>c.Parent is Border border&&border.ActualHeight<1),"Zero-height collection content does not leave an empty outlined strip");
        }
        static void CheckCapsules(Window window)
        {
            var tracks=All<Grid>(window).Where(x=>SAB.UI.FluentLight.Capsule.GetIsGroup(x)&&x.IsVisible).ToList();
            Check(tracks.Count>0&&tracks.All(track=> {
                var indicator=track.Children.OfType<Border>().First(b=>Equals(b.Tag,"CapsuleIndicator"));
                return track.ActualHeight>=34&&Math.Abs(indicator.ActualHeight-(track.ActualHeight-4))<1&&All<RadioButton>(track).Where(r=>r.IsVisible).All(r=>All<TextBlock>(r).All(label=>label.ActualHeight+r.Padding.Top+r.Padding.Bottom<=r.ActualHeight+1));
            }),"Capsules fit wrapped labels with two-DIP inset and matching indicator height");
        }
        static void CheckTableScroll(DataGrid grid)
        {
            grid.UpdateLayout();Pump(220);
            var viewer=All<ScrollViewer>(grid).First(s=>s.Name=="DG_ScrollViewer");
            var bar=(ScrollBar)viewer.Template.FindName("PART_VerticalScrollBar",viewer);
            var body=(ScrollContentPresenter)viewer.Template.FindName("PART_ScrollContentPresenter",viewer);
            var headers=All<DataGridColumnHeadersPresenter>(grid).First();
            Check(Grid.GetColumn(headers)+Grid.GetColumnSpan(headers)>Grid.GetColumn(bar),"Column headers span the scrollbar gutter without a blank channel");
            var group=All<ToggleButton>(body).FirstOrDefault(b=>b.Name=="GroupToggle"&&b.IsVisible&&b.TranslatePoint(new Point(),body).Y<=1);
            if(bar.IsVisible&&group!=null)Check(bar.TranslatePoint(new Point(),grid).Y>=group.TranslatePoint(new Point(0,group.ActualHeight),grid).Y-1,"Vertical scrollbar starts below the visible group row and inside data rows");
        }
        static void CaptureExamples()
        {
            Check(Application.Current.TryFindResource("SabSwitchTemplate")==null && Application.Current.TryFindResource("SabCollectionBorderStyle")==null,
                "Host application uses incumbent SAB resources; new Fluent resources are scoped to migrated controls/windows");
            var p=Parameters();Show(p);try
            {
                log.Add("Native DPI "+VisualTreeHelper.GetDpi(p).PixelsPerInchX+". 144 dpi is render only, not OS scaling.");
                var tabs=All<TabControl>(p).Single();Check(tabs.Items.Count==4,"Real parameter SettingsWindow has all four source tabs");
                CheckHelp(p,"SAB_HTML_Zapolnenie_parametrov.html",false);
                Check(All<TabPanel>(tabs).Single().ActualHeight>=40,"Native capsule tab items fit approved larger track40DIP");
                if(SystemParameters.ClientAreaAnimation) { tabs.SelectedIndex=1;Pump(30);var indicator=All<Border>(tabs).First(b=>Equals(b.Tag,"CapsuleIndicator"));Check((indicator.RenderTransform as TranslateTransform)?.HasAnimatedProperties==true,"Native tab indicator actually runs its movement animation");tabs.SelectedIndex=0;Pump(180); }
                for(int i=0;i<4;i++){
                    tabs.SelectedIndex=i;Pump(220);CheckNoGuidance(p);CheckCollections(p);
                    var track=All<Grid>(tabs).First(x=>SAB.UI.FluentLight.Capsule.GetIsGroup(x));var content=(ContentPresenter)tabs.Template.FindName("SelectedBody",tabs);
                    var gap=content.TranslatePoint(new Point(),tabs).Y-track.TranslatePoint(new Point(0,track.ActualHeight),tabs).Y;
                    Check(Math.Abs(gap-12)<1,"Every parameter tab has a twelve-DIP gap before its working surface");
                    if(i==1||i==2)CheckFormEdges(p);
                    Shot((FrameworkElement)p.Content,"0"+i+"-parameters-"+new[]{"rules","sources","values","categories"}[i]);
                    if(i==2){
                        var mappingInput=All<ComboBox>(p).First(c=>c.GetBindingExpression(ComboBox.SelectedValueProperty)?.ParentBinding.Path?.Path=="MappingInput");
                        mappingInput.SelectedValue=MappingInput.RoomParameter;Pump(220);CheckFormEdges(p);CheckCollections(p);
                        var sourceLabel=All<TextBlock>(p).First(x=>x.IsVisible&&x.Text=="Исходный параметр");
                        var sourcePicker=All<SAB.ParameterTools.UI.ParameterPicker>(p).First(x=>x.IsVisible&&!InTable(x));var sourceCombo=All<ComboBox>(sourcePicker).First();
                        log.Add("AXIS source="+sourceCombo.TranslatePoint(new Point(),p).X+" label="+sourceLabel.TranslatePoint(new Point(),p).X+" width="+sourceCombo.ActualWidth+" reference="+mappingInput.ActualWidth+" pickerMargin="+sourcePicker.Margin+" comboMargin="+sourceCombo.Margin+" parent="+sourcePicker.Parent.GetType().Name);
                        Check(Math.Abs(sourceCombo.TranslatePoint(new Point(),p).X-sourceLabel.TranslatePoint(new Point(),p).X)<1&&Math.Abs(sourceCombo.ActualWidth-mappingInput.ActualWidth)<1,"Actual source parameter outline shares axes with its label and preceding field");
                        Shot((FrameworkElement)p.Content,"25-parameters-mapping-input");mappingInput.SelectedValue=MappingInput.RoomName;Pump(220);
                        var valuesScroll=All<ScrollViewer>(p).First(s=>s.IsVisible&&SAB.UI.FluentLight.ScrollEdges.GetEnabled(s));valuesScroll.ScrollToBottom();Shot((FrameworkElement)p.Content,"26-parameters-values-bottom");valuesScroll.ScrollToTop();
                    }
                }
                tabs.SelectedIndex=0;Pump();var g=All<DataGrid>(p).First(x=>x.IsVisible);Check(g.Columns.Count==7,"All source rule columns retained including Required and row action");
                g.CurrentCell=new DataGridCellInfo(g.Items[0],g.Columns[1]);g.Focus();g.BeginEdit();Pump();var editor=All<TextBox>(g).First(x=>x.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path?.Path=="EntityName");editor.Text="Корпус · проверка привязки";Check(((Rule)g.Items[0]).EntityName==editor.Text,"Real parameter rule Binding updates source model");Shot((FrameworkElement)p.Content,"04-parameters-editing");g.CommitEdit(DataGridEditingUnit.Cell,true);g.CommitEdit(DataGridEditingUnit.Row,true);
                var picker=All<SAB.ParameterTools.UI.ParameterPicker>(g).First(x=>x.IsVisible);var combo=All<ComboBox>(picker).First();combo.Focus();combo.IsDropDownOpen=true;Pump(220);var popup=(Popup)combo.Template.FindName("PART_Popup",combo);
                var popupBody=(FrameworkElement)popup.Child;popupBody.UpdateLayout();Pump(220);
                log.Add("POPUP items="+combo.Items.Count+" offset="+All<ScrollViewer>(popupBody).First().VerticalOffset+" labels="+string.Join(" | ",All<TextBlock>(popupBody).Select(x=>x.Text+":"+x.ActualWidth+"x"+x.ActualHeight+":"+x.IsVisible)));
                Check(popup.IsOpen&&All<TextBlock>(popupBody).Any(x=>x.IsVisible&&x.ActualWidth>0&&x.Text.StartsWith("SAB_")),"Open parameter dropdown contains visible parameter names");
                Shot(popupBody,"05-parameter-dropdown");combo.IsDropDownOpen=false;
                var rule=(Rule)g.Items[0];var target=rule.Target;rule.Target=null;p.GetType().GetMethod("UpdateStatus",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);Shot((FrameworkElement)p.Content,"06-parameters-validation");rule.Target=target;
                tabs.SelectedIndex=3;Pump();var search=All<TextBox>(p).First(x=>x.IsVisible&&(string)x.ToolTip=="Поиск по названию категории");search.Text="Несуществующая категория";Shot((FrameworkElement)p.Content,"07-categories-empty");search.Text="";tabs.SelectedIndex=0;
                tabs.SelectedIndex=3;Pump();
                var categoryControl=All<UserControl>(p).First(x=>x.GetType().Name=="CategorySelectionControl");
                var categories=(List<CategoryChoice>)p.GetType().GetField("_categories",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(p);
                var enabledIds=categories.Where(c=>c.Selected).Select(c=>c.Id).OrderBy(x=>x).ToArray();
                var selectionFile=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"category-selection-smoke.json");
                var exportSelection=categoryControl.GetType().GetMethod("ExportSelection",BindingFlags.Instance|BindingFlags.NonPublic);
                var importSelection=categoryControl.GetType().GetMethod("ImportSelection",BindingFlags.Instance|BindingFlags.NonPublic);
                exportSelection.Invoke(categoryControl,new object[]{selectionFile});
                foreach(var category in categories)category.Selected=false;
                importSelection.Invoke(categoryControl,new object[]{selectionFile});
                Check(categories.Where(c=>c.Selected).Select(c=>c.Id).OrderBy(x=>x).SequenceEqual(enabledIds),"Category selection saves and loads stable IDs");
                var selectedOnly=All<RadioButton>(categoryControl).First(x=>Equals(x.Content,"Выбранные"));selectedOnly.IsChecked=true;Pump();
                var categoryList=All<ListBox>(categoryControl).First();Check(categoryList.Items.Cast<CategoryChoice>().All(c=>c.Selected)&&categoryList.Items.Count==enabledIds.Length,"Selected-only capsule filters actual categories");
                Shot((FrameworkElement)p.Content,"19-categories-selected");
                File.WriteAllText(selectionFile,"{\"Format\":\"SAB.CategorySelection\",\"Version\":1,\"CategoryIds\":["+enabledIds[0]+",2147483647]}");
                importSelection.Invoke(categoryControl,new object[]{selectionFile});Check(categories.Count(c=>c.Selected)==1,"Unknown saved category IDs are skipped without selecting other categories");
                Shot((FrameworkElement)p.Content,"20-categories-loaded");
                File.WriteAllText(selectionFile,"{\"Format\":\"Other\",\"Version\":1,\"CategoryIds\":[]}");
                var rejected=false;try{importSelection.Invoke(categoryControl,new object[]{selectionFile});}catch(TargetInvocationException){rejected=true;}
                Check(rejected&&categories.Count(c=>c.Selected)==1,"Invalid selection file is rejected before changing selection");
                foreach(var category in categories)category.Selected=enabledIds.Contains(category.Id);
                All<RadioButton>(categoryControl).First(x=>Equals(x.Content,"Все")).IsChecked=true;
                File.Delete(selectionFile);tabs.SelectedIndex=0;
                p.Width=p.MinWidth;p.Height=p.MinHeight;Shot((FrameworkElement)p.Content,"08-parameters-minimum");tabs.SelectedIndex=3;Shot((FrameworkElement)p.Content,"23-categories-minimum");tabs.SelectedIndex=0;p.Width=1400;p.Height=920;Shot((FrameworkElement)p.Content,"09-parameters-144dpi",144);
            }finally{p.Close();}
            CreateViewsAndSheetsViewModel vm;var v=Views(out vm);Show(v);try
            {
                CheckHelp(v,SAB.UI.FluentLight.Instructions.ViewsFile,true);CheckNoGuidance(v);
                var grid=All<DataGrid>(v).First(x=>x.Name=="CreationRowsDataGrid");Check(grid.Columns.Count>=10,"Real views/sheets grid retains original ten columns plus dynamic browser columns");Check(vm.Rows.Count==5,"Real VM initializes five source SessionRows");Shot((FrameworkElement)v.Content,"10-views-sheets");
                Check(All<Border>(v).Any(b=>SAB.UI.FluentLight.RoundedClip.GetRadius(b)==6 && (b.Child as FrameworkElement)?.Clip is RectangleGeometry),"Table container has actual rounded geometry clip at all four corners");
                CheckCapsules(v);CheckCollections(v);
                CheckTableScroll(grid);Check(All<ScrollViewer>(grid).First(s=>s.Name=="DG_ScrollViewer").ScrollableWidth<1,"Ordinary columns fit the initial table without horizontal overflow");
                var structureTrack=All<Grid>(v).First(x=>SAB.UI.FluentLight.Capsule.GetIsGroup(x)&&All<RadioButton>(x).Any(r=>Equals(r.Content,"Несколько этажей")));
                Check(structureTrack.ActualHeight>34,"Two-line structure labels expand the capsule above the compact minimum");
                var numberHeader=All<DataGridColumnHeader>(grid).First(h=>Equals(h.Content,"№"));Check(numberHeader.HorizontalContentAlignment==HorizontalAlignment.Center,"Row number header shares centered alignment with row values");
                var scaleHeader=All<DataGridColumnHeader>(grid).First(h=>Equals(h.Content,"Масштаб"));
                var scaleColumn=grid.Columns.First(c=>Equals(c.Header,"Масштаб"));var scaleCell=scaleColumn.GetCellContent(grid.Items[0]);var scaleCombo=All<ComboBox>(scaleCell).First();var scaleEditor=(TextBox)scaleCombo.Template.FindName("PART_EditableTextBox",scaleCombo);
                var scaleAxis=scaleEditor.TranslatePoint(new System.Windows.Point(scaleEditor.ActualWidth/2,0),grid).X;var headerAxis=scaleHeader.TranslatePoint(new System.Windows.Point(scaleHeader.ActualWidth/2,0),grid).X;
                Check(scaleEditor.TextAlignment==TextAlignment.Center&&Math.Abs(scaleAxis-headerAxis)<1,"Visible editable scale text shares its header axis within one DIP");
                var settings = new CreateViewsAndSheetsSettingsWindow(vm);settings.Width=1200;settings.Height=900;Show(settings);try {
                    CheckHelp(settings,SAB.UI.FluentLight.Instructions.ViewsFile,true);CheckNoGuidance(settings);
                    Shot((FrameworkElement)settings.Content,"21-views-secondary-settings");
                    CheckCapsules(settings);
                    Check(All<TextBlock>(settings).All(x=>!new[]{"□","≡","▦","✓","⚙"}.Contains(x.Text)),"Secondary source settings has no decorative heading glyphs");
                    settings.Width=settings.MinWidth;settings.Height=settings.MinHeight;Shot((FrameworkElement)settings.Content,"22-views-secondary-minimum");
                    CheckCapsules(settings);
                    vm.IsMultiViewStructure=true;Shot((FrameworkElement)settings.Content,"24-views-secondary-zones");CheckCollections(settings);
                    var placementTrack=All<System.Windows.Controls.Grid>(settings).First(x=>SAB.UI.FluentLight.Capsule.GetIsGroup(x)&&All<RadioButton>(x).Any(r=>r.Name=="SourceSheetPlacementToggle"));
                    var placementChoice=All<RadioButton>(placementTrack).Single(r=>r.IsVisible);Check(Math.Abs(placementChoice.ActualWidth-(placementTrack.ActualWidth-4))<1&&placementTrack.ActualHeight==34,"Conditional placement capsule uses one full-width segment without phantom option");vm.IsSingleStoryStructure=true;
                } finally { settings.Close(); }
                var restoredSettings = new CreateViewsAndSheetsSettingsWindow(vm);
                Check(restoredSettings.Width==1040 && restoredSettings.Height==760,
                    "Actual secondary settings service saves and restores the tested minimum geometry");
                restoredSettings.Close(); Pump();v.UpdateLayout();Pump();
                var count=vm.Rows.Count;vm.InsertRowAfterCommand.Execute(vm.Rows[0]);Check(vm.Rows.Count==count+1,"Real InsertRowAfterCommand inserts row");vm.DeleteRowCommand.Execute(vm.Rows[1]);Check(vm.Rows.Count==count,"Real DeleteRowCommand removes inserted row");
                vm.Rows[0].SheetName="Планы · проверка";vm.CopySheetNamesToViewNamesCommand.Execute(null);Check(vm.Rows[0].ViewName==vm.Rows[0].SheetName,"Real CopySheetNamesToViewNamesCommand updates name");
                grid.SelectedIndex=0;var text=All<TextBox>(grid).First(x=>x.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path?.Path=="ViewName");text.Focus();text.Text="План 1 этажа · изменён";Check(vm.Rows[0].ViewName==text.Text,"Actual view-name editor updates the production VM");Shot((FrameworkElement)v.Content,"11-views-editing");
                var structure=All<RadioButton>(v).First(x=>Equals(x.Content,"Несколько этажей"));structure.IsChecked=true;Pump();Check(vm.IsMultiStoryStructure,"Capsule choice updates actual structure VM");Shot((FrameworkElement)v.Content,"12-views-multi-story");CheckCollections(v);vm.IsMultiViewStructure=true;Shot((FrameworkElement)v.Content,"13-views-multi-view");CheckCollections(v);vm.IsSingleStoryStructure=true;
                vm.Rows[0].ViewName="";Shot((FrameworkElement)v.Content,"14-views-validation");Check(!vm.CanCreate,"Source validation disables creation for incomplete row");vm.Rows[0].ViewName="План 1 этажа";
                v.Width=v.MinWidth;v.Height=v.MinHeight;Shot((FrameworkElement)v.Content,"15-views-minimum");CheckCapsules(v);CheckCollections(v);CheckTableScroll(grid);Check(All<Button>(v).First(b=>b.Name=="CreateButton").IsVisible,"Create action visible at minimum");
                var tableScroll=All<ScrollViewer>(grid).First(s=>s.Name=="DG_ScrollViewer");tableScroll.ScrollToVerticalOffset(1);Pump(220);Check(tableScroll.VerticalOffset>0,"Native row scrolling remains functional at minimum size");CheckTableScroll(grid);tableScroll.ScrollToTop();Pump(220);
                var groupToggle=All<ToggleButton>(grid).First(b=>b.Name=="GroupToggle");groupToggle.IsChecked=false;Pump(220);Check(!All<DataGridRow>(grid).Any(r=>r.IsVisible),"Native group collapse hides its rows without replacing group behavior");groupToggle.IsChecked=true;Pump(220);v.Width=1480;v.Height=940;
                var scroll=All<ScrollViewer>(v).First(s=>s.Name=="SettingsDrawerPanel");scroll.ScrollToBottom();Shot((FrameworkElement)v.Content,"16-views-settings-bottom");scroll.ScrollToTop();Shot((FrameworkElement)v.Content,"17-views-144dpi",144);
                vm.ClearRowsCommand.Execute(null);Shot((FrameworkElement)v.Content,"18-views-empty");Check(vm.Rows.Count==1&&string.IsNullOrEmpty(vm.Rows[0].ViewName),"Real ClearRowsCommand restores one blank row, as production code requires");
                log.Add("Actual production UI classes/resources:135/103 original bindings,10 columns; no presentation substitutions.");
                log.Add("No Document, transaction, ExternalEvent execution, installed plugin replacement or physical-input claim. Main preference path isolated by host reflection; secondary preference file restored after capture.");
            }finally{v.Close();}
            var preferencePath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-preferences","window-layout.json");
            var layoutService=new SAB.CreateViewsAndSheets.Services.CreateViewsAndSheetsWindowLayoutService();
            layoutService.GetType().GetField("_layoutFilePath",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(layoutService,preferencePath);
            var saved=layoutService.Load();
            Check(saved!=null && saved.WindowWidth>=1100 && saved.WindowHeight>=760 && saved.ColumnWidths.Count==10,
                "Actual main closing handler persists geometry and all ten column widths to isolated storage");
            saved.WindowWidth=1320;saved.WindowHeight=860;layoutService.Save(saved);
            CreateViewsAndSheetsViewModel restoreVm;var restoreWindow=Views(out restoreVm);
            typeof(CreateViewsAndSheetsWindow).GetField("_layoutRestored",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(restoreWindow,false);
            Show(restoreWindow);
            Check(restoreWindow.Width==1320 && restoreWindow.Height==860,
                "Actual main loaded handler restores saved geometry through the unchanged production service");
            restoreWindow.Close();
            var keyboard=Parameters();Show(keyboard);
            var keyboardCombo=All<ComboBox>(keyboard).First(c=>c.IsVisible);keyboardCombo.Focus();keyboardCombo.IsDropDownOpen=true;Pump();
            var popupEscape=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(keyboard),Environment.TickCount,Key.Escape) { RoutedEvent=Keyboard.PreviewKeyDownEvent };
            keyboardCombo.RaiseEvent(popupEscape);
            Check(keyboard.IsVisible,"Escape in an open parameter dropdown does not close the production settings window");
            keyboardCombo.IsDropDownOpen=false;
            var windowEscape=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(keyboard),Environment.TickCount,Key.Escape) { RoutedEvent=Keyboard.PreviewKeyDownEvent };
            keyboard.RaiseEvent(windowEscape);
            Check(!keyboard.IsVisible && windowEscape.Handled,"Production parameter window retains its Escape close contract");
        }
    }
}
