using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SAB.InteriorElevations.Services.Selection;
using SAB.UI.Notifications;

namespace SAB.UiShowcase
{
    internal static class SelectionGuidanceEvidence
    {
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        static readonly List<string> log = new List<string>();
        static string output;
        static void Check(bool value,string message) { log.Add((value?"PASS ":"FAIL ")+message); if(!value) throw new InvalidOperationException(message); }
        static void Pump() => Elevations.ElevationPreview.Pump(220);
        static void Shot(FrameworkElement visual,string name)
        {
            visual.UpdateLayout();Pump();
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth),(int)Math.Ceiling(visual.ActualHeight),96,96,PixelFormats.Pbgra32);
            var drawing=new DrawingVisual();
            using(var context=drawing.RenderOpen()) context.DrawRectangle(new VisualBrush(visual),null,new Rect(0,0,visual.ActualWidth,visual.ActualHeight));
            bitmap.Render(drawing);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(output,name+".png")))encoder.Save(stream);
            log.Add("CAPTURE "+name+"; actualType="+visual.GetType().FullName+"; assembly="+typeof(NotificationStack).Assembly.Location+"; sizeDIP="+visual.ActualWidth+"x"+visual.ActualHeight+"; nativeDPI="+VisualTreeHelper.GetDpi(visual).PixelsPerInchX+"; renderDPI=96");
        }
        internal static int Run(string directory)
        {
            output=directory;Directory.CreateDirectory(output);log.Clear();
            Window owner=null;
            try
            {
                Check(Application.Current.Resources.MergedDictionaries.Count==0,"Actual notification styles are fully scoped");
                owner=new Window{Title="SAB selection-guidance test owner",Width=360,Height=180,ShowInTaskbar=false};owner.Show();owner.Activate();Pump();
                var ownerHandle=new WindowInteropHelper(owner).Handle;
                var unrelated=new NotificationMessage("SAB · Развёртки","Сообщение",NotificationTone.Information,"Другое уведомление остаётся доступным.");
                var stack=NotificationStack.Show(ownerHandle,unrelated);Pump();
                var overview=ElevationSelectionGuidance.Overview();
                Check(overview.Summary.Contains("Escape")&&overview.Summary.Contains("Затем выберите помещение"),"Overview explains lines then Escape then room");
                var room=ElevationSelectionGuidance.Room();Check(room.Summary=="Линии выбраны, выберите помещение.","Room reminder matches exact user text");
                var stages=new[]{overview,ElevationSelectionGuidance.Group(1),ElevationSelectionGuidance.GroupCompleted(1),ElevationSelectionGuidance.Group(2),room};
                var names=new[]{"overview","group-1","group-1-completed","group-2","room"};
                for(int index=0;index<stages.Length;index++)
                {
                    IntPtr foreground=GetForegroundWindow();
                    ElevationSelectionGuidance.Show(ownerHandle,stages[index]);Pump();
                    Check(GetForegroundWindow()==foreground,"Guide does not steal native activation: "+names[index]);
                    Check(stack.Cards.Count==2&&stack.Cards.Any(card=>ReferenceEquals(card.Message,unrelated)),"Only previous guide replaced; unrelated notice retained: "+names[index]);
                    var guide=stack.Cards.Single(card=>ReferenceEquals(card.Message,stages[index]));
                    Check(guide.ActualWidth<=416&&guide.ActualHeight>100,"Guide text rendered within shared width: "+names[index]);
                    var summary=Elevations.ElevationPreview.Visual<TextBox>(guide).Single(box=>box.Text==stages[index].Summary);
                    var summaryOrigin=summary.TranslatePoint(new Point(),guide);
                    Check(summary.IsVisible&&summaryOrigin.Y>=0&&summaryOrigin.Y+summary.ActualHeight<=guide.ActualHeight,"Actual summary visible and fully inside captured card: "+names[index]);
                    Check(stages[index].Tone==NotificationTone.Information&&stages[index].ElementIds==null&&stages[index].Support==null,"Guide has information tone without result diagnostics: "+names[index]);
                    Shot(guide,names[index]);
                }
                Check(stages[1].Outcome.Contains("№1")&&stages[3].Outcome.Contains("№2"),"Repeated groups receive their current number");
                Check(stages[2].Summary.Contains("следующую группу")&&stages[2].Summary.Contains("завершите выбор групп"),"Group completion offers next group before room transition");
                Shot((FrameworkElement)stack.Content,"stack");
                ElevationSelectionGuidance.Dismiss();Pump();
                Check(stack.Cards.Count==1&&ReferenceEquals(stack.Cards[0].Message,unrelated),"Completion/cancel dismisses only own guide");
                ElevationSelectionGuidance.Dismiss();Pump();Check(stack.Cards.Count==1,"Repeated guide cleanup is harmless");
                stack.Dismiss(unrelated);Pump();Check(!stack.IsVisible,"Dismissing last specific notice releases stack");
                log.Add("COMPLETED; actual shared WPF with selection-stage DTO fixtures; actual Revit picking, physical input and OS DPI transitions NOT RUN.");return 0;
            }
            finally
            {
                ElevationSelectionGuidance.Dismiss();NotificationStack.CloseAll();if(owner!=null)owner.Close();
                File.WriteAllLines(Path.Combine(output,"verification.txt"),log);
            }
        }
    }
}
