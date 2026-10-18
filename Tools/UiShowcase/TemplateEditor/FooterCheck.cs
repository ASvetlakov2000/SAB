using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SAB.ViewTemplateGraphics.Views;

namespace SAB.TemplateEditorFooterCheck
{
    public static class Program
    {
        [DllImport("kernel32",CharSet=CharSet.Unicode)] static extern bool SetDllDirectory(string path);
        static readonly List<string> log=new List<string>();
        static string root,output;
        static int checks,images;
        [STAThread] public static int Main()
        {
            var dir=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while(dir!=null&&!File.Exists(Path.Combine(dir.FullName,@"SAB\SAB.csproj")))dir=dir.Parent;
            root=dir.FullName;output=Path.Combine(root,@"Docs\Ui\DesignSystem\screenshots-template-editor-footer");
            Directory.CreateDirectory(output);
            string revit=@"C:\Program Files\Autodesk\Revit 2023";SetDllDirectory(revit);
            AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=> {var p=Path.Combine(revit,new AssemblyName(e.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};
            try{return Run();}
            catch(Exception e){log.Add("FAIL "+e);File.WriteAllLines(Path.Combine(output,"verification.txt"),log);File.WriteAllText(Path.Combine(root,@"outputs\ui-production-preview\template-editor\footer\error.txt"),e.ToString());return 1;}
        }
        [MethodImpl(MethodImplOptions.NoInlining)] static int Run()
        {
            log.Add("Actual ViewTemplateGraphicsWindow from SAB.dll with detached DTO loader. No live Revit, installation, physical input or OS-DPI verification.");
            typeof(TemplateEditorPresentation).GetProperty("PersistenceEnabled",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,false);
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
            var vm=Fixtures.CreateViewModel();var window=new ViewTemplateGraphicsWindow(vm);window.Show();Pump();
            try {
                Check(window.GetType().Assembly.GetName().Name=="SAB","actual production window class");
                var tabs=All<TabControl>(window).First();tabs.SelectedIndex=1;Pump();
                Check(All<TextBlock>(window).First(x=>x.Name=="SelectionStatusText").GetBindingExpression(TextBlock.TextProperty)!=null,"StatusText binding retained");
                foreach(var state in new[]{new {Name="normal",Width=1780.0,Height=940.0},new {Name="minimum",Width=1240.0,Height=760.0},new {Name="tall",Width=1900.0,Height=1120.0}}) {
                    window.Width=state.Width;window.Height=state.Height;Pump();Measure(window,state.Name);Capture(window,state.Name);
                }
                window.Width=1240;window.Height=760;Pump();
                var text=Named<TextBlock>(window,"SelectionStatusText");
                string longStatus=string.Join(" ",Enumerable.Repeat("Выбрано шаблонов: 124; изменено строк настроек: 9876 — длинное сообщение о результате проверки выбранных шаблонов.",8));
                text.SetCurrentValue(TextBlock.TextProperty,longStatus);Pump();Measure(window,"long-status");Capture(window,"long-status");
                Check(Named<Border>(window,"SelectionStatusBorder").ActualHeight>44,"long status grows automatically");
                text.GetBindingExpression(TextBlock.TextProperty).UpdateTarget();Pump();
                TemplateEditorPresentation.Show("Проверка",string.Join(" ",Enumerable.Repeat("Не удалось применить часть настроек. Проверьте выбранные шаблоны и допустимость параметров категорий.",4)));
                Pump();Measure(window,"validation");Capture(window,"validation");
                var validation=Named<TextBlock>(window,"TemplateEditorValidation");Check(validation.IsVisible&&validation.ActualHeight>20,"long error wraps and remains visible");
                var panel=Named<Border>(window,"SelectionStatusBorder");var button=Named<Button>(window,"ApplyButton");
                Check(Math.Abs(Bounds(validation,window).Top-Bounds(panel,window).Bottom-12)<1,"status to error gap12");
                Check(Math.Abs(Bounds(button,window).Top-Bounds(validation,window).Bottom-12)<1,"error to actions gap12");
                validation.Visibility=Visibility.Collapsed;Pump();
                for(int index=0;index<tabs.Items.Count;index++){tabs.SelectedIndex=index;Pump();Measure(window,"tab-"+index);}
                Check(tabs.Items.Count==7,"all seven tabs retained");
                tabs.SelectedIndex=1;window.Width=1780;window.Height=940;Pump();
                var footer=Named<Border>(window,"TemplateEditorFooter");CaptureElement(footer,"footer-detail");
                log.Add("COMPLETED "+checks+" PASS / "+images+" PNG; actual WPF at96 render DPI, not OS scaling.");
                File.WriteAllLines(Path.Combine(output,"verification.txt"),log);return 0;
            }finally{window.Close();app.Shutdown();}
        }
        static void Measure(Window w,string state)
        {
            var panel=Named<Border>(w,"SelectionStatusBorder");var text=Named<TextBlock>(w,"SelectionStatusText");var footer=Named<Border>(w,"TemplateEditorFooter");
            Rect p=Bounds(panel,w),t=Bounds(text,w),f=Bounds(footer,w),card=Bounds(Named<Border>(w,"SettingsCard"),w),left=Bounds(Named<Border>(w,"TemplateCard"),w);
            Rect ink=Ink(VisualTreeHelper.GetDrawing(text));Check(!ink.IsEmpty,state+" native glyph ink found");
            ink=text.TransformToAncestor(w).TransformBounds(ink);
            Check(t.Left-p.Left>=12&&p.Right-t.Right>=12&&t.Top-p.Top>=10&&p.Bottom-t.Bottom>=10,state+" text layout clears all four frame edges");
            Check(ink.Left-p.Left>=12&&p.Right-ink.Right>=12&&ink.Top-p.Top>=10&&p.Bottom-ink.Bottom>=10,state+" actual glyph ink clears frame");
            Check(Math.Abs(p.Top-card.Bottom-16)<1&&Math.Abs(left.Bottom-card.Bottom)<1,state+" both cards to status gap16");
            Check(Math.Abs(p.Top-f.Top-12)<1,state+" footer boundary to status gap12");
            Check(panel.ActualHeight>=44&&panel.Padding==new Thickness(12,10,12,10),state+" status min44 padding12,10");
            Rect buttons=Bounds(Named<Button>(w,"ApplyButton"),w);
            Check(buttons.Top>=p.Bottom+12-1&&buttons.Bottom<=f.Bottom-12+1,state+" actions clear status and bottom padding12");
            Check(text.TextWrapping==TextWrapping.Wrap&&text.LineHeight==20&&text.FontSize==13,state+" native text line and wrap settings");
            log.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"BOUNDS {0}: panel={1} text={2} ink={3} innerTop={4:F2} innerBottom={5:F2} cardGap={6:F2}",state,p,t,ink,ink.Top-p.Top,p.Bottom-ink.Bottom,p.Top-card.Bottom));
        }
        static Rect Ink(Drawing drawing)
        {
            if(drawing is GlyphRunDrawing glyph) {var box=glyph.GlyphRun.ComputeInkBoundingBox();if(!box.IsEmpty)box.Offset(glyph.GlyphRun.BaselineOrigin.X,glyph.GlyphRun.BaselineOrigin.Y);return box;}
            var result=Rect.Empty;
            if(drawing is DrawingGroup group) {foreach(var child in group.Children)result.Union(Ink(child));if(!result.IsEmpty&&group.Transform!=null)result=group.Transform.TransformBounds(result);}
            return result;
        }
        static T Named<T>(DependencyObject w,string name) where T:FrameworkElement=>All<T>(w).First(x=>x.Name==name);
        static IEnumerable<T> All<T>(DependencyObject node) where T:DependencyObject=>TemplateEditorPresentation.Children<T>(node);
        static Rect Bounds(FrameworkElement element,Visual root)=>element.TransformToAncestor(root).TransformBounds(new Rect(0,0,element.ActualWidth,element.ActualHeight));
        static void Check(bool value,string name){checks++;log.Add((value?"PASS ":"FAIL ")+name);if(!value)throw new InvalidOperationException(name);}
        static void Pump(){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
        static void Capture(Window window,string name)=>CaptureElement((FrameworkElement)window.Content,name);
        static void CaptureElement(FrameworkElement element,string name)
        {
            element.UpdateLayout();int width=(int)Math.Ceiling(element.ActualWidth),height=(int)Math.Ceiling(element.ActualHeight);
            if(width<50||height<20)throw new InvalidOperationException("Invalid capture "+name);
            var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
            if(element.Name=="TemplateEditorFooter") {
                // An attached subtree has a parent layout offset; sample its actual visual at local origin.
                var visual=new DrawingVisual();using(var context=visual.RenderOpen())context.DrawRectangle(new VisualBrush(element),null,new Rect(0,0,width,height));
                bitmap.Render(visual);
            } else bitmap.Render(element);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(output,name+".png")))encoder.Save(file);
            images++;log.Add("CAPTURE "+name+" "+width+"x"+height+" WPF96dpi");
        }
    }
}
