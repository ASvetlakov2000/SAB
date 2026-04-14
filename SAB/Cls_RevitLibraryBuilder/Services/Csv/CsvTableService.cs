using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

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

    /// <summary>
    /// Внутренний helper чтения XLSX таблиц для naming workflow.
    /// </summary>
    internal class NamingSpreadsheetImportService
    {
        public CsvTable ReadAsTable(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("XLSX file path is empty.");
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("XLSX file not found.", filePath);
            }

            string extension = Path.GetExtension(filePath) ?? string.Empty;

            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only XLSX format is supported by this reader.");
            }

            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, false))
            {
                List<string> sharedStrings = ReadSharedStrings(archive);
                ZipArchiveEntry worksheetEntry = ResolveWorksheetEntry(archive);

                if (worksheetEntry == null)
                {
                    throw new InvalidOperationException("Worksheet was not found in XLSX file.");
                }

                XDocument worksheetXml;

                using (Stream worksheetStream = worksheetEntry.Open())
                {
                    worksheetXml = XDocument.Load(worksheetStream);
                }

                return ParseWorksheetAsTable(worksheetXml, sharedStrings);
            }
        }

        private static CsvTable ParseWorksheetAsTable(XDocument worksheetXml, List<string> sharedStrings)
        {
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XElement worksheet = worksheetXml.Root;

            if (worksheet == null)
            {
                throw new InvalidOperationException("Invalid worksheet XML.");
            }

            XElement sheetData = worksheet.Element(ns + "sheetData");

            if (sheetData == null)
            {
                throw new InvalidOperationException("Worksheet does not contain sheetData.");
            }

            List<List<string>> physicalRows = new List<List<string>>();

            foreach (XElement rowElement in sheetData.Elements(ns + "row"))
            {
                List<string> rowValues = new List<string>();

                foreach (XElement cellElement in rowElement.Elements(ns + "c"))
                {
                    string reference = GetAttributeValue(cellElement, "r");
                    int columnIndex = ResolveColumnIndex(reference);
                    string value = ResolveCellValue(cellElement, ns, sharedStrings);

                    while (rowValues.Count <= columnIndex)
                    {
                        rowValues.Add(string.Empty);
                    }

                    rowValues[columnIndex] = value;
                }

                physicalRows.Add(rowValues);
            }

            if (physicalRows.Count == 0)
            {
                throw new InvalidOperationException("XLSX worksheet is empty.");
            }

            List<string> header = physicalRows[0];
            CsvTable table = new CsvTable(NormalizeHeader(header));

            // Блок отвечает за чтение столбцов из XLSX файла
            // Здесь нельзя менять порядок строк, так как переименование идет построчно
            for (int i = 1; i < physicalRows.Count; i++)
            {
                List<string> rowValues = physicalRows[i];

                while (rowValues.Count < table.Header.Count)
                {
                    rowValues.Add(string.Empty);
                }

                table.Rows.Add(new CsvTableRow(i + 1, rowValues));
            }

            return table;
        }

        private static List<string> NormalizeHeader(List<string> header)
        {
            List<string> result = new List<string>();

            for (int i = 0; i < header.Count; i++)
            {
                string value = header[i] ?? string.Empty;
                result.Add(value.Trim());
            }

            return result;
        }

        private static string ResolveCellValue(XElement cellElement, XNamespace ns, List<string> sharedStrings)
        {
            string type = GetAttributeValue(cellElement, "t");

            if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
            {
                XElement isElement = cellElement.Element(ns + "is");

                if (isElement == null)
                {
                    return string.Empty;
                }

                return ReadInnerText(isElement, ns);
            }

            XElement valueElement = cellElement.Element(ns + "v");
            string rawValue = valueElement != null ? valueElement.Value : string.Empty;

            if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase))
            {
                int sharedIndex;

                if (int.TryParse(rawValue, out sharedIndex) && sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
                {
                    return sharedStrings[sharedIndex];
                }

                return string.Empty;
            }

            if (string.Equals(type, "b", StringComparison.OrdinalIgnoreCase))
            {
                return rawValue == "1" ? "TRUE" : "FALSE";
            }

            return rawValue ?? string.Empty;
        }

        private static string ReadInnerText(XElement container, XNamespace ns)
        {
            string result = string.Empty;

            foreach (XElement textNode in container.Descendants(ns + "t"))
            {
                result += textNode.Value;
            }

            return result;
        }

        private static ZipArchiveEntry ResolveWorksheetEntry(ZipArchive archive)
        {
            ZipArchiveEntry sheet1 = archive.GetEntry("xl/worksheets/sheet1.xml");

            if (sheet1 != null)
            {
                return sheet1;
            }

            for (int i = 0; i < archive.Entries.Count; i++)
            {
                ZipArchiveEntry entry = archive.Entries[i];

                if (entry.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase) &&
                    entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            List<string> result = new List<string>();
            ZipArchiveEntry sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");

            if (sharedStringsEntry == null)
            {
                return result;
            }

            XDocument sharedStringsXml;

            using (Stream sharedStream = sharedStringsEntry.Open())
            {
                sharedStringsXml = XDocument.Load(sharedStream);
            }

            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XElement root = sharedStringsXml.Root;

            if (root == null)
            {
                return result;
            }

            foreach (XElement item in root.Elements(ns + "si"))
            {
                result.Add(ReadInnerText(item, ns));
            }

            return result;
        }

        private static int ResolveColumnIndex(string cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference))
            {
                return 0;
            }

            int index = 0;

            for (int i = 0; i < cellReference.Length; i++)
            {
                char current = cellReference[i];

                if (current >= 'A' && current <= 'Z')
                {
                    index = (index * 26) + (current - 'A' + 1);
                    continue;
                }

                if (current >= 'a' && current <= 'z')
                {
                    index = (index * 26) + (current - 'a' + 1);
                    continue;
                }

                break;
            }

            return index > 0 ? index - 1 : 0;
        }

        private static string GetAttributeValue(XElement element, string attributeName)
        {
            XAttribute attribute = element.Attribute(attributeName);

            if (attribute == null)
            {
                return string.Empty;
            }

            return attribute.Value ?? string.Empty;
        }
    }
}
