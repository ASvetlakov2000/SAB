using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace SAB.UiShowcase
{
    public class Observable : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    public sealed class ParameterOption
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public string Identity { get; set; }
        public string Summary => DataType + " · " + Identity;
        public string Details => Name + "\n" + Summary + "\nGUID: " + Guid;
        public string Guid { get; set; }
        public override string ToString() => Name;
    }
    public sealed class Rule : Observable, IDataErrorInfo
    {
        string entity; ParameterOption parameter; string source; bool enabled=true;
        public bool Enabled { get => enabled; set { enabled=value; Changed(nameof(Enabled)); } }
        public string Entity { get => entity; set { entity=value; Changed(nameof(Entity)); Changed(nameof(ErrorText)); } }
        public ParameterOption Parameter { get => parameter; set { parameter=value; Changed(nameof(Parameter)); Changed(nameof(ErrorText)); } }
        public string Source { get => source; set { source=value; Changed(nameof(Source)); } }
        public string ErrorText => string.IsNullOrWhiteSpace(Entity) ? "Введите название сущности." : Parameter==null ? "Выберите параметр назначения." : "";
        public string Error => ErrorText;
        public string this[string name] => name==nameof(Entity)&&string.IsNullOrWhiteSpace(Entity) ? ErrorText : "";
        public override string ToString()=>Entity;
    }
    public sealed class CategoryChoice : Observable
    {
        bool selected;
        public string Name { get; set; }
        public int Count { get; set; }
        public bool Selected { get => selected; set { selected=value; Changed(nameof(Selected)); } }
        public string Display => Name + "  ·  " + Count;
    }
    public sealed class ValuePair { public string Source { get; set; } public string Target { get; set; } }
    public sealed class LevelValue { public string Name { get; set; } public string Elevation { get; set; } public string Prefix { get; set; } }
    public sealed class Relay : ICommand
    {
        readonly Action action; readonly Func<bool> can;
        public Relay(Action action, Func<bool> can=null) { this.action=action;this.can=can; }
        public bool CanExecute(object p) => can?.Invoke()??true;
        public void Execute(object p) => action();
        public event EventHandler CanExecuteChanged { add { CommandManager.RequerySuggested+=value; } remove { CommandManager.RequerySuggested-=value; } }
    }
    public sealed class DemoModel : Observable
    {
        public ObservableCollection<Rule> Rules { get; } = new ObservableCollection<Rule>();
        public ObservableCollection<ParameterOption> Parameters { get; } = new ObservableCollection<ParameterOption>();
        public ObservableCollection<CategoryChoice> Categories { get; } = new ObservableCollection<CategoryChoice>();
        public ObservableCollection<ValuePair> Values { get; } = new ObservableCollection<ValuePair>();
        public ObservableCollection<LevelValue> Levels { get; } = new ObservableCollection<LevelValue>();
        public string[] Sources { get; } = { "Имя семейства", "Имя типа", "Имя материала", "Параметр элемента", "Постоянное значение", "Уровень элемента" };
        Rule selectedRule; string status="Тестовые данные · модель Revit не подключена";
        public Rule SelectedRule { get => selectedRule; set { selectedRule=value; Changed(nameof(SelectedRule)); } }
        public string Status { get => status; set { status=value; Changed(nameof(Status)); } }
        public int RuleCount => Rules.Count;
        public ICommand Add { get; } public ICommand Remove { get; } public ICommand Save { get; } public ICommand Reset { get; }
        public DemoModel()
        {
            string[] names={"SAB_Материал_Имя","SAB_Материал_Описание","SAB_Материал_Модель","SAB_Развертка_Номер","SAB_Элемент_Код","SAB_Элемент_Наименование","Комментарии","Марка","SAB_Помещение_Номер","SAB_Уровень_Имя","SAB_Элемент_Длина","SAB_Элемент_Площадь"};
            for(int i=0;i<names.Length;i++) Parameters.Add(new ParameterOption {Name=names[i],DataType=i==10?"Длина":i==11?"Площадь":"Текст",Identity=i<6?"Общий · экземпляр":"Проектный · экземпляр",Guid="00000000-0000-4000-8000-"+(i+1).ToString("D12")});
            string[] cats={"Стены","Перекрытия","Двери","Окна","Обобщённые модели","Каркас несущий","Колонны","Потолки","Крыши","Ограждения"};
            for(int i=0;i<cats.Length;i++) Categories.Add(new CategoryChoice {Name=cats[i],Count=12+i*7,Selected=i<4});
            for(int i=0;i<5;i++) Values.Add(new ValuePair{Source=new[]{"Бетон","Кирпич","Сталь","Дерево","Стекло"}[i],Target=new[]{"БТ","КР","СТ","ДР","СК"}[i]});
            for(int i=0;i<6;i++) Levels.Add(new LevelValue{Name=$"{i+1:00} · Этаж {i+1}",Elevation=(i*3.3).ToString("0.000"),Prefix=$"Э{i+1}"});
            Rules.CollectionChanged+=(s,e)=>Changed(nameof(RuleCount));
            Add=new Relay(()=>{var r=new Rule{Entity="Новая сущность",Parameter=Parameters[0],Source=Sources[0]};Rules.Add(r);SelectedRule=r;Status="Правило добавлено в демонстрацию.";});
            Remove=new Relay(()=>{Rules.Remove(SelectedRule);SelectedRule=Rules.FirstOrDefault();Status="Правило удалено из демонстрации.";},()=>SelectedRule!=null);
            Save=new Relay(()=>Status=Rules.Any(r=>r.ErrorText!="")?"Ошибка: заполните название и параметр назначения.":"Проверка пройдена. Изменения сохранены только в памяти стенда.");
            Reset=new Relay(LoadRules); LoadRules();
        }
        public void LoadRules()
        {
            Rules.Clear(); string[] entities={"Материал — имя","Материал — описание","Материал — модель","Маркировка развёрток","Код элемента","Наименование элемента","Комментарии","Марка"};
            for(int i=0;i<8;i++) Rules.Add(new Rule{Entity=entities[i],Parameter=Parameters[i],Source=Sources[i%Sources.Length],Enabled=i!=6});
            SelectedRule=Rules[0];Status="Тестовые данные · модель Revit не подключена";
        }
    }
}
