using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SAB.UI
{
    public sealed class ShortSettingsWindowConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is double && (double)value < 700;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    // Preserve the desktop layout and stack settings in a narrow window.
    public sealed class AdaptiveSettingsGrid : Grid
    {
        public static readonly DependencyProperty ForceCompactProperty = DependencyProperty.Register(
            "ForceCompact", typeof(bool), typeof(AdaptiveSettingsGrid),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));
        public bool ForceCompact
        {
            get { return (bool)GetValue(ForceCompactProperty); }
            set { SetValue(ForceCompactProperty, value); }
        }
        private Window _window;
        private bool _compact;
        private List<RowDefinition> _rows;
        private List<GridLength> _columns;
        private List<CellLayout> _cells;

        public AdaptiveSettingsGrid()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _window = Window.GetWindow(this);
            if (_window != null) _window.SizeChanged += OnWindowSizeChanged;
            InvalidateMeasure();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_window != null) _window.SizeChanged -= OnWindowSizeChanged;
            _window = null;
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged) InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size constraint)
        {
            Window window = _window ?? Window.GetWindow(this);
            double width = window == null ? constraint.Width :
                (window.ActualWidth > 0 ? window.ActualWidth : window.Width);
            bool compact = ForceCompact || width < 780;
            if (compact != _compact)
            {
                if (_cells == null)
                {
                    _rows = RowDefinitions.ToList();
                    _columns = ColumnDefinitions.Select(c => c.Width).ToList();
                    _cells = Children.OfType<FrameworkElement>()
                        .Select(c => new CellLayout(c)).ToList();
                    // Column groups keep each label beside its own input when stacked in a sidebar.
                    _cells = ForceCompact
                        ? _cells.OrderBy(c => c.Column).ThenBy(c => c.Row).ToList()
                        : _cells.OrderBy(c => c.Row).ThenBy(c => c.Column).ToList();
                }
                RowDefinitions.Clear();
                if (compact)
                {
                    for (int i = 0; i < ColumnDefinitions.Count; i++)
                        ColumnDefinitions[i].Width = i == 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
                    for (int i = 0; i < _cells.Count; i++)
                    {
                        CellLayout cell = _cells[i];
                        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        SetRow(cell.Element, i);
                        SetColumn(cell.Element, 0);
                        SetRowSpan(cell.Element, 1);
                        SetColumnSpan(cell.Element, 1);
                        Thickness margin = cell.Margin;
                        margin.Bottom += i + 1 < _cells.Count ? 10 : 0;
                        cell.Element.Margin = margin;
                    }
                }
                else
                {
                    foreach (RowDefinition row in _rows) RowDefinitions.Add(row);
                    for (int i = 0; i < _columns.Count; i++) ColumnDefinitions[i].Width = _columns[i];
                    foreach (CellLayout cell in _cells)
                    {
                        SetRow(cell.Element, cell.Row);
                        SetColumn(cell.Element, cell.Column);
                        SetRowSpan(cell.Element, cell.RowSpan);
                        SetColumnSpan(cell.Element, cell.ColumnSpan);
                        cell.Element.Margin = cell.Margin;
                    }
                }
                _compact = compact;
            }
            return base.MeasureOverride(constraint);
        }

        private sealed class CellLayout
        {
            public readonly FrameworkElement Element;
            public readonly int Row, Column, RowSpan, ColumnSpan;
            public readonly Thickness Margin;

            public CellLayout(FrameworkElement element)
            {
                Element = element;
                Row = GetRow(element);
                Column = GetColumn(element);
                RowSpan = GetRowSpan(element);
                ColumnSpan = GetColumnSpan(element);
                Margin = element.Margin;
            }
        }
    }
}
