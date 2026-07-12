using System;
using System.IO;
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

            LoadSabStyles();

            Title = "Настройки напоминания о синхронизации";
            Width = 560;
            Height = 540;
            MinWidth = 560;
            MinHeight = 540;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            Background = GetBrush("SabBrush.WindowBackground", "#F7F8FA");

            Content = CreateContent();
            Loaded += OnLoaded;
            SabWindowBehaviorService.Apply(this);
        }

        public event EventHandler TestDuckRequested;

        public SyncReminderSettings Settings { get; private set; }

        private UIElement CreateContent()
        {
            Grid root = new Grid();
            root.Margin = new Thickness(20);
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            UIElement header = CreateHeader();
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            Border panel = CreateStyledBorder("SabPanelBorderStyle");
            panel.Margin = new Thickness(0, 16, 0, 0);
            panel.Child = CreateMainPanelContent();
            Grid.SetRow(panel, 1);
            root.Children.Add(panel);

            UIElement footer = CreateFooter();
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            return root;
        }

        private UIElement CreateHeader()
        {
            StackPanel panel = new StackPanel();

            TextBlock titleText = new TextBlock();
            titleText.Text = "Напоминание о синхронизации";
            ApplyStyle(titleText, "SabWindowTitleTextStyle");
            panel.Children.Add(titleText);

            TextBlock subtitleText = new TextBlock();
            subtitleText.Text = "Персонаж появляется в рабочей области Revit после заданного времени без синхронизации.";
            subtitleText.Margin = new Thickness(0, 4, 0, 0);
            ApplyStyle(subtitleText, "SabWindowSubtitleTextStyle");
            panel.Children.Add(subtitleText);

            return panel;
        }

        private UIElement CreateMainPanelContent()
        {
            StackPanel panel = new StackPanel();

            panel.Children.Add(CreateEnablePanel());
            panel.Children.Add(CreateTimerGrid());
            panel.Children.Add(CreateDivider());
            panel.Children.Add(CreateAnimationModeGrid());
            panel.Children.Add(CreatePreviewInfoPanel());

            return panel;
        }

        private UIElement CreateEnablePanel()
        {
            Border border = CreateStyledBorder("SabPrimarySettingBorderStyle");

            _enabledCheckBox = new CheckBox();
            _enabledCheckBox.Content = "Включить напоминание";
            ApplyStyle(_enabledCheckBox, "SabCheckBoxStyle");
            border.Child = _enabledCheckBox;

            return border;
        }

        private Grid CreateTimerGrid()
        {
            Grid timerGrid = new Grid();
            timerGrid.Margin = new Thickness(0, 0, 0, 14);
            timerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            timerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            timerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            timerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            timerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock label = CreateFieldLabel("Таймер до напоминания, минут");
            Grid.SetRow(label, 0);
            Grid.SetColumn(label, 0);
            Grid.SetColumnSpan(label, 2);
            timerGrid.Children.Add(label);

            _minutesSlider = new Slider();
            _minutesSlider.Minimum = 1;
            _minutesSlider.Maximum = 720;
            _minutesSlider.TickFrequency = 5;
            _minutesSlider.IsSnapToTickEnabled = false;
            _minutesSlider.VerticalAlignment = VerticalAlignment.Center;
            _minutesSlider.ValueChanged += OnMinutesSliderValueChanged;
            Grid.SetRow(_minutesSlider, 1);
            Grid.SetColumn(_minutesSlider, 0);
            timerGrid.Children.Add(_minutesSlider);

            _minutesTextBox = new TextBox();
            _minutesTextBox.Width = 76;
            _minutesTextBox.Margin = new Thickness(14, 0, 0, 0);
            _minutesTextBox.TextAlignment = TextAlignment.Center;
            _minutesTextBox.PreviewTextInput += OnMinutesPreviewTextInput;
            _minutesTextBox.LostFocus += OnMinutesLostFocus;
            ApplyStyle(_minutesTextBox, "SabTextBoxStyle");
            Grid.SetRow(_minutesTextBox, 1);
            Grid.SetColumn(_minutesTextBox, 1);
            timerGrid.Children.Add(_minutesTextBox);

            TextBlock hint = CreateFieldHint("Можно указать от 1 до 720 минут.");
            hint.Margin = new Thickness(0, 6, 0, 0);
            Grid.SetRow(hint, 2);
            Grid.SetColumn(hint, 0);
            Grid.SetColumnSpan(hint, 2);
            timerGrid.Children.Add(hint);

            return timerGrid;
        }

        private Grid CreateAnimationModeGrid()
        {
            Grid modeGrid = new Grid();
            modeGrid.Margin = new Thickness(0, 14, 0, 0);
            modeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            modeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            modeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock label = CreateFieldLabel("Режим анимации");
            Grid.SetRow(label, 0);
            modeGrid.Children.Add(label);

            _animationModeComboBox = new ComboBox();
            _animationModeComboBox.Items.Add(CreateAnimationModeItem("Только утка", SyncReminderAnimationMode.DuckOnly));
            _animationModeComboBox.Items.Add(CreateAnimationModeItem("Утка и следы", SyncReminderAnimationMode.DuckWithPoop));
            _animationModeComboBox.Items.Add(CreateAnimationModeItem("Мордочка: Scottish Fold", SyncReminderAnimationMode.PeekingScottishFold));
            _animationModeComboBox.Items.Add(CreateAnimationModeItem("Мордочка: медвежонок", SyncReminderAnimationMode.PeekingBear));
            ApplyStyle(_animationModeComboBox, "SabComboBoxStyle");
            Grid.SetRow(_animationModeComboBox, 1);
            modeGrid.Children.Add(_animationModeComboBox);

            TextBlock hint = CreateFieldHint("Предпросмотр можно открыть без сохранения настроек.");
            hint.Margin = new Thickness(0, 6, 0, 0);
            Grid.SetRow(hint, 2);
            modeGrid.Children.Add(hint);

            return modeGrid;
        }

        private UIElement CreatePreviewInfoPanel()
        {
            Border border = new Border();
            border.Margin = new Thickness(0, 16, 0, 0);
            border.Padding = new Thickness(10, 8, 10, 8);
            border.CornerRadius = new CornerRadius(5);
            border.Background = GetBrush("SabBrush.AccentLight", "#EAF3FF");
            border.BorderBrush = GetBrush("SabBrush.Accent", "#0F6CBD");
            border.BorderThickness = new Thickness(1);

            TextBlock textBlock = new TextBlock();
            textBlock.Text = "Кнопка предпросмотра показывает выбранного персонажа временно. Если закрыть это окно без сохранения, предпросмотр тоже закроется.";
            textBlock.TextWrapping = TextWrapping.Wrap;
            textBlock.Foreground = GetBrush("SabBrush.Accent", "#0F6CBD");
            textBlock.FontSize = 12;
            border.Child = textBlock;

            return border;
        }

        private UIElement CreateDivider()
        {
            Border divider = new Border();
            divider.Height = 1;
            divider.Margin = new Thickness(0, 2, 0, 0);
            divider.Background = GetBrush("SabBrush.BorderWeak", "#E6EAF0");
            return divider;
        }

        private UIElement CreateFooter()
        {
            Grid footer = new Grid();
            footer.Margin = new Thickness(0, 16, 0, 0);
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Button testButton = CreateButton("Показать персонажа", "SabOutlineButtonStyle", 158);
            testButton.HorizontalAlignment = HorizontalAlignment.Left;
            testButton.Click += OnTestDuckClick;
            Grid.SetColumn(testButton, 0);
            footer.Children.Add(testButton);

            StackPanel buttonsPanel = new StackPanel();
            buttonsPanel.Orientation = Orientation.Horizontal;
            buttonsPanel.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(buttonsPanel, 1);

            Button cancelButton = CreateButton("Отмена", "SabNeutralButtonStyle", 104);
            cancelButton.Margin = new Thickness(0, 0, 10, 0);
            cancelButton.IsCancel = true;
            cancelButton.Click += OnCancelClick;
            buttonsPanel.Children.Add(cancelButton);

            Button saveButton = CreateButton("Сохранить", "SabPrimaryButtonStyle", 116);
            saveButton.IsDefault = true;
            saveButton.Click += OnSaveClick;
            buttonsPanel.Children.Add(saveButton);

            footer.Children.Add(buttonsPanel);
            return footer;
        }

        private TextBlock CreateFieldLabel(string text)
        {
            TextBlock textBlock = new TextBlock();
            textBlock.Text = text;
            ApplyStyle(textBlock, "SabFieldLabelTextStyle");
            return textBlock;
        }

        private TextBlock CreateFieldHint(string text)
        {
            TextBlock textBlock = new TextBlock();
            textBlock.Text = text;
            ApplyStyle(textBlock, "SabFieldHintTextStyle");
            return textBlock;
        }

        private Border CreateStyledBorder(string styleKey)
        {
            Border border = new Border();
            ApplyStyle(border, styleKey);

            if (border.Style == null)
            {
                border.Background = Brushes.White;
                border.BorderBrush = CreateBrush("#D8DEE8");
                border.BorderThickness = new Thickness(1);
                border.CornerRadius = new CornerRadius(6);
                border.Padding = new Thickness(14);
            }

            return border;
        }

        private Button CreateButton(string text, string styleKey, double width)
        {
            Button button = new Button();
            button.Content = text;
            button.Width = width;
            button.Cursor = Cursors.Hand;
            ApplyStyle(button, styleKey);
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

        private void LoadSabStyles()
        {
            try
            {
                string assemblyFolder = Path.GetDirectoryName(typeof(SyncReminderSettingsWindow).Assembly.Location);
                if (string.IsNullOrWhiteSpace(assemblyFolder))
                {
                    return;
                }

                string stylesPath = Path.Combine(assemblyFolder, "UI", "Styles", "SABWindowStyles.xaml");
                if (!File.Exists(stylesPath))
                {
                    return;
                }

                ResourceDictionary dictionary = new ResourceDictionary();
                dictionary.Source = new Uri(stylesPath, UriKind.Absolute);
                Resources.MergedDictionaries.Add(dictionary);
            }
            catch
            {
                // If shared styles are not available, the window falls back to local colors.
            }
        }

        private void ApplyStyle(FrameworkElement element, string styleKey)
        {
            if (element == null || string.IsNullOrWhiteSpace(styleKey))
            {
                return;
            }

            Style style = TryFindResource(styleKey) as Style;
            if (style != null)
            {
                element.Style = style;
            }
        }

        private Brush GetBrush(string resourceKey, string fallbackColor)
        {
            Brush brush = TryFindResource(resourceKey) as Brush;
            if (brush != null)
            {
                return brush;
            }

            return CreateBrush(fallbackColor);
        }

        private static SolidColorBrush CreateBrush(string color)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }
    }
}
