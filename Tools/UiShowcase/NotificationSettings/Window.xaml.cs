using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace SAB.NotificationSettingsPreview
{
    public partial class SettingsWindow : Window
    {
        public SettingsModel Model { get; }
        public SettingsWindow()
        {
            Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri(Path.Combine(Program.Root,@"SAB\UI\Styles\SABFluentLight.xaml"))});
            InitializeComponent();
            Application.Current.MainWindow=this;
            var badge=new Style(typeof(Border));
            badge.Setters.Add(new Setter(Border.PaddingProperty,new Thickness(6,1,6,1)));
            badge.Setters.Add(new Setter(Border.CornerRadiusProperty,new CornerRadius(4)));
            badge.Setters.Add(new Setter(Border.HorizontalAlignmentProperty,HorizontalAlignment.Left));
            badge.Setters.Add(new Setter(Border.BackgroundProperty,new Binding("KindBackground")));
            Resources.Add("NotificationTypeBadge",badge);
            // Columns are outside the visual tree: resolve the existing shared style explicitly.
            foreach(var column in RulesGrid.Columns.OfType<DataGridTextColumn>().Concat(JournalGrid.Columns.OfType<DataGridTextColumn>()))
                column.ElementStyle=(Style)FindResource("SabDataGridTextBlockStyle");
            Model=new SettingsModel();DataContext=Model;Model.CloseRequested+=Close;
            PreviewKeyDown+=(s,e)=> {
                if(e.Key==Key.Enter && SearchBox.IsKeyboardFocusWithin) {Model.SearchCommand.Execute(null);e.Handled=true;}
                if(e.Key==Key.Escape && !ActionBox.IsDropDownOpen && !FilterBox.IsDropDownOpen) {Close();e.Handled=true;}
            };
            Closed+=(s,e)=>Model.CloseRequested-=Close;
        }
    }
}
