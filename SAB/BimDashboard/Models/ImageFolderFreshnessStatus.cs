namespace SAB.BimDashboard.Models
{
    /// <summary>
    /// Describes how closely a PNG folder matches the selected CSV by modification time.
    /// </summary>
    public enum ImageFolderFreshnessStatus
    {
        NoMatchingCsv,
        NotFound,
        Current,
        TimeDifferenceWarning,
        Outdated,
        ReadError
    }
}
