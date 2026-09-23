using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.Attributes;
using System.Diagnostics;
using Helpers.Notifications.ToastNotifications;
using Path = System.IO.Path;
using TextBox = System.Windows.Controls.TextBox;

namespace SAB.Notifications
{
 public class Settings
 {
  public bool Enabled = true;
  public bool SkipOrdinary = true;
  public bool SkipImportant = true;
  public int ToastSeconds = 10;
  public string Background = "#F7F8FA";
  public string Foreground = "#1F2937";
  public string Accent = "#0F6CBD";
  public Dictionary<string,string> Folders = new Dictionary<string,string>();
  // Only verified DialogId -> result mappings should be added here.
  public Dictionary<string,int> DialogResults = new Dictionary<string,int>();
 }
 public class Entry
 {
  public string Time = DateTimeOffset.Now.ToString("o");
  public string Model, Transaction, Failure, Severity, Text, Ids, Action, LogPath, HtmlLogPath;
  [ScriptIgnore] public bool AwaitDeleteCommit;
  [ScriptIgnore] public Document Doc;
  [ScriptIgnore] public long Revision = -1;
  [ScriptIgnore] public long CapturedRevision;
  public string UndoUnavailableReason;
 }
 public class NotificationController
 {
  internal static NotificationController Current;
  internal static readonly string Home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RevitQuiet");
  internal Settings Config;
  internal readonly List<Entry> Entries = new List<Entry>();
  readonly List<Entry> pending = new List<Entry>();
  readonly List<Tuple<Entry,WeakReference>> undoButtons = new List<Tuple<Entry,WeakReference>>();
  readonly Dictionary<Document,long> revisions = new Dictionary<Document,long>();
  readonly Dictionary<Document,int> passes = new Dictionary<Document,int>();
  readonly HashSet<string> asked = new HashSet<string>();
  readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
  ExternalEvent undoEvent;
  UndoHandler undo;
  Window notice;
  SabStyledToastHost toastHost;
  string journalPath;
  readonly DeleteWarningButton deleteButton = new DeleteWarningButton();
  internal bool Enabled;
  public Result Start(UIControlledApplication app, RibbonPanel settingsPanel)
  {
   Current = this; Directory.CreateDirectory(Home);
   try { Config = json.Deserialize<Settings>(File.ReadAllText(Path.Combine(Home,"settings.json"))); }
   catch { Config = new Settings(); }
   Config = Config ?? new Settings(); Config.Folders = Config.Folders ?? new Dictionary<string,string>(); Config.DialogResults = Config.DialogResults ?? new Dictionary<string,int>();
   Enabled = Config.Enabled;
   journalPath = app.ControlledApplication.RecordingJournalFilename;
   undo = new UndoHandler(this); undoEvent = ExternalEvent.Create(undo);
    SAB.Helpers.Ribbon.AddPushButtonSingle(settingsPanel, "QuietSettings", "Уведомления", typeof(Options).FullName, "SAB.Resources.Notifications_32.png", "SAB.Resources.Notifications_16.png");
   app.ControlledApplication.FailuresProcessing += Failures;
   app.ControlledApplication.DocumentChanged += Changed;
   app.ControlledApplication.DocumentClosed += Closed;
   app.DialogBoxShowing += Dialog; app.Idling += Idle;
   return Result.Succeeded;
  }
  public Result Stop(UIControlledApplication app)
  {
   app.ControlledApplication.FailuresProcessing -= Failures;
   app.ControlledApplication.DocumentChanged -= Changed;
   app.ControlledApplication.DocumentClosed -= Closed;
   app.DialogBoxShowing -= Dialog; app.Idling -= Idle;
   deleteButton.Dispose(); notice?.Close(); toastHost?.Close(); undoEvent?.Dispose(); Current = null; return Result.Succeeded;
  }
  internal void Save() { File.WriteAllText(Path.Combine(Home,"settings.json"), json.Serialize(Config)); }
  static string Model(Document d)
  {
   if (d == null) return "NoDocument";
   if (d.IsWorkshared) return ModelPathUtils.ConvertModelPathToUserVisiblePath(d.GetWorksharingCentralModelPath());
   return string.IsNullOrEmpty(d.PathName) ? "Unsaved_" + d.Title : d.PathName;
  }
  void Closed(object sender, DocumentClosedEventArgs e)
  {
   foreach (var d in revisions.Keys.Where(d => !d.IsValidObject).ToList()) revisions.Remove(d);
   foreach (var r in Entries.Where(r => r.Doc != null && !r.Doc.IsValidObject)) { r.Doc = null; r.Revision = -1; }
  }
  void Changed(object sender, DocumentChangedEventArgs e)
  {
   var d = e.GetDocument(); long rev; revisions.TryGetValue(d,out rev); revisions[d] = ++rev;
   var names = e.GetTransactionNames();
   foreach (var r in Entries.Where(r => r.Doc == d && r.Revision == -1))
   {
    bool deletion = r.AwaitDeleteCommit;
    if (e.Operation == UndoOperation.TransactionCommitted && UndoEligibility.Matches(r.CapturedRevision, rev-1,
        r.Transaction, names, deletion, e.GetDeletedElementIds().Count))
    {
     r.Revision = rev;
     if (deletion)
     {
      r.Ids = string.Join(",", e.GetDeletedElementIds().Select(id => id.ToString()));
      r.Transaction = string.Join(" / ", names);
      r.Action = "Удалено элементов: " + e.GetDeletedElementIds().Count + ". Удаление подтверждено Revit";
     }
     r.AwaitDeleteCommit = false;
     Append(r);
    }
   }
  }
  void Failures(object sender, FailuresProcessingEventArgs e)
  {
   if (!Enabled) return;
   var a = e.GetFailuresAccessor(); bool resolved = false, rollback = false;
   int pass; passes.TryGetValue(a.GetDocument(),out pass); passes[a.GetDocument()] = ++pass;
   foreach (var f in a.GetFailureMessages())
   {
    if (f.GetSeverity() == FailureSeverity.Warning ? !Config.SkipOrdinary : !Config.SkipImportant) continue;
    var r = new Entry { Doc = a.GetDocument(), Model = Model(a.GetDocument()), Transaction = a.GetTransactionName(), Failure = f.GetFailureDefinitionId().Guid.ToString(), Severity = f.GetSeverity().ToString(), Text = f.GetDescriptionText(), Ids = string.Join(",", f.GetFailingElementIds().Concat(f.GetAdditionalElementIds()).Select(id => id.ToString()).Distinct()), Action = "Передано Revit" };
    // Persist the original message BEFORE changing or deleting any failure.
    if (!Record(r)) { r.Action = "Автопропуск отключён: локальный журнал недоступен"; pending.Add(r); Entries.Add(r); continue; }
    if (Enabled)
    {
     try
     {
      if (pass > 32 && f.GetSeverity() != FailureSeverity.Warning) { rollback = true; r.Action = "Запрошен откат: превышен лимит повторной обработки"; }
      else if (f.GetSeverity() == FailureSeverity.Warning) { a.DeleteWarning(f); r.Action = "Предупреждение скрыто"; }
      else if (f.GetSeverity() == FailureSeverity.Error && a.IsTransactionBeingCommitted())
      {
       var attempts = a.GetAttemptedResolutionTypes(f);
       var choices = Enum.GetValues(typeof(FailureResolutionType)).Cast<FailureResolutionType>()
        .OrderBy(t => t == FailureResolutionType.DeleteElements ? 1 : 0);
       bool done = false;
       foreach (var t in choices)
       {
        if (attempts.Contains(t) || !f.HasResolutionOfType(t) || !a.IsFailureResolutionPermitted(f,t)) continue;
        f.SetCurrentResolutionType(t); a.ResolveFailure(f); resolved = done = true; r.Action = "Применено разрешение: " + t; break;
       }
       if (!done) { rollback = true; r.Action = "Запрошен откат: нет доступного разрешения"; }
      }
      else { rollback = true; r.Action = "Запрошен откат неустранимой ошибки"; }
     }
     catch (Exception ex) { rollback = true; r.Action = "Запрошен откат; обработчик: " + ex.Message; }
    }
    Append(r); pending.Add(r); Entries.Add(r);
   }
   if (rollback)
   {
    foreach (var r in pending.Where(r => r.Doc == a.GetDocument() && r.Transaction == a.GetTransactionName())) { r.Revision = -2; r.Action += "\nИтог: запрошен откат всей транзакции"; Append(r); }
    var opts = a.GetFailureHandlingOptions(); opts.SetClearAfterRollback(true); a.SetFailureHandlingOptions(opts);
    e.SetProcessingResult(FailureProcessingResult.ProceedWithRollBack);
   }
   else if (resolved) e.SetProcessingResult(FailureProcessingResult.ProceedWithCommit);
  }
  bool Record(Entry r)
  {
   if (r.Doc != null) revisions.TryGetValue(r.Doc,out r.CapturedRevision);
   try { r.LogPath = Path.Combine(Home,"spool.jsonl"); File.AppendAllText(r.LogPath,json.Serialize(r) + Environment.NewLine); return true; }
   catch { Enabled = false; return false; }
  }
  void Append(Entry r)
  {
   try { File.AppendAllText(r.LogPath,json.Serialize(r) + Environment.NewLine); }
   catch (Exception ex) { Enabled = false; r.Action += "\nЗапись журнала не удалась: " + ex.Message; }
  }
  void Dialog(object sender, DialogBoxShowingEventArgs e)
  {
   deleteButton.Cancel();
   if (!Enabled) return;
   var ui = sender as UIApplication;
   var d = ui?.ActiveUIDocument?.Document;
   string deletionText = e.DialogId == "Dialog_Revit_DocWarnDialog" ? DeleteWarningJournal.Read(journalPath) : null;
   int configuredCode;
   if (deletionText == null && !Config.DialogResults.TryGetValue(e.DialogId ?? "", out configuredCode)) return;
   var r = new Entry { Doc = d, Model = Model(d), Failure = e.DialogId, Severity = "Dialog", Text = deletionText ?? (e as TaskDialogShowingEventArgs)?.Message ?? (e as MessageBoxShowingEventArgs)?.Message ?? "Revit API не предоставляет текст этого диалога", Ids = "", Action = "Диалог оставлен Revit" };
   if (!Record(r)) { r.Action = "Автопропуск отключён: локальный журнал недоступен"; pending.Add(r); Entries.Add(r); return; }
   int code;
   if (Enabled && Config.SkipImportant && deletionText != null)
   {
    r.Action = "Ожидание штатной кнопки OK";
    if (ui != null) deleteButton.Arm(ui.MainWindowHandle, posted => {
      r.AwaitDeleteCommit = posted;
      r.Action = posted ? "Нажата кнопка OK; ожидается результат удаления" : "Автонажатие OK не выполнено; требуется штатное подтверждение";
      Append(r);
    });
   }
   else if (Enabled && Config.SkipImportant && Config.DialogResults.TryGetValue(e.DialogId ?? "",out code))
    r.Action = e.OverrideResult(code) ? "Диалог закрыт, код: " + code : "Revit отклонил код: " + code;
   Append(r); pending.Add(r); Entries.Add(r);
  }
  void Idle(object sender, IdlingEventArgs e)
  {
   RefreshUndoButtons((UIApplication)sender);
   if (!Enabled) { pending.Clear(); passes.Clear(); return; }
   if (pending.Count == 0) return;
   deleteButton.Cancel();
   foreach (var r in pending.Where(r => r.Revision == -1 && !r.AwaitDeleteCommit)) r.Revision = -2;
   foreach (var r in pending.Where(r => r.AwaitDeleteCommit)) { r.AwaitDeleteCommit = false; r.Revision = -2; r.Action = "Удаление не подтверждено Revit; действие не выполнено или отменено"; Append(r); }
   var batch = pending.ToList(); pending.Clear(); passes.Clear();
   foreach (var r in batch)
   {
    try
    {
     string dir;
     bool local = Path.IsPathRooted(r.Model) && !r.Model.StartsWith("RSN:",StringComparison.OrdinalIgnoreCase);
     if (Config.Folders.TryGetValue(r.Model,out dir)) { }
     else if (local) dir = Path.GetDirectoryName(r.Model);
     else if (!Config.Folders.TryGetValue(r.Model,out dir) && asked.Add(r.Model))
     {
      using (var picker = new System.Windows.Forms.FolderBrowserDialog { Description = "Выберите или создайте папку журнала для " + r.Model, ShowNewFolderButton = true })
       if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK) { dir = picker.SelectedPath; Config.Folders[r.Model] = dir; Save(); }
     }
     if (!string.IsNullOrWhiteSpace(dir))
     {
      string target = Path.Combine(dir,"RevitQuiet-" + DateTime.Now.ToString("yyyy-MM-dd") + ".jsonl");
      var prior = r.LogPath; r.LogPath = target;
      try { File.AppendAllText(target,json.Serialize(r) + Environment.NewLine); } catch { r.LogPath = prior; throw; }
     }
    }
    catch (Exception ex) { r.Action += "\nЖурнал сохранён локально: " + ex.Message; }
   }
   foreach (var r in batch) WriteHtml(r);
   ShowToasts((UIApplication)sender, batch.Where(r => r.Severity != "Dialog" || r.Action != "Диалог оставлен Revit"));
  }
  void WriteHtml(Entry r)
  {
   try { r.HtmlLogPath = ReadableJournal.Write(Path.GetDirectoryName(r.LogPath),r); }
   catch (Exception ex)
   {
    r.Action += "\nЖурнал рядом с моделью недоступен: " + ex.Message;
    try { r.HtmlLogPath = ReadableJournal.Write(Home,r); }
    catch { Enabled = false; r.Action += "\nНе удалось записать HTML-журнал"; }
   }
  }
  internal string GetJournal(UIApplication ui)
  {
   string model = Model(ui?.ActiveUIDocument?.Document);
   var last = Entries.LastOrDefault(r => r.Model == model && !string.IsNullOrEmpty(r.HtmlLogPath));
   if (last != null && File.Exists(last.HtmlLogPath)) return last.HtmlLogPath;
   string dir;
   if (!Config.Folders.TryGetValue(model,out dir)) dir = Path.IsPathRooted(model) ? Path.GetDirectoryName(model) : Home;
   try { return ReadableJournal.Write(dir,null); } catch { return ReadableJournal.Write(Home,null); }
  }
  internal static void OpenFile(string path) { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
  internal void DismissToasts() { deleteButton.Cancel(); toastHost?.Close(); toastHost = null; }
  void ShowToasts(UIApplication ui, IEnumerable<Entry> rows)
  {
   var list = rows.ToList(); if (list.Count == 0) return;
   if (toastHost == null)
   {
    toastHost = new SabStyledToastHost { ShowActivated = false, SizeToContent = SizeToContent.Height };
    new System.Windows.Interop.WindowInteropHelper(toastHost).Owner = ui.MainWindowHandle;
    toastHost.SizeChanged += (s,e) => { toastHost.Left = SystemParameters.WorkArea.Right - toastHost.ActualWidth - 12; toastHost.Top = SystemParameters.WorkArea.Bottom - toastHost.ActualHeight - 12; };
    toastHost.Show();
   }
   foreach (var r in list)
   {
    var details = new StackPanel { MaxWidth = 340, Margin = new Thickness(0,8,0,0) };
    details.Children.Add(new TextBlock { Text = "ID элементов (через запятую)", FontSize = 12 });
    details.Children.Add(Box(r.Ids));
    details.Children.Add(new TextBlock { Text = r.Action, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
    var actions = new StackPanel { Orientation = Orientation.Horizontal };
    var cancel = new Button { Content = "Откатить", Padding = new Thickness(8,4,8,4), IsEnabled = CanUndo(ui,r), ToolTip = r.UndoUnavailableReason ?? "Отменить последнее действие Revit целиком" };
    ToolTipService.SetShowOnDisabled(cancel,true); undoButtons.Add(Tuple.Create(r,new WeakReference(cancel)));
    cancel.Click += (s,e) => { undo.Target = r; if (undoEvent.Raise() == ExternalEventRequest.Accepted) cancel.IsEnabled = false; };
    var journal = new Button { Content = "Журнал", Padding = new Thickness(8,4,8,4), Margin = new Thickness(8,0,0,0), IsEnabled = !string.IsNullOrEmpty(r.HtmlLogPath) };
    journal.Click += (s,e) => OpenFile(r.HtmlLogPath);
    actions.Children.Add(cancel); actions.Children.Add(journal); details.Children.Add(actions);
    toastHost.ShowDetailedToast("SAB — " + (r.Severity == "Error" ? "Ошибка" : "Предупреждение"), r.Text,
       r.Severity == "Error" ? ToastType.Error : ToastType.Warning, details, Config.ToastSeconds);
   }
  }
  internal void Show(UIApplication ui, IEnumerable<Entry> rows)
  {
   notice?.Close();
   var stack = new StackPanel { Margin = new Thickness(18) };
   stack.Children.Add(new TextBlock { Text = "Уведомления Revit", FontSize = 22, Margin = new Thickness(0,0,0,14) });
   foreach (var r in rows.Reverse())
   {
    stack.Children.Add(new TextBlock { Text = r.Time + " • " + r.Severity + "\n" + r.Transaction, TextWrapping = TextWrapping.Wrap });
    stack.Children.Add(Box(r.Text)); stack.Children.Add(new TextBlock { Text = "ID элементов — через запятую" }); stack.Children.Add(Box(r.Ids));
    stack.Children.Add(new TextBlock { Text = r.Action + "\nЖурнал: " + r.LogPath, TextWrapping = TextWrapping.Wrap });
    var b = new Button { Content = "Откатить", Margin = new Thickness(0,8,0,18), IsEnabled = CanUndo(ui,r), ToolTip = "Отменяет последнее действие Revit целиком. После следующих изменений недоступно." };
    b.Click += (s,e) => { undo.Target = r; if (undoEvent.Raise() == ExternalEventRequest.Accepted) b.IsEnabled = false; };
    stack.Children.Add(b);
   }
   notice = new Window { Title = "Revit Quiet — уведомления", Width = 520, Height = 580, ShowActivated = false, Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, Background = Brush(Config.Background,"#F7F8FA"), Foreground = Brush(Config.Foreground,"#1F2937") };
   notice.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(Path.Combine(Path.GetDirectoryName(typeof(NotificationController).Assembly.Location), "UI", "Styles", "SABWindowStyles.xaml"), UriKind.Absolute) });
   notice.FontFamily = new FontFamily("Segoe UI"); notice.FontSize = 13;
   foreach (var child in stack.Children) { if (child is Button button) button.Style = (Style)notice.Resources["SabNeutralButtonStyle"]; if (child is TextBox box) box.Style = (Style)notice.Resources["SabTextBoxStyle"]; }
   new System.Windows.Interop.WindowInteropHelper(notice).Owner = ui.MainWindowHandle;
   notice.Show();
  }
  static SolidColorBrush Brush(string value,string fallback) { try { return (SolidColorBrush)new BrushConverter().ConvertFromString(value); } catch { return (SolidColorBrush)new BrushConverter().ConvertFromString(fallback); } }
  static TextBox Box(string text) => new TextBox { Text = text ?? "", IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,6,0,10), Padding = new Thickness(8), MaxHeight = 260, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
  void RefreshUndoButtons(UIApplication ui)
  {
   for (int i=undoButtons.Count-1;i>=0;i--)
   {
    var button = undoButtons[i].Item2.Target as Button;
    if (button == null) { undoButtons.RemoveAt(i); continue; }
    var r = undoButtons[i].Item1;
    button.IsEnabled=CanUndo(ui,r); button.ToolTip=r.UndoUnavailableReason ?? "Отменить последнее действие Revit целиком";
   }
  }
  internal bool CanUndo(UIApplication ui, Entry r)
  {
   long rev;
   string reason = null;
   if (r.Doc == null || !r.Doc.IsValidObject) reason="Модель закрыта или не определена";
   else if (ui.ActiveUIDocument?.Document != r.Doc) reason="Откройте модель этого уведомления";
   else if (r.Revision < 0) reason="Изменение ещё не подтверждено, отменено или откат уже запрошен";
   else if (!revisions.TryGetValue(r.Doc,out rev) || rev != r.Revision) reason="После этого события модель уже изменена; используйте историю отмены Revit";
   else if (r.Doc.IsModifiable || !ui.CanPostCommand(RevitCommandId.LookupPostableCommandId(PostableCommand.Undo))) reason="Revit ещё выполняет команду; дождитесь её завершения";
   r.UndoUnavailableReason=reason; return reason==null;
  }
  class UndoHandler : IExternalEventHandler
  {
   readonly NotificationController app; internal Entry Target;
   public UndoHandler(NotificationController app) { this.app = app; }
   public string GetName() => "Revit Quiet Undo";
   public void Execute(UIApplication ui)
   {
    var r = Target; Target = null;
    if (r == null) return;
    if (!app.CanUndo(ui,r)) { TaskDialog.Show("Revit Quiet", "Откат недоступен: документ или последнее действие изменились. Используйте историю отмены Revit."); return; }
    ui.PostCommand(RevitCommandId.LookupPostableCommandId(PostableCommand.Undo));
    r.Revision = -1; r.Action += "\nПользователь запросил Undo последнего действия"; app.Append(r); app.WriteHtml(r);
   }
  }
 }
 [Transaction(TransactionMode.Manual)] public class Toggle : IExternalCommand
 {
  public Result Execute(ExternalCommandData d,ref string m,ElementSet e) { var a=NotificationController.Current; a.Enabled=!a.Enabled; a.Config.Enabled=a.Enabled; a.Save(); TaskDialog.Show("Revit Quiet", a.Enabled ? "Автопропуск включён" : "Автопропуск выключен"); return Result.Succeeded; }
 }
 [Transaction(TransactionMode.Manual)] public class History : IExternalCommand
 {
  public Result Execute(ExternalCommandData d,ref string m,ElementSet e) { NotificationController.OpenFile(NotificationController.Current.GetJournal(d.Application)); return Result.Succeeded; }
 }
 [Transaction(TransactionMode.Manual)] public class Options : IExternalCommand
 {
  public Result Execute(ExternalCommandData d,ref string m,ElementSet e)
  {
   BuildWindow(d.Application).ShowDialog(); return Result.Succeeded;
  }
  public static Window BuildWindow(UIApplication ui)
  {
   var a = NotificationController.Current; var c = a.Config;
   var w = new Window { Title = "SAB — Уведомления", Width = 620, Height = 760, WindowStartupLocation = WindowStartupLocation.CenterOwner };
   var panel = new StackPanel { Margin = new Thickness(24) };
   w.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(Path.Combine(Path.GetDirectoryName(typeof(NotificationController).Assembly.Location), "UI", "Styles", "SABWindowStyles.xaml"), UriKind.Absolute) });
   w.Background = (System.Windows.Media.Brush)w.Resources["SabBrush.WindowBackground"];
   w.Foreground = (System.Windows.Media.Brush)w.Resources["SabBrush.Text"];
   w.FontFamily = new FontFamily("Segoe UI"); w.FontSize = 13;
   panel.Children.Add(new TextBlock { Text = "Уведомления Revit", Style = (Style)w.Resources["SabWindowTitleTextStyle"], Margin = new Thickness(0,0,0,20) });
   var enabled = new System.Windows.Controls.Primitives.ToggleButton { IsChecked = a.Enabled, Width = 52, Height = 28, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Включить или отключить обработку уведомлений" };
   enabled.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ToggleButton'><Border Name='Track' Background='#98A2B3' CornerRadius='14'><Ellipse Name='Thumb' Width='22' Height='22' Margin='3' Fill='White' HorizontalAlignment='Left'/></Border><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='Track' Property='Background' Value='#0F6CBD'/><Setter TargetName='Thumb' Property='HorizontalAlignment' Value='Right'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");

   enabled.Click += (s,args) =>
   {
    bool previous = a.Enabled;
    try { a.Enabled = enabled.IsChecked == true; c.Enabled = a.Enabled; a.Save(); if (!a.Enabled) a.DismissToasts(); }
    catch (Exception ex) { a.Enabled = previous; c.Enabled = previous; enabled.IsChecked = previous; System.Windows.MessageBox.Show(w,ex.Message,"Не удалось сохранить состояние"); }
   };
   var toggleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,16) }; toggleRow.Children.Add(enabled); toggleRow.Children.Add(new TextBlock { Text = "Обработка уведомлений", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12,0,0,0), FontWeight = FontWeights.SemiBold }); panel.Children.Add(toggleRow);
   var ordinary = AddLevelToggle(panel, enabled.Template, "Обычные уведомления", "Скрывать предупреждения без изменения модели", c.SkipOrdinary);
   var important = AddLevelToggle(panel, enabled.Template, "Важные уведомления", "Автоматически изменять, рассоединять и удалять элементы для продолжения", c.SkipImportant);
   ordinary.Click += (s,args) => { bool old=c.SkipOrdinary; try { c.SkipOrdinary=ordinary.IsChecked==true; a.Save(); } catch(Exception ex) { c.SkipOrdinary=old; ordinary.IsChecked=old; System.Windows.MessageBox.Show(w,ex.Message); } };
   important.Click += (s,args) => { bool old=c.SkipImportant; try { c.SkipImportant=important.IsChecked==true; a.Save(); } catch(Exception ex) { c.SkipImportant=old; important.IsChecked=old; System.Windows.MessageBox.Show(w,ex.Message); } };
   panel.Children.Add(new TextBlock { Text = "Toast исчезает автоматически через (секунд, 3–60)" });
   var duration = new TextBox { Text = Math.Max(3,Math.Min(60,c.ToastSeconds)).ToString(), Margin = new Thickness(0,8,0,12) };
   panel.Children.Add(duration);
   string journalPath = a.GetJournal(ui);
   var openJournal = new Button { Content = "Открыть журнал в браузере", Margin = new Thickness(0,0,0,8) };
   openJournal.Click += (s,args) => NotificationController.OpenFile(journalPath);
   var openFolder = new Button { Content = "Показать папку журнала", Margin = new Thickness(0,0,0,8) };
   openFolder.Click += (s,args) => NotificationController.OpenFile(Path.GetDirectoryName(journalPath));
   panel.Children.Add(openJournal); panel.Children.Add(openFolder);
   panel.Children.Add(new TextBox { Text = journalPath, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,16) });
   panel.Children.Add(new TextBlock { Text = "Неустранимая ошибка отменяет операцию. Кнопка «Откатить» доступна только для последнего распознанного изменения и отменяет действие Revit целиком.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,20) });
   panel.Children.Add(new TextBlock { Text = "Папка журнала текущей модели", FontWeight = FontWeights.SemiBold });
   string key = ui?.ActiveUIDocument == null ? null : ModelKey(ui.ActiveUIDocument.Document);
   string saved; var folder = new TextBox { Text = key != null && c.Folders.TryGetValue(key,out saved) ? saved : "", IsReadOnly = true, Margin = new Thickness(0,8,0,8) };
   panel.Children.Add(folder);
   var browse = new Button { Content = "Выбрать или создать папку…", IsEnabled = key != null, Margin = new Thickness(0,0,0,12) };
   browse.Click += (s,args) => { using (var f = new System.Windows.Forms.FolderBrowserDialog { ShowNewFolderButton = true }) if (f.ShowDialog() == System.Windows.Forms.DialogResult.OK) folder.Text = f.SelectedPath; };
   panel.Children.Add(browse);
   panel.Children.Add(new TextBlock { Text = "Для файловых моделей журнал по умолчанию находится рядом с центральной моделью. При недоступности папки сохраняется локальная копия.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,16) });
   var save = new Button { Content = "Сохранить", Style = (Style)w.Resources["SabPrimaryButtonStyle"] };
   save.Click += (s,args) => {
    try {

     int seconds; if (!int.TryParse(duration.Text,out seconds) || seconds < 3 || seconds > 60) throw new ArgumentException("Укажите длительность от 3 до 60 секунд"); c.ToastSeconds=seconds;
     c.Enabled=enabled.IsChecked==true; c.SkipOrdinary=ordinary.IsChecked==true; c.SkipImportant=important.IsChecked==true; a.Enabled=c.Enabled;
     if(key!=null && !string.IsNullOrWhiteSpace(folder.Text)) c.Folders[key]=folder.Text;
     a.Save(); w.Close();
    } catch(Exception ex) { System.Windows.MessageBox.Show(w,ex.Message,"Не удалось сохранить"); }
   };
   panel.Children.Add(save);
   var credit = new TextBlock { Margin=new Thickness(0,10,0,0), FontSize=11 }; var creditLink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("Иконка: Icons8")); creditLink.Click += (s,args) => NotificationController.OpenFile("https://icons8.com/icon/kf0pHHgd6OsR/external-bell-calendar-time-dreamstale-lineal-dreamstale-5"); credit.Inlines.Add(creditLink); panel.Children.Add(credit);
   w.Content = new ScrollViewer { Content=panel, VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
   foreach (var child in panel.Children) { if (child is TextBox box) box.Style=(Style)w.Resources["SabTextBoxStyle"]; if(child is CheckBox check) check.Style=(Style)w.Resources["SabCheckBoxStyle"]; }
   browse.Style=(Style)w.Resources["SabNeutralButtonStyle"]; openJournal.Style=(Style)w.Resources["SabNeutralButtonStyle"]; openFolder.Style=(Style)w.Resources["SabNeutralButtonStyle"];
   if(ui!=null) new System.Windows.Interop.WindowInteropHelper(w).Owner=ui.MainWindowHandle; return w;
  }
  static System.Windows.Controls.Primitives.ToggleButton AddLevelToggle(StackPanel panel, ControlTemplate template, string title, string description, bool value)
  {
   var toggle = new System.Windows.Controls.Primitives.ToggleButton { Template=template, Width=52, Height=28, IsChecked=value, VerticalAlignment=VerticalAlignment.Center };
   var row = new StackPanel { Orientation=Orientation.Horizontal, Margin=new Thickness(0,0,0,14) };
   var labels = new StackPanel { Margin=new Thickness(12,0,0,0), MaxWidth=465 };
   labels.Children.Add(new TextBlock { Text=title, FontWeight=FontWeights.SemiBold });
   labels.Children.Add(new TextBlock { Text=description, TextWrapping=TextWrapping.Wrap, FontSize=12 });
   row.Children.Add(toggle); row.Children.Add(labels); panel.Children.Add(row); return toggle;
  }
  static string ModelKey(Document d) { if(d.IsWorkshared) return ModelPathUtils.ConvertModelPathToUserVisiblePath(d.GetWorksharingCentralModelPath()); return string.IsNullOrEmpty(d.PathName) ? "Unsaved_"+d.Title : d.PathName; }
 }
}
