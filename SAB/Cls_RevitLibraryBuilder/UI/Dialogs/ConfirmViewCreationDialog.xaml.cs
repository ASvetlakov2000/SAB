using System.Windows;

namespace SAB.Cls_RevitLibraryBuilder.UI.Dialogs
{
    public partial class ConfirmViewCreationDialog : Window
    {
        public bool Result { get; private set; }

        public ConfirmViewCreationDialog(string categoryName)
            : this("Создание вида", $"Создать план вида для категории:\n\n{categoryName}?", "Да", "Нет")
        {
        }

        public ConfirmViewCreationDialog(string titleText, string messageText, string yesButtonText, string noButtonText)
        {
            InitializeComponent();

            TitleText.Text = titleText;
            MessageText.Text = messageText;
            YesButton.Content = yesButtonText;
            NoButton.Content = noButtonText;
        }

        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            // ------------------------------------------------------------
            // фиксируем результат
            // ------------------------------------------------------------
            Result = true;

            // ------------------------------------------------------------
            // закрываем окно с true
            // ------------------------------------------------------------
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
