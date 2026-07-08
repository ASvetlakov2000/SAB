using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SAB.UI;

namespace SAB.SyncReminder
{
    internal class SyncReminderSettingsWindow : Window
    {
        private readonly SyncReminderSettings _initialSettings;
        private CheckBox _enabledCheckBox;
        private Slider _minutesSlider;
        private TextBox _minutesTextBox;
        private ComboBox _animationModeComboBox;
        private bool _isUpdatingMinutes;

        public SyncReminderSettingsWindow(SyncReminderSettings settings)
        {
            _initialSettings = settings == null ? SyncReminderSettings.CreateDefault() : settings.Clone();
            Settings = _initialSettings.Clone();

            Title = "Настройки напоминания о синхронизации";
            Width = 500;
            Height = 365;
            MinWidth = 500;
            MinHeight = 365;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            Background = CreateBrush("#F7F8FA");

            Content = CreateContent();
            Loaded += OnLoaded;
            SabWindowBehaviorService.Apply(this);
        }

        public event EventHandler TestDuckRequested;

        public SyncReminderSettings Settings { get; private set; }

        private UIElement CreateContent()
        {
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Border panel = CreatePanelBorder();
            panel.Margin = new Thickness(22, 18, 20, 14);
            panel.Child = CreateMainPanelContent();
            Grid.SetRow(panel, 0);
            root.Children.Add(panel);

            Border footer = CreateFooter();
            Grid.SetRow(footer, 1);
            root.Children.Add(footer);

            return root;
        }

        private UIElement CreateMainPanelContent()
        {
            StackPanel panel = new StackPanel();

            StackPanel headerPanel = new StackPanel();
            headerPanel.Orientation = Orientation.Horizontal;
            headerPanel.Margin = new Thickness(0, 0, 0, 16);

            Border iconBorder = new Border();
            iconBorder.Width = 24;
            iconBorder.Height = 24;
            iconBorder.CornerRadius = new CornerRadius(5);
            iconBorder.Background = CreateBrush("#EAF3FF");
            iconBorder.BorderBrush = CreateBrush("#0F6CBD");
            iconBorder.BorderThickness = new Thickness(1);
            iconBorder.Margin = new Thickness(0, 0, 9, 0);

            TextBlock iconText = new TextBlock();
            iconText.Text = "!";
            iconText.FontWeight = FontWeights.SemiBold;
            iconText.Foreground = CreateBrush("#0F6CBD");
            iconText.HorizontalAlignment = HorizontalAlignment.Center;
            iconText.VerticalAlignment = VerticalAlignment.Center;
            iconBorder.Child = iconText;

            TextBlock headerText = new TextBlock();
            headerText.Text = "Напоминание о синхронизации";
            headerText.FontSize = 16;
            headerText.FontWeight = FontWeights.SemiBold;
            headerText.Foreground = CreateBrush("#1F2937");
            headerText.VerticalAlignment = VerticalAlignment.Center;

            headerPanel.Children.Add(iconBorder);
            headerPanel.Children.Add(headerText);
            panel.Children.Add(headerPanel);

            _enabledCheckBox = new CheckBox();
            _enabledCheckBox.Content = "Включить напоминание";
            _enabledCheckBox.FontSize = 13;
            _enabledCheckBox.Foreground = CreateBrush("#1F2937");
            _enabledCheckBox.Margin = new Thickness(0, 0, 0, 18);
            panel.Children.Add(_enabledCheckBox);

            Grid timerGrid = CreateTimerGrid();
            panel.Children.Add(timerGrid);

            Grid modeGrid = CreateAnimationModeGrid();
            modeGrid.Margin = new Thickness(0, 16, 0, 0);
            panel.Children.Add(modeGrid);

            return panel;
        }

