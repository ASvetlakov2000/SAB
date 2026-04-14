using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using SAB.BimDashboard.Models;
using Forms = System.Windows.Forms;

namespace SAB.BimDashboard.UI
{
    /// <summary>
    /// Диалог выбора источника данных для MVP dashboard.
    /// Важно: окно собирается программно без XAML, чтобы исключить проблемы pack URI в среде Revit add-in.
    /// </summary>
    public partial class DataSourceDialog : Window
    {
        private RadioButton _revitRadioButton;
        private RadioButton _csvRadioButton;
        private RadioButton _excelRadioButton;
        private TextBox _filePathTextBox;
        private Button _browseButton;

        public DataSourceDialog()
        {
            SelectedSourceType = DataSourceType.Revit;
            SelectedFilePath = string.Empty;

            BuildUi();
            UpdateSourceState();
        }

        public DataSourceType SelectedSourceType { get; private set; }

        public string SelectedFilePath { get; private set; }

        // Блок создания UI программно вместо InitializeComponent.
        private void BuildUi()
        {
            Title = "Источник данных dashboard";
            Width = 520;
            Height = 260;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            Grid rootGrid = new Grid();
            rootGrid.Margin = new Thickness(14);
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock header = new TextBlock
            {
                Margin = new Thickness(0, 0, 0, 10),
                FontWeight = FontWeights.SemiBold,
                Text = "Выберите источник данных для HTML dashboard:"
            };
            Grid.SetRow(header, 0);
            rootGrid.Children.Add(header);

            StackPanel sourcePanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(sourcePanel, 1);

            _revitRadioButton = new RadioButton
            {
                Content = "Текущая модель Revit",
                IsChecked = true,
                Margin = new Thickness(0, 0, 0, 6)
            };
            _revitRadioButton.Checked += SourceRadioButton_Checked;
            sourcePanel.Children.Add(_revitRadioButton);

            _csvRadioButton = new RadioButton
            {
                Content = "CSV файл",
                Margin = new Thickness(0, 0, 0, 6)
            };
            _csvRadioButton.Checked += SourceRadioButton_Checked;
            sourcePanel.Children.Add(_csvRadioButton);

            _excelRadioButton = new RadioButton
            {
                Content = "Excel файл (XLSX)"
            };
            _excelRadioButton.Checked += SourceRadioButton_Checked;
            sourcePanel.Children.Add(_excelRadioButton);

            rootGrid.Children.Add(sourcePanel);

            TextBlock pathLabel = new TextBlock
            {
                Margin = new Thickness(0, 0, 0, 6),
                Text = "Путь к файлу (для CSV/XLSX):"
            };
            Grid.SetRow(pathLabel, 2);
            rootGrid.Children.Add(pathLabel);

            Grid pathGrid = new Grid();
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(pathGrid, 3);

            _filePathTextBox = new TextBox
            {
                Height = 28,
                VerticalContentAlignment = VerticalAlignment.Center,
                IsEnabled = false
            };
            Grid.SetColumn(_filePathTextBox, 0);
            pathGrid.Children.Add(_filePathTextBox);

            _browseButton = new Button
            {
                Width = 100,
                Height = 28,
                Margin = new Thickness(10, 0, 0, 0),
                Content = "Обзор",
                IsEnabled = false
            };
            _browseButton.Click += BrowseButton_Click;
            Grid.SetColumn(_browseButton, 1);
            pathGrid.Children.Add(_browseButton);

            rootGrid.Children.Add(pathGrid);

            StackPanel actionsPanel = new StackPanel
            {
                Margin = new Thickness(0, 14, 0, 0),
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(actionsPanel, 4);

            Button okButton = new Button
            {
                Width = 100,
                Height = 30,
                Margin = new Thickness(0, 0, 8, 0),
                Content = "Построить"
            };
            okButton.Click += OkButton_Click;
            actionsPanel.Children.Add(okButton);

            Button cancelButton = new Button
            {
                Width = 100,
                Height = 30,
                Content = "Отмена"
            };
            cancelButton.Click += CancelButton_Click;
            actionsPanel.Children.Add(cancelButton);

            rootGrid.Children.Add(actionsPanel);
            Content = rootGrid;
        }

        private void SourceRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            UpdateSourceState();
        }

        private void UpdateSourceState()
        {
            // Блок переключения режима выбора файла.
            bool isCsv = _csvRadioButton.IsChecked == true;
            bool isExcel = _excelRadioButton.IsChecked == true;
            bool fileSelectionEnabled = isCsv || isExcel;

            _filePathTextBox.IsEnabled = fileSelectionEnabled;
            _browseButton.IsEnabled = fileSelectionEnabled;

            if (_revitRadioButton.IsChecked == true)
            {
                SelectedSourceType = DataSourceType.Revit;
            }
            else if (isCsv)
            {
                SelectedSourceType = DataSourceType.Csv;
            }
            else
            {
                SelectedSourceType = DataSourceType.Excel;
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            using (Forms.OpenFileDialog dialog = new Forms.OpenFileDialog())
            {
                dialog.Multiselect = false;
                if (Directory.Exists(@"Z:\IN"))
                {
                    dialog.InitialDirectory = @"Z:\IN";
                }

                if (SelectedSourceType == DataSourceType.Csv)
                {
                    dialog.Title = "Выберите CSV файл";
                    dialog.Filter = "CSV (*.csv)|*.csv|All files (*.*)|*.*";
                }
                else
                {
                    dialog.Title = "Выберите XLSX файл";
                    dialog.Filter = "Excel (*.xlsx)|*.xlsx|All files (*.*)|*.*";
                }

                Forms.DialogResult dialogResult = dialog.ShowDialog();

                if (dialogResult == Forms.DialogResult.OK)
                {
                    _filePathTextBox.Text = dialog.FileName;
                }
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Блок валидации для источников с внешним файлом.
            if (SelectedSourceType == DataSourceType.Csv || SelectedSourceType == DataSourceType.Excel)
            {
                string filePath = (_filePathTextBox.Text ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(filePath))
                {
                    MessageBox.Show("Укажите путь к файлу данных.", "BIM Dashboard", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!File.Exists(filePath))
                {
                    MessageBox.Show("Файл не найден: " + filePath, "BIM Dashboard", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (SelectedSourceType == DataSourceType.Csv && !string.Equals(Path.GetExtension(filePath), ".csv", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Для источника CSV нужен файл с расширением .csv.", "BIM Dashboard", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (SelectedSourceType == DataSourceType.Excel && !string.Equals(Path.GetExtension(filePath), ".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Для источника Excel нужен файл с расширением .xlsx.", "BIM Dashboard", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SelectedFilePath = filePath;
            }
            else
            {
                SelectedFilePath = string.Empty;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
