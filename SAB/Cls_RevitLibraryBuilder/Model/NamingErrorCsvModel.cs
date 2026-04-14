namespace RevitLibraryBuilder.Models
{
    /// <summary>
    /// Строка отчета по ошибкам при пакетном переименовании.
    /// </summary>
    public class NamingErrorCsvModel
    {
        public string OldName { get; set; }

        public string NewName { get; set; }

        public string ErrorText { get; set; }
    }
}
