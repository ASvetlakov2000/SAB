using System.Collections.Generic;

namespace SAB.BimDashboard.Models
{
    /// <summary>
    /// Универсальная запись, в которую приводятся данные из всех источников.
    /// </summary>
    public class UnifiedRecord
    {
        public UnifiedRecord()
        {
            Fields = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        }

        public string Category { get; set; }

        public string Name { get; set; }

        public double? Value { get; set; }

        public double? Area { get; set; }

        public double? Length { get; set; }

        public double? Count { get; set; }

        // Блок универсальных полей: хранит исходные столбцы из CSV/XLSX/Revit.
        public Dictionary<string, string> Fields { get; set; }
    }
}
