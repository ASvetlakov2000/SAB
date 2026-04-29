using RevitLibraryBuilder.Services;
using SAB.BimDashboard.Models;
using System;
using System.Collections.Generic;

namespace SAB.BimDashboard.Services.Data
{
    /// <summary>
    /// Fills thumbnail path from runtime folders when source row has no image path.
    /// </summary>
    internal static class ThumbnailRuntimePathEnricher
    {
        public static void Enrich(List<UnifiedRecord> records, DashboardProfileType profile)
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

                string resolvedPath = ResolveByProfile(record.Fields, profile);

                if (string.IsNullOrWhiteSpace(resolvedPath))
                {
                    continue;
                }

                SetThumbnailPath(record.Fields, resolvedPath);
            }
        }

        private static string ResolveByProfile(Dictionary<string, string> fields, DashboardProfileType profile)
        {
            if (fields == null)
            {
                return string.Empty;
            }

            if (profile == DashboardProfileType.Lines)
            {
                string lineName = FindFieldValue(fields, "Наименование", "Name");
                return ThumbnailPathResolverService.ResolveForLineStyle(lineName);
            }

            if (profile == DashboardProfileType.FillPatterns)
            {
                string fillName = FindFieldValue(fields, "Наименование", "Name");
                return ThumbnailPathResolverService.ResolveForFillPattern(fillName);
            }

            string familyName = FindFieldValue(fields, "Семейство", "Family");
            string typeName = FindFieldValue(fields, "Типоразмер", "TypeName", "Тип");

            if (string.IsNullOrWhiteSpace(typeName))
            {
                return string.Empty;
            }

            bool? preferLoadable = profile == DashboardProfileType.LoadableFamilies
                ? (bool?)true
                : profile == DashboardProfileType.SystemFamilies
                    ? (bool?)false
                    : null;

            return ThumbnailPathResolverService.ResolveByFamilyAndType(
                familyName,
                typeName,
                preferLoadable);
        }

        private static bool HasAnyThumbnailValue(Dictionary<string, string> fields)
        {
            return HasFieldValue(fields, "ThumbnailPath") ||
                   HasFieldValue(fields, "Thumbnail") ||
                   HasFieldValue(fields, "IconPath") ||
                   HasFieldValue(fields, "Миниатюра");
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
                if (string.Equals(pair.Key, "Миниатюра", StringComparison.OrdinalIgnoreCase))
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

            fields["Миниатюра"] = resolvedPath;
        }
    }
}
