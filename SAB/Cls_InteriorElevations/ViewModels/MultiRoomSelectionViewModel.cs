using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.ViewModels
{
    public class MultiRoomSelectionViewModel : INotifyPropertyChanged
    {
        public MultiRoomSelectionViewModel(ObservableCollection<MultiRoomSelectionItem> rows)
        {
            Rows = rows ?? new ObservableCollection<MultiRoomSelectionItem>();
            if (Rows.Count == 0)
            {
                Rows.Add(new MultiRoomSelectionItem());
            }

            RenumberRows();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<MultiRoomSelectionItem> Rows { get; private set; }

        public string StatusText
        {
            get
            {
                int completedCount = 0;
                int selectedLinesCount = 0;
                for (int index = 0; index < Rows.Count; index++)
                {
                    MultiRoomSelectionItem row = Rows[index];
                    if (row == null)
                    {
                        continue;
                    }

                    if (row.IsCompleted)
                    {
                        completedCount++;
                    }

                    selectedLinesCount += row.SelectedLinesCount;
                }

                return "Строк: " + Rows.Count +
                       " | Заполнено: " + completedCount +
                       " | Выбрано линий: " + selectedLinesCount;
            }
        }

        public string ValidationText
        {
            get
            {
                string validationMessage;
                return TryValidateTransfer(out validationMessage)
                    ? "Список помещений готов к передаче."
                    : validationMessage;
            }
        }

        public void AddRowAfter(MultiRoomSelectionItem sourceRow)
        {
            int insertIndex = Rows.Count;
            if (sourceRow != null)
            {
                int sourceIndex = Rows.IndexOf(sourceRow);
                if (sourceIndex >= 0)
                {
                    insertIndex = sourceIndex + 1;
                }
            }

            Rows.Insert(insertIndex, new MultiRoomSelectionItem());
            RenumberRows();
            NotifySummaryChanged();
        }

        public void DeleteRow(MultiRoomSelectionItem row)
        {
            if (row != null)
            {
                Rows.Remove(row);
            }

            if (Rows.Count == 0)
            {
                Rows.Add(new MultiRoomSelectionItem());
            }

            RenumberRows();
            NotifySummaryChanged();
        }

        public void RefreshSummary()
        {
            NotifySummaryChanged();
        }

        public bool TryValidateTransfer(out string validationMessage)
        {
            validationMessage = string.Empty;
            if (Rows.Count == 0)
            {
                validationMessage = "Добавьте хотя бы одно помещение.";
                return false;
            }

            for (int rowIndex = 0; rowIndex < Rows.Count; rowIndex++)
            {
                MultiRoomSelectionItem row = Rows[rowIndex];
                if (row == null || !row.IsCompleted)
                {
                    validationMessage = "Помещение №" + (rowIndex + 1) +
                                        ": выберите линии и помещение либо удалите пустую строку.";
                    return false;
                }

                for (int previousIndex = 0; previousIndex < rowIndex; previousIndex++)
                {
                    MultiRoomSelectionItem previousRow = Rows[previousIndex];
                    if (previousRow == null || previousRow.RoomData == null || row.RoomData == null)
                    {
                        continue;
                    }

                    if (RevitElementIdUtils.AreEqual(
                            previousRow.RoomData.RoomElementId,
                            row.RoomData.RoomElementId))
                    {
                        validationMessage = "Помещение №" + row.RoomNumber + " " + row.RoomName +
                                            " добавлено в список больше одного раза.";
                        return false;
                    }
                }
            }

            return true;
        }

        private void RenumberRows()
        {
            for (int index = 0; index < Rows.Count; index++)
            {
                if (Rows[index] != null)
                {
                    Rows[index].Index = index + 1;
                }
            }
        }

        private void NotifySummaryChanged()
        {
            OnPropertyChanged("StatusText");
            OnPropertyChanged("ValidationText");
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
