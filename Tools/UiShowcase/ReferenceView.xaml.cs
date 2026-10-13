using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace SAB.UiShowcase
{
    public partial class ReferenceView : UserControl
    {
        DemoModel Model => (DemoModel)DataContext;
        public ReferenceView()
        {
            InitializeComponent();
            DataContextChanged+=(s,e)=>{if(e.NewValue is DemoModel m)m.Rules.CollectionChanged+=(o,a)=>EmptyState.Visibility=m.Rules.Count==0?Visibility.Visible:Visibility.Collapsed;};
        }
        void ConfigureSource(object s,RoutedEventArgs e) {Model.SelectedRule=(Rule)((FrameworkElement)s).DataContext;WorkspaceTabs.SelectedIndex=1;}
        void ExportDemo(object s,RoutedEventArgs e)
        {
            var dialog=new SaveFileDialog{Filter="Демонстрационные правила (*.sab-demo.json)|*.sab-demo.json",FileName="SAB-preview.sab-demo.json"};
            if(dialog.ShowDialog()!=true)return;
            try {File.WriteAllText(dialog.FileName,new JavaScriptSerializer().Serialize(Model.Rules.Select(r=>new ExportRule{Entity=r.Entity,Parameter=r.Parameter?.Name,Source=r.Source,Enabled=r.Enabled}).ToArray()));Model.Status="Экспортирован файл стенда. Это не формат настроек рабочего SAB.";}catch(Exception ex){Model.Status="Не удалось экспортировать: "+ex.Message;}
        }
        void ImportDemo(object s,RoutedEventArgs e)
        {
            var dialog=new OpenFileDialog{Filter="Демонстрационные правила (*.sab-demo.json)|*.sab-demo.json"};if(dialog.ShowDialog()!=true)return;
            try {var data=new JavaScriptSerializer().Deserialize<ExportRule[]>(File.ReadAllText(dialog.FileName));if(data==null||data.Length>10000)throw new InvalidDataException("Некорректный список правил.");var mapped=data.Select(r=>new Rule{Entity=r.Entity,Parameter=Model.Parameters.FirstOrDefault(p=>p.Name==r.Parameter),Source=r.Source,Enabled=r.Enabled}).ToList();Model.Rules.Clear();foreach(var r in mapped)Model.Rules.Add(r);Model.SelectedRule=Model.Rules.FirstOrDefault();Model.Status="Импортированы тестовые правила. Модель Revit не изменялась.";}catch(Exception ex){Model.Status="Не удалось импортировать: "+ex.Message;}
        }
        public sealed class ExportRule {public string Entity{get;set;}public string Parameter{get;set;}public string Source{get;set;}public bool Enabled{get;set;}}
    }
}
