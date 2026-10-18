using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace SAB.Notifications
{
    public sealed class Relay : ICommand
    {
        readonly Action execute; readonly Func<bool> can;
        public Relay(Action execute, Func<bool> can = null) { this.execute=execute; this.can=can; }
        public event EventHandler CanExecuteChanged;
        public bool CanExecute(object p) => can == null || can();
        public void Execute(object p) { if(CanExecute(p)) execute(); }
        public void Refresh() => CanExecuteChanged?.Invoke(this,EventArgs.Empty);
    }
    public abstract class Notifier : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void Changed(string name=null) => PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));
    }
    public sealed class Rule : Notifier
    {
        public const string Show = "Не подавлять", Toast = "Подавлять с уведомлением SAB", Journal = "Подавлять с записью в журнал";
        string action=Show; bool consent;
        public string Name { get; set; }
        public string Description { get; set; }
        public string Code { get; set; }
        public string KindLabel { get; set; }
        public string FailureId { get; set; }
        public string Identifier => string.IsNullOrEmpty(FailureId) ? Code : Code + " · " + FailureId;
        public string Protection { get; set; }
        public bool HasOk { get; set; }
        public bool Protected { get; set; }
        public bool Unknown { get; set; }
        public bool Destructive { get; set; }
        public bool CanEdit => HasOk && !Protected && !Unknown;
        public string[] Actions => CanEdit ? new[]{Show,Toast,Journal} : new[]{Show};
        public string Action {
            get=>action;
            set { string next=Actions.Contains(value)?value:Show; if(next==action)return; action=next; if(action==Show)consent=false; Changed(); }
        }
        public bool Consent { get=>consent; set { bool next=Destructive && action!=Show && value; if(consent==next)return; consent=next; Changed(); } }
        public string ActionShort => action;
        public string Meaning => Destructive?"Подтверждение удаления":Protected?Code=="OWNERSHIP"?"Ограничение доступа":Code=="SKETCH"?"Ошибка контура или профиля":"Ошибка Revit":Unknown?"Неизвестное уведомление":Code=="C01"||Code=="C08"?"Сообщение о результате":HasOk?"Возможная проблема в модели":"Сообщение без подтверждения";
        public Brush KindForeground => (Brush)SAB.UI.FluentLight.FluentResources.Find(Destructive||Protected?"SabBrush.Error":Unknown||!HasOk?"SabBrush.TextSecondary":Code=="C01"||Code=="C08"?"SabBrush.Accent":"SabBrush.Warning");
        public Brush KindBackground => new SolidColorBrush((KindForeground as SolidColorBrush)?.Color ?? Colors.Gray) {Opacity=.08};
        public string ConfirmationLabel => Unknown?"Подтверждение ещё не проверено":Protected?"Решение принимает пользователь":HasOk?"Наличие кнопки OK ещё не проверено":"Не требует подтверждения";
        public Visibility ConsentVisibility => Destructive && action!=Show?Visibility.Visible:Visibility.Collapsed;
        public string Validation => Destructive && action!=Show && !Consent?"Подтвердите разрешение на зависимое удаление.":"";
        public Visibility ValidationVisibility => Validation.Length>0?Visibility.Visible:Visibility.Collapsed;
        public string CapabilityNote => CanEdit?"Правило сохранится. Автоподтверждение недоступно: тип окна Revit ещё не проверен.":Unknown?"До определения типа окна: не подавлять.":Protected?"Обязательное правило: "+Protection:"Нажатие OK не требуется.";
        public string Effect => CanEdit && action!=Show ? "Выбранное действие ожидает проверки. Сейчас сообщение остаётся в Revit." : !CanEdit?Protected?"Штатное сообщение и выбор действий Revit сохраняются.":"Сообщение остаётся видимым в Revit.":action==Show?"Штатный диалог; решение принимает пользователь.":Destructive?"Будет подтверждено удаление дополнительных элементов. Действие записывается в журнал.":action==Toast?"OK автоматически; исходный текст — в уведомлении SAB и журнале.":"OK автоматически; исходный текст — в журнале.";
    }
    public sealed class LogRow { public string Time { get; set; } public string Name { get; set; } public string Action { get; set; } public string Code { get; set; } public string Model { get; set; } public string Ids { get; set; } public string Severity { get; set; } public string DisplayTime => DateTimeOffset.TryParse(Time,out var time)?time.ToLocalTime().ToString("dd.MM HH:mm:ss"):Time; public string Detail => Model + "\n" + Code + "\nID: " + Ids; }
    public sealed class SettingsModel : Notifier
    {
        readonly NotificationSettingsStore store;
        readonly Action<Settings> apply;
        readonly Settings original;
        bool saveFailed, saved;
        public ObservableCollection<Rule> Rules { get; } = new ObservableCollection<Rule>();
        public ObservableCollection<LogRow> Log { get; } = new ObservableCollection<LogRow>();
        public ICollectionView View { get; }
        public string[] Filters { get; } = new[]{"Все сообщения","С подавлением","Без подтверждения","Всегда показывать"};
        public event Action CloseRequested;
        public Relay SearchCommand { get; }
        public Relay ResetSearchCommand { get; }
        public Relay SaveCommand { get; }
        public Relay CancelCommand { get; }
        public Relay JournalCommand { get; }
        public Relay HelpCommand { get; }
        bool enabled, family=true, group=true, workshared=true, dirty;
        string search="", appliedSearch="", filter="Все сообщения", duration="10", folder, result="";
        Rule selected;
        public bool Enabled { get=>enabled; set { if(enabled==value)return;enabled=value;MarkDirty(); } }
        public bool FamilyException { get=>family; set { if(family==value)return;family=value;MarkDirty(); } }
        public bool GroupException { get=>group; set { if(group==value)return;group=value;MarkDirty(); } }
        public bool WorksharedException { get=>workshared; set { if(workshared==value)return;workshared=value;MarkDirty(); } }
        public string Search { get=>search; set { search=value??""; Changed(nameof(Search)); } }
        public string Filter { get=>filter; set { filter=value;RefreshView();Changed(nameof(Filter)); } }
        public string Duration { get=>duration; set { if(duration==value)return;duration=value;MarkDirty(); } }
        public string Folder { get=>folder; set { if(folder==value)return;folder=value;MarkDirty(); } }
        public Rule Selected { get=>selected; set { selected=value;Changed(nameof(Selected));Changed(nameof(DetailVisibility)); } }
        public Visibility DetailVisibility => Selected==null?Visibility.Collapsed:Visibility.Visible;
        public Visibility EmptyVisibility => View.IsEmpty?Visibility.Visible:Visibility.Collapsed;
        public Visibility JournalEmptyVisibility => Log.Count==0?Visibility.Visible:Visibility.Collapsed;
        public string Summary => "Показано: "+View.Cast<Rule>().Count()+" из "+Rules.Count+"   ·   Настроено: "+Rules.Count(r=>r.Action!=Rule.Show)+"   ·   Доступно к подавлению: 0";
        public string Validation {
            get {
                if(Rules.Any(r=>r.Validation.Length>0))return "Не подтверждено разрешение на зависимое удаление.";
                if(!int.TryParse(Duration,out int seconds)||seconds<3||seconds>60)return "Укажите длительность уведомления от 3 до 60 секунд.";
                if(string.IsNullOrWhiteSpace(Folder))return "Укажите папку журнала.";
                try { if(!Path.IsPathRooted(Folder)||Path.GetPathRoot(Folder).Length<=2)return "Укажите полный путь к папке журнала.";Path.GetFullPath(Folder);if(File.Exists(Folder))return "Выберите папку, а не файл журнала."; }
                catch { return "Путь к папке журнала содержит недопустимые символы."; }
                return "";
            }
        }
        public bool CanSave => Validation.Length==0;
        public bool Dirty => dirty;
        public string Status => Validation.Length>0?Validation:result.Length>0?result:dirty?"Есть несохранённые изменения":"";
        public Visibility StatusVisibility => Status.Length>0?Visibility.Visible:Visibility.Collapsed;
        public Brush StatusBrush => (Brush)SAB.UI.FluentLight.FluentResources.Find(Validation.Length>0||saveFailed?"SabBrush.Error":saved?"SabBrush.Success":"SabBrush.TextSecondary");
        public SettingsModel(NotificationSettingsStore store, Settings settings, Action<Settings> apply = null)
        {
            this.store=store;this.apply=apply;original=store.Clone(settings);
            enabled=original.Enabled;family=original.FamilyException;group=original.GroupException;workshared=original.WorksharedException;
            duration=original.ToastSeconds.ToString();folder=original.JournalFolder;result=store.LoadNotice??"";saveFailed=store.LoadFailed;
            Add("C01","Созданные элементы не видны на текущем виде","Ни один из созданных элементов не является видимым на текущем виде. Проверьте активный вид, его параметры и настройку видимости.","Информация · OK",true);
            Add("C08","Смежные области вида объединились","Некоторые из областей вида были размещены рядом. При нулевом размере разрыва смежные области объединяются.","Информация · OK",true);
            Add("C02","Повторяется маркировка типоразмера","Элементы имеют повторяющиеся значения «Маркировка типоразмера».","Предупреждение · OK",true);
            Add("C03","Идентичные экземпляры в одном месте","В одном и том же месте имеются идентичные экземпляры. Это приведёт к дублированию позиций в спецификациях.","Предупреждение · OK",true);
            Add("C04","Перекрываются объёмы помещений","Перекрытие объёмов помещений. Скорректируйте верхний предел и смещение предела помещений.","Предупреждение · OK",true);
            Add("C05","Несколько помещений в одной области","Несколько помещений находятся в одной окружённой области. Для лишних элементов будет отображаться «Избыточное помещение».","Предупреждение · OK",true);
            Add("C06","Объединённые элементы не пересекаются","Выделенные элементы объединены, но они не пересекаются.","Предупреждение · OK",true);
            Add("C07","Требуется просмотр координаций связи","Для экземпляра связи требуется просмотр координаций.","Предупреждение · OK",true);
            var delete=Add("D01","Удаление зависимых элементов","Помимо выбранных элементов будут удалены дополнительные зависимые элементы: виды, размеры, марки и связанные элементы модели.","Подтверждение удаления · OK",true);delete.Destructive=true;
            Add("SKETCH","Незамкнутый контур или неверный профиль","Линии должны образовывать замкнутые контуры. Выделенные линии разомкнуты с одной стороны.","Защищено · контур и профиль",true,true,"контуры и профили");
            Add("OWNERSHIP","Элемент занят другим пользователем","Элемент принадлежит другому пользователю. Изменение недоступно до освобождения элемента.","Защищено · владение и права",true,true,"владение и права доступа");
            Add("FAILURE","Ошибка завершения операции Revit","Не удалось завершить операцию. Требуется штатное решение ошибки Revit.","Защищено · ошибка Revit",true,true,"ошибки Revit");
            Add("NONMODAL","Обычное немодальное уведомление","Предупреждение отображается без обязательного подтверждения OK.","Без обязательного OK",false);
            var unknown=Add("UNKNOWN","Новое сообщение, ещё не определённое","Тип окна и доступные действия ещё не определены.","Тип окна не определён",false);unknown.Unknown=true;
            string[] ids={"b1c8f862-00fc-4046-b92c-6ca64d53384f","152dfcbc-2fe3-4e76-9da4-4937a2e4e3a5","6e1efefe-c8e0-483d-8482-150b9f1da21a","b4176cef-6086-45a8-a066-c3fd424c9412","a22de05c-4c92-4bdc-9ce3-a965d2cf316c","83d4a67c-818c-4291-adaf-f2d33064fea8","1b9dacf3-db22-45d5-b071-42516278ffb1","3d983f31-9ee3-4c3a-bed8-663b32cecec5"};
            for(int i=0;i<ids.Length;i++)Rules[i].FailureId=ids[i];
            Rules.First(r=>r.Code=="D01").FailureId="Dialog_Revit_DocWarnDialog";
            try { foreach(var row in store.ReadJournal())Log.Add(row); }
            catch(Exception ex) {result="Не удалось прочитать журнал: "+ex.Message;saveFailed=true;}
            foreach(var rule in Rules) {
                if(original.Rules.TryGetValue(rule.Code,out var saved) && saved!=null) {
                    rule.Action=saved.Action=="Toast"?Rule.Toast:saved.Action=="Journal"?Rule.Journal:Rule.Show;rule.Consent=saved.Consent;
                }
                var observed=Log.FirstOrDefault(row=>row.Code==rule.FailureId);
                if(observed!=null&&!string.IsNullOrWhiteSpace(observed.Name))rule.Description=observed.Name;
            }
            foreach(var row in Log.GroupBy(row=>row.Code).Select(g=>g.First()).Where(row=>!string.IsNullOrWhiteSpace(row.Code)&&!Rules.Any(rule=>rule.FailureId==row.Code)).Take(100))
                Rules.Add(new Rule {Code=row.Code,Name=row.Name,Description=row.Name,Unknown=true});
            View=CollectionViewSource.GetDefaultView(Rules);View.Filter=Matches;
            SearchCommand=new Relay(()=>{appliedSearch=Search.Trim();RefreshView();});
            ResetSearchCommand=new Relay(()=>{Search="";appliedSearch="";filter=Filters[0];RefreshView();Changed(nameof(Filter));});
            SaveCommand=new Relay(Save,()=>CanSave);
            CancelCommand=new Relay(()=>CloseRequested?.Invoke());
            JournalCommand=new Relay(OpenJournal);
            HelpCommand=new Relay(()=>Open(Path.Combine(Path.GetDirectoryName(typeof(SettingsWindow).Assembly.Location),@"Docs\PluginInstructions\SAB_HTML_Uvedomleniya.html")));
            foreach(var rule in Rules)rule.PropertyChanged+=RuleChanged;
            Selected=Rules[0];
        }
        Rule Add(string code,string name,string text,string kind,bool ok,bool protect=false,string protection="")
        { var rule=new Rule {Code=code,Name=name,Description=text,KindLabel=kind,HasOk=ok,Protected=protect,Protection=protection};Rules.Add(rule);return rule; }
        bool Matches(object item)
        {
            var r=(Rule)item;
            return (appliedSearch.Length==0||(r.Name+" "+r.Description+" "+r.Code).IndexOf(appliedSearch,StringComparison.OrdinalIgnoreCase)>=0)
                && (Filter==Filters[0]||Filter==Filters[1]&&r.Action!=Rule.Show||Filter==Filters[2]&&!r.HasOk&&!r.Unknown||Filter==Filters[3]&&r.Protected);
        }
        public void RefreshView()
        {
            var prior=Selected;View.Refresh();var visible=View.Cast<Rule>().ToList();
            Selected=prior!=null&&visible.Contains(prior)?prior:visible.FirstOrDefault();Changed(nameof(EmptyVisibility));Changed(nameof(Summary));
        }
        void RuleChanged(object sender,PropertyChangedEventArgs e)
        {
            MarkDirty();RefreshView();
        }
        void MarkDirty() { dirty=true;result="";saveFailed=false;saved=false;Changed();SaveCommand?.Refresh(); }
        public void SetRuntimeError(string message) { saveFailed=true;result=message;Changed(); }
        public void Save()
        {
            if(!CanSave)return;
            try {
                var next=store.Clone(original);
                next.Enabled=Enabled;next.FamilyException=FamilyException;next.GroupException=GroupException;next.WorksharedException=WorksharedException;
                next.ToastSeconds=int.Parse(Duration);next.JournalFolder=Path.GetFullPath(Folder.Trim());
                foreach(var rule in Rules)next.Rules[rule.Code]=new RuleSetting {Action=rule.Action==Rule.Toast?"Toast":rule.Action==Rule.Journal?"Journal":"Show",Consent=rule.Consent};
                store.Save(next);apply?.Invoke(store.Clone(next));
                dirty=false;saveFailed=false;saved=true;result="Настройки сохранены";Changed();SaveCommand.Refresh();
            } catch(Exception ex) {saveFailed=true;result="Не удалось сохранить настройки: "+ex.Message;Changed();}
        }
        void OpenJournal()
        {
            try {Open(ExportJournal());}
            catch(Exception ex) {saveFailed=true;result="Не удалось открыть журнал: "+ex.Message;Changed();}
        }
        public string ExportJournal()
        {
            // Export follows committed settings, never an unsaved folder draft.
            string journalFolder=store.Load().JournalFolder;
            Directory.CreateDirectory(journalFolder);
            Func<string,string> e=value=>WebUtility.HtmlEncode(value??"");
            string html="<!doctype html><html lang='ru'><meta charset='utf-8'><title>SAB — Журнал уведомлений</title><style>body{font:16px Segoe UI;max-width:1100px;margin:40px auto}td,th{padding:12px;text-align:left;border-bottom:1px solid #ddd;overflow-wrap:anywhere}table{width:100%;table-layout:fixed}pre{white-space:pre-wrap}</style><h1>Журнал уведомлений</h1><table><tr><th>Время / модель</th><th>Сообщение / код</th><th>Действие</th></tr>";
            html+=string.Concat(Log.Select(r=>"<tr><td>"+e(r.Time)+"<br>"+e(r.Model)+"</td><td><pre>"+e(r.Name)+"</pre>"+e(r.Code)+"<br>ID: "+e(r.Ids)+"</td><td>"+e(r.Action)+"</td></tr>"))+"</table></html>";
            string path=Path.Combine(journalFolder,"SAB-Уведомления.html");File.WriteAllText(path,html,new System.Text.UTF8Encoding(false));return path;
        }
        void Open(string path) { try {Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch(Exception ex){saveFailed=true;result="Не удалось открыть файл: "+ex.Message;Changed();} }
    }
}

