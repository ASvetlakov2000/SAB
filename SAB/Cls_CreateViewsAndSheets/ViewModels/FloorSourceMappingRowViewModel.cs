using System.ComponentModel;
using SAB.CreateViewsAndSheets.Models;

namespace SAB.CreateViewsAndSheets.ViewModels
{
    public class FloorSourceMappingRowViewModel : INotifyPropertyChanged
    {
        private string _floorName;
        private RevitElementItem _selectedSourceView;
        private RevitElementItem _selectedSourceSheet;

        public FloorSourceMappingRowViewModel()
        {
            _floorName = string.Empty;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string FloorName
        {
            get { return _floorName; }
            set
            {
                string newValue = value ?? string.Empty;
                if (_floorName == newValue)
                {
                    return;
                }

                _floorName = newValue;
                OnPropertyChanged("FloorName");
            }
        }

        public RevitElementItem SelectedSourceView
        {
            get { return _selectedSourceView; }
            set
            {
                if (ReferenceEquals(_selectedSourceView, value))
                {
                    return;
                }

                _selectedSourceView = value;
                OnPropertyChanged("SelectedSourceView");
            }
        }

        public RevitElementItem SelectedSourceSheet
        {
            get { return _selectedSourceSheet; }
            set
            {
                if (ReferenceEquals(_selectedSourceSheet, value))
                {
                    return;
                }

                _selectedSourceSheet = value;
                OnPropertyChanged("SelectedSourceSheet");
            }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
