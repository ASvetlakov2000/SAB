using System;
using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SAB.ParameterTools.Core;

namespace SAB.ParameterTools.UI
{
    // Each picker owns its view: searching one rule never filters other parameter lists.
    public sealed class ParameterPicker : StackPanel
    {
        public static readonly DependencyProperty ParametersProperty = DependencyProperty.Register("Parameters", typeof(IEnumerable), typeof(ParameterPicker), new PropertyMetadata(null, Changed));
        public static readonly DependencyProperty SelectedParameterProperty = DependencyProperty.Register("SelectedParameter", typeof(ParameterRef), typeof(ParameterPicker), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, SelectedChanged));
        public IEnumerable Parameters { get { return (IEnumerable)GetValue(ParametersProperty); } set { SetValue(ParametersProperty, value); } }
        public ParameterRef SelectedParameter { get { return (ParameterRef)GetValue(SelectedParameterProperty); } set { SetValue(SelectedParameterProperty, value); } }
        private readonly ComboBox _combo;
        private readonly ContentControl _details;
        private ListCollectionView _view;
        private bool _updating, _cancelled;
        public ParameterPicker()
        {
            MinHeight = 86; VerticalAlignment = VerticalAlignment.Center;
            _combo = new ComboBox { IsEditable = true, IsTextSearchEnabled = false, StaysOpenOnEdit = true,
                Height = 32, VerticalContentAlignment = VerticalAlignment.Center, ItemTemplate = Template(true), MaxDropDownHeight = 420,
                ToolTip = "Введите часть названия и выберите параметр из отфильтрованного списка. Одноимённые параметры различаются по GUID/ID." };
            TextSearch.SetTextPath(_combo, "Name"); _combo.SetResourceReference(Control.ForegroundProperty, "SabBrush.Accent");
            _details = new ContentControl { ContentTemplate = Template(false), Margin = new Thickness(8, 2, 4, 2), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            Children.Add(_combo); Children.Add(_details);
            _combo.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(Search));
            _combo.SelectionChanged += (s, e) => {
                if (_updating || e.AddedItems.Count == 0) return;
                if (_combo.IsDropDownOpen && (Keyboard.IsKeyDown(Key.Down) || Keyboard.IsKeyDown(Key.Up))) return;
                SetCurrentValue(SelectedParameterProperty, e.AddedItems[0]);
            };
            _combo.DropDownClosed += (s, e) => {
                if (!_updating && !_cancelled && _combo.SelectedItem is ParameterRef selected) SetCurrentValue(SelectedParameterProperty, selected);
                _cancelled = false; Restore();
            };
            _combo.LostKeyboardFocus += (s, e) => { if (!_combo.IsDropDownOpen) Restore(); };
            _combo.PreviewKeyDown += (s, e) => {
                if (e.Key == Key.Escape) { _cancelled = true; _combo.IsDropDownOpen = false; Restore(); e.Handled = true; }
                if (e.Key == Key.Enter && _view != null && (_combo.SelectedItem is ParameterRef || _view.Count == 1)) {
                    SetCurrentValue(SelectedParameterProperty, _combo.SelectedItem ?? _view.GetItemAt(0)); _combo.IsDropDownOpen = false; e.Handled = true;
                }
            };
        }
        private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var picker = (ParameterPicker)d;
            picker._view = new ListCollectionView((picker.Parameters ?? new object[0]).Cast<ParameterRef>().ToList());
            picker._updating = true; picker._combo.ItemsSource = picker._view; picker._updating = false; picker.Restore();
        }
        private static void SelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        { ((ParameterPicker)d).Restore(); }
        private void Search(object sender, TextChangedEventArgs e)
        {
            if (_updating || _view == null || !(e.OriginalSource is TextBox text)) return;
            if (Keyboard.IsKeyDown(Key.Up) || Keyboard.IsKeyDown(Key.Down)) return;
            string query = text.Text;
            int caret = text.CaretIndex;
            _updating = true;
            try {
                _combo.SelectedItem = null;
                _view.Filter = item => ((ParameterRef)item).Name?.IndexOf(query.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0;
                _combo.Text = query; text.CaretIndex = Math.Min(caret, query.Length);
                _combo.IsDropDownOpen = true;
            } finally { _updating = false; }
        }
        private void Restore()
        {
            if (_updating) return; _updating = true;
            try { if (_view != null) _view.Filter = null; _combo.SelectedItem = SelectedParameter;
                _combo.Text = SelectedParameter?.Name ?? ""; _details.Content = SelectedParameter;
                ToolTip = SelectedParameter?.Display;
            } finally { _updating = false; }
        }
        internal static DataTemplate Template(bool includeName)
        {
            var panel = new FrameworkElementFactory(typeof(StackPanel));
            if (includeName) {
                var name = Text("Name", 13); name.SetResourceReference(TextBlock.ForegroundProperty, "SabBrush.Accent");
                name.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold); panel.AppendChild(name);
            }
            var type = Text("DataType", 11); type.SetResourceReference(TextBlock.ForegroundProperty, "SabBrush.TextSecondary"); panel.AppendChild(type);
            var group = Text("GroupBehavior", 11);
            var style = new Style(typeof(TextBlock)); style.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.DimGray));
            var vary = new DataTrigger { Binding = new Binding("VariesAcrossGroups"), Value = true };
            vary.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(6, 118, 71))));
            var fixedValue = new DataTrigger { Binding = new Binding("VariesAcrossGroups"), Value = false };
            fixedValue.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(180, 35, 24))));
            style.Triggers.Add(vary); style.Triggers.Add(fixedValue); group.SetValue(FrameworkElement.StyleProperty, style); panel.AppendChild(group);
            var identity = Text("Identity", 11); identity.SetValue(TextBlock.ForegroundProperty, Brushes.Black); panel.AppendChild(identity);
            panel.SetBinding(FrameworkElement.ToolTipProperty, new Binding("Display"));
            return new DataTemplate { VisualTree = panel };
        }
        private static FrameworkElementFactory Text(string path, double size)
        {
            var text = new FrameworkElementFactory(typeof(TextBlock)); text.SetBinding(TextBlock.TextProperty, new Binding(path));
            text.SetValue(TextBlock.FontWeightProperty, FontWeights.Normal);
            text.SetValue(TextBlock.FontSizeProperty, size); text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap); return text;
        }
    }
}
