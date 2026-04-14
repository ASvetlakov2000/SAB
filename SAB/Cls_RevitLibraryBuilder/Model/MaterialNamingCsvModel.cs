namespace RevitLibraryBuilder.Models
{
    /// <summary>
    /// Строка CSV для переименования/удаления материалов.
    /// </summary>
    public class MaterialNamingCsvModel
    {
        public int RowIndex { get; set; }

        public string MaterialNameOld { get; set; }

        public string MaterialNameNew { get; set; }

        public string DescriptionOld { get; set; }

        public string DescriptionNew { get; set; }

        public string Manufacturer { get; set; }

        public string Model { get; set; }

        public string Keynote { get; set; }

        public string Marking { get; set; }

        public bool DeleteMaterial { get; set; }
    }
}
