namespace SAB.CreateViewsAndSheets.Models
{
    public static class WarningMessageSeverity
    {
        private const string CriticalWarningPrefix = "[SAB_CRITICAL_WARNING]";

        public static string MarkCritical(string warningText)
        {
            string cleanWarningText = warningText ?? string.Empty;
            if (IsCritical(cleanWarningText))
            {
                return cleanWarningText;
            }

            return CriticalWarningPrefix + cleanWarningText;
        }

        public static bool IsCritical(string warningText)
        {
            return !string.IsNullOrWhiteSpace(warningText) &&
                   warningText.StartsWith(CriticalWarningPrefix, System.StringComparison.Ordinal);
        }

        public static string Clean(string warningText)
        {
            string cleanWarningText = warningText ?? string.Empty;
            if (!IsCritical(cleanWarningText))
            {
                return cleanWarningText;
            }

            return cleanWarningText.Substring(CriticalWarningPrefix.Length);
        }
    }
}
