using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace SAB.FilledRegionFromMaterial
{
    public partial class MaterialSelectionWindow : Window
    {
        private readonly IList<MaterialSelectionItem> _rows;
        private readonly ICollectionView _view;
        private string _filter = "All";
        private bool _applying;
        private bool _refreshPending;
        public IList<string> SelectedMaterialUniqueIds { get; private set; }

        public MaterialSelectionWindow(IEnumerable<MaterialSelectionItem> rows)
        {
            _contentLoaded = true;
            SAB.Helpers.WpfComponentLoader.Load(this, "Cls_FilledRegionFromMaterial/MaterialSelectionWindow.xaml");
            _rows = (rows ?? Enumerable.Empty<MaterialSelectionItem>()).ToList();
            _view = new ListCollectionView(_rows.ToList());
            _view.Filter = Matches;
            _view.SortDescriptions.Add(new SortDescription(nameof(MaterialSelectionItem.Name), ListSortDirection.Ascending));
            MaterialsGrid.ItemsSource = _view;
            foreach (MaterialSelectionItem row in _rows) row.PropertyChanged += RowChanged;
            Closed += (sender, args) => { foreach (MaterialSelectionItem row in _rows) row.PropertyChanged -= RowChanged; };
            Refresh();
        }

        private bool Matches(object value)
        {
            var row = (MaterialSelectionItem)value;
            return (_filter == "All" || (_filter == "Included" ? row.Included : !row.Included))
                && (string.IsNullOrWhiteSpace(SearchBox.Text) || row.Name.IndexOf(SearchBox.Text.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0);
        }

        private void Refresh()
        {
            if (_view == null) return;
            var highlighted = MaterialsGrid.SelectedItems.Cast<MaterialSelectionItem>().ToList();
            _view.Refresh();
            foreach (MaterialSelectionItem row in highlighted)
                if (_view.Contains(row) && !MaterialsGrid.SelectedItems.Contains(row)) MaterialsGrid.SelectedItems.Add(row);
            UpdateStatus();
        }

        private void RowChanged(object sender, PropertyChangedEventArgs args)
        {
            if (_applying || args.PropertyName != nameof(MaterialSelectionItem.Included)) return;
            var changed = (MaterialSelectionItem)sender;
            var package = ToggleTargets(changed).ToList();
            _applying = true;
            try { foreach (MaterialSelectionItem row in package) row.Included = changed.Included; }
            finally { _applying = false; }
            if (_refreshPending) return;
            _refreshPending = true;
            // Automation also changes IsChecked without Click. Apply the package before deferring filtering.
            Dispatcher.BeginInvoke(new Action(() => { _refreshPending = false; if (IsLoaded) Refresh(); }), DispatcherPriority.Background);
        }

        private void UpdateStatus()
        {
            if (_view == null) return;
            var visible = _view.Cast<MaterialSelectionItem>().ToList();
            var included = _rows.Where(row => row.Included).ToList();
            int hidden = included.Count(row => !visible.Contains(row));
            CountsText.Text = "Показано: " + visible.Count + " из " + _rows.Count + " · Выбрано: " + included.Count
                + (hidden > 0 ? " · Скрыто выбранных: " + hidden : string.Empty)
                + " · Выделено строк: " + MaterialsGrid.SelectedItems.Count;
            int create = included.Count(row => !row.WillSkip && !row.UpdatesExistingType);
            int update = included.Count(row => !row.WillSkip && row.UpdatesExistingType);
            int skip = included.Count(row => row.WillSkip);
            StatusText.Text = included.Count == 0 ? "Выберите материалы для создания типов."
                : "К созданию: " + create + " · К обновлению: " + update + (skip > 0 ? " · Без штриховки: " + skip : string.Empty);
            RunButton.IsEnabled = create + update > 0;
            RunButton.Content = "Создать и обновить" + (included.Count > 0 ? " (" + included.Count + ")" : string.Empty);
            EnableButton.IsEnabled = DisableButton.IsEnabled = MaterialsGrid.SelectedItems.Count > 0;
            SelectAllButton.IsEnabled = visible.Count > 0;
            EmptyText.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Apply(IEnumerable<MaterialSelectionItem> rows, bool included)
        {
            var snapshot = rows.ToList();
            _applying = true;
            try { foreach (MaterialSelectionItem row in snapshot) row.Included = included; }
            finally { _applying = false; }
            Refresh();
        }

        private IEnumerable<MaterialSelectionItem> ToggleTargets(MaterialSelectionItem row)
        {
            return MaterialsGrid.SelectedItems.Contains(row)
                ? MaterialsGrid.SelectedItems.Cast<MaterialSelectionItem>().ToList()
                : new List<MaterialSelectionItem> { row };
        }

        private void ToggleMouseDown(object sender, MouseButtonEventArgs args)
        {
            var toggle = (ToggleButton)sender;
            var row = toggle.DataContext as MaterialSelectionItem;
            if (row == null) return;
            // Keep native DataGrid highlight intact instead of reducing the selected package to this cell.
            args.Handled = true;
            Apply(ToggleTargets(row), !row.Included);
        }

        private void ToggleClick(object sender, RoutedEventArgs args)
        {
            var toggle = (ToggleButton)sender;
            var row = toggle.DataContext as MaterialSelectionItem;
            if (row != null) Apply(ToggleTargets(row), toggle.IsChecked == true);
            args.Handled = true;
        }

        private void SelectAllClick(object sender, RoutedEventArgs args) { MaterialsGrid.SelectAll(); }
        private void EnableClick(object sender, RoutedEventArgs args) { Apply(MaterialsGrid.SelectedItems.Cast<MaterialSelectionItem>(), true); }
        private void DisableClick(object sender, RoutedEventArgs args) { Apply(MaterialsGrid.SelectedItems.Cast<MaterialSelectionItem>(), false); }
        private void GridSelectionChanged(object sender, SelectionChangedEventArgs args) { UpdateStatus(); }
        private void SearchChanged(object sender, TextChangedEventArgs args)
        {
            if (_view == null) return;
            MaterialsGrid.UnselectAll(); Refresh();
        }
        private void FilterChanged(object sender, RoutedEventArgs args)
        {
            _filter = (string)((RadioButton)sender).Tag;
            if (_view == null) return;
            MaterialsGrid.UnselectAll(); Refresh();
        }

        private void GridKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            { MaterialsGrid.SelectAll(); args.Handled = true; }
            if (args.Key == Key.Space)
            {
                var selected = MaterialsGrid.SelectedItems.Cast<MaterialSelectionItem>().ToList();
                if (selected.Count > 0) { Apply(selected, !selected.All(row => row.Included)); args.Handled = true; }
            }
        }

        private void WindowKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key != Key.Escape) return;
            if (SearchBox.IsKeyboardFocusWithin && SearchBox.Text.Length > 0) SearchBox.Clear();
            else Close();
            args.Handled = true;
        }

        private void CancelClick(object sender, RoutedEventArgs args) { Close(); }
        private void RunClick(object sender, RoutedEventArgs args)
        {
            if (!_rows.Any(row => row.Included && !row.WillSkip)) return;
            SelectedMaterialUniqueIds = _rows.Where(row => row.Included).Select(row => row.UniqueId).Distinct(StringComparer.Ordinal).ToList();
            DialogResult = true;
        }
    }
}
