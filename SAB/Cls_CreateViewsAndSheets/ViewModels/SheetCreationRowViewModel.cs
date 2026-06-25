using System.ComponentModel;
using SAB.CreateViewsAndSheets.Models;

namespace SAB.CreateViewsAndSheets.ViewModels
{
    public class SheetCreationRowViewModel : INotifyPropertyChanged
    {
        private int _rowNumber;
        private string _floorName;
        private string _viewName;
        private string _viewScaleText;
        private RevitElementItem _selectedViewTemplate;
        private string _sheetNumber;
        private string _sheetName;
        private string _rowError;
        private bool _isScaleEnabled;

        public SheetCreationRowViewModel()
        {
            _floorName = string.Empty;
            _viewName = string.Empty;
            _viewScaleText = "50";
            _sheetNumber = string.Empty;
            _sheetName = string.Empty;
            _rowError = string.Empty;
            _isScaleEnabled = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public int RowNumber
        {
            get { return _rowNumber; }
            set
            {
                if (_rowNumber == value)
                {
                    return;
                }

                _rowNumber = value;
                OnPropertyChanged("RowNumber");
            }
        }

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

        public string ViewName
        {
            get { return _viewName; }
            set
            {
                string newValue = value ?? string.Empty;
                if (_viewName == newValue)
                {
                    return;
                }

                _viewName = newValue;
                OnPropertyChanged("ViewName");
                OnPropertyChanged("IsFilled");
            }
        }

        public string ViewScaleText
        {
            get { return _viewScaleText; }
            set
            {
                string newValue = value ?? string.Empty;
                if (_viewScaleText == newValue)
                {
                    return;
                }

                _viewScaleText = newValue;
                OnPropertyChanged("ViewScaleText");
            }
        }

        public RevitElementItem SelectedViewTemplate
        {
            get { return _selectedViewTemplate; }
            set
            {
                if (ReferenceEquals(_selectedViewTemplate, value))
                {
                    return;
                }

                _selectedViewTemplate = value;
                IsScaleEnabled = value == null || !value.ControlsScale;
                OnPropertyChanged("SelectedViewTemplate");
            }
        }

        public string SheetNumber
        {
            get { return _sheetNumber; }
            set
            {
                string newValue = value ?? string.Empty;
                if (_sheetNumber == newValue)
                {
                    return;
                }

                _sheetNumber = newValue;
                OnPropertyChanged("SheetNumber");
                OnPropertyChanged("IsFilled");
            }
        }

        public string SheetName
        {
            get { return _sheetName; }
            set
            {
                string newValue = value ?? string.Empty;
                if (_sheetName == newValue)
                {
                    return;
                }

                _sheetName = newValue;
                OnPropertyChanged("SheetName");
                OnPropertyChanged("IsFilled");
            }
        }

        public string RowError
        {
            get { return _rowError; }
            set
            {
                string newValue = value ?? string.Empty;
                if (_rowError == newValue)
                {
                    return;
                }

                _rowError = newValue;
                OnPropertyChanged("RowError");
                OnPropertyChanged("HasError");
            }
        }

        public bool HasError
        {
            get { return !string.IsNullOrWhiteSpace(RowError); }
        }

        public bool IsScaleEnabled
        {
            get { return _isScaleEnabled; }
            set
            {
                if (_isScaleEnabled == value)
                {
                    return;
                }

                _isScaleEnabled = value;
                OnPropertyChanged("IsScaleEnabled");
            }
        }

        public bool IsFilled
        {
            get
            {
                return !string.IsNullOrWhiteSpace(ViewName) ||
                       !string.IsNullOrWhiteSpace(SheetNumber) ||
                       !string.IsNullOrWhiteSpace(SheetName);
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