        private Grid CreateTimerGrid()
        {
            Grid timerGrid = new Grid();
            timerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            timerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            timerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            timerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock label = new TextBlock();
            label.Text = "Таймер до напоминания, минут";
            label.FontSize = 12;
            label.FontWeight = FontWeights.SemiBold;
            label.Foreground = CreateBrush("#1F2937");
            label.Margin = new Thickness(0, 0, 0, 7);
            Grid.SetRow(label, 0);
            Grid.SetColumn(label, 0);
            Grid.SetColumnSpan(label, 2);
            timerGrid.Children.Add(label);

            _minutesSlider = new Slider();
            _minutesSlider.Minimum = 1;
            _minutesSlider.Maximum = 240;
            _minutesSlider.TickFrequency = 5;
            _minutesSlider.IsSnapToTickEnabled = false;
            _minutesSlider.VerticalAlignment = VerticalAlignment.Center;
            _minutesSlider.ValueChanged += OnMinutesSliderValueChanged;
            Grid.SetRow(_minutesSlider, 1);
            Grid.SetColumn(_minutesSlider, 0);
            timerGrid.Children.Add(_minutesSlider);

            _minutesTextBox = new TextBox();
            _minutesTextBox.Width = 72;
            _minutesTextBox.Height = 30;
            _minutesTextBox.Margin = new Thickness(14, 0, 0, 0);
            _minutesTextBox.VerticalContentAlignment = VerticalAlignment.Center;
            _minutesTextBox.TextAlignment = TextAlignment.Center;
            _minutesTextBox.BorderBrush = CreateBrush("#D8DEE8");
            _minutesTextBox.Background = Brushes.White;
            _minutesTextBox.Foreground = CreateBrush("#1F2937");
            _minutesTextBox.PreviewTextInput += OnMinutesPreviewTextInput;
            _minutesTextBox.LostFocus += OnMinutesLostFocus;
            Grid.SetRow(_minutesTextBox, 1);
            Grid.SetColumn(_minutesTextBox, 1);
            timerGrid.Children.Add(_minutesTextBox);

            return timerGrid;
        }

        private Grid CreateAnimationModeGrid()
        {
            Grid modeGrid = new Grid();
            modeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            modeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock label = new TextBlock();
            label.Text = "Режим анимации";
            label.FontSize = 12;
            label.FontWeight = FontWeights.SemiBold;
            label.Foreground = CreateBrush("#1F2937");
            label.Margin = new Thickness(0, 0, 0, 7);
            Grid.SetRow(label, 0);
            modeGrid.Children.Add(label);

            _animationModeComboBox = new ComboBox();
            _animationModeComboBox.Height = 30;
            _animationModeComboBox.BorderBrush = CreateBrush("#D8DEE8");
            _animationModeComboBox.Background = Brushes.White;
            _animationModeComboBox.Foreground = CreateBrush("#1F2937");
            _animationModeComboBox.Items.Add(CreateAnimationModeItem("Только утка", SyncReminderAnimationMode.DuckOnly));
            _animationModeComboBox.Items.Add(CreateAnimationModeItem("Утка и следы", SyncReminderAnimationMode.DuckWithPoop));
            Grid.SetRow(_animationModeComboBox, 1);
            modeGrid.Children.Add(_animationModeComboBox);

            return modeGrid;
        }

        private Border CreateFooter()
        {
            Border footer = new Border();
            footer.Background = Brushes.White;
            footer.BorderBrush = CreateBrush("#D8DEE8");
            footer.BorderThickness = new Thickness(0, 1, 0, 0);
            footer.Padding = new Thickness(22, 10, 22, 10);

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Button testButton = CreateButton("Показать утку", false);
            testButton.Width = 128;
            testButton.HorizontalAlignment = HorizontalAlignment.Left;
            testButton.Click += OnTestDuckClick;
            Grid.SetColumn(testButton, 0);
            grid.Children.Add(testButton);

            StackPanel buttonsPanel = new StackPanel();
            buttonsPanel.Orientation = Orientation.Horizontal;
            buttonsPanel.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(buttonsPanel, 1);

            Button cancelButton = CreateButton("Отмена", false);
            cancelButton.Margin = new Thickness(0, 0, 10, 0);
            cancelButton.Click += OnCancelClick;
            buttonsPanel.Children.Add(cancelButton);

            Button saveButton = CreateButton("Сохранить", true);
            saveButton.IsDefault = true;
            saveButton.Click += OnSaveClick;
            buttonsPanel.Children.Add(saveButton);

            grid.Children.Add(buttonsPanel);
            footer.Child = grid;

            return footer;
        }

        private Border CreatePanelBorder()
        {
            Border border = new Border();
            border.Background = Brushes.White;
            border.BorderBrush = CreateBrush("#D8DEE8");
            border.BorderThickness = new Thickness(1);
            border.CornerRadius = new CornerRadius(6);
            border.Padding = new Thickness(14);
            return border;
        }

