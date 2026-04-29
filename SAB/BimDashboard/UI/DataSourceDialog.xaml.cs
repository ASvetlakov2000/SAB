using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SAB.BimDashboard.Models;
using Forms = System.Windows.Forms;

namespace SAB.BimDashboard.UI
{
    /// <summary>
    /// Диалог выбора CSV источника для профильного dashboard.
    /// </summary>
    public partial class DataSourceDialog : Window
    {
        private ComboBox _profileComboBox;
        private TextBlock _selectedFilePathText;
        private string _selectedFilePath;

        public DataSourceDialog()
        {
            SelectedSourceType = DataSourceType.Csv;
            SelectedFilePath = string.Empty;
            SelectedProfileType = DashboardProfileType.SystemFamilies;
            _selectedFilePath = string.Empty;

            BuildUi();
            UpdateProfileSelection();
        }

        public DataSourceType SelectedSourceType { get; private set; }

        public DashboardProfileType SelectedProfileType { get; private set; }

        public string SelectedFilePath { get; private set; }

        private void BuildUi()
        {
            Title = "Источник данных dashboard";
            Width = 700;
            Height = 470;
            MinWidth = 680;
            MinHeight = 450;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(Color.FromRgb(242, 242, 242));

            Grid rootGrid = new Grid();
            rootGrid.Margin = new Thickness(16);
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock titleText = new TextBlock
            {
                Text = "Источник данных для просмотра категорий",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(17, 17, 17)),
                Margin = new Thickness(0, 0, 0, 14)
            };
            Grid.SetRow(titleText, 0);
            rootGrid.Children.Add(titleText);

            Border profileCard = CreateCardBorder();
            Grid.SetRow(profileCard, 1);

            StackPanel profilePanel = new StackPanel();
            profileCard.Child = profilePanel;
            profilePanel.Children.Add(CreateSectionHeader("Профиль просмотра"));

            _profileComboBox = new ComboBox
            {
                Margin = new Thickness(10),
                Height = 32,
                FontSize = 14,
                BorderBrush = new SolidColorBrush(Colors.Black),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Colors.White),
                IsEnabled = false
            };
            _profileComboBox.Items.Add(new ProfileItem("Системные семейства", DashboardProfileType.SystemFamilies));
            _profileComboBox.Items.Add(new ProfileItem("Загружаемые семейства", DashboardProfileType.LoadableFamilies));
            _profileComboBox.Items.Add(new ProfileItem("Линии", DashboardProfileType.Lines));
            _profileComboBox.Items.Add(new ProfileItem("Штриховки", DashboardProfileType.FillPatterns));
            _profileComboBox.SelectionChanged += ProfileComboBox_SelectionChanged;
            _profileComboBox.SelectedIndex = 0;
            profilePanel.Children.Add(_profileComboBox);

            rootGrid.Children.Add(profileCard);

            Border sourceCard = CreateCardBorder();
            Grid.SetRow(sourceCard, 2);
            sourceCard.Margin = new Thickness(0, 10, 0, 0);

            StackPanel sourcePanel = new StackPanel();
            sourceCard.Child = sourcePanel;
            sourcePanel.Children.Add(CreateSectionHeader("CSV источник"));

