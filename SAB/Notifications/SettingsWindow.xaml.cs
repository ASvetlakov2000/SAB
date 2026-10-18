using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace SAB.Notifications
{
    public partial class SettingsWindow : Window
    {
        public SettingsModel Model { get; }
        public SettingsWindow(NotificationSettingsStore store, Settings settings, Action<Settings> apply = null)
        {
            Resources.MergedDictionaries.Add(SAB.UI.FluentLight.FluentResources.CreateDictionary());
            InitializeComponent();
            // Columns are outside the visual tree: resolve the existing shared style explicitly.
            foreach(var column in RulesGrid.Columns.OfType<DataGridTextColumn>().Concat(JournalGrid.Columns.OfType<DataGridTextColumn>()))
                column.ElementStyle=(Style)FindResource("SabDataGridReadableTextBlockStyle");
            Model=new SettingsModel(store,settings,apply);DataContext=Model;Model.CloseRequested+=Close;
            PreviewKeyDown+=(s,e)=> {
                if(e.Key==Key.Enter && SearchBox.IsKeyboardFocusWithin) {Model.SearchCommand.Execute(null);e.Handled=true;}
                if(e.Key==Key.Escape && !ActionBox.IsDropDownOpen && !FilterBox.IsDropDownOpen) {Close();e.Handled=true;}
            };
            Closed+=(s,e)=>Model.CloseRequested-=Close;
        }
    }
}
