using System;

namespace SAB.BimDashboard.Models
{
    /// <summary>
    /// Result of locating and validating one profile-specific PNG folder.
    /// </summary>
    public class ImageFolderMatchResult
    {
        public ImageFolderMatchResult()
        {
            CsvFilePath = string.Empty;
            FolderPath = string.Empty;
            Message = string.Empty;
            Status = ImageFolderFreshnessStatus.NotFound;
        }

        public DashboardProfileType Profile { get; set; }

        public string CsvFilePath { get; set; }

        public string FolderPath { get; set; }

        public DateTime? CsvModifiedAt { get; set; }

        public DateTime? NewestImageModifiedAt { get; set; }

        public int PngFileCount { get; set; }

        public double DifferenceHours { get; set; }

        public bool IsAutoDetected { get; set; }

        public ImageFolderFreshnessStatus Status { get; set; }

        public string Message { get; set; }
    }
}
