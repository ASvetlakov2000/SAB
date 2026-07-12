using System.Collections.Generic;

namespace SAB.BimDashboard.Models
{
    /// <summary>
    /// Result of preparing and opening the HTML viewer.
    /// </summary>
    public class DashboardLaunchResult
    {
        public DashboardLaunchResult()
        {
            Warnings = new List<string>();
        }

        public string HtmlPath { get; set; }

        public int LoadedCsvFilesCount { get; set; }

        public int LoadedRecordsCount { get; set; }

        public List<string> Warnings { get; private set; }
    }
}
