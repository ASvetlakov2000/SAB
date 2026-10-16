using System;
using System.ComponentModel;

namespace SAB.FilledRegionFromMaterial
{
    /// <summary>Detached document values; the selection window never accesses Revit objects.</summary>
    public sealed class MaterialSelectionItem : INotifyPropertyChanged
    {
        private bool _included;
        public string UniqueId { get; private set; }
        public string Name { get; private set; }
        public string CutPatternText { get; private set; }
        public bool HasCutPattern { get; private set; }
        public bool UpdatesExistingType { get; private set; }
        public bool WillSkip { get; private set; }
        public string ActionKind { get { return !HasCutPattern ? "NoPattern" : UpdatesExistingType ? "Update" : "Create"; } }
        public string ActionText { get { return !HasCutPattern ? "Нет штриховки" : UpdatesExistingType ? "Обновить" : "Создать"; } }
        public string ActionDescription { get; private set; }
        public bool Included
        {
            get { return _included; }
            set
            {
                if (_included == value) return;
                _included = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Included)));
            }
        }

        public MaterialSelectionItem(string uniqueId, string name, string cutPatternText,
            bool hasCutPattern, bool updatesExistingType, bool skipWithoutCutPattern, string targetName)
        {
            if (string.IsNullOrWhiteSpace(uniqueId)) throw new ArgumentException("Материал должен иметь идентификатор.", nameof(uniqueId));
            UniqueId = uniqueId; Name = name ?? string.Empty; CutPatternText = cutPatternText ?? string.Empty;
            HasCutPattern = hasCutPattern; UpdatesExistingType = updatesExistingType;
            WillSkip = !hasCutPattern && skipWithoutCutPattern;
            ActionDescription = WillSkip ? "Материал будет пропущен: нет штриховки сечения."
                : (updatesExistingType ? "Обновить тип «" : "Создать тип «") + targetName + "»."
                    + (!hasCutPattern ? " Штриховки сечения нет; пропуск выключен в настройках." : string.Empty);
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
