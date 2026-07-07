using System.Collections.Generic;

namespace SAB.CreateViewsAndSheets.Models
{
    public class SheetDeletionResult
    {
        public SheetDeletionResult()
        {
            Warnings = new List<string>();
        }

        public int DeletedSheetsCount { get; set; }

        public int DeletedViewsCount { get; set; }

        public List<string> Warnings { get; private set; }
    }
}
