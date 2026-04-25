using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Helpers.Notifications.ToastNotifications;
using SAB.InteriorElevations.Models;

namespace SAB.InteriorElevations.Views
{
    public class PlanCornerMarkAlignmentWindow : Window
    {
        private TextBox _cornerOffsetTextBox;
        private readonly PlanCornerMarkAlignmentSettings _initialSettings;

        public PlanCornerMarkAlignmentWindow(PlanCornerMarkAlignmentSettings initialSettings)
        {
            _initialSettings = initialSettings;

            Title = "Выравнивание марок углов на плане";
            Width = 480;
            Height = 230;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            BuildWindowContent();
        }

        public PlanCornerMarkAlignmentSettings SelectedSettings { get; private set; }

        private void BuildWindowContent()
        {
            Grid rootGrid = new Grid();
            rootGrid.Margin = new Thickness(16);
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock descriptionTextBlock = new TextBlock();
            descriptionTextBlock.Text = "Выберите параметры выравнивания марок SA_Марка угла_План.";
            descriptionTextBlock.TextWrapping = TextWrapping.Wrap;
            descriptionTextBlock.Margin = new Thickness(0, 0, 0, 12);
            Grid.SetRow(descriptionTextBlock, 0);
            rootGrid.Children.Add(descriptionTextBlock);

            StackPanel offsetPanel = new StackPanel();
            offsetPanel.Orientation = Orientation.Vertical;
            offsetPanel.Margin = new Thickness(0, 0, 0, 10);
            Grid.SetRow(offsetPanel, 1);

            TextBlock offsetLabel = new TextBlock();
            offsetLabel.Text = "Отступ марки от угла (мм):";
            offsetLabel.Margin = new Thickness(0, 0, 0, 4);
            offsetPanel.Children.Add(offsetLabel);

            _cornerOffsetTextBox = new TextBox();
            _cornerOffsetTextBox.MinWidth = 160;
            _cornerOffsetTextBox.Text = GetInitialCornerOffsetText();
            offsetPanel.Children.Add(_cornerOffsetTextBox);

            rootGrid.Children.Add(offsetPanel);

            StackPanel buttonsPanel = new StackPanel();
            buttonsPanel.Orientation = Orientation.Horizontal;
            buttonsPanel.HorizontalAlignment = HorizontalAlignment.Right;
            buttonsPanel.Margin = new Thickness(0, 8, 0, 0);
            Grid.SetRow(buttonsPanel, 3);

            Button okButton = new Button();
            okButton.Content = "ОК";
            okButton.Width = 100;
            okButton.Margin = new Thickness(0, 0, 8, 0);
            okButton.Click += OkButton_Click;
            buttonsPanel.Children.Add(okButton);

            Button cancelButton = new Button();
            cancelButton.Content = "Отмена";
            cancelButton.Width = 100;
            cancelButton.Click += CancelButton_Click;
            buttonsPanel.Children.Add(cancelButton);

            rootGrid.Children.Add(buttonsPanel);
            Content = rootGrid;
        }

        private string GetInitialCornerOffsetText()
        {
            if (_initialSettings == null)
            {
                return "80";
            }

            return _initialSettings.CornerOffsetMm.ToString("F1", CultureInfo.InvariantCulture);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            double cornerOffsetMm;
            if (!TryParseMillimeters(_cornerOffsetTextBox.Text, out cornerOffsetMm))
            {
                ToastNotifier.ShowWarning("SAB Развертки", "Введите корректное значение для отступа марки от угла.");
                return;
            }

            if (cornerOffsetMm < 0)
            {
                ToastNotifier.ShowWarning("SAB Развертки", "Отступ марки от угла не может быть отрицательным.");
                return;
            }

            SelectedSettings = new PlanCornerMarkAlignmentSettings();
            SelectedSettings.CornerOffsetMm = cornerOffsetMm;
            SelectedSettings.LeaderBreakAngle = PlanLeaderBreakAngleType.Degrees90;

            DialogResult = true;
            Close();
        }

        private bool TryParseMillimeters(string text, out double value)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
