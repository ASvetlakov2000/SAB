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

namespace SAB.NotificationSettingsPreview
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
        public Brush KindForeground => (Application.Current?.MainWindow?.TryFindResource(Destructive||Protected?"SabBrush.Error":Unknown||!HasOk?"SabBrush.TextSecondary":Code=="C01"||Code=="C08"?"SabBrush.Accent":"SabBrush.Warning") as Brush) ?? Brushes.Black;
        public Brush KindBackground => new SolidColorBrush((KindForeground as SolidColorBrush)?.Color ?? Colors.Gray) {Opacity=.08};
        public string ConfirmationLabel => Unknown?"Подтверждение ещё не проверено":Protected?"Решение принимает пользователь":HasOk?"Требует нажатия OK · пример":"Не требует подтверждения";
        public Visibility ConsentVisibility => Destructive && action!=Show?Visibility.Visible:Visibility.Collapsed;
        public string Validation => Destructive && action!=Show && !Consent?"Подтвердите разрешение на зависимое удаление.":"";
        public Visibility ValidationVisibility => Validation.Length>0?Visibility.Visible:Visibility.Collapsed;
        public string CapabilityNote => CanEdit?"Демонстрационный диалог: наличие OK в Revit ещё не проверено.":Unknown?"До определения типа окна: не подавлять.":Protected?"Обязательное правило: "+Protection:"Нажатие OK не требуется.";
        public string Effect => !CanEdit?Protected?"Штатное сообщение и выбор действий Revit сохраняются.":"Сообщение остаётся видимым в Revit.":action==Show?"Штатный диалог; решение принимает пользователь.":Destructive?"Будет подтверждено удаление дополнительных элементов. Действие записывается в журнал.":action==Toast?"OK автоматически; исходный текст — в уведомлении SAB и журнале.":"OK автоматически; исходный текст — в журнале.";
    }
    public sealed class LogRow { public string Time { get; set; } public string Name { get; set; } public string Action { get; set; } }
    public sealed class SettingsModel : Notifier
    {
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
        public string Summary => "Показано: "+View.Cast<Rule>().Count()+" из "+Rules.Count+"   ·   Подавляется: "+Rules.Count(r=>r.Action!=Rule.Show)+"   ·   "+(Enabled?"Подавление включено":"Подавление выключено");
        public string Validation {
            get {
                if(Rules.Any(r=>r.Validation.Length>0))return "Не подтверждено разрешение на зависимое удаление.";
                if(!int.TryParse(Duration,out int seconds)||seconds<3||seconds>60)return "Укажите длительность уведомления от 3 до 60 секунд.";
                if(string.IsNullOrWhiteSpace(Folder))return "Укажите папку журнала.";
                try { if(!Path.IsPathRooted(Folder))return "Укажите полный путь к папке журнала.";Path.GetFullPath(Folder); }
                catch { return "Путь к папке журнала содержит недопустимые символы."; }
                return "";
            }
        }
        public bool CanSave => Validation.Length==0;
        public bool Dirty => dirty;
        public string Status => Validation.Length>0?Validation:result.Length>0?result:dirty?"Есть несохранённые изменения":"";
        public Visibility StatusVisibility => Status.Length>0?Visibility.Visible:Visibility.Collapsed;
        public Brush StatusBrush => (Brush)Application.Current.MainWindow.FindResource(Validation.Length>0?"SabBrush.Error":result.Length>0&&!dirty?"SabBrush.Success":"SabBrush.TextSecondary");
        public SettingsModel()
        {
            folder=Path.Combine(Program.Output,"Журнал");
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
            View=CollectionViewSource.GetDefaultView(Rules);View.Filter=Matches;
            SearchCommand=new Relay(()=>{appliedSearch=Search.Trim();RefreshView();});
            ResetSearchCommand=new Relay(()=>{Search="";appliedSearch="";filter=Filters[0];RefreshView();Changed(nameof(Filter));});
            SaveCommand=new Relay(Save,()=>CanSave);
            CancelCommand=new Relay(()=>CloseRequested?.Invoke());
            JournalCommand=new Relay(OpenJournal);
            HelpCommand=new Relay(()=>Open(Path.Combine(Program.Root,@"Tools\UiShowcase\NotificationSettings\Help.html")));
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
            var r=(Rule)sender;Log.Insert(0,new LogRow {Time=DateTime.Now.ToString("HH:mm:ss"),Name=r.Name,Action="Выбрано: "+r.ActionShort});
            Changed(nameof(JournalEmptyVisibility));MarkDirty();RefreshView();
        }
        void MarkDirty() { dirty=true;result="";Changed();SaveCommand?.Refresh(); }
        public void Save()
        {
            if(!CanSave)return;
            try {
                Directory.CreateDirectory(Program.Output);
                var state=new {Scope="standalone-preview-only",Enabled,FamilyException,GroupException,WorksharedException,Duration,Folder,Rules=Rules.Select(r=>new{r.Code,r.Action,r.Consent}).ToArray()};
                File.WriteAllText(Path.Combine(Program.Output,"demo-settings.json"),new JavaScriptSerializer().Serialize(state));
                dirty=false;result="Настройки сохранены в макете";Changed();SaveCommand.Refresh();
            } catch(Exception ex) {result="Не удалось сохранить макет: "+ex.Message;Changed();}
        }
        void OpenJournal()
        {
            try {
                if(!CanSave) {result=Validation;Changed();return;}
                Open(WriteDemoJournal());
            } catch(Exception ex) {result="Не удалось открыть журнал: "+ex.Message;Changed();}
        }
        internal string WriteDemoJournal()
        {
            // Folder is a proposed production preference; preview I/O remains isolated.
            string demoFolder=Path.Combine(Program.Output,"Журнал");Directory.CreateDirectory(demoFolder);
            string html="<!doctype html><html lang='ru'><meta charset='utf-8'><title>SAB — журнал макета</title><style>body{font:16px Segoe UI;max-width:1000px;margin:40px auto}td,th{padding:12px;text-align:left;border-bottom:1px solid #ddd}</style><h1>Журнал макета</h1><p>Настройка правил. Автоматические ответы Revit не выполнялись.</p><table><tr><th>Время</th><th>Сообщение</th><th>Выбор</th></tr>";
            html+=string.Concat(Log.Select(r=>"<tr><td>"+WebUtility.HtmlEncode(r.Time)+"</td><td>"+WebUtility.HtmlEncode(r.Name)+"</td><td>"+WebUtility.HtmlEncode(r.Action)+"</td></tr>"))+"</table></html>";
            string path=Path.Combine(demoFolder,"SAB-demo-journal.html");File.WriteAllText(path,html);return path;
        }
        void Open(string path) { try {Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch(Exception ex){result="Не удалось открыть файл: "+ex.Message;Changed();} }
    }
}
