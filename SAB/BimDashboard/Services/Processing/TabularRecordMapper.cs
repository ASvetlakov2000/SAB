using System;
using System.Collections.Generic;
using System.Globalization;
using SAB.BimDashboard.Models;

namespace SAB.BimDashboard.Services.Processing
{
    /// <summary>
    /// Общий маппер табличных источников (CSV/XLSX) в единую модель UnifiedRecord.
    /// </summary>
    public class TabularRecordMapper
    {
        public List<UnifiedRecord> Map(TabularDataSet dataSet, List<string> warnings)
        {
            if (dataSet == null)
            {
                throw new InvalidOperationException("Табличный набор данных не создан.");
            }

            if (dataSet.Headers == null || dataSet.Headers.Count == 0)
            {
                throw new InvalidOperationException("Табличный набор не содержит заголовков.");
            }

            if (dataSet.Rows == null)
            {
                throw new InvalidOperationException("Табличный набор не содержит строк.");
            }

            List<UnifiedRecord> result = new List<UnifiedRecord>();

            // Блок отвечает за формирование модели данных для HTML из табличных строк.
            for (int rowIndex = 0; rowIndex < dataSet.Rows.Count; rowIndex++)
            {
                TabularRow sourceRow = dataSet.Rows[rowIndex];

                if (sourceRow == null)
                {
                    continue;
                }

                if (IsRowEmpty(sourceRow, dataSet.Headers))
                {
                    continue;
                }

                UnifiedRecord record = new UnifiedRecord();

                // Блок копирования исходных колонок в универсальный словарь.
                for (int headerIndex = 0; headerIndex < dataSet.Headers.Count; headerIndex++)
                {
                    string header = dataSet.Headers[headerIndex];
                    string value = GetCellValue(sourceRow.Values, header);
                    record.Fields[header] = value;
                }

                // Блок системных полей, необходимых для сортировки/фильтрации в HTML.
                if (!record.Fields.ContainsKey("RowNumber"))
                {
                    record.Fields["RowNumber"] = sourceRow.RowNumber.ToString(CultureInfo.InvariantCulture);
                }

                if (!record.Fields.ContainsKey("RecordType"))
                {
                    record.Fields["RecordType"] = "TabularRecord";
                }

                // Блок извлечения стандартных полей для summary.
                record.Category = FindFieldValue(record.Fields, "Категория", "Category");
                record.Name = FindFieldValue(record.Fields, "Наименование", "Name", "Типоразмер", "TypeName", "Семейство", "Family", "MaterialName_Old", "Description_Old");
                record.Value = ParseNullableDouble(FindFieldValue(record.Fields, "Value", "Значение"));
                record.Area = ParseNullableDouble(FindFieldValue(record.Fields, "Area", "Площадь"));
                record.Length = ParseNullableDouble(FindFieldValue(record.Fields, "Length", "Длина"));
                record.Count = ParseNullableDouble(FindFieldValue(record.Fields, "Count", "Qty", "Quantity", "Количество"));

                if (string.IsNullOrWhiteSpace(record.Category) && !string.IsNullOrWhiteSpace(record.Name))
                {
                    record.Category = "Без категории";
                }

                if (string.IsNullOrWhiteSpace(record.Name) && !string.IsNullOrWhiteSpace(record.Category))
                {
                    record.Name = record.Category;
                }

                if (!record.Count.HasValue)
                {
                    // Для табличных источников без Count считаем одну строку как один элемент.
                    record.Count = 1;
                }

                if (IsRecordEmpty(record))
                {
                    warnings?.Add("Пропущена пустая строка: " + sourceRow.RowNumber.ToString(CultureInfo.InvariantCulture));
                    continue;
                }

                result.Add(record);
            }

            if (result.Count == 0)
            {
                warnings?.Add("После преобразования не осталось строк с данными.");
            }

            return result;
        }

        private static string GetCellValue(Dictionary<string, string> values, string header)
        {
            if (values == null || string.IsNullOrWhiteSpace(header))
            {
                return string.Empty;
            }

            string value;

            if (values.TryGetValue(header, out value))
            {
                return (value ?? string.Empty).Trim();
            }

            return string.Empty;
        }

        private static bool IsRowEmpty(TabularRow row, List<string> headers)
        {
            if (row == null || row.Values == null || headers == null || headers.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < headers.Count; i++)
            {
                string header = headers[i];
                string value = GetCellValue(row.Values, header);

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsRecordEmpty(UnifiedRecord record)
        {
            if (record == null)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(record.Category) || !string.IsNullOrWhiteSpace(record.Name))
            {
                return false;
            }

            if (record.Value.HasValue || record.Area.HasValue || record.Length.HasValue || record.Count.HasValue)
            {
                return false;
            }

            if (record.Fields == null)
            {
                return true;
            }

            foreach (KeyValuePair<string, string> pair in record.Fields)
            {
                if (!string.IsNullOrWhiteSpace(pair.Value))
                {
                    return false;
                }
            }

            return true;
        }

        private static string FindFieldValue(Dictionary<string, string> fields, params string[] aliases)
        {
            if (fields == null || aliases == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < aliases.Length; i++)
            {
                string alias = aliases[i] ?? string.Empty;
                string value;

                if (fields.TryGetValue(alias, out value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static double? ParseNullableDouble(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            string value = raw.Trim().Replace(" ", string.Empty);
            double parsed;

            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            if (double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out parsed))
            {
                return parsed;
            }

            value = value.Replace(",", ".");

            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            return null;
        }
    }
}
