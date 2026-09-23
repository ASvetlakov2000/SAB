using System;
using System.Collections.Generic;
using System.Text;

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
            ParameterValuesByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public string SheetNumber { get; set; }

        public string SheetName { get; set; }

        public string FloorName { get; set; }

        public string SectionName { get; set; }

        public Dictionary<string, string> ParameterValuesByName { get; private set; }

        public void SetParameterValue(string parameterName, string value)
        {
            string cleanParameterName = (parameterName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(cleanParameterName))
            {
                return;
            }

            ParameterValuesByName[cleanParameterName] = (value ?? string.Empty).Trim();
        }

        public bool TryGetParameterValue(string parameterName, out string value)
        {
            value = string.Empty;
            string cleanParameterName = (parameterName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(cleanParameterName) || ParameterValuesByName == null)
            {
                return false;
            }

            if (ParameterValuesByName.TryGetValue(cleanParameterName, out value))
            {
                value = (value ?? string.Empty).Trim();
                return true;
            }

            string normalizedParameterName = NormalizeParameterName(cleanParameterName);
            foreach (KeyValuePair<string, string> pair in ParameterValuesByName)
            {
                if (!string.Equals(
                        NormalizeParameterName(pair.Key),
                        normalizedParameterName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                value = (pair.Value ?? string.Empty).Trim();
                return true;
            }

            return false;
        }

        private static string NormalizeParameterName(string parameterName)
        {
            string source = (parameterName ?? string.Empty).Trim().ToLowerInvariant();
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < source.Length; i++)
            {
                char character = source[i];
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }
    }
}
