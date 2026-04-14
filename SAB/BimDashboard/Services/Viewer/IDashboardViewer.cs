namespace SAB.BimDashboard.Services.Viewer
{
    /// <summary>
    /// Контракт для способа открытия готового HTML dashboard.
    /// </summary>
    public interface IDashboardViewer
    {
        void Open(string htmlFilePath);
    }
}
