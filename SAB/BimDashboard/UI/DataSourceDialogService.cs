using System.Diagnostics;
using System.Windows.Interop;
using SAB.BimDashboard.Models;

namespace SAB.BimDashboard.UI
{
    /// <summary>
    /// Сервис показа WPF-диалога выбора источника данных.
    /// </summary>
    public static class DataSourceDialogService
    {
        public static bool ShowDialog(out DataSourceType sourceType, out DashboardProfileType profileType, out string filePath)
        {
            DataSourceDialog dialog = new DataSourceDialog();

            WindowInteropHelper helper = new WindowInteropHelper(dialog);
            helper.Owner = Process.GetCurrentProcess().MainWindowHandle;

            bool? dialogResult = dialog.ShowDialog();

            sourceType = dialog.SelectedSourceType;
            profileType = dialog.SelectedProfileType;
            filePath = dialog.SelectedFilePath;

            return dialogResult == true;
        }
    }
}
