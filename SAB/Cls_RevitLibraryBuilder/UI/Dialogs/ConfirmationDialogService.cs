using System.Diagnostics;
using System.Windows.Interop;

namespace SAB.Cls_RevitLibraryBuilder.UI.Dialogs
{
    public static class ConfirmationDialogService
    {
        public static bool Ask(string title, string message)
        {
            ConfirmViewCreationDialog dialog = new ConfirmViewCreationDialog(
                title,
                message,
                "Да",
                "Нет");

            WindowInteropHelper helper = new WindowInteropHelper(dialog);
            helper.Owner = Process.GetCurrentProcess().MainWindowHandle;

            return dialog.ShowDialog() == true && dialog.Result;
        }
    }
}
