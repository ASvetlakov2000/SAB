using System.Collections.Generic;

namespace SAB.BimDashboard.Models
{
    /// <summary>
    /// Результат чтения данных конкретным провайдером.
    /// </summary>
    public class ProviderResult
    {
        public ProviderResult()
        {
            Records = new List<UnifiedRecord>();
            Warnings = new List<string>();
        }

        public string ProjectName { get; set; }

        public string SourceProfile { get; set; }

        public List<UnifiedRecord> Records { get; set; }

        public List<string> Warnings { get; set; }
    }
}
