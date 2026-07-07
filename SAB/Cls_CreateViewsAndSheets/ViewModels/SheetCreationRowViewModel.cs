using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Autodesk.Revit.DB;
using SAB.CreateViewsAndSheets.Models;
using SAB.InteriorElevations.Utils;

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
        private SheetBrowserParameterValueViewModel _projectSectionParameterValue;
        private bool _isDeletionRow;
        private bool _isSelectedForDeletion;
        private ElementId _existingSheetId;
        private string _placedViewsText;

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
            _existingSheetId = ElementId.InvalidElementId;
            _placedViewsText = string.Empty;
            ExistingPlacedViewIds = new List<ElementId>();
            ExistingPlacedViewNames = new List<string>();
            SheetBrowserParameterValues = new ObservableCollection<SheetBrowserParameterValueViewModel>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<SheetBrowserParameterValueViewModel> SheetBrowserParameterValues { get; private set; }

        public List<ElementId> ExistingPlacedViewIds { get; private set; }

        public List<string> ExistingPlacedViewNames { get; private set; }

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
                OnPropertyChanged("ProjectSectionGroupName");
            }
        }

        public string ProjectSectionGroupName
        {
            get
            {
                string sectionName = string.Empty;
                if (SheetBrowserParameterValues.Count > 0 && SheetBrowserParameterValues[0] != null)
                {
                    sectionName = (SheetBrowserParameterValues[0].Value ?? string.Empty).Trim();
                }

                if (string.IsNullOrWhiteSpace(sectionName))
                {
                    sectionName = (SheetBrowserParameterValue ?? string.Empty).Trim();
                }

                return string.IsNullOrWhiteSpace(sectionName) ? "Без раздела" : sectionName;
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
                if (IsDeletionRow)
                {
                    return IsSelectedForDeletion;
                }

                return !string.IsNullOrWhiteSpace(ViewName) ||
                       !string.IsNullOrWhiteSpace(SheetNumber) ||
                       !string.IsNullOrWhiteSpace(SheetName);
            }
        }

        public bool IsDeletionRow
        {
            get { return _isDeletionRow; }
            set
            {
                if (_isDeletionRow == value)
                {
                    return;
                }

                _isDeletionRow = value;
                OnPropertyChanged("IsDeletionRow");
                OnPropertyChanged("IsFilled");
            }
        }

        public bool IsSelectedForDeletion
        {
            get { return _isSelectedForDeletion; }
            set
            {
                if (_isSelectedForDeletion == value)
                {
                    return;
                }

                _isSelectedForDeletion = value;
                OnPropertyChanged("IsSelectedForDeletion");
                OnPropertyChanged("IsFilled");
            }
        }

        public ElementId ExistingSheetId
        {
            get { return _existingSheetId; }
            set
            {
                if (RevitElementIdUtils.AreEqual(_existingSheetId, value))
                {
                    return;
                }

                _existingSheetId = value ?? ElementId.InvalidElementId;
                OnPropertyChanged("ExistingSheetId");
            }
        }

        public string PlacedViewsText
        {
            get { return _placedViewsText; }
            set
            {
                string newValue = value ?? string.Empty;
                if (_placedViewsText == newValue)
                {
                    return;
                }

                _placedViewsText = newValue;
                OnPropertyChanged("PlacedViewsText");
            }
        }

        public void EnsureSheetBrowserParameterValues(IList<SheetBrowserParameterLevelViewModel> levels)
        {
            if (levels == null)
            {
                SheetBrowserParameterValues.Clear();
                UpdateProjectSectionParameterSubscription();
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

            UpdateProjectSectionParameterSubscription();
        }

        private SheetBrowserParameterValueViewModel CreateParameterValue(SheetBrowserParameterLevelViewModel level)
        {
            SheetBrowserParameterValueViewModel value = new SheetBrowserParameterValueViewModel();
            value.ParameterId = level != null ? level.ParameterId : Autodesk.Revit.DB.ElementId.InvalidElementId;
            value.ParameterName = level != null ? level.ParameterName : string.Empty;
            return value;
        }

        private void UpdateProjectSectionParameterSubscription()
        {
            SheetBrowserParameterValueViewModel newProjectSectionValue =
                SheetBrowserParameterValues.Count > 0 ? SheetBrowserParameterValues[0] : null;
            if (ReferenceEquals(_projectSectionParameterValue, newProjectSectionValue))
            {
                return;
            }

            if (_projectSectionParameterValue != null)
            {
                _projectSectionParameterValue.PropertyChanged -= ProjectSectionParameterValue_PropertyChanged;
            }

            _projectSectionParameterValue = newProjectSectionValue;
            if (_projectSectionParameterValue != null)
            {
                _projectSectionParameterValue.PropertyChanged += ProjectSectionParameterValue_PropertyChanged;
            }

            OnPropertyChanged("ProjectSectionGroupName");
        }

        private void ProjectSectionParameterValue_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e == null || string.Equals(e.PropertyName, "Value", StringComparison.Ordinal))
            {
                OnPropertyChanged("ProjectSectionGroupName");
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
