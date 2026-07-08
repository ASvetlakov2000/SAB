using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SyncReminderTest
{
    internal class SettingsWindow : Window
    {
        private readonly ReminderSettings _initialSettings;
        private CheckBox _enabledCheckBox;
        private Slider _minutesSlider;
        private TextBox _minutesTextBox;
        private ComboBox _animationComboBox;
        private bool _isUpdatingMinutes;

        public SettingsWindow(ReminderSettings settings)
        {
            _initialSettings = settings == null ? ReminderSettings.CreateDefault() : settings.Clone();
            Settings = _initialSettings.Clone();

            Title = "Настройки напоминания";
            Width = 430;
            Height = 325;
            MinWidth = 430;
            MinHeight = 325;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;

            Content = CreateContent();
            Loaded += OnLoaded;
        }

        public ReminderSettings Settings { get; private set; }

        private UIElement CreateContent()
        {
            Grid root = new Grid();
            root.Margin = new Thickness(18);
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock titleText = new TextBlock();
            titleText.Text = "Напоминание о синхронизации";
            titleText.FontFamily = new FontFamily("Segoe UI");
            titleText.FontSize = 18;
            titleText.FontWeight = FontWeights.SemiBold;
            titleText.Margin = new Thickness(0, 0, 0, 14);
            Grid.SetRow(titleText, 0);
            root.Children.Add(titleText);

            _enabledCheckBox = new CheckBox();
            _enabledCheckBox.Content = "Включить функционал";
            _enabledCheckBox.FontFamily = new FontFamily("Segoe UI");
            _enabledCheckBox.FontSize = 14;
            _enabledCheckBox.Margin = new Thickness(0, 0, 0, 18);
            Grid.SetRow(_enabledCheckBox, 1);
            root.Children.Add(_enabledCheckBox);

            Grid timerGrid = CreateTimerGrid();
            Grid.SetRow(timerGrid, 2);
            root.Children.Add(timerGrid);

            Grid animationGrid = CreateAnimationGrid();
            Grid.SetRow(animationGrid, 3);
            root.Children.Add(animationGrid);

            StackPanel buttonsPanel = new StackPanel();
            buttonsPanel.Orientation = Orientation.Horizontal;
            buttonsPanel.HorizontalAlignment = HorizontalAlignment.Right;

            Button cancelButton = new Button();
            cancelButton.Content = "Отмена";
            cancelButton.Width = 92;
            cancelButton.Height = 30;
            cancelButton.Margin = new Thickness(0, 0, 8, 0);
            cancelButton.Click += OnCancelClick;
            buttonsPanel.Children.Add(cancelButton);

            Button saveButton = new Button();
            saveButton.Content = "Сохранить";
            saveButton.Width = 104;
            saveButton.Height = 30;
            saveButton.IsDefault = true;
            saveButton.Click += OnSaveClick;
            buttonsPanel.Children.Add(saveButton);

            Grid.SetRow(buttonsPanel, 4);
            root.Children.Add(buttonsPanel);

            return root;
        }

        private Grid CreateTimerGrid()
        {
            Grid timerGrid = new Grid();
            timerGrid.Margin = new Thickness(0, 0, 0, 18);
            timerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            timerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            timerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            timerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock label = new TextBlock();
            label.Text = "Таймер до напоминания, минут";
            label.FontFamily = new FontFamily("Segoe UI");
            label.FontSize = 13;
            label.Margin = new Thickness(0, 0, 0, 6);
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
            _minutesTextBox.Width = 64;
            _minutesTextBox.Height = 28;
            _minutesTextBox.Margin = new Thickness(12, 0, 0, 0);
            _minutesTextBox.VerticalContentAlignment = VerticalAlignment.Center;
            _minutesTextBox.TextAlignment = TextAlignment.Center;
            _minutesTextBox.PreviewTextInput += OnMinutesPreviewTextInput;
            _minutesTextBox.LostFocus += OnMinutesLostFocus;
            Grid.SetRow(_minutesTextBox, 1);
            Grid.SetColumn(_minutesTextBox, 1);
            timerGrid.Children.Add(_minutesTextBox);

            return timerGrid;
        }

        private Grid CreateAnimationGrid()
        {
            Grid animationGrid = new Grid();
            animationGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            animationGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock label = new TextBlock();
            label.Text = "Анимация";
            label.FontFamily = new FontFamily("Segoe UI");
            label.FontSize = 13;
            label.Margin = new Thickness(0, 0, 0, 6);
            Grid.SetRow(label, 0);
            animationGrid.Children.Add(label);

            _animationComboBox = new ComboBox();
            _animationComboBox.Height = 30;
            _animationComboBox.Items.Add(CreateAnimationItem("Туман + уточка", ReminderAnimationMode.FogAndDuck));
            _animationComboBox.Items.Add(CreateAnimationItem("Только туман на панели", ReminderAnimationMode.FogOnly));
            _animationComboBox.Items.Add(CreateAnimationItem("Только уточка в рабочем окне", ReminderAnimationMode.DuckOnly));
            Grid.SetRow(_animationComboBox, 1);
            animationGrid.Children.Add(_animationComboBox);

            return animationGrid;
        }

        private ComboBoxItem CreateAnimationItem(string title, ReminderAnimationMode mode)
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

        private void SelectAnimationMode(ReminderAnimationMode mode)
        {
            if (_animationComboBox == null)
            {
                return;
            }

            for (int i = 0; i < _animationComboBox.Items.Count; i++)
            {
                ComboBoxItem item = _animationComboBox.Items[i] as ComboBoxItem;
                if (item != null && item.Tag is ReminderAnimationMode && (ReminderAnimationMode)item.Tag == mode)
                {
                    _animationComboBox.SelectedIndex = i;
                    return;
                }
            }

            _animationComboBox.SelectedIndex = 0;
        }

        private ReminderAnimationMode GetSelectedAnimationMode()
        {
            ComboBoxItem item = _animationComboBox.SelectedItem as ComboBoxItem;
            if (item != null && item.Tag is ReminderAnimationMode)
            {
                return (ReminderAnimationMode)item.Tag;
            }

            return ReminderAnimationMode.FogAndDuck;
        }
    }
}
