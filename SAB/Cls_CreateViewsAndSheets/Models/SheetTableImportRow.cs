namespace SAB.CreateViewsAndSheets.Models
{
    public class SheetTableImportRow
    {
        public SheetTableImportRow()
        {
            SheetNumber = string.Empty;
            SheetName = string.Empty;
            FloorName = string.Empty;
            SectionName = string.Empty;
        }

        public string SheetNumber { get; set; }

        public string SheetName { get; set; }

        public string FloorName { get; set; }

        public string SectionName { get; set; }
    }
}
