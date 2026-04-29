using System;
using System.Collections.Generic;
using SAB.BimDashboard.Models;

namespace SAB.BimDashboard.Services.Processing
{
    /// <summary>
    /// Преобразование универсальных записей в итоговую модель DashboardData.
    /// </summary>
    public class DataMapper
    {
        public DashboardData Map(ProviderResult providerResult)
        {
            if (providerResult == null)
            {
                throw new ArgumentNullException(nameof(providerResult));
            }

            // Блок отвечает за формирование HTML модели.
            DashboardData dashboardData = new DashboardData();
            dashboardData.CatalogName = "RevitLibraryBuilder";
            dashboardData.ProjectName = string.IsNullOrWhiteSpace(providerResult.ProjectName) ? "Без названия" : providerResult.ProjectName;
            dashboardData.SourceName = dashboardData.ProjectName;
            dashboardData.SourceFormat = ResolveSourceFormat(providerResult.Records);
            dashboardData.SourceProfile = providerResult.SourceProfile ?? string.Empty;
            dashboardData.GeneratedAt = DateTime.Now;

            BuildSummary(providerResult.Records, dashboardData.Summary);
            BuildStructuredList(providerResult.Records, dashboardData.Columns, dashboardData.Rows);

            return dashboardData;
        }

        // Блок построения верхних summary-метрик.
        private static void BuildSummary(List<UnifiedRecord> records, SummaryData summary)
        {
            if (summary == null)
            {
                return;
            }

            if (records == null)
            {
                summary.TotalElements = 0;
                return;
            }

            double totalElements = 0;

            for (int i = 0; i < records.Count; i++)
            {
                UnifiedRecord record = records[i];

                if (record == null)
                {
                    continue;
                }

                if (record.Count.HasValue)
                {
                    totalElements += record.Count.Value;
                }
            }

            summary.TotalElements = Convert.ToInt32(Math.Round(totalElements <= 0 ? records.Count : totalElements, MidpointRounding.AwayFromZero));
        }

        // Блок формирования структуры таблицы (колонки + строки).
        private static void BuildStructuredList(List<UnifiedRecord> records, List<string> columns, List<List<string>> rows)
        {
            if (columns == null || rows == null)
            {
                return;
            }

            columns.Clear();
            rows.Clear();

            if (records == null || records.Count == 0)
            {
                return;
            }

            string[] preferredColumns =
            {
                "RowNumber",
                "Наименование",
                "Категория",
                "Семейство",
                "Типоразмер",
                "Миниатюра",
                "ThumbnailPath"
            };

            HashSet<string> seenColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < preferredColumns.Length; i++)
            {
                string preferred = preferredColumns[i];

                if (HasAnyField(records, preferred))
                {
                    columns.Add(preferred);
                    seenColumns.Add(preferred);
                }
            }

            for (int i = 0; i < records.Count; i++)
            {
                UnifiedRecord record = records[i];

                if (record == null || record.Fields == null)
                {
                    continue;
                }

                foreach (KeyValuePair<string, string> pair in record.Fields)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                    {
                        continue;
                    }

                    if (seenColumns.Contains(pair.Key))
                    {
                        continue;
                    }

                    columns.Add(pair.Key);
                    seenColumns.Add(pair.Key);
                }
            }

            if (columns.Count == 0)
            {
                columns.Add("Наименование");
                columns.Add("Категория");
            }

            for (int i = 0; i < records.Count; i++)
            {
                UnifiedRecord record = records[i];
                List<string> rowValues = new List<string>();

                for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                {
                    string column = columns[columnIndex];
                    string value = GetRecordValue(record, column);
                    rowValues.Add(value);
                }

                rows.Add(rowValues);
            }
        }

        private static bool HasAnyField(List<UnifiedRecord> records, string fieldName)
        {
            if (records == null || string.IsNullOrWhiteSpace(fieldName))
            {
                return false;
            }

            for (int i = 0; i < records.Count; i++)
            {
                UnifiedRecord record = records[i];

                if (record == null || record.Fields == null)
                {
                    continue;
                }

                string value;

                if (record.Fields.TryGetValue(fieldName, out value) && !string.IsNullOrWhiteSpace(value))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetRecordValue(UnifiedRecord record, string column)
        {
            if (record == null || string.IsNullOrWhiteSpace(column))
            {
                return string.Empty;
            }

            if (record.Fields != null)
            {
                string value;

                if (record.Fields.TryGetValue(column, out value))
                {
                    return value ?? string.Empty;
                }
            }

            switch (column)
            {
                case "Категория":
                case "Category":
                    return record.Category ?? string.Empty;
                case "Наименование":
                case "Name":
                    return record.Name ?? string.Empty;
                case "Count":
                    return record.Count.HasValue ? record.Count.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
                case "Area":
                    return record.Area.HasValue ? record.Area.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
                case "Length":
                    return record.Length.HasValue ? record.Length.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
                case "Value":
                    return record.Value.HasValue ? record.Value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
                default:
                    return string.Empty;
            }
        }

        private static string ResolveSourceFormat(List<UnifiedRecord> records)
        {
            if (records == null)
            {
                return "Не определен";
            }

            for (int i = 0; i < records.Count; i++)
            {
                UnifiedRecord record = records[i];

                if (record == null || record.Fields == null)
                {
                    continue;
                }

                string sourceType;
                if (record.Fields.TryGetValue("SourceType", out sourceType) && !string.IsNullOrWhiteSpace(sourceType))
                {
                    return sourceType.Trim();
                }
            }

            return "Не определен";
        }
    }
}
