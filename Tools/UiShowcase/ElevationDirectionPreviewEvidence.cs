using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SAB.InteriorElevations.Views;

namespace SAB.UiShowcase
{
    internal static class ElevationDirectionPreviewEvidence
    {
        static readonly List<string> log = new List<string>();
        static string output;
        static void Check(bool value,string message) {log.Add((value?"PASS ":"FAIL ")+message);if(!value)throw new InvalidOperationException(message);}
        static void Pump()=>Elevations.ElevationPreview.Pump(220);
        static T Find<T>(Window w,string name) where T:FrameworkElement => Elevations.ElevationSession.Find<T>(w,name);
        static void Shot(Window w,string name)
        {
            w.UpdateLayout();Pump();var body=(FrameworkElement)w.Content;
            var bmp=new RenderTargetBitmap((int)Math.Ceiling(body.ActualWidth),(int)Math.Ceiling(body.ActualHeight),96,96,PixelFormats.Pbgra32);bmp.Render(body);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using(var file=File.Create(Path.Combine(output,name+".png")))encoder.Save(file);
            log.Add("CAPTURE "+name+"; actualType="+w.GetType().FullName+"; assembly="+w.GetType().Assembly.Location+"; sizeDIP="+body.ActualWidth+"x"+body.ActualHeight+"; nativeDPI="+VisualTreeHelper.GetDpi(body).PixelsPerInchX+"; renderDPI=96");
        }
        internal static int Run(string directory)
        {
            output=directory;Directory.CreateDirectory(output);log.Clear();
            string prefs=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SAB","WindowSizes");
            var original=Directory.Exists(prefs)?Directory.GetFiles(prefs,"InteriorElevations*.json").ToDictionary(p=>p,File.ReadAllBytes):new Dictionary<string,byte[]>();
            ElevationSettingsWindow settings=null;ElevationDirectionPreviewWindow preview=null;
            try
            {
                settings=new ElevationSettingsWindow(RedesignProduction.Settings(),true,"Помещение А-101 · линий: 8",""){ShowInTaskbar=false};settings.Show();settings.Width=1100;settings.Height=880;Pump();
                var show=Find<Button>(settings,"ShowDirectionPreviewButton");show.BringIntoView();Pump();
                Check(show.IsVisible&&show.IsEnabled,"Preview entry enabled after line selection");
                Check(Find<TextBox>(settings,"DetailNumberPrefixEditor")!=null && Find<TextBox>(settings,"DetailNumberSuffixEditor")!=null,"Existing numbering affix editors retained");
                Check(Elevations.ElevationPreview.Visual<Button>(settings).Count(b=>b.IsVisible&&Equals(b.Content,"Инструкция"))==1,"Settings retains one Instruction footer");
                CheckFooter(settings);
                Shot(settings,"settings-preview-entry");settings.Close();settings=null;
                settings=new ElevationSettingsWindow(RedesignProduction.Settings(),true,"Помещение А-101 · линий: 8",""){ShowInTaskbar=false};
                settings.Show();settings.Width=settings.MinWidth;settings.Height=settings.MinHeight;Pump();CheckFooter(settings);
                Shot(settings,"settings-minimum-footer");
                typeof(ElevationSettingsWindow).GetMethod("SetWarningInfoText",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(settings,new object[]{string.Join(" ",Enumerable.Repeat("Не удалось подготовить выбранные линии помещения: проверьте настройки и повторите действие.",8))});Pump();
                CheckFooter(settings);Shot(settings,"settings-minimum-validation");settings.Close();settings=null;
                settings=new ElevationSettingsWindow(RedesignProduction.Settings(),true,"Помещение А-101 · линий: 8",""){ShowInTaskbar=false};
                var modal=settings;
                modal.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>Find<Button>(modal,"ShowDirectionPreviewButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
                bool? accepted=modal.ShowDialog();
                Check(accepted==true && modal.RequestedAction==ElevationSettingsWindowAction.ShowDirectionPreview && modal.SelectedSettings!=null,"Footer preview action returns prepared settings through actual modal window");settings=null;
                settings=new ElevationSettingsWindow(RedesignProduction.Settings()){ShowInTaskbar=false};settings.Show();Pump();
                Check(!Find<Button>(settings,"ShowDirectionPreviewButton").IsEnabled,"Preview entry disabled without selected lines");settings.Close();settings=null;
                preview=new ElevationDirectionPreviewWindow("Временных марок: 8. Марок по виртуальному замыканию: 3. Без определённой внутренней стороны: 2."){ShowInTaskbar=false};
                int flip=0,create=0,cancel=0;preview.FlipRequested+=()=>flip++;preview.CreateRequested+=()=>create++;preview.CancelRequested+=()=>cancel++;
                preview.Show();Pump();
                var buttons=Elevations.ElevationPreview.Visual<Button>(preview).Where(b=>b.IsVisible).ToList();
                Check(buttons.Count==4,"Compact preview has Flip, Instruction, Cancel and Create only");
                Check(buttons.All(b=>b.ActualHeight>=34),"All preview action targets at least 34 DIP");
                Check(Application.Current.Resources.MergedDictionaries.Count==0,"Actual windows load scoped resources without Revit application dictionary");
                Check(SAB.UI.FluentLight.Instructions.GetFile(Find<Button>(preview,"PreviewHelpButton"))=="SAB_HTML_Sozdanie_i_korrektirovka_razvertok.html","Preview Instruction points to packaged local guide");
                Find<Button>(preview,"FlipPreviewButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Find<Button>(preview,"CreatePreviewButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Find<Button>(preview,"CancelPreviewButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(flip==1&&create==1&&cancel==1,"Actual UI buttons emit distinct controller actions");
                var flipButton=Find<Button>(preview,"FlipPreviewButton");flipButton.Focus();Pump();
                Check(flipButton.IsKeyboardFocused,"Flip action accepts keyboard focus");
                flipButton.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));Pump();
                Check(Find<Button>(preview,"PreviewHelpButton").IsKeyboardFocused,"Tab traversal reaches Instruction after Flip");
                Shot(preview,"preview-normal");
                preview.SetStatus("Ожидание Revit…",true);Pump();
                Check(preview.IsBusy&&!Find<Button>(preview,"FlipPreviewButton").IsEnabled&&!Find<Button>(preview,"CreatePreviewButton").IsEnabled&&!Find<Button>(preview,"CancelPreviewButton").IsEnabled,"Pending action disables duplicate commands");
                preview.Close();Pump();Check(preview.IsVisible&&cancel==1,"Window X is held while cleanup request is pending");Shot(preview,"preview-busy");
                preview.Width=preview.MinWidth;
                preview.SetStatus(string.Join(" ",Enumerable.Repeat("Не удалось удалить временные марки направлений: элемент занят другим пользователем. Повторите отмену после освобождения элемента.",14)));Pump();
                var status=Find<TextBlock>(preview,"PreviewStatus");
                var scroll=Elevations.ElevationPreview.Visual<ScrollViewer>(preview).First(s=>s.Content==status);
                Check(scroll.ActualHeight<=180.1&&scroll.ScrollableHeight>0,"Long Russian failure message has bounded vertical scrolling");
                Check(Find<Button>(preview,"CreatePreviewButton").IsVisible && preview.ActualHeight<600,"Minimum width retains footer with long status");
                Check(status.TextWrapping==TextWrapping.Wrap && status.ActualWidth<preview.ActualWidth,"Failure text wraps inside preview");Shot(preview,"preview-minimum-error");
                preview.Close();Pump();Check(preview.IsVisible&&cancel==2,"Window X requests cleanup before it can close");
                preview.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(preview),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});
                Check(cancel==3&&preview.IsVisible,"Escape requests cancellation without bypassing cleanup");
                preview.CloseAfterCleanup();Pump();Check(!preview.IsVisible,"Controller cleanup completion closes actual window");
                log.Add("COMPLETED; actual SAB.dll WPF; simulated routed input only; live Revit/RFA placement and physical input/OS DPI transitions NOT RUN.");return 0;
            }
            finally
            {
                if(settings!=null)settings.Close();if(preview!=null&&preview.IsVisible)preview.CloseAfterCleanup();
                if(Directory.Exists(prefs))foreach(string p in Directory.GetFiles(prefs,"InteriorElevations*.json"))if(original.ContainsKey(p))File.WriteAllBytes(p,original[p]);else File.Delete(p);
                File.WriteAllLines(Path.Combine(output,"verification.txt"),log);
            }
        }

