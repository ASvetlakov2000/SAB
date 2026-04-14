using SAB.BimDashboard.Models;

namespace SAB.BimDashboard.Services.Data
{
    /// <summary>
    /// Контракт источника данных для dashboard.
    /// </summary>
    public interface IDataProvider
    {
        bool CanHandle(DataSourceType sourceType);

        ProviderResult Load(DataProviderContext context);
    }
}
