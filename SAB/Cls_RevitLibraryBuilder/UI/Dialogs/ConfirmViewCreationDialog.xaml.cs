using System;
using System.Windows;
using System.Windows.Controls;
using SAB.UI;

namespace SAB.Cls_RevitLibraryBuilder.UI.Dialogs
{
    /// <summary>
    /// Simple confirmation dialog for view creation actions.
    /// </summary>
    public class ConfirmViewCreationDialog : Window
    {
        private readonly TextBlock _messageTextBlock;
        private readonly Button _yesButton;
        private readonly Button _noButton;

        public bool Result { get; private set; }

        public ConfirmViewCreationDialog(string categoryName)
            : this(BuildCategoryQuestion(categoryName), "Да", "Нет")
        {
        }

        public ConfirmViewCreationDialog(string titleText, string yesButtonText, string noButtonText)
        {
            string safeTitle = string.IsNullOrWhiteSpace(titleText) ? "Подтвердите действие" : titleText.Trim();
            string safeYesText = string.IsNullOrWhiteSpace(yesButtonText) ? "Да" : yesButtonText.Trim();
            string safeNoText = string.IsNullOrWhiteSpace(noButtonText) ? "Нет" : noButtonText.Trim();

            // Basic window parameters.
            Title = "Подтверждение";
            Width = 420;
            Height = 190;
            MinWidth = 380;
            MinHeight = 170;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;

            Grid rootGrid = new Grid
            {
                Margin = new Thickness(14)
            };

            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _messageTextBlock = new TextBlock
            {
                Text = safeTitle,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(2, 2, 2, 12),
                FontSize = 13
            };
            Grid.SetRow(_messageTextBlock, 0);
            rootGrid.Children.Add(_messageTextBlock);

            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(buttonPanel, 1);

            _yesButton = new Button
            {
                Width = 95,
                Height = 28,
                Margin = new Thickness(0, 0, 8, 0),
                Content = safeYesText,
                IsDefault = true
            };
            _yesButton.Click += YesButton_Click;

            _noButton = new Button
            {
                Width = 95,
                Height = 28,
                Content = safeNoText,
                IsCancel = true
            };
            _noButton.Click += NoButton_Click;

            buttonPanel.Children.Add(_yesButton);
            buttonPanel.Children.Add(_noButton);
            rootGrid.Children.Add(buttonPanel);

            Content = rootGrid;
            WindowSizeSettingsService.Apply(this, "RevitLibraryBuilder.ConfirmViewCreationDialog");
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            DialogResult = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            DialogResult = false;
            Close();
        }

        private static string BuildCategoryQuestion(string categoryName)
        {
            string safeCategory = string.IsNullOrWhiteSpace(categoryName) ? "Без категории" : categoryName.Trim();
            return "Создать план с категорией: " + safeCategory + "?";
        }
    }
}
