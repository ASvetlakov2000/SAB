using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RevitLibraryBuilder.Services.Csv
{
    /// <summary>
    /// Сервис для чтения/записи CSV с поддержкой кавычек.
    /// </summary>
    public class CsvTableService
    {
        public CsvTable Read(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("CSV file path is empty.");
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("CSV file not found.", filePath);
            }

            string[] lines = File.ReadAllLines(filePath);

            if (lines.Length == 0)
            {
                throw new InvalidOperationException("CSV file is empty.");
            }

            List<string> header = ParseCsvLine(lines[0]);

            if (header.Count == 0)
            {
                throw new InvalidOperationException("CSV header is empty.");
            }

            CsvTable table = new CsvTable(header);

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                List<string> values = ParseCsvLine(line);

                while (values.Count < header.Count)
                {
                    values.Add(string.Empty);
                }

                table.Rows.Add(new CsvTableRow(i + 1, values));
            }

            return table;
        }

        public void Write(string filePath, List<string> header, List<List<string>> rows)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("CSV file path is empty.");
            }

            if (header == null || header.Count == 0)
            {
                throw new ArgumentException("CSV header is empty.");
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(JoinRow(header));

            if (rows != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    List<string> row = rows[i] ?? new List<string>();
                    stringBuilder.AppendLine(JoinRow(row));
                }
            }

            File.WriteAllText(filePath, stringBuilder.ToString(), Encoding.UTF8);
        }

        // Блок парсинга CSV-строки с учетом кавычек и экранирования
        private static List<string> ParseCsvLine(string line)
        {
            List<string> values = new List<string>();
            StringBuilder currentValue = new StringBuilder();
            bool insideQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char currentChar = line[i];

                if (currentChar == '"')
                {
                    if (insideQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentValue.Append('"');
                        i++;
                    }
                    else
                    {
                        insideQuotes = !insideQuotes;
                    }

                    continue;
                }

                if (currentChar == ',' && !insideQuotes)
                {
                    values.Add(currentValue.ToString().Trim());
                    currentValue.Clear();
                    continue;
                }

                currentValue.Append(currentChar);
            }

            values.Add(currentValue.ToString().Trim());
            return values;
        }

        private static string JoinRow(List<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder stringBuilder = new StringBuilder();

            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    stringBuilder.Append(',');
                }

                stringBuilder.Append(Escape(values[i]));
            }

            return stringBuilder.ToString();
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }
    }

    public class CsvTable
    {
        public CsvTable(List<string> header)
        {
            Header = header ?? new List<string>();
            Rows = new List<CsvTableRow>();
        }

        public List<string> Header { get; private set; }

        public List<CsvTableRow> Rows { get; private set; }

        public int FindHeaderIndex(string headerName)
        {
            if (string.IsNullOrWhiteSpace(headerName))
            {
                return -1;
            }

            for (int i = 0; i < Header.Count; i++)
            {
                if (string.Equals(Header[i], headerName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        public bool HasColumn(string headerName)
        {
            return FindHeaderIndex(headerName) >= 0;
        }

        public void ValidateRequiredColumns(List<string> requiredColumns)
        {
            List<string> missingColumns = new List<string>();

            for (int i = 0; i < requiredColumns.Count; i++)
            {
                if (!HasColumn(requiredColumns[i]))
                {
                    missingColumns.Add(requiredColumns[i]);
                }
            }

            if (missingColumns.Count > 0)
            {
                throw new InvalidOperationException("CSV is missing required columns: " + string.Join(", ", missingColumns));
            }
        }
    }

    public class CsvTableRow
    {
        public CsvTableRow(int rowIndex, List<string> values)
        {
            RowIndex = rowIndex;
            Values = values ?? new List<string>();
        }

        public int RowIndex { get; private set; }

        public List<string> Values { get; private set; }

        public string GetValue(int index)
        {
            if (index < 0 || index >= Values.Count)
            {
                return string.Empty;
            }

            return Values[index];
        }
    }
}
