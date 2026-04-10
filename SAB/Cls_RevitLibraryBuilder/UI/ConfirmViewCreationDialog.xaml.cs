using System.Windows;

namespace UI.Dialogs
{
    public partial class ConfirmViewCreationDialog : Window
    {
        public bool Result { get; private set; }

        public ConfirmViewCreationDialog(string categoryName)
        {
            InitializeComponent();

            TitleText.Text = "Создание вида";
            MessageText.Text = $"Создать план вида для:\n\n{categoryName}?";
        }

        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            Close();
        }

        private void No_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            Close();
        }
    }
}