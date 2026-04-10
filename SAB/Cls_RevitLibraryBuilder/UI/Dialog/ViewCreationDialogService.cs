using System.Windows;

namespace UI.Dialogs
{
    public static class ViewCreationDialogService
    {
        public static bool Ask(string categoryName)
        {
            bool result = false;

            Application.Current.Dispatcher.Invoke(() =>
            {
                var dlg = new ConfirmViewCreationDialog(categoryName);
                dlg.ShowDialog();
                result = dlg.Result;
            });

            return result;
        }
    }
}