using RevitLibraryBuilder.Services;
using SAB.BimDashboard.Models;
using System;
using System.Collections.Generic;

namespace SAB.BimDashboard.Services.Data
{
    /// <summary>
    /// Fills ThumbnailPath from runtime thumbnail folders when source row has no image path.
    /// </summary>
    internal static class ThumbnailRuntimePathEnricher
    {
        public static void Enrich(List<UnifiedRecord> records)
        {
            if (records == null || records.Count == 0)
            {
                return;
            }

            for (int i = 0; i < records.Count; i++)
            {
                UnifiedRecord record = records[i];

                if (record == null || record.Fields == null)
                {
                    continue;
                }

                if (HasAnyThumbnailValue(record.Fields))
                {
                    continue;
                }

                string familyName = FindFieldValue(record.Fields, "Family", "Семейство");
                string typeName = FindFieldValue(record.Fields, "TypeName", "Типоразмер", "Тип");

                if (string.IsNullOrWhiteSpace(typeName))
                {
                    continue;
                }

                string resolvedPath = ThumbnailPathResolverService.ResolveByFamilyAndType(
                    familyName,
                    typeName,
                    null);

                if (string.IsNullOrWhiteSpace(resolvedPath))
                {
                    continue;
                }

                SetThumbnailPath(record.Fields, resolvedPath);
            }
        }

        private static bool HasAnyThumbnailValue(Dictionary<string, string> fields)
        {
            return HasFieldValue(fields, "ThumbnailPath") ||
                   HasFieldValue(fields, "Thumbnail") ||
                   HasFieldValue(fields, "IconPath");
        }

        private static bool HasFieldValue(Dictionary<string, string> fields, string fieldName)
        {
            string value = FindFieldValue(fields, fieldName);
            return !string.IsNullOrWhiteSpace(value);
        }

        private static string FindFieldValue(Dictionary<string, string> fields, params string[] fieldNames)
        {
            if (fields == null || fieldNames == null || fieldNames.Length == 0)
            {
                return string.Empty;
            }

            for (int nameIndex = 0; nameIndex < fieldNames.Length; nameIndex++)
            {
                string fieldName = fieldNames[nameIndex];

                if (string.IsNullOrWhiteSpace(fieldName))
                {
                    continue;
                }

                string exactValue;
                if (fields.TryGetValue(fieldName, out exactValue))
                {
                    return exactValue ?? string.Empty;
                }

                foreach (KeyValuePair<string, string> pair in fields)
                {
                    if (string.Equals(pair.Key, fieldName, StringComparison.OrdinalIgnoreCase))
                    {
                        return pair.Value ?? string.Empty;
                    }
                }
            }

            return string.Empty;
        }

        private static void SetThumbnailPath(Dictionary<string, string> fields, string resolvedPath)
        {
            if (fields == null)
            {
                return;
            }

            string existingKey = null;

            foreach (KeyValuePair<string, string> pair in fields)
            {
                if (string.Equals(pair.Key, "ThumbnailPath", StringComparison.OrdinalIgnoreCase))
                {
                    existingKey = pair.Key;
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(existingKey))
            {
                fields[existingKey] = resolvedPath;
                return;
            }

            fields["ThumbnailPath"] = resolvedPath;
        }
    }
}