        private Button CreateButton(string text, bool isPrimary)
        {
            Button button = new Button();
            button.Content = text;
            button.Width = isPrimary ? 112 : 96;
            button.Height = 32;
            button.Padding = new Thickness(12, 0, 12, 0);
            button.FontWeight = FontWeights.SemiBold;
            button.BorderThickness = new Thickness(1);
            button.Cursor = Cursors.Hand;

            if (isPrimary)
            {
                button.Background = CreateBrush("#0F6CBD");
                button.BorderBrush = CreateBrush("#0F6CBD");
                button.Foreground = Brushes.White;
            }
            else
            {
                button.Background = Brushes.White;
                button.BorderBrush = CreateBrush("#D8DEE8");
                button.Foreground = CreateBrush("#1F2937");
            }

            return button;
        }

        private ComboBoxItem CreateAnimationModeItem(string title, SyncReminderAnimationMode mode)
        {
            ComboBoxItem item = new ComboBoxItem();
            item.Content = title;
            item.Tag = mode;
            return item;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _enabledCheckBox.IsChecked = _initialSettings.IsEnabled;
            SetMinutes(_initialSettings.ReminderDelayMinutes);
            SelectAnimationMode(_initialSettings.AnimationMode);
        }

        private void OnMinutesSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingMinutes)
            {
                return;
            }

            int minutes = (int)Math.Round(_minutesSlider.Value);
            SetMinutes(minutes);
        }

        private void OnMinutesPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            for (int i = 0; i < e.Text.Length; i++)
            {
                if (!char.IsDigit(e.Text[i]))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private void OnMinutesLostFocus(object sender, RoutedEventArgs e)
        {
            int minutes;
            if (!TryReadMinutes(out minutes))
            {
                SetMinutes(1);
                return;
            }

            SetMinutes(minutes);
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            int minutes;
            if (!TryReadMinutes(out minutes))
            {
                MessageBox.Show(this, "Укажите таймер от 1 до 720 минут.", "Настройки", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Settings.IsEnabled = _enabledCheckBox.IsChecked == true;
            Settings.ReminderDelayMinutes = minutes;
            Settings.AnimationMode = GetSelectedAnimationMode();

            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OnTestDuckClick(object sender, RoutedEventArgs e)
        {
            int minutes;
            if (!TryReadMinutes(out minutes))
            {
                MessageBox.Show(this, "Укажите таймер от 1 до 720 минут.", "Настройки", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Settings.IsEnabled = _enabledCheckBox.IsChecked == true;
            Settings.ReminderDelayMinutes = minutes;
            Settings.AnimationMode = GetSelectedAnimationMode();

            EventHandler handler = TestDuckRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private bool TryReadMinutes(out int minutes)
        {
            minutes = 0;

            if (_minutesTextBox == null)
            {
                return false;
            }

            if (!int.TryParse(_minutesTextBox.Text, out minutes))
            {
                return false;
            }

            if (minutes < 1 || minutes > 720)
            {
                return false;
            }

            return true;
        }

        private void SetMinutes(int minutes)
        {
            if (minutes < 1)
            {
                minutes = 1;
            }

            if (minutes > 720)
            {
                minutes = 720;
            }

            _isUpdatingMinutes = true;

            if (_minutesTextBox != null)
            {
                _minutesTextBox.Text = minutes.ToString();
            }

            if (_minutesSlider != null)
            {
                _minutesSlider.Value = Math.Min(_minutesSlider.Maximum, minutes);
            }

            _isUpdatingMinutes = false;
        }

        private void SelectAnimationMode(SyncReminderAnimationMode mode)
        {
            if (_animationModeComboBox == null)
            {
                return;
            }

            for (int i = 0; i < _animationModeComboBox.Items.Count; i++)
            {
                ComboBoxItem item = _animationModeComboBox.Items[i] as ComboBoxItem;
                if (item != null && item.Tag is SyncReminderAnimationMode && (SyncReminderAnimationMode)item.Tag == mode)
                {
                    _animationModeComboBox.SelectedIndex = i;
                    return;
                }
            }

            _animationModeComboBox.SelectedIndex = 0;
        }

        private SyncReminderAnimationMode GetSelectedAnimationMode()
        {
            if (_animationModeComboBox != null)
            {
                ComboBoxItem item = _animationModeComboBox.SelectedItem as ComboBoxItem;
                if (item != null && item.Tag is SyncReminderAnimationMode)
                {
                    return (SyncReminderAnimationMode)item.Tag;
                }
            }

            return SyncReminderAnimationMode.DuckOnly;
        }

        private static SolidColorBrush CreateBrush(string color)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }
    }
}
