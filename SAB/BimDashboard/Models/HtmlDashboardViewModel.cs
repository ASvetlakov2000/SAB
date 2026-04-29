using System;
using System.Collections.Generic;

namespace SAB.BimDashboard.Models
{
    /// <summary>
    /// Модель, которая напрямую сериализуется и передается в HTML viewer.
    /// </summary>
    public class HtmlDashboardViewModel
    {
        public HtmlDashboardViewModel()
        {
            Columns = new List<string>();
            Rows = new List<List<string>>();
            Summary = new SummaryData();
        }

        public string CatalogName { get; set; }

        public string SourceName { get; set; }

        public string SourceFormat { get; set; }

        public string SourceProfile { get; set; }

        public DateTime GeneratedAt { get; set; }

        public SummaryData Summary { get; set; }

        public List<string> Columns { get; set; }

        public List<List<string>> Rows { get; set; }
    }
}
