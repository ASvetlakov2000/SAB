using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using Autodesk.Revit.DB;

namespace SAB.MaterialQuantity
{
    internal sealed class MaterialRuleRow : INotifyPropertyChanged
    {
        private string _rule;

        internal ElementId MaterialId { get; }
        internal string OriginalRule { get; private set; }
        internal string FallbackUnit { get; }
        public string Name { get; }
        public string CurrentRule
        {
            get
            {
                return OriginalRule == MaterialUnitResolver.Auto
                    ? (FallbackUnit == null ? "Не задано" : FallbackUnit + " · ADSK")
                    : OriginalRule + " · SAB";
            }
        }
        public bool IsUsed { get; }
        public string Rule
        {
            get { return _rule; }
            set
            {
                if (_rule == value) return;
                _rule = value;
                Changed(nameof(Rule));
                Changed(nameof(IsChanged));
                Changed(nameof(Result));
            }
        }

        public bool IsChanged { get { return Rule != OriginalRule; } }
        public string Result
        {
            get
            {
                if (Rule == MaterialUnitResolver.Auto)
                    return FallbackUnit == null ? "Не задано" : FallbackUnit + " · ADSK";
                return Rule;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        internal MaterialRuleRow(Material material, bool isUsed)
        {
            MaterialId = material.Id;
            Name = material.Name;
            IsUsed = isUsed;
            FallbackUnit = MaterialUnitResolver.ResolveFallback(material);
            string explicitRule = MaterialUnitResolver.ReadExplicit(material);
            OriginalRule = explicitRule ?? MaterialUnitResolver.Auto;
            _rule = OriginalRule;
        }

        internal void AcceptChanges()
        {
            OriginalRule = Rule;
            Changed(nameof(CurrentRule));
            Changed(nameof(IsChanged));
        }

        private void Changed(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    internal sealed class MaterialRulesViewModel : INotifyPropertyChanged
    {
        private string _search = "";
        private string _filter = "Все материалы";
        private string _bulkRule = "м²";
        private string _message = "Выделите строки в таблице.";
        private bool _showUsedOnly = true;

        public ObservableCollection<MaterialRuleRow> Rows { get; }
        public ICollectionView VisibleRows { get; }
        public IReadOnlyList<string> UnitChoices { get; } =
            new[] { MaterialUnitResolver.Auto, "м", "м²", "м³", MaterialUnitResolver.Ignore };
        public IReadOnlyList<string> FilterChoices { get; } =
            new[] { "Все материалы", "Без единицы", "Изменённые" };
        public IReadOnlyList<string> BulkChoices { get; } =
            new[] { "м", "м²", "м³", MaterialUnitResolver.Ignore, MaterialUnitResolver.Auto };
        public string UsedMaterialsButtonText
        {
            get { return _showUsedOnly ? "Показать все материалы" : "Только используемые"; }
        }

        public string Search
        {
            get { return _search; }
            set { if (_search == value) return; _search = value ?? ""; Notify(); VisibleRows.Refresh(); Notify(nameof(Status)); }
        }
        public string Filter
        {
            get { return _filter; }
            set { if (_filter == value) return; _filter = value; Notify(); VisibleRows.Refresh(); Notify(nameof(Status)); }
        }
        public string BulkRule
        {
            get { return _bulkRule; }
            set { if (_bulkRule == value) return; _bulkRule = value; Notify(); }
        }
        public string Message
        {
            get { return _message; }
            private set { _message = value; Notify(); }
        }
        public string Status
        {
            get
            {
                int visible = VisibleRows.Cast<object>().Count();
                int changed = Rows.Count(row => row.IsChanged);
                int unknown = Rows.Count(row => row.Result == "Не задано");
                int used = Rows.Count(row => row.IsUsed);
                return "Показано: " + visible + " из " + Rows.Count +
                    "  |  Используется: " + used +
                    "  |  Изменено: " + changed + "  |  Без единицы: " + unknown;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        internal MaterialRulesViewModel(IEnumerable<Material> materials, ISet<long> usedMaterialIds)
        {
            if (usedMaterialIds == null) throw new ArgumentNullException(nameof(usedMaterialIds));
            Rows = new ObservableCollection<MaterialRuleRow>(materials
                .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(item => new MaterialRuleRow(
                    item,
                    usedMaterialIds.Contains(RevitElementId.Key(item.Id)))));
            foreach (MaterialRuleRow row in Rows) row.PropertyChanged += OnRowChanged;
            VisibleRows = CollectionViewSource.GetDefaultView(Rows);
            VisibleRows.Filter = Match;
        }

        internal void ApplyBulk(IEnumerable<MaterialRuleRow> selection)
        {
            MaterialRuleRow[] rows = selection.ToArray();
            if (rows.Length == 0)
            {
                Message = "Сначала выделите материалы в таблице.";
                return;
            }
            foreach (MaterialRuleRow row in rows) row.Rule = BulkRule;
            Message = "Правило назначено: " + rows.Length + " материалам. Пересчитайте материалы.";
        }

        internal void ToggleUsedMaterials()
        {
            _showUsedOnly = !_showUsedOnly;
            VisibleRows.Refresh();
            Notify(nameof(UsedMaterialsButtonText));
            Notify(nameof(Status));
        }

        internal void AcceptChanges(IEnumerable<MaterialRuleRow> rows)
        {
            foreach (MaterialRuleRow row in rows) row.AcceptChanges();
            VisibleRows.Refresh();
            Notify(nameof(Status));
        }

        internal void ReportRulesSaved(int count)
        {
            Message = count == 0
                ? "Все правила уже сохранены."
                : "Сохранено правил: " + count + ". Можно запустить пересчёт позже.";
        }

        private bool Match(object item)
        {
            var row = item as MaterialRuleRow;
            if (row == null) return false;
            if (_showUsedOnly && !row.IsUsed) return false;
            if (!string.IsNullOrWhiteSpace(Search) &&
                row.Name.IndexOf(Search.Trim(), StringComparison.CurrentCultureIgnoreCase) < 0)
                return false;
            if (Filter == "Без единицы") return row.Result == "Не задано";
            if (Filter == "Изменённые") return row.IsChanged;
            return true;
        }

        private void OnRowChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName != nameof(MaterialRuleRow.Rule)) return;
            VisibleRows.Refresh();
            Notify(nameof(Status));
        }

        private void Notify([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
