using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SAB.FilledRegionFromMaterial
{
    public partial class SettingsWindow : Window
    {
        private readonly ParameterSnapshot _snapshot;
        private readonly ObservableCollection<ParameterMappingSetting> _mappings;

        public SettingsWindow(PluginSettings settings, ParameterSnapshot snapshot)
        {
            _contentLoaded = true;
            SAB.Helpers.WpfComponentLoader.Load(this, "Cls_FilledRegionFromMaterial/SettingsWindow.xaml");
            _snapshot = snapshot;
            Settings = Clone(settings);
            _mappings = new ObservableCollection<ParameterMappingSetting>(Settings.ParameterMappings);

            MaterialParameterOptions = snapshot.MaterialParameters;
            TargetParameterOptions = snapshot.TargetParameters;
            DataContext = this;
            MappingGrid.ItemsSource = _mappings;
            _mappings.CollectionChanged += (sender, args) => UpdateStatus();

            PrefixTextBox.Text = Settings.TypeNamePrefix;
            IncludeUnusedMaterialsCheckBox.IsChecked = Settings.IncludeUnusedMaterials;
            IncludePaintedMaterialsCheckBox.IsChecked = Settings.IncludePaintedMaterials;
            SkipWithoutPatternCheckBox.IsChecked = Settings.SkipMaterialsWithoutCutPattern;
            CopyForegroundCheckBox.IsChecked = Settings.CopyForegroundPattern;
            CopyBackgroundCheckBox.IsChecked = Settings.CopyBackgroundPattern;
            CopyColorsCheckBox.IsChecked = Settings.CopyPatternColors;

            UsedMaterialsText.Text = snapshot.UsedMaterials.ToString();
            TotalMaterialsText.Text = snapshot.TotalMaterials.ToString();
            RegionTypesText.Text = snapshot.FilledRegionTypes.ToString();
            RegionInstancesText.Text = snapshot.FilledRegionInstances.ToString();
            UpdateStatus();
        }

        public PluginSettings Settings { get; private set; }
        public bool RunRequested { get; private set; }
        public IList<ParameterOption> MaterialParameterOptions { get; private set; }
        public IList<ParameterOption> TargetParameterOptions { get; private set; }

        private void AddMappingClick(object sender, RoutedEventArgs e)
        {
            _mappings.Add(CreateDefaultMapping());
            ValidationText.Text = string.Empty;
        }

        private void InsertMappingClick(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            ParameterMappingSetting current = button == null ? null : button.Tag as ParameterMappingSetting;
            int index = current == null ? _mappings.Count : _mappings.IndexOf(current) + 1;
            _mappings.Insert(index, CreateDefaultMapping());
            ValidationText.Text = string.Empty;
        }

        private void RemoveMappingClick(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            ParameterMappingSetting mapping = button == null ? null : button.Tag as ParameterMappingSetting;
            if (mapping != null)
            {
                _mappings.Remove(mapping);
                ValidationText.Text = string.Empty;
            }
        }

        private ParameterMappingSetting CreateDefaultMapping()
        {
            return new ParameterMappingSetting
            {
                SourceKey = _snapshot.MaterialParameters.FirstOrDefault() == null
                    ? null
                    : _snapshot.MaterialParameters.First().Key,
                TargetSelectionKey = _snapshot.TargetParameters.FirstOrDefault() == null
                    ? null
                    : _snapshot.TargetParameters.First().SelectionKey
            };
        }

        private void UpdateStatus()
        {
            StatusText.Text = "Соответствий параметров: " + _mappings.Count +
                              ". Значения обновляются при запуске синхронизации.";
        }

        private void MappingGridSizeChanged(object sender, SizeChangedEventArgs e)
        {
            const double fixedColumnsWidth = 44 + 84 + 4;
            double flexibleWidth = Math.Max(330, e.NewSize.Width - fixedColumnsWidth);
            double sourceWidth = Math.Max(150, flexibleWidth * 0.45);
            double targetWidth = Math.Max(180, flexibleWidth - sourceWidth);

            SourceMappingColumn.Width = new DataGridLength(sourceWidth);
            TargetMappingColumn.Width = new DataGridLength(targetWidth);
        }

        private void SaveClick(object sender, RoutedEventArgs e)
        {
            AcceptSettings(false);
        }

        private void RunClick(object sender, RoutedEventArgs e)
        {
            AcceptSettings(true);
        }

        private void AcceptSettings(bool run)
        {
            if (!MappingGrid.CommitEdit(DataGridEditingUnit.Cell, true) ||
                !MappingGrid.CommitEdit(DataGridEditingUnit.Row, true))
            {
                ValidationText.Text = "Завершите редактирование соответствия параметров.";
                return;
            }
            string prefix = (PrefixTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(prefix))
            {
                ValidationText.Text = "Укажите непустой префикс типов.";
                return;
            }

            ParameterMappingSetting incomplete = _mappings.FirstOrDefault(mapping =>
                string.IsNullOrWhiteSpace(mapping.SourceKey) ||
                string.IsNullOrWhiteSpace(mapping.TargetSelectionKey));
            if (incomplete != null)
            {
                ValidationText.Text = "Заполните или удалите незавершённое соответствие параметров.";
                return;
            }

            Settings.TypeNamePrefix = prefix;
            Settings.IncludeUnusedMaterials = IncludeUnusedMaterialsCheckBox.IsChecked == true;
            Settings.IncludePaintedMaterials = IncludePaintedMaterialsCheckBox.IsChecked == true;
            Settings.SkipMaterialsWithoutCutPattern = SkipWithoutPatternCheckBox.IsChecked == true;
            Settings.CopyForegroundPattern = CopyForegroundCheckBox.IsChecked == true;
            Settings.CopyBackgroundPattern = CopyBackgroundCheckBox.IsChecked == true;
            Settings.CopyPatternColors = CopyColorsCheckBox.IsChecked == true;
            Settings.ParameterMappings = _mappings.ToList();

            RunRequested = run;
            DialogResult = true;
            Close();
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void MinimizeWindowClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseWindowClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private static PluginSettings Clone(PluginSettings source)
        {
            return new PluginSettings
            {
                TypeNamePrefix = source.TypeNamePrefix,
                IncludeUnusedMaterials = source.IncludeUnusedMaterials,
                IncludePaintedMaterials = source.IncludePaintedMaterials,
                CopyForegroundPattern = source.CopyForegroundPattern,
                CopyBackgroundPattern = source.CopyBackgroundPattern,
                CopyPatternColors = source.CopyPatternColors,
                SkipMaterialsWithoutCutPattern = source.SkipMaterialsWithoutCutPattern,
                ParameterMappings = source.ParameterMappings
                    .Select(mapping => new ParameterMappingSetting
                    {
                        SourceKey = mapping.SourceKey,
                        TargetSelectionKey = mapping.TargetSelectionKey
                    })
                    .ToList()
            };
        }
    }
}
