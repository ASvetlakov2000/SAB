using System.Windows.Forms;

namespace SAB.Cls_RevitLibraryBuilder.UI.Dialogs
{
    public static class ViewCreationDialogService
    {
        public static bool Ask(string categoryName)
        {
            // ------------------------------------------------------------
            // Защищённый вызов диалога (никогда не падает)
            // ------------------------------------------------------------

            try
            {
                var result = MessageBox.Show(
                    $"Создать вид для: {categoryName}?",
                    "Создание вида",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                return result == DialogResult.Yes;
            }
            catch
            {
                // Если UI сломался — просто НЕ создаём вид
                return false;
            }
        }
    }
}