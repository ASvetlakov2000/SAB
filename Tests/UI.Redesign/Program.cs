using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.ViewModels;
using SAB.InteriorElevations.Views;
using SAB.CreateViewsAndSheets.Models;
using SAB.CreateViewsAndSheets.ViewModels;
using SAB.CreateViewsAndSheets.Views;
using SAB.UI;

internal static class Program
{
    [DllImport("kernel32", CharSet=CharSet.Unicode)] private static extern bool SetDllDirectory(string path);
    static string output;
    static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Drain() { var frame=new DispatcherFrame();var timer=new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval=TimeSpan.FromMilliseconds(60) };
        timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame); }
    static IEnumerable<T> All<T>(DependencyObject parent) where T : DependencyObject
    { for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) { var c=VisualTreeHelper.GetChild(parent,i); if(c is T typed)yield return typed; foreach(var n in All<T>(c))yield return n; } }
    static void Open(Window window) { window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=-10000;window.Top=-10000;window.ShowInTaskbar=false;window.Show();Drain();window.Width=1400;window.Height=900;Drain();window.UpdateLayout(); }
    static void Capture(Window window,string name,int dpi=96)
    {
        Drain();window.UpdateLayout();
        var content=(FrameworkElement)window.Content;content.InvalidateVisual();Drain();
        var drawing=new DrawingVisual();using(var context=drawing.RenderOpen()) {
            context.DrawRectangle(window.Background ?? Brushes.White,null,new Rect(0,0,content.ActualWidth,content.ActualHeight));
            var brush = new VisualBrush(content) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top,
                ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0,0,content.ActualWidth,content.ActualHeight) };
            context.DrawRectangle(brush,null,new Rect(0,0,content.ActualWidth,content.ActualHeight)); }
        var image=new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth*dpi/96.0),(int)Math.Ceiling(content.ActualHeight*dpi/96.0),dpi,dpi,PixelFormats.Pbgra32);image.Render(drawing);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var f=File.Create(Path.Combine(output,name+".png")))png.Save(f);
    }
    [STAThread] static int Main(string[] args)
    {
        SetDllDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Autodesk","Revit 2023"));
        return Run(args);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int Run(string[] args)
    {
        try {
            SetDllDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Autodesk","Revit 2023"));
            output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);
            new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };SabWindowAnimationService.Enabled=false;
            // Revit document collectors cannot run outside Revit. Initialize only presentation
            // collections in this fixture; production still uses its original constructor.
            var elevations=(ElevationSettingsViewModel)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ElevationSettingsViewModel));
            foreach(var property in typeof(ElevationSettingsViewModel).GetProperties().Where(p=>p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition()==typeof(ObservableCollection<>)))
                property.SetValue(elevations,Activator.CreateInstance(property.PropertyType));
            typeof(ElevationSettingsViewModel).GetField("_namingPreviewContexts",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(elevations,new List<ElevationNamingPreviewContext>());
            elevations.ElevationNamePart1Text="ELV_";elevations.ElevationNamePart2Text="[Номер помещения]_[Имя помещения]";elevations.ElevationNamePart3Text="_[Начальный угол]-[Конечный угол]";
            elevations.ElevationTitlePart1Text="Развёртка ";elevations.ElevationTitlePart2Text="[Начальный угол]-[Конечный угол]";
            elevations.ViewScaleText="50";elevations.TopOffsetMmText="3000";elevations.BottomOffsetMmText="0";
            elevations.ViewTemplates.Add(new RevitElementOption { DisplayName="АР_Развёртки" });elevations.SelectedViewTemplate=elevations.ViewTemplates[0];
            elevations.ElevationViewFamilyTypes.Add(new RevitElementOption { DisplayName="Развёртка интерьера" });elevations.SelectedElevationViewFamilyType=elevations.ElevationViewFamilyTypes[0];
            var contexts=Enumerable.Range(0,8).Select(i=>new ElevationNamingPreviewContext {
                RoomData=new RoomData { RoomNumber=(101+i/4).ToString(),RoomName=i<4?"Кабинет":"Переговорная" },StartPointNumber=i%4+1,EndPointNumber=i%4+2 }).ToList();
            elevations.SetNamingPreviewContexts(contexts);
            Require(elevations.NamingPreviewItems.Count==8,"Every selected contour segment must be previewed.");
            var ew=new ElevationSettingsWindow(elevations,true,"Тестовые данные · 2 помещения · 8 линий", "Проверьте настройки перед созданием.");Open(ew);
            var eg=All<DataGrid>(ew).Single(g=>g.Name=="ElevationPreviewDataGrid");eg.SelectedIndex=2;Drain();
            Require(All<TextBlock>(ew).Single(t=>t.Name=="ElevationPreviewViewName").Text=="Имя вида: "+elevations.NamingPreviewItems[2].ViewName,"Selected elevation preview lost its element-name binding.");
            Capture(ew,"elevations");
            var tabs=All<TabControl>(ew).First();
            ew.Width=ew.MinWidth;ew.Height=ew.MinHeight;
            for(int i=0;i<tabs.Items.Count;i++){tabs.SelectedIndex=i;Drain();Capture(ew,"elevations-min-tab"+i);}
            ew.Close();
            var rows=new ObservableCollection<MultiRoomSelectionItem>();var mv=new MultiRoomSelectionViewModel(rows);var first=rows[0];mv.AddRowAfter(first);var second=rows[1];mv.AddRowAfter(first);
            Require(rows.Count==3 && ReferenceEquals(rows[2],second) && rows.Select(r=>r.Index).SequenceEqual(new[]{1,2,3}),"Multi-room plus must insert immediately below its row.");
            var mw=new MultiRoomSelectionWindow(rows);Open(mw);Capture(mw,"multiple-rooms");mw.Close();
            var vm=new CreateViewsAndSheetsViewModel(new List<RevitElementItem>(),new List<RevitElementItem>(),new List<RevitElementItem>(),new List<RevitElementItem>(),new List<RevitElementItem>(),new Dictionary<long,List<string>>(),new Dictionary<long,HashSet<long>>(),new List<RevitElementItem>(),new HashSet<string>(),new HashSet<string>(),new CreateViewsAndSheetsSettings());
            var original=vm.Rows[0];original.ViewName="01_План первого этажа";original.SheetNumber="АР-01";original.SheetName="План первого этажа";
            vm.InsertRowAfterCommand.Execute(original);var last=vm.Rows[1];last.ViewName="02_План второго этажа";last.SheetNumber="АР-02";last.SheetName="План второго этажа";
            vm.InsertRowAfterCommand.Execute(original);Require(vm.Rows.Count==3 && ReferenceEquals(vm.Rows[2],last),"Views/sheets plus must insert immediately below its row.");
            var vw=new CreateViewsAndSheetsWindow(vm);Open(vw);var vg=All<DataGrid>(vw).Single(g=>g.Name=="CreationRowsDataGrid");vg.SelectedItem=original;Drain();
            Capture(vw,"views-and-sheets");
            Require(All<TextBlock>(vw).Any(t=>t.Text=="Название листа: "+original.SheetName),"Selected sheet preview lost its element-name binding.");
            Capture(vw,"views-and-sheets");vw.Width=vw.MinWidth;vw.Height=vw.MinHeight;Capture(vw,"views-and-sheets-min");vw.Close();
            var checks=new Window { Title="SAB · проверка галочки", Width=520,Height=300 };
            checks.Resources.MergedDictionaries.Add(new ResourceDictionary { Source=new Uri("pack://application:,,,/SAB;component/UI/Styles/SABRedesignStyles.xaml") });
            var states=new StackPanel { Margin=new Thickness(24) };var checkedBox=new CheckBox { Content="Выбранная категория",IsChecked=true,Margin=new Thickness(0,8,0,8) };
            states.Children.Add(checkedBox);states.Children.Add(new CheckBox { Content="Не выбрано" });states.Children.Add(new CheckBox { Content="Смешанный выбор",IsThreeState=true,IsChecked=null });checks.Content=states;
            Open(checks);checkedBox.ApplyTemplate();
            var box=(Border)checkedBox.Template.FindName("Box",checkedBox);var tick=(System.Windows.Shapes.Path)checkedBox.Template.FindName("Tick",checkedBox);
            Rect mark=tick.TransformToAncestor(box).TransformBounds(new Rect(tick.RenderSize));
            Require(Math.Abs(mark.X+mark.Width/2-box.ActualWidth/2)<0.5 && Math.Abs(mark.Y+mark.Height/2-box.ActualHeight/2)<0.5,"Check mark must be geometrically centered in its frame.");
            checks.Width=520;checks.Height=300;Capture(checks,"checkbox-states");
            checkedBox.Focus();Drain();Require(checkedBox.IsKeyboardFocused,"Checkbox keyboard focus was not established.");
            Require(((SolidColorBrush)box.BorderBrush).Color==Color.FromRgb(31,41,55),"Checked checkbox keyboard focus must differ from its blue checked state.");
            Capture(checks,"checkbox-192dpi",192);checks.Close();
            Console.WriteLine("PASS actual window constructors, live text previews, all elevation contexts, row-plus insertion/order, four elevation tabs, minimum layouts.");return 0;
        } catch(Exception ex) { Console.Error.WriteLine(ex);return 1; }
    }
}
