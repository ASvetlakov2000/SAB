using System;
using System.Collections.Generic;

namespace SAB.BimDashboard.Models
{
    /// <summary>
    /// Единая модель dashboard, которая передается в HTML/JS слой.
    /// </summary>
    public class DashboardData
    {
        public DashboardData()
        {
            Columns = new List<string>();
            Rows = new List<List<string>>();
            Summary = new SummaryData();
        }

        public string ProjectName { get; set; }

        public DateTime GeneratedAt { get; set; }

        // Блок списка колонок для табличного просмотра.
        public List<string> Columns { get; set; }

        // Блок строк структурированного списка. Каждая строка хранит значения в порядке Columns.
        public List<List<string>> Rows { get; set; }

        public SummaryData Summary { get; set; }
    }
}
