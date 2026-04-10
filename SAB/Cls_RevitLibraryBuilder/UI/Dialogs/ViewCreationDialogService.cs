using System;
using System.Windows;
using System.Windows.Interop;

namespace SAB.Cls_RevitLibraryBuilder.UI.Dialogs
{
    public static class ViewCreationDialogService
    {
        public static bool Ask(string name)
        {
            var dialog = new ConfirmViewCreationDialog(name);

            var helper = new WindowInteropHelper(dialog);

            helper.Owner = System.Diagnostics.Process
                .GetCurrentProcess()
                .MainWindowHandle;

            return dialog.ShowDialog() == true && dialog.Result;
        }
    }
}