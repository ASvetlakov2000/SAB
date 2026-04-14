namespace RevitLibraryBuilder.Models
{
    /// <summary>
    /// Строка CSV для переименования типоразмеров.
    /// </summary>
    public class TypeNamingCsvModel
    {
        public int RowIndex { get; set; }

        public string Category { get; set; }

        public string FamilyOld { get; set; }

        public string FamilyNew { get; set; }

        public string TypeNameOld { get; set; }

        public string TypeNameNew { get; set; }
    }
}
