using System.Collections.Generic;

namespace SAB.BimDashboard.Models
{
    /// <summary>
    /// Универсальная табличная модель для CSV/XLSX до преобразования в UnifiedRecord.
    /// </summary>
    public class TabularDataSet
    {
        public TabularDataSet()
        {
            Headers = new List<string>();
            Rows = new List<TabularRow>();
        }

        public List<string> Headers { get; set; }

        public List<TabularRow> Rows { get; set; }
    }
}
