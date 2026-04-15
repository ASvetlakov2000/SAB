using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SAB.Cls_RevitLibraryBuilder.UI.Dialogs
{
    public partial class ConfirmViewCreationDialog : Window
    {
        public bool Result { get; private set; }

        public ConfirmViewCreationDialog(string categoryName)
            : this("Создать план с категорией?", "Да", "Нет")
        {
        }

        public ConfirmViewCreationDialog(string titleText, string yesButtonText, string noButtonText)
        {
            BuildUi(titleText, yesButtonText, noButtonText);
        }

        private void BuildUi(string titleText, string yesButtonText, string noButtonText)
        {
            Title = "Confirm";
            Width = 320;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;

            Border border = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(0x67, 0x6C, 0x73)),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(3),
                Padding = new Thickness(20)
            };

            StackPanel rootPanel = new StackPanel
            {
                Width = 300,
                Height = 90,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock titleBlock = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(titleText) ? "Создать план с категорией?" : titleText,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            StackPanel buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            };

            Button noButton = CreateDialogButton(string.IsNullOrWhiteSpace(noButtonText) ? "Нет" : noButtonText);
            noButton.Margin = new Thickness(0, 0, 0, 10);
            noButton.Click += No_Click;

            Button yesButton = CreateDialogButton(string.IsNullOrWhiteSpace(yesButtonText) ? "Да" : yesButtonText);
            yesButton.Margin = new Thickness(30, 0, 0, 10);
            yesButton.Click += Yes_Click;

            buttonsPanel.Children.Add(noButton);
            buttonsPanel.Children.Add(yesButton);

            rootPanel.Children.Add(titleBlock);
            rootPanel.Children.Add(buttonsPanel);

            border.Child = rootPanel;
            Content = border;
        }

        private static Button CreateDialogButton(string caption)
        {
            return new Button
            {
                Content = caption,
                Width = 80,
                Height = 30,
                FontSize = 18,
                Background = new SolidColorBrush(Color.FromRgb(0x67, 0x6C, 0x73)),
                Foreground = Brushes.White,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(2),
                Padding = new Thickness(6, 2, 6, 2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            DialogResult = true;
            Close();
        }

        private void No_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            DialogResult = false;
            Close();
        }
    }
}
