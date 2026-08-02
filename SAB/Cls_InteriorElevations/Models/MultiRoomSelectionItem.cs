using System.Collections.Generic;
using System.ComponentModel;
using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Models
{
    public class MultiRoomSelectionItem : INotifyPropertyChanged
    {
        private int _index;
        private RoomData _roomData;
        private List<List<DetailLine>> _lineGroups;
        private List<DetailLine> _selectedLines;

        public MultiRoomSelectionItem()
        {
            _lineGroups = new List<List<DetailLine>>();
            _selectedLines = new List<DetailLine>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public int Index
        {
            get { return _index; }
            set
            {
                if (_index == value)
                {
                    return;
                }

                _index = value;
                OnPropertyChanged("Index");
            }
        }

        public List<List<DetailLine>> LineGroups
        {
            get { return _lineGroups; }
        }

        public List<DetailLine> SelectedLines
        {
            get { return _selectedLines; }
        }

        public RoomData RoomData
        {
            get { return _roomData; }
        }

        public string RoomNumber
        {
            get { return _roomData != null ? _roomData.RoomNumber : string.Empty; }
        }

        public string RoomName
        {
            get { return _roomData != null ? _roomData.RoomName : string.Empty; }
        }

        public int LineGroupsCount
        {
            get { return _lineGroups != null ? _lineGroups.Count : 0; }
        }

        public int SelectedLinesCount
        {
            get { return _selectedLines != null ? _selectedLines.Count : 0; }
        }

        public bool IsCompleted
        {
            get { return _roomData != null && SelectedLinesCount > 0; }
        }

        public string StatusText
        {
            get { return IsCompleted ? "Готово" : "Не выбрано"; }
        }

        public void ApplySelection(
            List<List<DetailLine>> lineGroups,
            List<DetailLine> selectedLines,
            RoomData roomData)
        {
            _lineGroups = lineGroups ?? new List<List<DetailLine>>();
            _selectedLines = selectedLines ?? new List<DetailLine>();
            _roomData = roomData;

            OnPropertyChanged("LineGroups");
            OnPropertyChanged("SelectedLines");
            OnPropertyChanged("RoomData");
            OnPropertyChanged("RoomNumber");
            OnPropertyChanged("RoomName");
            OnPropertyChanged("LineGroupsCount");
            OnPropertyChanged("SelectedLinesCount");
            OnPropertyChanged("IsCompleted");
            OnPropertyChanged("StatusText");
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
