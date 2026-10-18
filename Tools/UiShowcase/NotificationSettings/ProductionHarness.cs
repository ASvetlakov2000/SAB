using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SAB.Notifications;

namespace SAB.NotificationSettingsProduction
{
    public static class Program
    {
        static string root, output, captures;
        static readonly List<string> log=new List<string>();
        static int checks, images;
        [STAThread] public static int Main(string[] args)
        {
            root=FindRoot();output=Path.Combine(root,@"outputs\ui-production-preview\notification-settings\production");
            captures=Path.Combine(root,@"Docs\Ui\DesignSystem\screenshots-notification-settings\production");
            Directory.CreateDirectory(output);
            try {
                var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
                if(args.Contains("--capture")) {Core();Visual();WriteProof();return 0;}
                using(var mutex=new Mutex(true,@"Local\SAB.NotificationSettings.ProductionHost",out bool owner))
                using(var activate=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\SAB.NotificationSettings.ProductionActivate")) {
                    if(!owner) {activate.Set();return 0;}
                    try {
                        var store=new NotificationSettingsStore(Path.Combine(output,"interactive-profile"));
                        var window=new SettingsWindow(store,store.Load());
                        window.Title+=" · просмотр";
                        app.MainWindow=window;app.ShutdownMode=ShutdownMode.OnMainWindowClose;
                        var listener=ThreadPool.RegisterWaitForSingleObject(activate,(s,timeout)=>app.Dispatcher.BeginInvoke(new Action(()=> {
                            if(window.WindowState==WindowState.Minimized)window.WindowState=WindowState.Normal;
                            window.Show();window.Activate();
                        })),null,Timeout.Infinite,false);
                        try {return app.Run(window);}finally{listener.Unregister(null);}
                    } finally {mutex.ReleaseMutex();}
                }
            } catch(Exception ex) {
                log.Add("FAIL: "+ex);File.WriteAllLines(Path.Combine(output,"verification.txt"),log);
                File.WriteAllText(Path.Combine(output,"error.txt"),ex.ToString());return 1;
            }
        }
        static void Core()
        {
            log.Add("Actual production SAB.dll classes; isolated fixture profiles only. No Revit execution, installation or physical/OS-DPI checks.");
            string home=Path.Combine(output,"test-profile");Directory.CreateDirectory(home);
            File.WriteAllText(Path.Combine(home,"spool.jsonl"),"");
            string config=Path.Combine(home,"settings.json");
            // Each scenario overwrites only this fixed test profile; never the user's LocalAppData.
            File.WriteAllText(config,"null");
            var store=new NotificationSettingsStore(home);var settings=store.Load();
            Check("null settings disabled",!settings.Enabled);
            var fresh=new NotificationSettingsStore(Path.Combine(output,"fresh-"+Guid.NewGuid().ToString("N")));
            Check("first launch is OFF and performs no profile write",!fresh.Load().Enabled&&!Directory.Exists(fresh.Home));
            Check("fresh model and all rules are OFF",!new SettingsModel(fresh,fresh.Load()).Enabled&&new SettingsModel(fresh,fresh.Load()).Rules.All(r=>r.Action==Rule.Show));
            File.WriteAllText(config,"{\"Enabled\":true,\"SkipOrdinary\":true,\"SkipImportant\":true,\"DialogResults\":{\"any\":1}}");
            Check("legacy blanket opt-in cannot enable new settings",!store.Load().Enabled&&store.Load().Rules.Count==0);
            Check("loading legacy does not rewrite user's file",File.ReadAllText(config).Contains("SkipImportant"));
            File.WriteAllText(config,"{broken");Check("corrupt config disabled",!store.Load().Enabled);
            File.WriteAllText(config,"{\"SchemaVersion\":99,\"Enabled\":true}");Check("unknown schema disabled",!store.Load().Enabled);
            settings=new Settings {SchemaVersion=2,Enabled=false,JournalFolder=Path.Combine(home,"export")};store.Save(settings);
            int applied=0;Settings runtime=store.Load();
            var model=new SettingsModel(store,runtime,next=>{runtime=next;applied++;});
            model.Enabled=true;model.FamilyException=false;model.GroupException=false;model.WorksharedException=false;
            model.Rules[0].Action=Rule.Toast;model.Rules[1].Action=Rule.Journal;
            Check("draft changes leave runtime and disk OFF",!runtime.Enabled&&!store.Load().Enabled&&applied==0);
            model.Save();Check("explicit Save enables and persists all preferences",runtime.Enabled&&store.Load().Enabled&&!store.Load().FamilyException&&!store.Load().GroupException&&!store.Load().WorksharedException&&applied==1);
            Check("stable per-rule actions round trip",store.Load().Rules["C01"].Action=="Toast"&&store.Load().Rules["C08"].Action=="Journal");
            Check("saved opt-in survives restart",new SettingsModel(store,store.Load()).Enabled);
            string prior=File.ReadAllText(config);
            using(var locked=new FileStream(config,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) {
                model.Enabled=false;model.Save();
                Check("failed Save keeps runtime and draft and reports error",runtime.Enabled&&model.Dirty&&model.Status.StartsWith("Не удалось сохранить")&&applied==1);
            }
            Check("atomic failure preserves prior settings and removes temporary file",File.ReadAllText(config)==prior&&!Directory.GetFiles(home,"*.tmp").Any());
            foreach(var rule in model.Rules.Where(r=>r.Protected||r.Unknown||!r.HasOk))rule.Action=Rule.Toast;
            Check("protected unknown and no-confirmation cannot select suppression",model.Rules.Where(r=>r.Protected||r.Unknown||!r.HasOk).All(r=>r.Action==Rule.Show&&r.Actions.Length==1));
            var deletion=model.Rules.First(r=>r.Code=="D01");deletion.Action=Rule.Journal;
            Check("dependent deletion requires explicit consent",!model.CanSave&&deletion.Validation.Length>0);
            deletion.Consent=true;Check("consent releases validation",model.CanSave);deletion.Action=Rule.Show;
            Check("show revokes deletion consent",!deletion.Consent);
            model.Duration="61";Check("duration validation",!model.CanSave);model.Duration="10";
            model.Folder="C:relative";Check("drive-relative folder rejected",!model.CanSave);model.Folder=settings.JournalFolder;
            string text="<script>alert('x')</script> Исходное сообщение";
            var row=new {Time="2026-10-18T12:00:00+03:00",Failure="fixture-unknown",Severity="Error",Text=text,Model="fixture.rvt",Ids="123,456",Action="Оставлено Revit"};
            var serializer=new JavaScriptSerializer();
            File.WriteAllLines(Path.Combine(home,"spool.jsonl"),new[]{serializer.Serialize(row),"{truncated",serializer.Serialize(row)});
            var journalModel=new SettingsModel(store,store.Load());
            Check("journal reads actual records deduplicates and skips malformed line",journalModel.Log.Count==1&&journalModel.Log[0].Name==text);
            Check("observed unknown notification is visible and cannot be suppressed",journalModel.Rules.Any(r=>r.Code=="fixture-unknown"&&r.Unknown&&!r.CanEdit));
            string unsaved=Path.Combine(home,"unsaved-export-"+Guid.NewGuid().ToString("N"));journalModel.Folder=unsaved;
            string exported=journalModel.ExportJournal();
            Check("export uses committed folder and escapes source text",!Directory.Exists(unsaved)&&exported.StartsWith(settings.JournalFolder)&&File.ReadAllText(exported).Contains("&lt;script&gt;"));
            Check("draft rule clicks never manufacture journal entries",model.Log.Count==0);
        }
        static void Visual()
        {
            Directory.CreateDirectory(captures);
            string home=Path.Combine(output,"visual-profile");Directory.CreateDirectory(home);
            var store=new NotificationSettingsStore(home);
            File.WriteAllText(Path.Combine(home,"settings.json"),"{broken");
            var recovery=new SettingsWindow(store,store.Load());recovery.Show();Pump();
            Check("corrupt settings recovery is OFF and error red",!recovery.Model.Enabled&&recovery.Model.Status.Length>0&&((SolidColorBrush)recovery.Model.StatusBrush).Color==((SolidColorBrush)recovery.FindResource("SabBrush.Error")).Color);
            Capture(recovery,"00-corrupt-settings");recovery.Close();
            File.WriteAllText(Path.Combine(home,"settings.json"),"{\"Enabled\":true}");
            recovery=new SettingsWindow(store,store.Load());recovery.Show();Pump();
            Check("legacy migration notice is neutral and OFF",!recovery.Model.Enabled&&((SolidColorBrush)recovery.Model.StatusBrush).Color==((SolidColorBrush)recovery.FindResource("SabBrush.TextSecondary")).Color);
            Capture(recovery,"00-legacy-settings");recovery.Close();
            store.Save(new Settings {SchemaVersion=2,JournalFolder=Path.Combine(home,"Journal")});
            File.WriteAllText(Path.Combine(home,"spool.jsonl"),"");
            var w=new SettingsWindow(store,store.Load());w.Show();Pump();
            try {
                Check("actual window is owned by production SAB assembly",w.GetType().Assembly.GetName().Name=="SAB");
                Check("exactly Rules and Journal tabs",Get<TabControl>(w,"Tabs").Items.Count==2);
                var toggle=Get<ToggleButton>(w,"EnabledToggle");toggle.ApplyTemplate();
                var track=(FrameworkElement)toggle.Template.FindName("Track",toggle);
                var knob=(FrameworkElement)toggle.Template.FindName("Knob",toggle);
                Check("large switch track56x32 knob24 hit64x40",track.ActualWidth==56&&track.ActualHeight==32&&knob.ActualWidth==24&&toggle.ActualWidth==64&&toggle.ActualHeight==40);
                Geometry(w);Capture(w,"01-default-off");
                toggle.Focus();Pump();Check("keyboard focus ring visible",((Border)toggle.Template.FindName("FocusRing",toggle)).BorderBrush==w.FindResource("SabBrush.Accent"));Capture(w,"02-switch-focus");
                toggle.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(w),0,Key.Space){RoutedEvent=Keyboard.KeyDownEvent});
                toggle.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(w),0,Key.Space){RoutedEvent=Keyboard.KeyUpEvent});Pump();
                Check("native routed Space toggles focused switch",w.Model.Enabled);
                toggle.IsEnabled=false;Pump();Check("disabled switch dims and refuses automation toggle",toggle.Opacity==0.5&&!toggle.IsEnabled);Capture(w,"02-switch-disabled");
                toggle.IsEnabled=true;w.Model.Enabled=false;Pump();
                ((IToggleProvider)new ToggleButtonAutomationPeer(toggle).GetPattern(PatternInterface.Toggle)).Toggle();Pump();
                Check("switch toggle edits draft only",w.Model.Enabled&&!store.Load().Enabled);
                w.Model.Selected.Action=Rule.Toast;Pump();Capture(w,"03-pending-rule");
                var actions=Get<ComboBox>(w,"ActionBox");actions.IsDropDownOpen=true;Pump();
                var popup=(Popup)actions.Template.FindName("PART_Popup",actions);
                Check("three complete action labels in native dropdown",actions.Items.Count==3&&popup.IsOpen&&actions.Items.Cast<string>().All(s=>s.StartsWith("Подавлять")||s==Rule.Show));
                CaptureElement((FrameworkElement)popup.Child,"04-actions-popup");actions.IsDropDownOpen=false;Pump();
                foreach(string code in new[]{"SKETCH","OWNERSHIP","NONMODAL","UNKNOWN"}) {
                    w.Model.Selected=w.Model.Rules.First(r=>r.Code==code);Pump();
                    Check(code+" action unavailable",!actions.IsEnabled);Capture(w,"05-"+code.ToLowerInvariant());
                }
                var delete=w.Model.Rules.First(r=>r.Code=="D01");w.Model.Selected=delete;delete.Action=Rule.Journal;Pump();
                Check("save disabled until deletion consent",!Get<Button>(w,"SaveButton").IsEnabled);Capture(w,"06-delete-validation");
                delete.Consent=true;Pump();Capture(w,"07-delete-consent");
                w.Model.Search="nothing-matches-fixture";var search=Get<TextBox>(w,"SearchBox");search.Focus();
                var enter=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(w),0,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent};
                search.RaiseEvent(enter);Pump();
                Check("Enter applies search empty state and clears detail",w.Model.View.IsEmpty&&w.Model.Selected==null);Capture(w,"08-empty");
                w.Model.ResetSearchCommand.Execute(null);
                w.Model.FamilyException=false;w.Model.GroupException=true;w.Model.WorksharedException=false;Pump();
                Check("context exclusions independent",!w.Model.FamilyException&&w.Model.GroupException&&!w.Model.WorksharedException);Capture(w,"09-context-options");
                Get<TabControl>(w,"Tabs").SelectedIndex=1;Pump();Capture(w,"10-journal-empty");
                w.Model.Duration="2";Pump();Check("invalid duration disables save and shows error",!Get<Button>(w,"SaveButton").IsEnabled&&Get<TextBlock>(w,"FooterStatus").IsVisible);Capture(w,"11-validation");w.Model.Duration="10";
                Get<TabControl>(w,"Tabs").SelectedIndex=0;w.Model.Selected=w.Model.Rules[0];w.Width=w.MinWidth;w.Height=w.MinHeight;Pump();Geometry(w);Capture(w,"12-minimum");
                var longRule=w.Model.Rules[1];longRule.Name="Объединение смежных областей вида с очень длинным русским названием для проверки читаемости настроек";longRule.Description=string.Join("\n",Enumerable.Repeat("Длинное сообщение Revit: полный текст доступен для чтения и выделения. Проверяем прокрутку и доступность настроек.",20));
                w.Model.Selected=longRule;Pump();
                Check("long text bounded and detail remains scrollable",All<TextBox>(Get<StackPanel>(w,"DetailPanel")).First(box=>box.IsReadOnly&&box.MaxHeight==150).ActualHeight<=150&&Get<ScrollViewer>(w,"DetailScroll").ScrollableHeight>0);
                Capture(w,"13-long-text");
                w.Width=1160;w.Height=820;w.Model.Selected=w.Model.Rules[0];Pump();
                Click(Get<Button>(w,"SaveButton"));Check("Save commits enabled without closing window",store.Load().Enabled&&!w.Model.Dirty&&w.IsVisible);Capture(w,"14-saved");
                string saved=File.ReadAllText(Path.Combine(home,"settings.json"));w.Model.Enabled=false;Click(Get<Button>(w,"CancelButton"));
                Check("Cancel closes without saving",!w.IsVisible&&File.ReadAllText(Path.Combine(home,"settings.json"))==saved);
            } finally {w.Close();}
            var serializer=new JavaScriptSerializer();
            File.WriteAllLines(Path.Combine(home,"spool.jsonl"),new[] {
                serializer.Serialize(new {Time="2026-10-18T12:01:02+03:00",Text="Исходное уведомление Revit из тестовой записи журнала",Failure="b1c8f862-00fc-4046-b92c-6ca64d53384f",Model="Тестовая модель.rvt",Ids="123,456",Action="Не подавлено; передано Revit"}),
                serializer.Serialize(new {Time="2026-10-18T12:02:03+03:00",Text="Неизвестное сообщение с длинным текстом, которое остаётся доступным пользователю Revit",Failure="fixture-dialog",Model="",Ids="",Action="Не подавлено; автоматическое подтверждение не проверено"})
            });
            w=new SettingsWindow(store,store.Load());w.Show();Get<TabControl>(w,"Tabs").SelectedIndex=1;Pump();Capture(w,"15-journal-records");
            var textCell=All<TextBlock>(Get<DataGrid>(w,"JournalGrid")).First(block=>block.Text.StartsWith("Не подавлено; автоматическое"));
            Check("journal ellipsis retains full accessible tooltip",Equals(textCell.ToolTip,textCell.Text));
            string last=File.ReadAllText(Path.Combine(home,"settings.json"));w.Model.Enabled=false;
            w.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(w),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});Pump();
            Check("Escape closes draft without saving",!w.IsVisible&&File.ReadAllText(Path.Combine(home,"settings.json"))==last);w.Close();
        }
        static void Geometry(SettingsWindow w)
        {
            var grid=Get<DataGrid>(w,"RulesGrid");var viewer=All<ScrollViewer>(grid).First(v=>v.Name=="DG_ScrollViewer");
            Check("rules fit without horizontal scrollbar",viewer.ScrollableWidth<1);
            var bar=All<ScrollBar>(grid).First(b=>b.Orientation==Orientation.Vertical);
            var header=All<DataGridColumnHeadersPresenter>(grid).First();
            Check("table header38 and rows44",header.ActualHeight==38&&All<DataGridRow>(grid).First().ActualHeight==44);
            Check("scrollbar begins below header",Bounds(bar,grid).Top>=Bounds(header,grid).Bottom-1);
            var surface=(FrameworkElement)w.Content;
            Check("footer fully within viewport",Bounds(Get<Button>(w,"SaveButton"),surface).Bottom<=surface.ActualHeight);
            Check("table and detail separated",Bounds(Get<Border>(w,"RulesFrame"),surface).Right+19<=Bounds(Get<ScrollViewer>(w,"DetailScroll"),surface).Left);
        }
        static T Get<T>(SettingsWindow w,string name) where T:class => (T)w.FindName(name);
        static Rect Bounds(FrameworkElement e,Visual ancestor)=>e.TransformToAncestor(ancestor).TransformBounds(new Rect(0,0,e.ActualWidth,e.ActualHeight));
        static void Click(Button b) {((IInvokeProvider)new ButtonAutomationPeer(b).GetPattern(PatternInterface.Invoke)).Invoke();Pump();}
        static void Check(string name,bool pass) {checks++;log.Add((pass?"PASS: ":"FAIL: ")+name);if(!pass)throw new InvalidOperationException(name);}
        static void Pump() {var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(150)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
        static void Capture(SettingsWindow w,string name)=>CaptureElement((FrameworkElement)w.Content,name);
        static void CaptureElement(FrameworkElement element,string name)
        {
            element.UpdateLayout();int width=(int)Math.Ceiling(element.ActualWidth),height=(int)Math.Ceiling(element.ActualHeight);
            if(width<50||height<30)throw new InvalidOperationException("Invalid capture: "+name);
            var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);image.Render(element);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
            using(var file=File.Create(Path.Combine(captures,name+".png")))encoder.Save(file);
            images++;log.Add("CAPTURE: "+name+" "+width+"x"+height+" (96dpi WPF render, not OS-DPI proof)");
        }
        static IEnumerable<T> All<T>(DependencyObject parent) where T:DependencyObject
        {for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is T typed)yield return typed;foreach(var descendant in All<T>(child))yield return descendant;}}
        static void WriteProof()
        {
            string error=Path.Combine(output,"error.txt");if(File.Exists(error))File.Delete(error);
            log.Add("COMPLETED: "+checks+" PASS; "+images+" PNG. Actual SAB.dll settings window; fixture data outside Revit. Live OK capabilities/installation/physical input/OS DPI NOT RUN.");
            File.WriteAllLines(Path.Combine(output,"verification.txt"),log);
            File.WriteAllLines(Path.Combine(captures,"verification.txt"),log);
        }
        static string FindRoot()
        {
            var directory=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while(directory!=null) {if(File.Exists(Path.Combine(directory.FullName,@"SAB\SAB.csproj")))return directory.FullName;directory=directory.Parent;}
            throw new DirectoryNotFoundException("SAB root not found");
        }
    }
}
