namespace SAB.CreateViewsAndSheets.Models
{
    public class CreateViewsAndSheetsProgressInfo
    {
        public int CurrentStep { get; set; }

        public int TotalSteps { get; set; }

        public int ProcessedItems { get; set; }

        public int TotalItems { get; set; }

        public string Stage { get; set; }

        public string Details { get; set; }

        public int CreatedItems { get; set; }

        public int UpdatedItems { get; set; }

        public int SkippedItems { get; set; }

        public int FailedItems { get; set; }

        public string Counters { get; set; }
    }
}
