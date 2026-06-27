using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using SAB.CreateViewsAndSheets.Models;

namespace SAB.CreateViewsAndSheets.ViewModels
{
    public class SheetCreationRowViewModel : INotifyPropertyChanged
    {
        private int _rowNumber;
        private SheetPlanKind _planKind;
        private string _floorName;
        private string _viewName;
        private string _viewScaleText;
        private RevitElementItem _selectedViewTemplate;
        private string _sheetNumber;
        private string _sheetName;
        private string _sheetBrowserParameterValue;
        private string _rowError;
        private bool _isScaleEnabled;

        public SheetCreationRowViewModel()
        {
            _planKind = SheetPlanKind.StandardPlan;
            _floorName = string.Empty;
            _viewName = string.Empty;
            _viewScaleText = "50";
            _sheetNumber = string.Empty;
            _sheetName = string.Empty;
            _sheetBrowserParameterValue = string.Empty;
            _rowError = string.Empty;
            _isScaleEnabled = true;
            SheetBrowserParameterValues = new ObservableCollection<SheetBrowserParameterValueViewModel>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<SheetBrowserParameterValueViewModel> SheetBrowserParameterValues { get; private set; }

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

        public SheetPlanKind PlanKind
        {
            get { return _planKind; }
            set
            {
                if (_planKind == value)
                {
                    return;
                }

                _planKind = value;
                OnPropertyChanged("PlanKind");
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

        public string SheetBrowserParameterValue
        {
            get { return _sheetBrowserParameterValue; }
            set
            {
                string newValue = value ?? string.Empty;
                if (_sheetBrowserParameterValue == newValue)
                {
                    return;
                }

                _sheetBrowserParameterValue = newValue;
                OnPropertyChanged("SheetBrowserParameterValue");
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

        public void EnsureSheetBrowserParameterValues(IList<SheetBrowserParameterLevelViewModel> levels)
        {
            if (levels == null)
            {
                SheetBrowserParameterValues.Clear();
                return;
            }

            while (SheetBrowserParameterValues.Count > levels.Count)
            {
                SheetBrowserParameterValues.RemoveAt(SheetBrowserParameterValues.Count - 1);
            }

            for (int i = 0; i < levels.Count; i++)
            {
                SheetBrowserParameterLevelViewModel level = levels[i];
                if (level == null)
                {
                    continue;
                }

                if (i >= SheetBrowserParameterValues.Count)
                {
                    SheetBrowserParameterValues.Add(CreateParameterValue(level));
                    continue;
                }

                SheetBrowserParameterValueViewModel value = SheetBrowserParameterValues[i];
                if (value == null)
                {
                    SheetBrowserParameterValues[i] = CreateParameterValue(level);
                    continue;
                }

                value.ParameterId = level.ParameterId;
                value.ParameterName = level.ParameterName;
            }
        }

        private SheetBrowserParameterValueViewModel CreateParameterValue(SheetBrowserParameterLevelViewModel level)
        {
            SheetBrowserParameterValueViewModel value = new SheetBrowserParameterValueViewModel();
            value.ParameterId = level != null ? level.ParameterId : Autodesk.Revit.DB.ElementId.InvalidElementId;
            value.ParameterName = level != null ? level.ParameterName : string.Empty;
            return value;
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

    public class SheetBrowserParameterValueViewModel : INotifyPropertyChanged
    {
        private Autodesk.Revit.DB.ElementId _parameterId;
        private string _parameterName;
        private string _value;

        public SheetBrowserParameterValueViewModel()
        {
            _parameterId = Autodesk.Revit.DB.ElementId.InvalidElementId;
            _parameterName = string.Empty;
            _value = string.Empty;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public Autodesk.Revit.DB.ElementId ParameterId
        {
            get { return _parameterId; }
            set
            {
                if (_parameterId == value)
                {
                    return;
                }

                _parameterId = value;
                OnPropertyChanged("ParameterId");
            }
        }

        public string ParameterName
        {
            get { return _parameterName; }
            set
            {
                string newValue = value ?? string.Empty;
                if (_parameterName == newValue)
                {
                    return;
                }

                _parameterName = newValue;
                OnPropertyChanged("ParameterName");
            }
        }

        public string Value
        {
            get { return _value; }
            set
            {
                string newValue = value ?? string.Empty;
                if (_value == newValue)
                {
                    return;
                }

                _value = newValue;
                OnPropertyChanged("Value");
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
