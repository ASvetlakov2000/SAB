using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using SAB.CreateViewsAndSheets.Models;
using SAB.RoomGeometryTools.Utils;

namespace SAB.CreateViewsAndSheets.ViewModels
{
    public class DeleteViewsAndSheetsViewModel : INotifyPropertyChanged
    {
        private bool _isAccepted;
        private bool _canDelete;
        private string _statusText;

        public DeleteViewsAndSheetsViewModel(IList<SheetDeletionItem> deletionItems)
        {
            Rows = new ObservableCollection<DeleteViewsAndSheetsRowViewModel>();
            _statusText = string.Empty;

            InitializeRows(deletionItems);

            SelectAllCommand = new RelayCommand(delegate { SetAllRowsSelected(true); });
            ClearSelectionCommand = new RelayCommand(delegate { SetAllRowsSelected(false); });
            DeleteCommand = new RelayCommand(delegate { AcceptWindow(); }, delegate { return CanDelete; });
            CancelCommand = new RelayCommand(delegate { CancelWindow(); });

            RefreshSelectionState();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler RequestClose;

        public ObservableCollection<DeleteViewsAndSheetsRowViewModel> Rows { get; private set; }

        public ICommand SelectAllCommand { get; private set; }

        public ICommand ClearSelectionCommand { get; private set; }

        public ICommand DeleteCommand { get; private set; }

        public ICommand CancelCommand { get; private set; }

        public bool IsAccepted
        {
            get { return _isAccepted; }
        }

        public bool CanDelete
        {
            get { return _canDelete; }
            private set
            {
                if (_canDelete == value)
                {
                    return;
                }

                _canDelete = value;
                OnPropertyChanged("CanDelete");

                RelayCommand deleteCommand = DeleteCommand as RelayCommand;
                if (deleteCommand != null)
                {
                    deleteCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string StatusText
        {
            get { return _statusText; }
            private set
            {
                string newValue = value ?? string.Empty;
                if (_statusText == newValue)
                {
                    return;
                }

                _statusText = newValue;
                OnPropertyChanged("StatusText");
            }
        }

        public bool TryBuildDeleteRequest(out List<SheetDeletionItem> items, out string validationMessage)
        {
            items = BuildSelectedItems();
            if (items.Count == 0)
            {
                validationMessage = "Выберите листы для удаления.";
                return false;
            }

            validationMessage = string.Empty;
            return true;
        }

        private void InitializeRows(IList<SheetDeletionItem> deletionItems)
        {
            if (deletionItems == null)
            {
                return;
            }

            for (int i = 0; i < deletionItems.Count; i++)
            {
                SheetDeletionItem item = deletionItems[i];
                if (item == null)
                {
                    continue;
                }

                DeleteViewsAndSheetsRowViewModel row = new DeleteViewsAndSheetsRowViewModel();
                row.RowNumber = item.RowNumber;
                row.SheetId = item.SheetId;
                row.SheetNumber = item.SheetNumber ?? string.Empty;
                row.SheetName = item.SheetName ?? string.Empty;

                if (item.PlacedViewIds != null)
                {
                    for (int j = 0; j < item.PlacedViewIds.Count; j++)
                    {
                        row.PlacedViewIds.Add(item.PlacedViewIds[j]);
                    }
                }

                if (item.PlacedViewNames != null)
                {
                    for (int j = 0; j < item.PlacedViewNames.Count; j++)
                    {
                        row.PlacedViewNames.Add(item.PlacedViewNames[j] ?? string.Empty);
                    }
                }

                row.PlacedViewsText = row.PlacedViewNames.Count == 0
                    ? "Нет размещенных видов"
                    : string.Join("; ", row.PlacedViewNames);

                row.PropertyChanged += Row_PropertyChanged;
                Rows.Add(row);
            }
        }

        private List<SheetDeletionItem> BuildSelectedItems()
        {
            List<SheetDeletionItem> result = new List<SheetDeletionItem>();
            for (int i = 0; i < Rows.Count; i++)
            {
                DeleteViewsAndSheetsRowViewModel row = Rows[i];
                if (row == null || !row.IsSelected)
                {
                    continue;
                }

                SheetDeletionItem item = new SheetDeletionItem();
                item.RowNumber = row.RowNumber;
                item.SheetId = row.SheetId;
                item.SheetNumber = row.SheetNumber;
                item.SheetName = row.SheetName;

                for (int j = 0; j < row.PlacedViewIds.Count; j++)
                {
                    item.PlacedViewIds.Add(row.PlacedViewIds[j]);
                }

                for (int j = 0; j < row.PlacedViewNames.Count; j++)
                {
                    item.PlacedViewNames.Add(row.PlacedViewNames[j]);
                }

                result.Add(item);
            }

            return result;
        }

        private void SetAllRowsSelected(bool isSelected)
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i] != null)
                {
                    Rows[i].IsSelected = isSelected;
                }
            }

            RefreshSelectionState();
        }

        private void Row_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e != null && string.Equals(e.PropertyName, "IsSelected", StringComparison.Ordinal))
            {
                RefreshSelectionState();
            }
        }

        private void RefreshSelectionState()
        {
            int selectedCount = 0;
            int selectedViewsCount = 0;
            for (int i = 0; i < Rows.Count; i++)
            {
                DeleteViewsAndSheetsRowViewModel row = Rows[i];
                if (row == null || !row.IsSelected)
                {
                    continue;
                }

                selectedCount++;
                selectedViewsCount += row.PlacedViewIds.Count;
            }

            CanDelete = selectedCount > 0;
            StatusText = "Листов: " + Rows.Count +
                         "    Выбрано: " + selectedCount +
                         "    Видов к удалению: " + selectedViewsCount;
        }

        private void AcceptWindow()
        {
            if (!CanDelete)
            {
                return;
            }

            _isAccepted = true;
            EventHandler handler = RequestClose;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void CancelWindow()
        {
            _isAccepted = false;
            EventHandler handler = RequestClose;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
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
