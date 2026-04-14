using System.Collections.Generic;
using System.Globalization;

namespace SAB.BimDashboard.Services.Tables
{
    /// <summary>
    /// Нормализация и уникализация заголовков таблицы.
    /// </summary>
    internal static class TabularHeaderNormalizer
    {
        public static List<string> Normalize(List<string> rawHeaders)
        {
            List<string> normalized = new List<string>();
            HashSet<string> used = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < rawHeaders.Count; i++)
            {
                string header = (rawHeaders[i] ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(header))
                {
                    header = "Column" + (i + 1).ToString(CultureInfo.InvariantCulture);
                }

                string uniqueHeader = header;
                int suffix = 2;

                while (used.Contains(uniqueHeader))
                {
                    uniqueHeader = header + "_" + suffix.ToString(CultureInfo.InvariantCulture);
                    suffix++;
                }

                used.Add(uniqueHeader);
                normalized.Add(uniqueHeader);
            }

            return normalized;
        }
    }
}
