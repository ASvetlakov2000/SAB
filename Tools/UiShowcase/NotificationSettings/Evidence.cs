using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SAB.NotificationSettingsPreview
{
    public static class Evidence
    {
        static readonly List<string> log=new List<string>();static string output;static int checks,captures;
        public static int VerifyBoundaries()
        {
            output=Path.Combine(Program.Root,@"Docs\Ui\DesignSystem\screenshots-notification-settings\revision-2");Directory.CreateDirectory(output);
            SettingsWindow window=null;
            try {
                window=new SettingsWindow();Application.Current.MainWindow=window;
                string external=Path.Combine(Path.GetTempPath(),"SAB-notification-external-"+Guid.NewGuid().ToString("N"));
                window.Model.Folder=external;
                string path=window.Model.WriteDemoJournal();
                Check("external folder preference receives no file or directory writes",!Directory.Exists(external));
                Check("demo journal remains inside fixed preview output",Path.GetFullPath(path).StartsWith(Path.GetFullPath(Program.Output)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&File.Exists(path));
                window.Model.Selected.Action=Rule.Toast;window.Model.Save();
                Check("configured external folder is stored only as a preference",File.ReadAllText(Path.Combine(Program.Output,"demo-settings.json")).Contains("SAB-notification-external-")&&!Directory.Exists(external));
                log.Add("COMPLETED: "+checks+" boundary PASS; no captures replaced.");return 0;
            }catch(Exception ex){log.Add("FAIL: "+ex);return 1;}
            finally {window?.Close();File.WriteAllLines(Path.Combine(output,"boundary-verification.txt"),log);}
        }
        public static int Run()
        {
            output=Path.Combine(Program.Root,@"Docs\Ui\DesignSystem\screenshots-notification-settings\revision-2");Directory.CreateDirectory(output);
            log.Add("Standalone native WPF preview, synthetic capabilities and data; shared SAB Fluent Light resources.");
            log.Add("No Revit callbacks, model transactions or production settings writes. Client-area RenderTargetBitmap captures of shown WPF, excluding OS titlebar.");
            log.Add("Physical input, complete accessibility traversal, OS DPI transitions and actual OK capability: NOT RUN.");
            SettingsWindow w=null;
            try {
                w=new SettingsWindow();Application.Current.MainWindow=w;w.Show();Pump();
                Check("all 14 fixture types visible",w.RulesGrid.Items.Count==14);
                Check("defaults: all show and global automatic OK off",!w.Model.Enabled&&w.Model.Rules.All(r=>r.Action==Rule.Show));
                Check("shared row44 and header38",w.RulesGrid.RowHeight==44&&All<DataGridColumnHeader>(w.RulesGrid).Where(h=>h.Content!=null).All(h=>h.ActualHeight==38));
                Check("exactly two rule columns",w.RulesGrid.Columns.Count==2);
                Check("exact user action labels",string.Join("|",w.Model.Rules[0].Actions)=="Не подавлять|Подавлять с уведомлением SAB|Подавлять с записью в журнал");
                Check("meaning and confirmation are separate",w.Model.Selected.Meaning=="Сообщение о результате"&&w.ConfirmationText.Text=="Требует нажатия OK · пример");
                Check("only Rules and Journal tabs remain",w.Tabs.Items.Count==2&&Equals(((TabItem)w.Tabs.Items[1]).Header,"Журнал"));
                Check("optional context checkboxes moved to Rules",w.FamilyException.IsVisible&&w.GroupException.IsVisible&&w.WorksharedException.IsVisible);
                Check("semantic badge is coloured",((SolidColorBrush)w.Model.Selected.KindForeground).Color==((SolidColorBrush)w.FindResource("SabBrush.Accent")).Color);
                Check("one help action and local file exists",All<Button>(w).Count(b=>Equals(b.Content,"Инструкция"))==1&&File.Exists(Path.Combine(Program.Root,@"Tools\UiShowcase\NotificationSettings\Help.html")));
                CheckGeometry(w);Capture(w,"01-default");

                w.ActionBox.SetCurrentValue(Selector.SelectedItemProperty,Rule.Toast);Pump();
                Toggle(w.EnabledToggle);Pump();
                Check("selected rule gets toast method only",w.Model.Selected.Action==Rule.Toast&&w.Model.Rules.Count(r=>r.Action!=Rule.Show)==1&&w.Model.Enabled);
                Check("changes stay in draft",w.Model.Dirty&&w.SaveButton.IsEnabled);
                Capture(w,"02-configured");
                w.ActionBox.IsDropDownOpen=true;Pump();
                var popup=(Popup)w.ActionBox.Template.FindName("PART_Popup",w.ActionBox);
                Check("actual method popup open and contains three actions",popup.IsOpen&&popup.Child.IsVisible&&w.ActionBox.Items.Count==3);
                CaptureElement((FrameworkElement)popup.Child,"03-method-dropdown");w.ActionBox.IsDropDownOpen=false;Pump();

                Toggle(w.EnabledToggle);Pump();
                Check("global pause preserves per-rule choices",!w.Model.Enabled&&w.Model.Rules[0].Action==Rule.Toast);Toggle(w.EnabledToggle);Pump();
                w.Model.Selected=w.Model.Rules.First(r=>r.Code=="D01");Pump();
                w.ActionBox.SetCurrentValue(Selector.SelectedItemProperty,Rule.Journal);Pump();
                Check("deletion without consent prevents save",!w.SaveButton.IsEnabled&&w.RuleValidation.IsVisible&&w.ConsentCheck.IsVisible);
                Capture(w,"04-deletion-validation");Toggle(w.ConsentCheck);Pump();
                Check("explicit deletion consent enables save",w.Model.Selected.Consent&&w.SaveButton.IsEnabled);Capture(w,"05-deletion-consent");
                w.ActionBox.SetCurrentValue(Selector.SelectedItemProperty,Rule.Show);Pump();
                Check("return to manual revokes deletion permission",!w.Model.Selected.Consent&&!w.ConsentCheck.IsVisible);

                w.Model.Selected=w.Model.Rules.First(r=>r.Code=="SKETCH");Pump();
                Check("sketch cannot auto confirm",!w.ActionBox.IsEnabled&&w.ActionBox.Items.Count==1);
                w.Model.Selected.Action=Rule.Journal;Check("protected model rejects injected automatic action",w.Model.Selected.Action==Rule.Show);Capture(w,"06-protected-sketch");
                w.Model.Selected=w.Model.Rules.First(r=>r.Code=="OWNERSHIP");Pump();Capture(w,"07-protected-ownership");
                w.Model.Selected=w.Model.Rules.First(r=>r.Code=="NONMODAL");Pump();
                Check("without OK always show",!w.ActionBox.IsEnabled&&w.Model.Selected.Action==Rule.Show);Capture(w,"08-without-ok");
                w.Model.Selected=w.Model.Rules.First(r=>r.Code=="UNKNOWN");Pump();
                Check("unknown window always show",!w.ActionBox.IsEnabled&&w.Model.Selected.Action==Rule.Show);Capture(w,"09-unknown");

                w.SearchBox.Text="НЕТ ТАКОГО СООБЩЕНИЯ";Click(w.SearchButton);Pump();
                Check("empty search has honest empty state and no stale details",w.RulesGrid.Items.Count==0&&w.EmptyPanel.IsVisible&&!w.DetailPanel.IsVisible);
                Capture(w,"10-empty");w.Model.ResetSearchCommand.Execute(null);Pump();
                w.SearchBox.Text="НЕ ВИДНЫ";w.SearchBox.Focus();
                w.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(w),0,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});Pump();
                Check("routed Enter searches case-insensitively",w.RulesGrid.Items.Count==1&&w.Model.Selected.Code=="C01");
                w.Model.ResetSearchCommand.Execute(null);w.FilterBox.SetCurrentValue(Selector.SelectedItemProperty,"С подавлением");Pump();
                Check("automatic filter follows rule choices",w.RulesGrid.Items.Count==1&&w.Model.Selected.Code=="C01");
                w.FilterBox.SetCurrentValue(Selector.SelectedItemProperty,"Все сообщения");Pump();

                w.Tabs.SelectedIndex=0;Pump();Check("exception toggles retain values",w.Model.FamilyException&&w.Model.GroupException&&w.Model.WorksharedException);Capture(w,"11-rule-contexts");
                Toggle(w.WorksharedException);Pump();Check("whole-workshared exception configurable but ownership remains protected",!w.Model.WorksharedException&&!w.Model.Rules.First(r=>r.Code=="OWNERSHIP").CanEdit);
                Toggle(w.WorksharedException);Pump();
                w.Tabs.SelectedIndex=1;Pump();Check("journal records draft choices without claiming Revit clicks",w.Model.Log.Count>0&&w.Model.Log.All(r=>r.Action.StartsWith("Выбрано:")));Capture(w,"12-journal");
                w.DurationBox.Text="2";Pump();Check("invalid duration prevents save",!w.SaveButton.IsEnabled&&w.FooterStatus.IsVisible);Capture(w,"13-duration-validation");w.DurationBox.Text="10";Pump();
                w.Model.Folder="";Pump();Check("empty log folder prevents save",!w.SaveButton.IsEnabled);w.Model.Folder=Path.Combine(Program.Output,"Журнал");Pump();

                w.Tabs.SelectedIndex=0;w.Model.Selected=w.Model.Rules[0];w.RulesGrid.ScrollIntoView(w.Model.Selected);Pump();
                w.Width=w.MinWidth;w.Height=w.MinHeight;Pump();CheckGeometry(w);Capture(w,"14-minimum");
                var longRule=w.Model.Rules[1];longRule.Name="Объединение смежных областей вида с очень длинным русским названием для проверки читаемости настроек";
                longRule.Description=string.Join("\n",Enumerable.Repeat("Проверка длинного сообщения: смежные области вида объединены при нулевом разрыве; полный текст доступен для чтения и выделения.",16));
                w.Model.Selected=longRule;w.RulesGrid.ScrollIntoView(longRule);Pump();
                Check("long message bounded and scrollable",All<TextBox>(w.DetailPanel).First(b=>b.IsReadOnly&&b.MaxHeight==150).ActualHeight<=150&&w.DetailScroll.ViewportHeight>0);Capture(w,"15-long-text");
                w.Width=1160;w.Height=820;w.Model.Selected=w.Model.Rules[0];w.RulesGrid.ScrollIntoView(w.Model.Selected);Pump();
                Click(w.SaveButton);Pump();Check("save writes isolated demo file only",File.ReadAllText(Path.Combine(Program.Output,"demo-settings.json")).Contains("standalone-preview-only")&&!w.Model.Dirty);Capture(w,"16-saved");
                string saved=File.ReadAllText(Path.Combine(Program.Output,"demo-settings.json"));
                w.Model.Enabled=false;Click(w.CancelButton);Pump();Check("cancel closes without saving draft",!w.IsVisible&&File.ReadAllText(Path.Combine(Program.Output,"demo-settings.json"))==saved);
                log.Add("COMPLETED: "+checks+" PASS; "+captures+" PNG. Scope: standalone WPF preview only.");return 0;
            }catch(Exception ex){log.Add("FAIL: "+ex);return 1;}
            finally{w?.Close();File.WriteAllLines(Path.Combine(output,"verification.txt"),log);}
        }
        static void CheckGeometry(SettingsWindow w)
        {
            var viewer=All<ScrollViewer>(w.RulesGrid).First(v=>v.Name=="DG_ScrollViewer");
            Check("rules fit width without horizontal scroll",viewer.ScrollableWidth<1);
            var bar=All<ScrollBar>(w.RulesGrid).First(b=>b.Orientation==Orientation.Vertical);
            var header=All<DataGridColumnHeadersPresenter>(w.RulesGrid).First();
            Check("table scrollbar below header",Bounds(bar,w.RulesGrid).Top>=Bounds(header,w.RulesGrid).Bottom-1);
            var root=(FrameworkElement)w.Content;
            Check("footer remains visible",Bounds(w.SaveButton,root).Bottom<=root.ActualHeight&&w.SaveButton.IsVisible);
            Check("detail and table do not overlap",Bounds(w.RulesFrame,root).Right+19<=Bounds(w.DetailScroll,root).Left);
        }
        static Rect Bounds(FrameworkElement e,Visual root)=>e.TransformToAncestor(root).TransformBounds(new Rect(0,0,e.ActualWidth,e.ActualHeight));
        static void Check(string label,bool pass){checks++;log.Add((pass?"PASS: ":"FAIL: ")+label);if(!pass)throw new InvalidOperationException(label);}
        static void Click(Button button){((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();Pump();}
        static void Toggle(ToggleButton button){((IToggleProvider)new ToggleButtonAutomationPeer(button).GetPattern(PatternInterface.Toggle)).Toggle();Pump();}
        static void Pump(){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
        static void Capture(SettingsWindow w,string name)=>CaptureElement((FrameworkElement)w.Content,name);
        static void CaptureElement(FrameworkElement visual,string name)
        {
            visual.UpdateLayout();int width=(int)Math.Ceiling(visual.ActualWidth),height=(int)Math.Ceiling(visual.ActualHeight);
            if(width<50||height<30)throw new InvalidOperationException("Invalid capture bounds: "+name);
            var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using(var stream=File.Create(Path.Combine(output,name+".png")))encoder.Save(stream);captures++;
            log.Add("CAPTURE: "+name+" "+width+"x"+height+" (96dpi WPF render, not OS-DPI proof)");
        }
        static IEnumerable<T> All<T>(DependencyObject root) where T:DependencyObject
        {for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T t)yield return t;foreach(var item in All<T>(child))yield return item;}}
    }
}
