using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;

namespace SAB.Cls_RevitLibraryBuilder.UI.Dialogs
{
    public static class ViewCreationDialogService
    {
        public static bool Ask(string categoryName)
        {
            ConfirmViewCreationDialog dialog = new ConfirmViewCreationDialog(categoryName)
            {
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            IntPtr ownerHandle = IntPtr.Zero;

            try
            {
                ownerHandle = Process.GetCurrentProcess().MainWindowHandle;
            }
            catch
            {
                ownerHandle = IntPtr.Zero;
            }

            if (ownerHandle != IntPtr.Zero)
            {
                WindowInteropHelper helper = new WindowInteropHelper(dialog);
                helper.Owner = ownerHandle;
            }

            bool? dialogResult = dialog.ShowDialog();
            return dialogResult == true && dialog.Result;
        }
    }
}
