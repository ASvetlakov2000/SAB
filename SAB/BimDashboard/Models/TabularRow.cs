using System.Collections.Generic;

namespace SAB.BimDashboard.Models
{
    /// <summary>
    /// Одна строка табличного источника с доступом к ячейкам по имени заголовка.
    /// </summary>
    public class TabularRow
    {
        public TabularRow()
        {
            Values = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        }

        public int RowNumber { get; set; }

        public Dictionary<string, string> Values { get; set; }
    }
}
