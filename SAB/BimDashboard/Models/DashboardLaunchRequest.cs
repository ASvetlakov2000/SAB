using System.Collections.Generic;

namespace SAB.BimDashboard.Models
{
    /// <summary>
    /// Input collected by the unified Library Builder window for the HTML viewer.
    /// Every field is optional; the viewer can open with a partial or empty set.
    /// </summary>
    public class DashboardLaunchRequest
    {
        public DashboardLaunchRequest()
        {
            CsvFilePaths = new List<string>();
        }

        public List<string> CsvFilePaths { get; set; }

        public string SystemFamilyImagesFolder { get; set; }

        public string LoadableFamilyImagesFolder { get; set; }

        public string LineImagesFolder { get; set; }

        public string FillImagesFolder { get; set; }
    }
}
