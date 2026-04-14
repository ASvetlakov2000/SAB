using System;
using System.Diagnostics;
using System.IO;

namespace SAB.BimDashboard.Services.Viewer
{
    /// <summary>
    /// MVP viewer: открытие dashboard во внешнем браузере по умолчанию.
    /// </summary>
    public class ExternalBrowserDashboardViewer : IDashboardViewer
    {
        public void Open(string htmlFilePath)
        {
            if (string.IsNullOrWhiteSpace(htmlFilePath))
            {
                throw new ArgumentException("Путь к HTML dashboard не задан.");
            }

            if (!File.Exists(htmlFilePath))
            {
                throw new FileNotFoundException("Файл HTML dashboard не найден.", htmlFilePath);
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = htmlFilePath,
                UseShellExecute = true
            };

            Process.Start(startInfo);
        }
    }
}
