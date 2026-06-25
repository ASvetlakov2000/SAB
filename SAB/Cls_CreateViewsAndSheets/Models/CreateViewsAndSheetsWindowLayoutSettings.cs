using System.Collections.Generic;

namespace SAB.CreateViewsAndSheets.Models
{
    public class CreateViewsAndSheetsWindowLayoutSettings
    {
        public CreateViewsAndSheetsWindowLayoutSettings()
        {
            ColumnWidths = new Dictionary<string, double>();
        }

        public double WindowWidth { get; set; }

        public double WindowHeight { get; set; }

        public Dictionary<string, double> ColumnWidths { get; set; }
    }
}
