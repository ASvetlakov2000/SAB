using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
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

            string[] sourceLines = File.ReadAllLines(filePath);

            if (sourceLines.Length == 0)
            {
                throw new InvalidOperationException("CSV файл пустой.");
            }

            List<CsvRecord> records = BuildCsvRecords(sourceLines);

            if (records.Count == 0)
            {
                throw new InvalidOperationException("CSV файл не содержит строк.");
            }

            int headerRecordIndex = FindFirstNonEmptyRecordIndex(records);

            if (headerRecordIndex < 0)
            {
                throw new InvalidOperationException("CSV файл не содержит заголовков.");
            }

            char delimiter = DetectDelimiter(records[headerRecordIndex].Text);
            List<string> rawHeaders = ParseCsvLine(records[headerRecordIndex].Text, delimiter);

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
            for (int recordIndex = headerRecordIndex + 1; recordIndex < records.Count; recordIndex++)
            {
                CsvRecord record = records[recordIndex];

                if (record == null || string.IsNullOrWhiteSpace(record.Text))
                {
                    continue;
                }

                List<string> values = ParseCsvLine(record.Text, delimiter);

                if (IsRowEmpty(values))
                {
                    continue;
                }

                EnsureHeadersForExtraColumns(dataSet.Headers, values.Count);

                TabularRow row = new TabularRow
                {
                    RowNumber = record.StartLineNumber
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

        private static List<CsvRecord> BuildCsvRecords(string[] sourceLines)
        {
            List<CsvRecord> records = new List<CsvRecord>();

            if (sourceLines == null || sourceLines.Length == 0)
            {
                return records;
            }

            StringBuilder buffer = new StringBuilder();
            bool insideQuotes = false;
            int recordStartLine = -1;

            for (int index = 0; index < sourceLines.Length; index++)
            {
                string line = sourceLines[index] ?? string.Empty;

                if (recordStartLine < 0)
                {
                    recordStartLine = index + 1;
                }

                if (buffer.Length > 0)
                {
                    buffer.Append(Environment.NewLine);
                }

                buffer.Append(line);
                UpdateQuoteState(line, ref insideQuotes);

                if (!insideQuotes)
                {
                    records.Add(new CsvRecord
                    {
                        Text = buffer.ToString(),
                        StartLineNumber = recordStartLine
                    });

                    buffer.Clear();
                    recordStartLine = -1;
                }
            }

            if (buffer.Length > 0)
            {
                records.Add(new CsvRecord
                {
                    Text = buffer.ToString(),
                    StartLineNumber = recordStartLine > 0 ? recordStartLine : sourceLines.Length
                });
            }

            return records;
        }

        private static int FindFirstNonEmptyRecordIndex(List<CsvRecord> records)
        {
            if (records == null)
            {
                return -1;
            }

            for (int index = 0; index < records.Count; index++)
            {
                CsvRecord record = records[index];

                if (record != null && !string.IsNullOrWhiteSpace(record.Text))
                {
                    return index;
                }
            }

            return -1;
        }

        private static void UpdateQuoteState(string text, ref bool insideQuotes)
        {
            if (text == null)
            {
                return;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];

                if (current != '"')
                {
                    continue;
                }

                if (insideQuotes && i + 1 < text.Length && text[i + 1] == '"')
                {
                    i++;
                    continue;
                }

                insideQuotes = !insideQuotes;
            }
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

        private class CsvRecord
        {
            public string Text { get; set; }

            public int StartLineNumber { get; set; }
        }
    }
}
