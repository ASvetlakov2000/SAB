using System;

namespace SAB.InteriorElevations.Utils
{
    public static class RevitNameUtils
    {
        private static readonly char[] InvalidNameCharacters =
        {
            '\\', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~'
        };

        public static string SanitizeName(string source, string fallbackValue)
        {
            string value = string.IsNullOrWhiteSpace(source) ? fallbackValue : source.Trim();

            for (int i = 0; i < InvalidNameCharacters.Length; i++)
            {
                value = value.Replace(InvalidNameCharacters[i].ToString(), string.Empty);
            }

            while (value.Contains("  "))
            {
                value = value.Replace("  ", " ");
            }

            value = value.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                value = fallbackValue;
            }

            return value;
        }

        public static string BuildJoinedName(string separator, params string[] parts)
        {
            string result = string.Empty;

            for (int i = 0; i < parts.Length; i++)
            {
                string current = parts[i] == null ? string.Empty : parts[i].Trim();
                if (string.IsNullOrWhiteSpace(current))
                {
                    continue;
                }

                if (result.Length > 0)
                {
                    result += separator;
                }

                result += current;
            }

            return result;
        }
    }
}
