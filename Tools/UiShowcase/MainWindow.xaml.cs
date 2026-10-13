using System;
using System.Windows;
using System.Windows.Controls;

namespace SAB.UiShowcase
{
    public partial class MainWindow : Window
    {
        public DemoModel Model { get; }=new DemoModel();
        public ReferenceView Reference { get; private set; }
        bool ready; string currentConcept="Unified";
        public MainWindow() { InitializeComponent(); DataContext=Model;Reference=new ReferenceView{DataContext=Model};ready=true;ShowPage(0); }
        public void ApplyConcept(string name)
        {
            if(currentConcept==name)return;
            currentConcept=name;
            Application.Current.Resources.MergedDictionaries[1]=new ResourceDictionary {Source=new Uri("Styles/"+name+".xaml",UriKind.Relative)};
            foreach(ComboBoxItem i in ConceptSelector.Items) if((string)i.Tag==name) {ConceptSelector.SelectedItem=i;break;}
        }
        void ConceptChanged(object s,SelectionChangedEventArgs e) {if(ready)ApplyConcept((string)((ComboBoxItem)ConceptSelector.SelectedItem).Tag);}
        void PageChanged(object s,SelectionChangedEventArgs e) {if(ready)ShowPage(PageSelector.SelectedIndex);}
        public void ShowPage(int index)
        {
            if(PageSelector.SelectedIndex!=index)PageSelector.SelectedIndex=index;
            PageHost.Children.Clear();
            PageHost.Children.Add(index==0?(UIElement)Reference:Gallery.Build(index,Model));
        }
    }
}
