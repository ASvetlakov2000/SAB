namespace RevitLibraryBuilder.Models
{
    /// <summary>
    /// Модель элемента для импорта/экспорта CSV
    /// </summary>
    public class ElementTypeCsvModel
    {
        public string Category { get; set; }
        public string Family { get; set; }
        public string TypeName { get; set; }
        public bool Include { get; set; }
    }
}