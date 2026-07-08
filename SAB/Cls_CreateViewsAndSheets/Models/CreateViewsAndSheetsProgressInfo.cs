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
    }
}
