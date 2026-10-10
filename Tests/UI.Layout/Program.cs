using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SAB.UI;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.ViewModels;
using SAB.InteriorElevations.Views;
using SAB.CreateViewsAndSheets.Models;
using SAB.CreateViewsAndSheets.ViewModels;
using SAB.CreateViewsAndSheets.Views;
using SAB.ParameterTools.Core;

internal static class Program
{
    [DllImport("kernel32", CharSet=CharSet.Unicode)] static extern bool SetDllDirectory(string path);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
    static string output;
    static readonly List<string> results = new List<string>();
    static void Check(bool value,string text) { results.Add((value?"PASS ":"FAIL ")+text); }
    static void Drain() { var frame=new DispatcherFrame(); var timer=new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval=TimeSpan.FromMilliseconds(60) };
        timer.Tick+=(s,e)=>{ timer.Stop();frame.Continue=false; };timer.Start();Dispatcher.PushFrame(frame); }
    static IEnumerable<T> All<T>(DependencyObject p) where T:DependencyObject { for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++) {
        var c=VisualTreeHelper.GetChild(p,i); if(c is T t)yield return t; foreach(var n in All<T>(c))yield return n; } }
    static void Open(Window w,double width,double height) { w.ShowInTaskbar=false;w.WindowStartupLocation=WindowStartupLocation.Manual;
        w.Left=-10000;w.Top=-10000;w.Show();Drain();w.Width=width;w.Height=height;Drain();w.UpdateLayout(); }
    static BitmapSource Capture(Window w,string name,int dpi=96) { Drain();w.UpdateLayout();var c=(FrameworkElement)w.Content;
        var visual=new DrawingVisual();using(var d=visual.RenderOpen()) { var brush=new VisualBrush(c) { Stretch=Stretch.None,
            AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(c.RenderSize) };
            d.DrawRectangle(brush,null,new Rect(c.RenderSize)); }
        var bmp=new RenderTargetBitmap((int)Math.Ceiling(c.ActualWidth*dpi/96.0),(int)Math.Ceiling(c.ActualHeight*dpi/96.0),dpi,dpi,PixelFormats.Pbgra32);bmp.Render(visual);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(Path.Combine(output,name+".png")))png.Save(f);return bmp; }
    static void Corners(Window w,string name) { var bmp=Capture(w,name);var pixel=new byte[4];bool rounded=true;
        foreach(var p in new[]{new Point(1,1),new Point(bmp.PixelWidth-2,1),new Point(1,bmp.PixelHeight-2),new Point(bmp.PixelWidth-2,bmp.PixelHeight-2)}) {
            bmp.CopyPixels(new Int32Rect((int)p.X,(int)p.Y,1,1),pixel,4,0);rounded &= pixel[3]<100; }
        Check(rounded,name+": all four content corners remain transparent; opaque child must not paint over rounded frame"); }
    static void ResizeEdges(Window w,string name) {
        var points=new[]{new Point(3,w.ActualHeight/2),new Point(w.ActualWidth-3,w.ActualHeight/2),new Point(w.ActualWidth/2,3),new Point(w.ActualWidth/2,w.ActualHeight-3),new Point(6,6),new Point(w.ActualWidth-6,6),new Point(6,w.ActualHeight-6),new Point(w.ActualWidth-6,w.ActualHeight-6)};
        int[] expected={10,11,12,15,13,14,16,17};bool ok=true;var handle=new WindowInteropHelper(w).Handle;
        for(int i=0;i<points.Length;i++){var p=w.PointToScreen(points[i]);int hit=SendMessage(handle,0x84,IntPtr.Zero,new IntPtr(((int)p.Y<<16)|((int)p.X&0xffff))).ToInt32();ok &= hit==expected[i];}
        Check(ok,name+": all eight native resize directions");
    }
    static ScrollViewer Scroller(DataGrid g) { return All<ScrollViewer>(g).First(s=>ReferenceEquals(s.TemplatedParent,g)); }
    static void Table(DataGrid g,string name) { Drain();g.UpdateLayout();var s=Scroller(g);
        Check(!double.IsInfinity(s.ViewportHeight) && s.ViewportHeight>0,name+": bounded vertical viewport");
        Check(s.ScrollableWidth<1 || g.HorizontalScrollBarVisibility==ScrollBarVisibility.Auto,name+": overflowing columns have horizontal access");
        s.ScrollToRightEnd();Drain();var column=g.Columns.Where(c=>c.Visibility==Visibility.Visible).OrderBy(c=>c.DisplayIndex).Last();
        if(g.Items.Count>0) { var item=g.Items[0];g.ScrollIntoView(item,column);Drain();var content=column.GetCellContent(item);
            if(content!=null) { var presenter=All<ScrollContentPresenter>(s).First();var rect=content.TransformToAncestor(presenter).TransformBounds(new Rect(content.RenderSize));
                Check(rect.Right<=presenter.ActualWidth+1 && rect.Left>=-1,name+": final cell is fully reachable"); }
            else Check(false,name+": final cell was not realized"); }
        s.ScrollToLeftEnd();s.ScrollToTop();Drain(); }
    static void Wheel(FrameworkElement target,int delta) { var e=new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,delta) { RoutedEvent=Mouse.PreviewMouseWheelEvent,Source=target };
        target.RaiseEvent(e);if(!e.Handled)target.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,delta) { RoutedEvent=Mouse.MouseWheelEvent,Source=target });Drain(); }
    static Window Parameters() {
        var profile=new Profile { Configured=true };var parameters=new List<ParameterRef>();
        for(int i=0;i<12;i++) { var p=new ParameterRef { Id=i+1,Name="SAB_Параметр_"+i,SharedGuid=Guid.NewGuid().ToString(),DataType="Текст",VariesAcrossGroups=true };parameters.Add(p);
            var rule=new Rule { Target=p,Group=SAB.ParameterTools.Core.ParameterGroup.Room,Source=i==0?RuleValueSource.Mapping:RuleValueSource.Room,EntityName="Правило "+i };
            if(i==0)for(int j=0;j<80;j++)rule.Mappings.Add(new ValueMapping { Key="Исходное значение "+j,Value="Результат "+j });profile.Rules.Add(rule); }
        for(int i=0;i<80;i++)profile.Levels.Add(new LevelMapping { LevelUniqueId=i.ToString(),LevelName="Уровень "+i,Value=i.ToString() });
        var categoryType=typeof(Profile).Assembly.GetType("SAB.ParameterTools.CategoryChoice");var categories=Activator.CreateInstance(typeof(List<>).MakeGenericType(categoryType));
        var type=typeof(Profile).Assembly.GetType("SAB.ParameterTools.UI.SettingsWindow");
        return (Window)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{profile,parameters,parameters,categories,profile.Levels,parameters},null);
    }
    [STAThread] static int Main(string[] args) { SetDllDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Autodesk","Revit 2023"));return Run(args); }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int Run(string[] args) {
        output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };SabWindowAnimationService.Enabled=false;
        var preferences=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SAB","WindowSizes");
        var saved=Directory.Exists(preferences)?Directory.GetFiles(preferences,"*.json").ToDictionary(p=>p,File.ReadAllBytes):new Dictionary<string,byte[]>();
        try {
            // Revit collectors require a document. Only presentation collections are initialized in this fixture.
            var ev=(ElevationSettingsViewModel)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ElevationSettingsViewModel));
            foreach(var p in typeof(ElevationSettingsViewModel).GetProperties().Where(p=>p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition()==typeof(ObservableCollection<>)))p.SetValue(ev,Activator.CreateInstance(p.PropertyType));
            typeof(ElevationSettingsViewModel).GetField("_namingPreviewContexts",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ev,new List<ElevationNamingPreviewContext>());
            ev.ElevationNamePart1Text="Развёртка_";ev.ElevationNamePart2Text="[Номер помещения]_[Имя помещения]";ev.ViewScaleText="50";
            var ew=new ElevationSettingsWindow(ev,true,"Тестовые данные · проверка вёрстки","Проверка окна вне Revit");Open(ew,980,720);Corners(ew,"elevations-normal");
            ResizeEdges(ew,"elevations");var et=All<TabControl>(ew).First();ew.Width=ew.MinWidth;ew.Height=ew.MinHeight;
            for(int i=0;i<et.Items.Count;i++){et.SelectedIndex=i;Drain();Capture(ew,"elevations-min-tab"+i);}ew.Close();
            var rows=new ObservableCollection<MultiRoomSelectionItem>();var mv=new MultiRoomSelectionViewModel(rows);for(int i=0;i<40;i++)mv.AddRowAfter(rows.Last());
            var mw=new MultiRoomSelectionWindow(rows);Open(mw,mw.MinWidth,mw.MinHeight);var mg=All<DataGrid>(mw).First();Table(mg,"multiple rooms");Capture(mw,"rooms-min");mw.Close();
            var vm=new CreateViewsAndSheetsViewModel(new List<RevitElementItem>(),new List<RevitElementItem>(),new List<RevitElementItem>(),new List<RevitElementItem>(),new List<RevitElementItem>(),new Dictionary<long,List<string>>(),new Dictionary<long,HashSet<long>>(),new List<RevitElementItem>(),new HashSet<string>(),new HashSet<string>(),new CreateViewsAndSheetsSettings());
            for(int i=0;i<120;i++){var row=vm.Rows.Last();row.ViewName="Тестовый вид "+i;row.SheetNumber="АР-"+i;row.SheetName="Длинное название листа для проверки таблицы "+i;vm.InsertRowAfterCommand.Execute(row);}
            var vw=new CreateViewsAndSheetsWindow(vm);Open(vw,1280,760);var vg=All<DataGrid>(vw).Single(g=>g.Name=="CreationRowsDataGrid");Capture(vw,"views-normal");
            vw.Width=vw.MinWidth;vw.Height=vw.MinHeight;Drain();Table(vg,"views/sheets grouped");Capture(vw,"views-min");Scroller(vg).ScrollToRightEnd();Drain();Capture(vw,"views-min-right");
            Check(All<DataGridRow>(vg).Count()<60,"grouped table virtualizes 121 rows");
            Scroller(vg).ScrollToLeftEnd();Drain();
            var resizeTarget=typeof(CreateViewsAndSheetsWindow).GetMethod("GetColumnResizeTarget",BindingFlags.Instance|BindingFlags.NonPublic);
            var header=All<DataGridColumnHeader>(vg).First(h=>h.Column!=null&&h.Column.DisplayIndex==3);
            var left=All<Thumb>(header).First(t=>t.Name=="PART_LeftHeaderGripper");var right=All<Thumb>(header).First(t=>t.Name=="PART_RightHeaderGripper");
            var previous=vg.Columns.Where(c=>c.Visibility==Visibility.Visible&&c.DisplayIndex<header.Column.DisplayIndex).OrderBy(c=>c.DisplayIndex).Last();
            Check(ReferenceEquals(resizeTarget.Invoke(vw,new object[]{left}),previous)&&ReferenceEquals(resizeTarget.Invoke(vw,new object[]{right}),header.Column),"left/right header grippers identify the actual resized column, skipping hidden columns");
            vw.Close();
            var vs=new CreateViewsAndSheetsSettingsWindow(vm);Open(vs,1040,700);
            foreach(var size in new[]{new Size(1040,700),new Size(vs.MinWidth,vs.MinHeight)}) {
                vs.Width=size.Width;vs.Height=size.Height;Drain();
                Check(All<FrameworkElement>(vs).Where(f=>(f is TextBox||f is ComboBox||f is Button)&&f.IsVisible&&f.ActualWidth>0).All(f=>{var p=f.TranslatePoint(new Point(),vs);return p.X>=-1&&p.X+f.ActualWidth<=vs.ActualWidth+1;}),"views/sheets settings: input bounds at "+size);
            }
            Capture(vs,"views-settings-min");vs.Close();
            var pw=Parameters();Open(pw,1120,820);Corners(pw,"parameters-normal");ResizeEdges(pw,"parameters");Capture(pw,"parameters-144dpi",144);pw.Width=pw.MinWidth;pw.Height=pw.MinHeight;Drain();
            var pt=All<TabControl>(pw).First();var pg=All<DataGrid>(pw).First();Table(pg,"parameter rules");Capture(pw,"parameters-min-rules");
            double manual=270;pg.Columns[4].Width=manual;pw.Width+=80;Drain();Check(Math.Abs(pg.Columns[4].ActualWidth-manual)<1,"manual rule column width survives window resize");pw.Width=pw.MinWidth;
            for(int i=1;i<pt.Items.Count;i++){pt.SelectedIndex=i;Drain();Capture(pw,"parameters-min-tab"+i);foreach(var g in All<DataGrid>(pw).Where(g=>g.IsVisible))Table(g,"parameter tab "+i);}
            pt.SelectedIndex=2;Drain();var nested=All<DataGrid>(pw).First(g=>g.IsVisible&&g.Items.Count==80);var inner=Scroller(nested);
            var outer=All<ScrollViewer>(pw).First(s=>s.TemplatedParent==null&&s.Content is StackPanel&&s.IsVisible);
            outer.ScrollToTop();inner.ScrollToTop();Drain();Wheel(inner,-120);Check(inner.VerticalOffset>0&&outer.VerticalOffset==0,"wheel inside table scrolls table before parent");
            inner.ScrollToBottom();outer.ScrollToTop();Drain();Wheel(inner,-120);Check(outer.VerticalOffset>0,"wheel at nested table bottom continues outer form");
            Capture(pw,"parameters-nested-scroll");
            inner.ScrollToTop();outer.ScrollToBottom();Drain();double outerBefore=outer.VerticalOffset;Wheel(inner,120);Check(outer.VerticalOffset<outerBefore,"wheel at nested table top continues outer form upwards");
            ((IList)nested.ItemsSource).Clear();Drain();outer.ScrollToBottom();Drain();outerBefore=outer.VerticalOffset;Wheel(inner,120);Check(outer.VerticalOffset<outerBefore,"wheel over empty nested table reaches outer form");pw.Close();
        } catch(Exception e) { results.Add("FAIL exception: "+e); }
        finally { app.Shutdown();if(Directory.Exists(preferences))foreach(var p in Directory.GetFiles(preferences,"*.json"))if(!saved.ContainsKey(p))File.Delete(p);
            foreach(var entry in saved)File.WriteAllBytes(entry.Key,entry.Value);File.WriteAllLines(Path.Combine(output,"results.txt"),results);foreach(var r in results)Console.WriteLine(r); }
        return args.Contains("--baseline")?0:results.Any(r=>r.StartsWith("FAIL"))?1:0;
    }
}
