using RevitLibraryBuilder.Models;
using System;
using System.Collections.Generic;

namespace RevitLibraryBuilder.Services.Csv
{
    /// <summary>
    /// Импорт строк размещения из CSV по заголовкам (с поддержкой алиасов).
    /// </summary>
    public class CsvImportService
    {
        private readonly CsvTableService _csvTableService = new CsvTableService();

        public List<ElementTypeCsvModel> ImportFromCsv(string path)
        {
            CsvTable table = _csvTableService.Read(path);

            // Блок сопоставления колонок по алиасам для обратной совместимости старых/новых CSV.
            int categoryIndex = FindColumnIndex(table, "Категория", "Category");
            int familyIndex = FindColumnIndex(table, "Семейство", "Family");
            int typeNameIndex = FindColumnIndex(table, "Типоразмер", "TypeName", "Тип");
            int includeIndex = FindColumnIndex(table, "Включить", "Include");

            if (categoryIndex < 0 || familyIndex < 0 || typeNameIndex < 0 || includeIndex < 0)
            {
                throw new InvalidOperationException(
                    "CSV не содержит обязательные колонки для размещения: Категория, Семейство, Типоразмер, Включить.");
            }

            List<ElementTypeCsvModel> result = new List<ElementTypeCsvModel>();

            for (int i = 0; i < table.Rows.Count; i++)
            {
                CsvTableRow row = table.Rows[i];

                if (row == null)
                {
                    continue;
                }

                string category = (row.GetValue(categoryIndex) ?? string.Empty).Trim();
                string family = (row.GetValue(familyIndex) ?? string.Empty).Trim();
                string typeName = (row.GetValue(typeNameIndex) ?? string.Empty).Trim();
                string includeText = (row.GetValue(includeIndex) ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(category) &&
                    string.IsNullOrWhiteSpace(family) &&
                    string.IsNullOrWhiteSpace(typeName))
                {
                    continue;
                }

                result.Add(new ElementTypeCsvModel
                {
                    Category = category,
                    Family = family,
                    TypeName = typeName,
                    Include = ParseInclude(includeText)
                });
            }

            return result;
        }

        private static int FindColumnIndex(CsvTable table, params string[] aliases)
        {
            if (table == null || aliases == null)
            {
                return -1;
            }

            for (int i = 0; i < aliases.Length; i++)
            {
                string alias = aliases[i];

                if (string.IsNullOrWhiteSpace(alias))
                {
                    continue;
                }

                int index = table.FindHeaderIndex(alias);

                if (index >= 0)
                {
                    return index;
                }
            }

            return -1;
        }

        private static bool ParseInclude(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim().ToUpperInvariant();

            return normalized == "TRUE" ||
                   normalized == "1" ||
                   normalized == "ДА" ||
                   normalized == "YES";
        }
    }
}
