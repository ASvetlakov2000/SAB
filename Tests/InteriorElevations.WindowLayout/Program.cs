using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SAB.UI;
class Program {
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
 static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
 static IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject {
  for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) { var c=VisualTreeHelper.GetChild(root,i); if(c is T) yield return (T)c; foreach(var n in Children<T>(c)) yield return n; }
 }
 static void Drain() { var frame=new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false)); Dispatcher.PushFrame(frame); }
 [STAThread] static int Main(string[] args) {
  try {
   string root=Path.GetFullPath(args[0]); var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown }; SabWindowAnimationService.Enabled=false;
   string windowFile=args.Length>1 ? args[1] : "ElevationSettingsWindow.xaml";
   string output=Path.Combine(root,"outputs",Path.GetFileNameWithoutExtension(windowFile)+"-resize"); Directory.CreateDirectory(output);
   string path=Path.Combine(root,"SAB/bin/Revit2023/Cls_InteriorElevations/Views",windowFile);
   Window window; using(var stream=File.OpenRead(path)) window=(Window)XamlReader.Load(stream,new ParserContext { BaseUri=new Uri(path) });
   window.ShowInTaskbar=false; window.Show(); Drain(); window.UpdateLayout();
   var handle=new WindowInteropHelper(window).Handle;
   var points=new[]{new Point(3,window.ActualHeight/2),new Point(window.ActualWidth-3,window.ActualHeight/2),new Point(window.ActualWidth/2,3),new Point(window.ActualWidth/2,window.ActualHeight-3),new Point(6,6),new Point(window.ActualWidth-6,6),new Point(6,window.ActualHeight-6),new Point(window.ActualWidth-6,window.ActualHeight-6)};
   int[] expected={10,11,12,15,13,14,16,17};
   for(int i=0;i<points.Length;i++) { Point p=window.PointToScreen(points[i]); var packed=new IntPtr(((int)p.Y<<16)|((int)p.X&0xffff)); int hit=SendMessage(handle,0x84,IntPtr.Zero,packed).ToInt32(); Check(hit==expected[i],"Resize edge "+i+": "+hit); }
   Console.WriteLine("PASS all eight native resize directions");
   var tabs=(TabControl)window.FindName("SettingsTabControl");
   foreach(var size in new[]{new Size(699,1242),new Size(559,994),new Size(420,747),new Size(420,540),new Size(980,720)}) {
    window.Width=size.Width; window.Height=size.Height; Drain(); window.UpdateLayout();
    for(int i=0;i<tabs.Items.Count;i++) {
     tabs.SelectedIndex=i; Drain(); window.UpdateLayout();
     foreach(var grid in Children<AdaptiveSettingsGrid>(window).Where(g=>g.IsVisible)) {
      if(size.Width<780) foreach(FrameworkElement cell in grid.Children) Check(Grid.GetColumn(cell)==0,"Unstacked compact grid");
      else Check(grid.ColumnDefinitions.Skip(1).Any(c=>c.Width.Value>0),"Desktop columns not restored");
     }
     foreach(var field in Children<FrameworkElement>(window).Where(f=>(f is TextBox || f is ComboBox || f is RichTextBox || f is Button) && f.IsVisible && f.ActualWidth>0)) {
      Point p=field.TranslatePoint(new Point(),window); Check(p.X>=-1 && p.X+field.ActualWidth<=window.ActualWidth+1,"Horizontal overflow: "+field.GetType().Name+" "+field.Name+" "+p.X+" + "+field.ActualWidth);
     }
     if(size.Height>450) {
      var bmp=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32); bmp.Render(window); var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bmp)); using(var file=File.Create(Path.Combine(output,size.Width+"x"+size.Height+"-tab"+i+".png"))) png.Save(file);
     }
    }
    Console.WriteLine("PASS "+size+" all tabs, input bounds and layout");
   }
   window.Close(); app.Shutdown(); return 0;
  } catch(Exception e) { Console.WriteLine(e); return 1; }
 }
}
