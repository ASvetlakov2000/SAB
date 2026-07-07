using System.Collections.Generic;
using System.ComponentModel;
using Autodesk.Revit.DB;

namespace SAB.CreateViewsAndSheets.ViewModels
{
    public class DeleteViewsAndSheetsRowViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;

        public DeleteViewsAndSheetsRowViewModel()
        {
            SheetId = ElementId.InvalidElementId;
            SheetNumber = string.Empty;
            SheetName = string.Empty;
            PlacedViewsText = string.Empty;
            PlacedViewIds = new List<ElementId>();
            PlacedViewNames = new List<string>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public int RowNumber { get; set; }

        public ElementId SheetId { get; set; }

        public string SheetNumber { get; set; }

        public string SheetName { get; set; }

        public string PlacedViewsText { get; set; }

        public List<ElementId> PlacedViewIds { get; private set; }

        public List<string> PlacedViewNames { get; private set; }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                OnPropertyChanged("IsSelected");
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