        static void CheckFooter(ElevationSettingsWindow window)
        {
            var expected=new[]{"Инструкция","Список помещений","Показать превью","Выбрать линии","Создать развёртки"};
            var buttons=Elevations.ElevationPreview.Visual<Button>(window).Where(b=>b.IsVisible&&expected.Contains(b.Content as string)).OrderBy(b=>b.TranslatePoint(new Point(),window).X).ToList();
            Check(buttons.Count==5&&buttons.Select(b=>b.Content as string).SequenceEqual(expected),"Five footer actions match requested order");
            Check(!Elevations.ElevationPreview.Visual<Button>(window).Any(b=>b.Name=="CancelButton"),"Main Cancel action removed; preview cancellation retained separately");
            Check(buttons.Skip(2).All(b=>b.Style==window.FindResource("SabPrimaryButtonStyle")),"Preview, Pick lines and Create use shared blue primary style");
            Check(buttons.Take(2).All(b=>b.Style!=window.FindResource("SabPrimaryButtonStyle")),"Instruction and room list retain neutral white style");
            var positions=buttons.Select(b=>b.TranslatePoint(new Point(),window)).ToList();
            Check(positions.Max(p=>p.Y)-positions.Min(p=>p.Y)<1 && buttons.All(b=>b.ActualHeight>=34),"Footer remains one row with 34 DIP minimum targets");
            Check(buttons.Select((b,i)=>positions[i].X>=0&&positions[i].X+b.ActualWidth<=window.ActualWidth).All(ok=>ok),"All five actions stay inside minimum window width");
            Check(Enumerable.Range(1,4).All(i=>positions[i].X-positions[i-1].X-buttons[i-1].ActualWidth>=7.9),"Footer buttons keep at least 8 DIP horizontal gaps");
        }
    }
}
