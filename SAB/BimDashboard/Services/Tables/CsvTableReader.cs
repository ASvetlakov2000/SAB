using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SAB.BimDashboard.Models;

namespace SAB.BimDashboard.Services.Tables
{
    /// <summary>
    /// Чтение CSV-файла в универсальный табличный набор.
    /// </summary>
    public class CsvTableReader : ITableReader
    {
        public bool CanRead(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            return string.Equals(Path.GetExtension(filePath), ".csv", StringComparison.OrdinalIgnoreCase);
        }

        public TabularDataSet Read(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Путь к CSV файлу не задан.");
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("CSV файл не найден.", filePath);
            }

            string[] lines = File.ReadAllLines(filePath);

            if (lines.Length == 0)
            {
                throw new InvalidOperationException("CSV файл пустой.");
            }

            int headerLineIndex = FindFirstNonEmptyLineIndex(lines);

            if (headerLineIndex < 0)
            {
                throw new InvalidOperationException("CSV файл не содержит заголовков.");
            }

            char delimiter = DetectDelimiter(lines[headerLineIndex]);
            List<string> rawHeaders = ParseCsvLine(lines[headerLineIndex], delimiter);

            if (rawHeaders.Count == 0)
            {
                throw new InvalidOperationException("CSV файл не содержит заголовков.");
            }

            if (!string.IsNullOrEmpty(rawHeaders[0]))
            {
                rawHeaders[0] = rawHeaders[0].TrimStart('\uFEFF');
            }

            TabularDataSet dataSet = new TabularDataSet();
            dataSet.Headers = TabularHeaderNormalizer.Normalize(rawHeaders);

            // Блок отвечает за чтение строк после заголовка и сбор TabularRow.
            for (int lineIndex = headerLineIndex + 1; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                List<string> values = ParseCsvLine(line, delimiter);

                if (IsRowEmpty(values))
                {
                    continue;
                }

                EnsureHeadersForExtraColumns(dataSet.Headers, values.Count);

                TabularRow row = new TabularRow
                {
                    RowNumber = lineIndex + 1
                };

                for (int columnIndex = 0; columnIndex < dataSet.Headers.Count; columnIndex++)
                {
                    string header = dataSet.Headers[columnIndex];
                    string value = columnIndex < values.Count ? (values[columnIndex] ?? string.Empty).Trim() : string.Empty;
                    row.Values[header] = value;
                }

                dataSet.Rows.Add(row);
            }

            if (dataSet.Rows.Count == 0)
            {
                throw new InvalidOperationException("CSV файл не содержит строк данных.");
            }

            return dataSet;
        }

        private static int FindFirstNonEmptyLineIndex(string[] lines)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        // Блок определения разделителя (поддержка ',', ';', '\t').
        private static char DetectDelimiter(string headerLine)
        {
            int commaCount = CountDelimiter(headerLine, ',');
            int semicolonCount = CountDelimiter(headerLine, ';');
            int tabCount = CountDelimiter(headerLine, '\t');

            if (semicolonCount >= commaCount && semicolonCount >= tabCount)
            {
                return ';';
            }

            if (tabCount >= commaCount && tabCount >= semicolonCount)
            {
                return '\t';
            }

            return ',';
        }

        private static int CountDelimiter(string line, char delimiter)
        {
            if (string.IsNullOrEmpty(line))
            {
                return 0;
            }

            int count = 0;

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == delimiter)
                {
                    count++;
                }
            }

            return count;
        }

        // Блок парсинга CSV-строки с учетом кавычек и экранирования.
        private static List<string> ParseCsvLine(string line, char delimiter)
        {
            List<string> values = new List<string>();

            if (line == null)
            {
                values.Add(string.Empty);
                return values;
            }

            bool insideQuotes = false;
            char[] buffer = new char[line.Length];
            int bufferIndex = 0;

            for (int i = 0; i < line.Length; i++)
            {
                char currentChar = line[i];

                if (currentChar == '"')
                {
                    if (insideQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        buffer[bufferIndex] = '"';
                        bufferIndex++;
                        i++;
                    }
                    else
                    {
                        insideQuotes = !insideQuotes;
                    }

                    continue;
                }

                if (currentChar == delimiter && !insideQuotes)
                {
                    values.Add(new string(buffer, 0, bufferIndex).Trim());
                    bufferIndex = 0;
                    continue;
                }

                buffer[bufferIndex] = currentChar;
                bufferIndex++;
            }

            values.Add(new string(buffer, 0, bufferIndex).Trim());
            return values;
        }

        private static bool IsRowEmpty(List<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static void EnsureHeadersForExtraColumns(List<string> headers, int valuesCount)
        {
            if (headers == null)
            {
                return;
            }

            while (headers.Count < valuesCount)
            {
                string header = "Column" + (headers.Count + 1).ToString(CultureInfo.InvariantCulture);
                headers.Add(header);
            }
        }
    }
}
