using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;
using SAB.TemplateEditorPreview.Models;
using SAB.TemplateEditorPreview.ViewModels;
using SAB.TemplateEditorPreview.Views;

namespace SAB.TemplateEditorPreview.Services { public class ViewTemplateGraphicsDataService { } }

namespace SAB.TemplateEditorPreview
{
    public static class PreviewDialogs
    {
        public static string LastMessage;
        public static void Show(string title, string message)
        {
            LastMessage = message;
            var window=Application.Current.Windows.OfType<Window>().FirstOrDefault(w=>w.IsActive) ?? Application.Current.Windows.OfType<Window>().LastOrDefault();
            var validation=window==null?null:PreviewShell.Children<TextBlock>(window).FirstOrDefault(x=>x.Name=="PreviewValidation");
            if(validation!=null) { validation.Text=message; validation.Visibility=System.Windows.Visibility.Visible; }
            else if (!Program.Capture) MessageBox.Show(message, title + " · макет", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    public static class PreviewShell
    {
        public static void Attach(Window window)
        {
            window.Loaded += (s,e) => {
                window.ShowInTaskbar = window.Owner == null;
                foreach (var button in Children<Button>(window)) if (button.Name == "InstructionButton")
                    button.Click += (a,b) => Process.Start(new ProcessStartInfo(Program.Root + @"\Docs\PluginInstructions\SAB_HTML_Redaktor_shablonov.html") { UseShellExecute = true });
                var instruction=Children<Button>(window).First(x=>x.Name=="InstructionButton");
                var actions=VisualTreeHelper.GetParent(instruction) as System.Windows.Controls.Grid;
                var footer=VisualTreeHelper.GetParent(actions) as System.Windows.Controls.Grid;
                int row=window is ViewTemplateGraphicsWindow?1:0;
                if(row==1) { footer.RowDefinitions.Insert(1,new RowDefinition { Height=GridLength.Auto }); System.Windows.Controls.Grid.SetRow(actions,2); }
                var status=new TextBlock { Name="PreviewValidation",TextWrapping=TextWrapping.Wrap,Visibility=System.Windows.Visibility.Collapsed,Margin=new Thickness(0,0,0,12),Foreground=(Brush)window.FindResource("SabBrush.Error") };
                System.Windows.Controls.Grid.SetRow(status,row); footer.Children.Add(status);
                // Keyboard invokes the existing search action; no separate filtering implementation.
                window.PreviewKeyDown += (a,b) => {
                    if (b.Key == Key.Enter && Keyboard.FocusedElement is TextBox box && ((box.Tag as string??"").Contains("Search") || box.Name.Contains("Search"))) {
                        var parent=VisualTreeHelper.GetParent(box);
                        while(parent!=null && !(parent is System.Windows.Controls.Grid)) parent=VisualTreeHelper.GetParent(parent);
                        var find=parent==null?null:Children<Button>(parent).FirstOrDefault(x=>x.Name.StartsWith("Find") || (x.Tag as string??"").StartsWith("Find"));
                        if(find!=null) { find.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); b.Handled=true; }
                    }
                };
            };
        }
        public static IEnumerable<T> Children<T>(DependencyObject node) where T:DependencyObject
        {
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++) {
                var child=VisualTreeHelper.GetChild(node,i); if(child is T t) yield return t;
                foreach(var nested in Children<T>(child)) yield return nested;
            }
        }
    }
    public static class Fixtures
    {
        public static List<TemplateSelectionItem> Templates()
        {
            var result = new List<TemplateSelectionItem>();
            string[] names={"АР · План этажа — рабочая документация", "АР · План отделки — согласование", "АР · Разрезы и фасады", "КР · План несущих конструкций", "ОВ · Координация инженерных систем", "АР · Экспликация помещений — очень длинное название шаблона для проверки полного имени и доступности данных"};
            for(int i=0;i<30;i++) result.Add(new TemplateSelectionItem {TemplateIdValue=i+1,Name=i<names.Length?names[i]:"АР · Рабочий шаблон "+(i+1).ToString("00"), ViewTypeName=i==2?"Разрез":"План этажа"});
            return result;
        }
        public static ViewTemplateGraphicsData Build(IList<TemplateSelectionItem> selected)
        {
            var data=new ViewTemplateGraphicsData { SourceTemplateIdValue=selected[0].TemplateIdValue, SourceTemplateName=selected.Count==1?selected[0].Name:"Сравнение шаблонов: "+selected.Count, AssignedViewCount=24, SupportsRevitLinkOverrides=false };
            data.LineWeights.Add(new NamedIntegerOption(-1,"По категории"));
            for(int i=1;i<=16;i++) data.LineWeights.Add(new NamedIntegerOption(i,i.ToString()));
            data.LinePatterns.Add(new NamedElementOption(-1,"По категории")); data.LinePatterns.Add(new NamedElementOption(101,"Сплошная")); data.LinePatterns.Add(new NamedElementOption(102,"Штриховая"));
            data.FillPatterns.Add(new NamedElementOption(-1,"Нет переопределения")); data.FillPatterns.Add(new NamedElementOption(201,"Сплошная заливка")); data.FillPatterns.Add(new NamedElementOption(202,"Бетон — диагональная штриховка 45°"));
            data.DetailLevels.Add(new NamedDetailLevelOption(ViewDetailLevel.Undefined,"По виду")); data.DetailLevels.Add(new NamedDetailLevelOption(ViewDetailLevel.Coarse,"Низкий")); data.DetailLevels.Add(new NamedDetailLevelOption(ViewDetailLevel.Medium,"Средний")); data.DetailLevels.Add(new NamedDetailLevelOption(ViewDetailLevel.Fine,"Высокий"));
            data.WorksetVisibilities.Add(new NamedIntegerOption((int)WorksetVisibility.UseGlobalSetting,"По общим настройкам")); data.WorksetVisibilities.Add(new NamedIntegerOption((int)WorksetVisibility.Visible,"Показать")); data.WorksetVisibilities.Add(new NamedIntegerOption((int)WorksetVisibility.Hidden,"Скрыть"));
            data.RevitLinkVisibilityTypes.Add(new NamedStringOption("ByHostView","По основному виду")); data.RevitLinkVisibilityTypes.Add(new NamedStringOption("ByLinkedView","По связанному виду"));
            foreach(var tab in new[]{data.ModelCategories,data.AnnotationCategories,data.AnalyticalCategories,data.ImportedCategories}) {
                tab.Section.IsIncluded=true; tab.IsGroupVisible=true;
                string[] names = tab==data.ModelCategories?new[]{"Стены","Двери","Окна","Перекрытия","Потолки","Крыши","Обобщённые модели","Несущие конструкции","Мебель","Сантехнические приборы"}:tab==data.AnnotationCategories?new[]{"Марки помещений","Размеры","Текстовые примечания","Обозначения разрезов"}:tab==data.AnalyticalCategories?new[]{"Аналитические узлы","Аналитические стержни","Аналитические панели"}:new[]{"Подоснова генерального плана.dwg","Координационные оси.dwg"};
                for(int i=0;i<names.Length;i++) {
                    bool model=tab==data.ModelCategories,analytical=tab==data.AnalyticalCategories;
                    // Synthetic category capabilities. Real category/view probes belong to stage2.
                    var row=new CategoryOverrideRow { CategoryIdValue=tab.Rows.Count+1000,Name=names[i],AllowsVisibilityControl=true,IsVisible=true,SupportsCut=model&&i!=8&&i!=9,SupportsSurfacePatterns=model||(analytical&&i==2),SupportsTransparency=model||(analytical&&i==2),SupportsDetailLevel=model||(analytical&&i==2),ParentCategoryIdValue=-1,IsExpanded=model&&i==0 };
                    row.Graphics.ProjectionLineWeight=i==0?3:-1; row.Graphics.DetailLevel=ViewDetailLevel.Undefined;
                    if(selected.Count>1 && i%2==1) row.Graphics.MergeValues(new GraphicOverrideData { ProjectionLineWeight=5 });
                    row.IsComparisonActive=selected.Count>1; tab.Rows.Add(row);
                    string[] children=model&&i==0?new[]{"Общие грани","Скрытые линии","Дополнительные линии"}:tab==data.AnnotationCategories&&i==3?new[]{"Границы разреза","Линии разреза"}:analytical&&i==1?new[]{"Локальные оси","Жёсткие связи"}:tab==data.ImportedCategories?new[]{"A-WALL","A-DOOR","A-ANNO"}:new string[0];
                    row.HasChildren=children.Length>0;
                    for(int c=0;c<children.Length;c++)tab.Rows.Add(new CategoryOverrideRow {CategoryIdValue=20000+(int)tab.Group*1000+i*10+c,ParentCategoryIdValue=row.CategoryIdValue,Name=children[c],IndentLevel=1,IsVisible=true,AllowsVisibilityControl=true});
                }
                tab.IsComparisonActive=selected.Count>1; tab.RefreshRowVisibility();
            }
            data.ModelCategories.Rows.Add(new CategoryOverrideRow {CategoryIdValue=(int)BuiltInCategory.OST_RvtLinks,Name="Связи Revit",AllowsVisibilityControl=true,IsVisible=true,ParentCategoryIdValue=-1,SupportsProjectionLines=false});
            data.FiltersSection.IsIncluded=true; data.WorksetsSection.IsIncluded=true;
            string[] filters={"АР · Несущие стены", "АР · Огнестойкость REI 60", "АР · Отделка помещений", "КР · Существующие конструкции", "ОВ · Проверка коллизий", "АР · Проверка параметров элементов — длинное название фильтра"};
            for(int i=0;i<filters.Length;i++) data.Filters.Add(new FilterOverrideRow {FilterIdValue=i+2000,Name=filters[i],IsIncluded=i<3,IsEnabled=true,IsVisible=true,IsPresentInTable=i<3});
            foreach(var name in new[]{"АР · Архитектура","КР · Несущие конструкции","ОВ · Инженерные системы","Общие уровни и оси"}) data.Worksets.Add(new WorksetOverrideRow {Name=name, WorksetIdValue=data.Worksets.Count+1,Visibility=WorksetVisibility.UseGlobalSetting});
            AddProperty(data,"Масштаб вида","1 : 100",StorageType.String,true);
            AddProperty(data,"Значение масштаба","100",StorageType.Integer,true);
            data.ViewProperties[1].ConfigureScaleValue(); data.ViewProperties[0].ConfigureScaleMode(data.ViewProperties[1],new List<int>{20,50,100,200,500});
            AddProperty(data,"Уровень детализации","Средний",StorageType.String,true,"Низкий","Средний","Высокий");
            AddProperty(data,"Визуальный стиль","Скрытая линия",StorageType.String,true,"Каркас","Скрытая линия","Тонированный");
            AddProperty(data,"Дисциплина","Архитектура",StorageType.String,true,"Архитектура","Конструкции","Координация");
            AddProperty(data,"Имя шаблона",data.SourceTemplateName,StorageType.String,false);
            AddProperty(data,"Стадия","Новая конструкция",StorageType.String,true,"Существующая","Новая конструкция");
            AddProperty(data,"Схема цветовой заливки","По назначению помещений",StorageType.String,true);
            AddProperty(data,"Графика модели","Изменить…",StorageType.None,false);
            var nav=data.ViewProperties.Last(); nav.ConfigureNavigation("Model");
            if(selected.Count>1) data.ViewProperties[2].MergeValue(new ViewTemplateParameterValue { StorageType=StorageType.ElementId,DisplayValue="Высокий",ElementIdValue=999 },true,true);
            data.StartTrackingChanges(); return data;
        }
        static void AddProperty(ViewTemplateGraphicsData data,string name,string value,StorageType type,bool editable,params string[] options)
        {
            if(options.Length>0) type=StorageType.ElementId;
            var row=new ViewTemplateParameterRow(data.ViewProperties.Count+4000,name,new ViewTemplateParameterValue {StorageType=type,DisplayValue=value,StringValue=value,ElementIdValue=Array.IndexOf(options,value)+5000},true,editable);
            for(int i=0;i<options.Length;i++) row.AddElementOption(i+5000,options[i]); data.ViewProperties.Add(row);
        }
    }
    public static class Program
    {
        [DllImport("kernel32",CharSet=CharSet.Unicode)] static extern bool SetDllDirectory(string path);
        [DllImport("user32")] static extern bool SetForegroundWindow(IntPtr handle);
        [DllImport("user32")] static extern bool ShowWindow(IntPtr handle,int command);
        static Mutex instanceMutex;
        public static bool Capture;
        public static string Root=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,@"..\..\.."));
        [STAThread] public static int Main(string[] args)
        {
            instanceMutex=new Mutex(true,@"Local\SAB.TemplateEditor.Preview.Stage1",out bool first);
            if(!first) {
                foreach(var process in Process.GetProcessesByName("SAB.TemplateEditor.Preview")) if(process.Id!=Process.GetCurrentProcess().Id&&process.MainWindowHandle!=IntPtr.Zero) { ShowWindow(process.MainWindowHandle,9);SetForegroundWindow(process.MainWindowHandle);break; }
                instanceMutex.Dispose();return 0;
            }
            string revit=@"C:\Program Files\Autodesk\Revit 2023";
            SetDllDirectory(revit);
            AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=> { var path=Path.Combine(revit,new AssemblyName(e.Name).Name+".dll"); return File.Exists(path)?Assembly.LoadFrom(path):null; };
            try { return Run(args); } catch(Exception ex) { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"error.txt"),ex.ToString()); return 1; }
            finally { instanceMutex.ReleaseMutex();instanceMutex.Dispose(); }
        }
        [MethodImpl(MethodImplOptions.NoInlining)] static int Run(string[] args)
        {
            Capture=args.Contains("--capture");
            var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
            try {
                if(Capture) Evidence.Run();
                else {
                    var vm=new ViewTemplateGraphicsViewModel(null,Fixtures.Templates(),new Services.ViewTemplateGraphicsDataService());
                    var window=new ViewTemplateGraphicsWindow(vm);
                    bool? accepted=window.ShowDialog();
                    if(accepted==true) MessageBox.Show("Макет: подготовлено шаблонов — "+window.TargetTemplateIdValues.Count+"; изменено строк — "+vm.ModifiedSettingCount+".\nТестовые данные. Запись в Revit не выполнялась.","SAB · Результат макета");
                }
                app.Shutdown(); return 0;
            } catch(Exception ex) { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"error.txt"),ex.ToString()); app.Shutdown(); return 1; }
        }
    }
}
