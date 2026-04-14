using SAB.BimDashboard.Models;

namespace SAB.BimDashboard.Services.Tables
{
    /// <summary>
    /// Контракт чтения табличного файла в универсальный табличный набор.
    /// </summary>
    public interface ITableReader
    {
        bool CanRead(string filePath);

        TabularDataSet Read(string filePath);
    }
}