            Button browseButton = new Button
            {
                Content = "Выбрать источник",
                Height = 34,
                Margin = new Thickness(10, 10, 10, 8),
                FontSize = 14,
                BorderBrush = new SolidColorBrush(Colors.Black),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromRgb(225, 225, 225)),
                Foreground = new SolidColorBrush(Color.FromRgb(16, 16, 16))
            };
            browseButton.Click += BrowseButton_Click;
            sourcePanel.Children.Add(browseButton);

            _selectedFilePathText = new TextBlock
            {
                Margin = new Thickness(10, 0, 10, 10),
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
                Text = "Источник не выбран"
            };
            sourcePanel.Children.Add(_selectedFilePathText);
            rootGrid.Children.Add(sourceCard);

            TextBlock infoText = new TextBlock
            {
                Margin = new Thickness(2, 10, 2, 0),
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Text = "Поддерживается только CSV. Профиль определяется автоматически по имени файла."
            };
            Grid.SetRow(infoText, 3);
            rootGrid.Children.Add(infoText);

            DockPanel actionsPanel = new DockPanel
            {
                Margin = new Thickness(0, 16, 0, 0),
                LastChildFill = false
            };
            Grid.SetRow(actionsPanel, 5);

            Button cancelButton = CreateActionButton("Отмена", 150);
            cancelButton.Click += CancelButton_Click;
            DockPanel.SetDock(cancelButton, Dock.Right);
            actionsPanel.Children.Add(cancelButton);

            Button okButton = CreateActionButton("Открыть просмотрщик", 200);
            okButton.Margin = new Thickness(0, 0, 8, 0);
            okButton.Click += OkButton_Click;
            DockPanel.SetDock(okButton, Dock.Right);
            actionsPanel.Children.Add(okButton);

            rootGrid.Children.Add(actionsPanel);

            Content = rootGrid;
        }

        private static Border CreateCardBorder()
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(252, 252, 252)),
                BorderBrush = new SolidColorBrush(Colors.Black),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(0)
            };
        }

        private static Border CreateSectionHeader(string text)
        {
            Border header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(235, 235, 235)),
                BorderBrush = new SolidColorBrush(Colors.Black),
                BorderThickness = new Thickness(0, 0, 0, 1),
                CornerRadius = new CornerRadius(10, 10, 0, 0),
                Padding = new Thickness(10, 8, 10, 8)
            };

            TextBlock label = new TextBlock
            {
                Text = text,
                FontSize = 15,
                Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32))
            };

            header.Child = label;
            return header;
        }

        private static Button CreateActionButton(string text, double width)
        {
            return new Button
            {
                Content = text,
                Width = width,
                Height = 36,
                FontSize = 14,
                BorderBrush = new SolidColorBrush(Colors.Black),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromRgb(225, 225, 225)),
                Foreground = new SolidColorBrush(Color.FromRgb(16, 16, 16))
            };
        }

        private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateProfileSelection();
        }

        private void UpdateProfileSelection()
        {
            ProfileItem selected = _profileComboBox.SelectedItem as ProfileItem;

            if (selected != null)
            {
                SelectedProfileType = selected.ProfileType;
            }
            else
            {
                SelectedProfileType = DashboardProfileType.SystemFamilies;
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            using (Forms.OpenFileDialog dialog = new Forms.OpenFileDialog())
            {
                dialog.Multiselect = false;
                dialog.Title = "Выберите CSV источник";
                dialog.Filter = "CSV (*.csv)|*.csv|All files (*.*)|*.*";

                Forms.DialogResult dialogResult = dialog.ShowDialog();

                if (dialogResult == Forms.DialogResult.OK)
                {
                    _selectedFilePath = dialog.FileName;
                    _selectedFilePathText.Text = dialog.FileName;
                    _selectedFilePathText.ToolTip = dialog.FileName;

                    DashboardProfileType detectedProfileType;

                    if (TryDetectProfileByFileName(dialog.FileName, out detectedProfileType))
                    {
                        SetSelectedProfile(detectedProfileType);
                    }
                }
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            string filePath = (_selectedFilePath ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(filePath))
            {
                MessageBox.Show("Выберите CSV источник.", "BIM Dashboard", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(filePath))
            {
                MessageBox.Show("Файл не найден: " + filePath, "BIM Dashboard", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!string.Equals(Path.GetExtension(filePath), ".csv", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Поддерживается только формат .csv", "BIM Dashboard", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DashboardProfileType detectedProfile;

            if (!TryDetectProfileByFileName(filePath, out detectedProfile))
            {
                MessageBox.Show(
                    "Имя файла не соответствует поддерживаемым шаблонам.\n\n" +
                    "Ожидается, что имя содержит:\n" +
                    "- \"Системные семейства\"\n" +
                    "- \"Загружаемые семейства\"\n" +
                    "- \"Линии\"\n" +
                    "- \"Штриховки\"",
                    "BIM Dashboard",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            SelectedSourceType = DataSourceType.Csv;
            SelectedFilePath = filePath;
            SelectedProfileType = detectedProfile;

            DialogResult = true;
            Close();
        }

        private bool TryDetectProfileByFileName(string filePath, out DashboardProfileType profileType)
        {
            profileType = DashboardProfileType.SystemFamilies;

            string fileNameWithoutExtension = string.Empty;

            try
            {
                fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
            }
            catch
            {
                return false;
            }

            if (fileNameWithoutExtension.IndexOf("Системные семейства", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                profileType = DashboardProfileType.SystemFamilies;
                return true;
            }

            if (fileNameWithoutExtension.IndexOf("Загружаемые семейства", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                profileType = DashboardProfileType.LoadableFamilies;
                return true;
            }

            if (fileNameWithoutExtension.IndexOf("Линии", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileNameWithoutExtension.IndexOf("LineStyles", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                profileType = DashboardProfileType.Lines;
                return true;
            }

            if (fileNameWithoutExtension.IndexOf("Штриховки", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileNameWithoutExtension.IndexOf("FillPatterns", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                profileType = DashboardProfileType.FillPatterns;
                return true;
            }

            return false;
        }

        private void SetSelectedProfile(DashboardProfileType profileType)
        {
            SelectedProfileType = profileType;

            for (int i = 0; i < _profileComboBox.Items.Count; i++)
            {
                ProfileItem item = _profileComboBox.Items[i] as ProfileItem;

                if (item == null)
                {
                    continue;
                }

                if (item.ProfileType == profileType)
                {
                    _profileComboBox.SelectedIndex = i;
                    break;
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private class ProfileItem
        {
            public ProfileItem(string displayName, DashboardProfileType profileType)
            {
                DisplayName = displayName;
                ProfileType = profileType;
            }

            public string DisplayName { get; private set; }

            public DashboardProfileType ProfileType { get; private set; }

            public override string ToString()
            {
                return DisplayName;
            }
        }
    }
}
