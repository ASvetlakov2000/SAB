using System.Collections.Generic;

namespace SAB.CreateViewsAndSheets.Models
{
    public class CreateViewsAndSheetsResult
    {
        public CreateViewsAndSheetsResult()
        {
            CreatedItems = new List<CreatedViewSheetInfo>();
            Warnings = new List<string>();
        }

        public List<CreatedViewSheetInfo> CreatedItems { get; private set; }

        public List<string> Warnings { get; private set; }

        public int CreatedViewsCount
        {
            get { return CreatedItems.Count; }
        }

        public int CreatedSheetsCount
        {
            get { return CreatedItems.Count; }
        }
    }
}
