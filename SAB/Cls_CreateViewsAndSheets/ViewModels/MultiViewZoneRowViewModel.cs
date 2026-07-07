using System.Collections.ObjectModel;
using System.ComponentModel;
using SAB.CreateViewsAndSheets.Models;

namespace SAB.CreateViewsAndSheets.ViewModels
{
    public class MultiViewZoneRowViewModel : INotifyPropertyChanged
    {
        private string _zoneName;
        private RevitElementItem _selectedSourceSheet;
        private RevitElementItem _selectedViewportType;
        private RevitElementItem _selectedTitleBlockType;

        public MultiViewZoneRowViewModel()
        {
            _zoneName = string.Empty;
            Floors = new ObservableCollection<MultiViewZoneFloorRowViewModel>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<MultiViewZoneFloorRowViewModel> Floors { get; private set; }

        public string ZoneName
        {
            get { return _zoneName; }
            set
            {
                string newValue = value ?? string.Empty;
                if (_zoneName == newValue)
                {
                    return;
                }

                _zoneName = newValue;
                OnPropertyChanged("ZoneName");
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

        public RevitElementItem SelectedViewportType
        {
            get { return _selectedViewportType; }
            set
            {
                if (ReferenceEquals(_selectedViewportType, value))
                {
                    return;
                }

                _selectedViewportType = value;
                OnPropertyChanged("SelectedViewportType");
            }
        }

        public RevitElementItem SelectedTitleBlockType
        {
            get { return _selectedTitleBlockType; }
            set
            {
                if (ReferenceEquals(_selectedTitleBlockType, value))
                {
                    return;
                }

                _selectedTitleBlockType = value;
                OnPropertyChanged("SelectedTitleBlockType");
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

    public class MultiViewZoneFloorRowViewModel : INotifyPropertyChanged
    {
        private string _floorName;
        private RevitElementItem _selectedSourceView;

        public MultiViewZoneFloorRowViewModel()
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
