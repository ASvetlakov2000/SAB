using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Reports;
using SAB.InteriorElevations.Views;
using SAB.UI.FluentLight;
using SAB.UI.Notifications;
using Point = System.Windows.Point;
using Grid = System.Windows.Controls.Grid;

namespace SAB.UiShowcase
{
    // This bootstrap deliberately has no host/legacy Application dictionaries.
    internal static class ElevationHotfixPreview
    {
        static readonly List<string> log=new List<string>();
        static string output;
        static IEnumerable<T> Visual<T>(DependencyObject root) where T:DependencyObject => Elevations.ElevationPreview.Visual<T>(root);
        static T Find<T>(Window w,string name) where T:FrameworkElement => Elevations.ElevationSession.Find<T>(w,name);
        static void Pump() => Elevations.ElevationPreview.Pump(180);
        static void Check(bool ok,string text) { log.Add((ok?"PASS ":"FAIL ")+text);File.WriteAllLines(Path.Combine(output,"verification.txt"),log);if(!ok)throw new InvalidOperationException(text); }
        static void Show(Window w) { w.ShowInTaskbar=false;w.Show();Pump();w.UpdateLayout(); }
        static void Shot(Window w,string name)
        {
            var body=(FrameworkElement)w.Content;body.UpdateLayout();
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(body.ActualWidth),(int)Math.Ceiling(body.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(body);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(output,name+".png")))encoder.Save(stream);
            log.Add("CAPTURE "+name+" | "+body.ActualWidth+"x"+body.ActualHeight+" DIP | nativeDpi="+VisualTreeHelper.GetDpi(body).PixelsPerInchX+" | actualType="+w.GetType().FullName+" | actualAssembly="+w.GetType().Assembly.GetName().Name);
        }
        static void Click(Window w,string name) => Find<Button>(w,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        static List<MultiRoomSelectionItem> Selections(int count)
        {
            var result=new List<MultiRoomSelectionItem>();
            for(int i=0;i<count;i++)
            {
                var row=new MultiRoomSelectionItem();
                var lines=Enumerable.Repeat<DetailLine>(null,i%7+4).ToList();
                row.ApplySelection(new List<List<DetailLine>>{lines},lines,new RoomData{RoomNumber=(i+1).ToString(),RoomName="Очень длинное название помещения №"+(i+1)+" · Спальня, переговорная и вспомогательная зона",RoomElementId=new ElementId(201+i)});
                result.Add(row);
            }
            return result;
        }
        static void Footer(ElevationSettingsWindow w)
        {
            var labels=new[]{"Инструкция","Отмена","Список помещений","Выбрать линии","Создать развёртки"};
            var buttons=labels.Select(label=>Visual<Button>(w).Single(b=>b.IsVisible&&Equals(b.Content,label))).ToArray();
            double right=-1;double y=buttons[0].TranslatePoint(new Point(0,0),w).Y;
            foreach(var button in buttons)
            {
                var p=button.TranslatePoint(new Point(),w);
                Check(p.X>=right&&Math.Abs(p.Y-y)<=1,"Footer order and common top axis: "+button.Content);
                Check(p.X>=0&&p.X+button.ActualWidth<=w.ActualWidth&&p.Y+button.ActualHeight<=w.ActualHeight,"Footer button fits actual viewport: "+button.Content);
                Check(Math.Abs(button.ActualHeight-34)<=1,"Footer common34DIP height: "+button.Content);right=p.X+button.ActualWidth;
            }
            for(int i=2;i<buttons.Length;i++)Check(Math.Abs(buttons[i].TranslatePoint(new Point(),w).X-buttons[i-1].TranslatePoint(new Point(buttons[i-1].ActualWidth,0),w).X-8)<=1,"Footer neighboring action gap8DIP");
            var status=Find<Border>(w,"WarningInfoBorder");
            if(status.IsVisible)Check(status.TranslatePoint(new Point(0,status.ActualHeight),w).Y+7<=y,"Nonempty footer status occupies separate row above actions");
            Check(Grid.GetRow(status)==0,"Footer status explicit row0");
            Check(!Find<Border>(w,"SelectionStatusBorder").IsVisible,"Long room/line selection summary is absent from main layout");
            Check(Visual<Button>(w).Count(b=>b.IsVisible&&labels.Contains(b.Content as string))==5,"Exactly five requested main footer buttons");
        }
        static void ListChecks(ElevationSelectionListWindow w,int count)
        {
            Check(w.GetType().Assembly==typeof(ElevationSettingsWindow).Assembly,"Read-only list is actual production SAB.dll class");
            var table=Find<DataGrid>(w,"RoomsDataGrid");Check(table.IsReadOnly&&!table.CanUserAddRows&&!table.CanUserDeleteRows,"Room list disables editing/add/delete");
            Check(table.Columns.Count==6&&table.Columns.All(c=>c is DataGridTextColumn),"Room list has six data columns and no action column");
            Check(table.Items.Count==count,"Room list retains every detached selected row: "+count);
            foreach(var header in Visual<System.Windows.Controls.Primitives.DataGridColumnHeader>(table).Where(h=>h.Column!=null&&h.Content is string))
            {
                var text=new System.Windows.Media.FormattedText((string)header.Content,System.Globalization.CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(header.FontFamily,header.FontStyle,header.FontWeight,header.FontStretch),header.FontSize,Brushes.Black,VisualTreeHelper.GetDpi(header).PixelsPerDip);
                Check(text.Width<=header.ActualWidth-header.Padding.Left-header.Padding.Right+1,"Full room-list header fits measured content width: "+header.Content);
            }
            Check(table.Parent is Border b&&b.Tag?.ToString()=="CollectionFrame","Read-only table owns its outline");
            Check(Visual<Button>(w).Where(b=>b.IsVisible).All(b=>Equals(b.Content,"Инструкция")||Equals(b.Content,"Закрыть")),"Read-only list has only instruction and close buttons");
            Check(w.Rows.All(row=>row.GetType().GetProperties().All(p=>p.PropertyType==typeof(int)||p.PropertyType==typeof(string))),"Snapshot property values are detached strings/counts, no Revit elements");
            Check(w.Rows.All(row=>row.GetType().GetProperties().All(p=>p.GetSetMethod()==null)),"Snapshot cannot be edited through public setters");
        }
        internal static int Run(string directory)
        {
            output=directory;Directory.CreateDirectory(output);log.Clear();
            string pref=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SAB","WindowSizes");
            var original=Directory.Exists(pref)?Directory.GetFiles(pref,"InteriorElevations*.json").ToDictionary(p=>p,File.ReadAllBytes):new Dictionary<string,byte[]>();
            try
            {
                Check(Application.Current.Resources.MergedDictionaries.Count==0,"Application starts with zero host/legacy resource dictionaries");
                var resources=FluentResources.CreateDictionary();
                foreach(string key in new[]{"SabBrush.PanelBackground","SabBrush.Border","SabBrush.BorderWeak","SabBrush.Text","SabBrush.TextSecondary","SabBrush.Accent","SabBrush.Success","SabBrush.Warning","SabBrush.Error","SabBrush.DisabledBackground","SabNotificationProgressStyle","SabNotificationTextStyle","SabNotificationCodeStyle"})Check(resources.Contains(key),"Notification resource resolves in scoped dictionary alone: "+key);
                foreach(var tone in new[]{NotificationTone.Success,NotificationTone.Warning,NotificationTone.Error})
                {
                    string copied=null;var message=new NotificationMessage("SAB · Развёртки",tone==NotificationTone.Success?"Завершено":tone==NotificationTone.Warning?"Завершено с предупреждениями":"Не удалось создать развёртки",tone,tone==NotificationTone.Error?"Операция не создала видов.":"Создано развёрток: 4.",tone==NotificationTone.Success?null:"482015, 482016",tone==NotificationTone.Success?null:"Причина: тип марки не выбран.",tone==NotificationTone.Success?null:"Не удалось разместить план-схему: тип видового экрана не выбран.");
                    var card=new NotificationCard(message,value=>copied=value);var w=new Window{Title="SAB · результат",Content=card,Width=416,SizeToContent=SizeToContent.Height,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,Background=Brushes.Transparent,AllowsTransparency=true};Show(w);
                    Check(card.Resources.MergedDictionaries.Count==1,"Actual card uses its own scoped dictionary: "+tone);
                    if(tone!=NotificationTone.Success){var copy=Visual<Button>(w).First(b=>Equals(b.Content,"Копировать"));copy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(copied==message.Warnings,"Clean-scope warning copy retains complete original warning: "+tone);}
                    Shot(w,"notification-clean-"+tone.ToString().ToLowerInvariant());w.Close();
                }
                int before=Application.Current.Windows.Count;
                try { NotificationStack.Show(IntPtr.Zero,null);throw new Exception("Expected invalid card failure"); } catch(ArgumentNullException) { Check(Application.Current.Windows.Count==before,"Failed new stack construction closes and removes incomplete window"); }
                var stack=NotificationStack.Show(IntPtr.Zero,new NotificationMessage("SAB · Развёртки","Завершено",NotificationTone.Success,"Создано развёрток: 4."));Pump();Check(stack.Cards.Count==1,"Successful notification recovers after failed construction without stale stack entry");stack.Close();
                var delivery=typeof(ElevationCreationReportService).GetMethod("DeliverNotificationSafely",BindingFlags.NonPublic|BindingFlags.Static);int shown=0,fallback=0;
                delivery.Invoke(null,new object[]{new Action(()=>shown++),new Action<Exception>(error=>fallback++)});Check(shown==1&&fallback==0,"Actual delivery uses primary on success");
                delivery.Invoke(null,new object[]{new Action(()=>{throw new InvalidOperationException("Injected resource failure");}),new Action<Exception>(error=>{Check(error.Message.Contains("Injected"),"Fallback receives original presentation failure");fallback++;})});Check(fallback==1,"Primary UI failure is contained after completed operation");
                delivery.Invoke(null,new object[]{new Action(()=>{throw new InvalidOperationException("Injected");}),new Action<Exception>(error=>{throw new IOException("Injected fallback/log failure");})});Check(true,"Secondary fallback/log failure does not escape to command failure path");
                var source=Selections(100);var snapshot=ElevationSelectionListItem.FromSelections(source);string name=snapshot[0].RoomName;int lines=snapshot[0].SelectedLinesCount;
                source[0].ApplySelection(new List<List<DetailLine>>(),new List<DetailLine>(),new RoomData{RoomName="Changed after snapshot"});Check(snapshot[0].RoomName==name&&snapshot[0].SelectedLinesCount==lines,"Actual selection snapshot retains original room/line values after live row changes");
                Check(snapshot.Count==100&&snapshot[99].Index==100&&snapshot.Sum(row=>row.SelectedLinesCount)>400,"Actual source selections map all rows and true line counts");
                var vm=RedesignProduction.Settings();vm.SetNamingPreviewContexts(Selections(100).Select(row=>new SAB.InteriorElevations.ViewModels.ElevationNamingPreviewContext{RoomData=row.RoomData,StartPointNumber=1,EndPointNumber=2}));var main=new ElevationSettingsWindow(vm,true,string.Join("; ",snapshot.Select(row=>row.RoomName)),"",snapshot);Show(main);Footer(main);Shot(main,"main-normal");
                main.Width=800;main.Height=650;Pump();Check(Math.Abs(main.ActualWidth-800)<=1,"Main width remains800DIP with100long room naming contexts");Footer(main);Shot(main,"main-minimum-many-selections");
                var tabs=Find<TabControl>(main,"SettingsTabControl");for(int i=0;i<tabs.Items.Count;i++){tabs.SelectedIndex=i;Pump();Footer(main);Shot(main,"main-minimum-tab-"+i);}
                typeof(ElevationSettingsWindow).GetMethod("SetWarningInfoText",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(main,new object[]{string.Join(Environment.NewLine,Enumerable.Repeat("Не выбран тип марки или видовой экран. Проверьте исходные данные текущего проекта.",18))});Pump();Footer(main);var warningScroll=Visual<ScrollViewer>(Find<Border>(main,"WarningInfoBorder")).First();Check(warningScroll.ScrollableHeight>0,"Long validation text scrolls within bounded footer row");Shot(main,"main-minimum-validation");
                typeof(ElevationSettingsWindow).GetMethod("SetWarningInfoText",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(main,new object[]{""});Pump();
                var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(150)};timer.Tick+=(sender,args)=>{var child=Application.Current.Windows.OfType<ElevationSelectionListWindow>().FirstOrDefault(w=>w.IsVisible);if(child==null)return;timer.Stop();ListChecks(child,100);Check(ReferenceEquals(child.Owner,main),"Footer list button opens read-only child owned by current settings");Shot(child,"list-from-footer");Click(child,"DoneButton");};timer.Start();Click(main,"SelectionListButton");Check(main.IsVisible&&ReferenceEquals(main.DataContext,vm),"Closing list returns same settings window/VM without selection or creation action");main.Close();
                foreach(int count in new[]{0,3,100})
                {
                    var rows=ElevationSelectionListItem.FromSelections(Selections(count));var w=new ElevationSelectionListWindow(rows);Show(w);ListChecks(w,count);Shot(w,"list-"+count+"-normal");w.Width=w.MinWidth;w.Height=w.MinHeight;Pump();ListChecks(w,count);var table=Find<DataGrid>(w,"RoomsDataGrid");if(count>0){table.ScrollIntoView(table.Items[count-1],table.Columns.Last());Pump();var last=Visual<DataGridRow>(table).First(row=>ReferenceEquals(row.Item,table.Items[count-1]));var rowAt=last.TranslatePoint(new Point(),w);var tableAt=table.TranslatePoint(new Point(),w);Check(rowAt.Y>=tableAt.Y+table.ColumnHeaderHeight-1&&rowAt.Y+last.ActualHeight<=tableAt.Y+table.ActualHeight+1,"Last selected room fully fits below header at minimum: "+count);var fullName=Visual<TextBlock>(last).FirstOrDefault(t=>t.Text==rows[count-1].RoomName);if(fullName!=null)Check(Equals(fullName.ToolTip,fullName.Text),"Truncated long room name exposes full actual text via tooltip");}Shot(w,"list-"+count+"-minimum");w.Close();
                }
                Check(Application.Current.Resources.MergedDictionaries.Count==0,"All actual surfaces completed without application legacy dictionaries");
                log.Add("NOT RUN actual Revit creation/transactions/rollback/native picking; physical keyboard/accessibility; OS DPI/monitor transitions.");log.Add("COMPLETED");return 0;
            }
            catch(Exception error){log.Add("ERROR "+error);return 1;}
            finally
            {
                foreach(var w in Application.Current.Windows.Cast<Window>().ToArray())w.Close();
                if(Directory.Exists(pref))foreach(string path in Directory.GetFiles(pref,"InteriorElevations*.json"))if(!original.ContainsKey(path))File.Delete(path);
                foreach(var entry in original)File.WriteAllBytes(entry.Key,entry.Value);
                File.WriteAllLines(Path.Combine(output,"verification.txt"),log);
            }
        }
    }
}

