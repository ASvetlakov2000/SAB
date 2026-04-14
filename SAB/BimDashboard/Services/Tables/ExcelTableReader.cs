using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using SAB.BimDashboard.Models;

namespace SAB.BimDashboard.Services.Tables
{
    /// <summary>
    /// Чтение XLSX через OpenXML-структуру файла (ZIP + XML) без внешних библиотек.
    /// </summary>
    public class ExcelTableReader : ITableReader
    {
        private static readonly XNamespace SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PackageRelationshipNs = "http://schemas.openxmlformats.org/package/2006/relationships";

        public bool CanRead(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            return string.Equals(Path.GetExtension(filePath), ".xlsx", StringComparison.OrdinalIgnoreCase);
        }

        public TabularDataSet Read(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Путь к Excel файлу не задан.");
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Excel файл не найден.", filePath);
            }

            if (!CanRead(filePath))
            {
                throw new InvalidOperationException("Поддерживается только формат XLSX.");
            }

            List<RowBuffer> rawRows;

            // Блок отвечает за чтение первой вкладки XLSX в промежуточный буфер строк.
            using (ZipArchive archive = ZipFile.OpenRead(filePath))
            {
                ZipArchiveEntry workbookEntry = ResolveWorksheetWorkbookEntry(archive);
                XDocument workbookDocument = LoadXml(workbookEntry, "Не удалось прочитать workbook.xml.");

                string worksheetPath = ResolveFirstWorksheetPath(archive, workbookEntry.FullName, workbookDocument);
                List<string> sharedStrings = ReadSharedStrings(archive);
                rawRows = ReadWorksheetRows(archive, worksheetPath, sharedStrings);
            }

            if (rawRows.Count == 0)
            {
                throw new InvalidOperationException("Первый лист Excel пустой.");
            }

            int headerIndex = FindFirstNonEmptyRowIndex(rawRows);

            if (headerIndex < 0)
            {
                throw new InvalidOperationException("Excel файл не содержит заголовков.");
            }

            List<string> rawHeaders = rawRows[headerIndex].Values;
            List<string> headers = TabularHeaderNormalizer.Normalize(rawHeaders);

            if (headers.Count == 0)
            {
                throw new InvalidOperationException("Excel файл не содержит заголовков.");
            }

            TabularDataSet dataSet = new TabularDataSet();
            dataSet.Headers = headers;

            // Блок отвечает за перенос строк данных после заголовка в TabularDataSet.
            for (int rowIndex = headerIndex + 1; rowIndex < rawRows.Count; rowIndex++)
            {
                RowBuffer sourceRow = rawRows[rowIndex];

                if (sourceRow == null || IsRowEmpty(sourceRow.Values))
                {
                    continue;
                }

                EnsureHeadersForExtraColumns(dataSet.Headers, sourceRow.Values.Count);

                TabularRow row = new TabularRow
                {
                    RowNumber = sourceRow.RowNumber
                };

                for (int columnIndex = 0; columnIndex < dataSet.Headers.Count; columnIndex++)
                {
                    string header = dataSet.Headers[columnIndex];
                    string value = columnIndex < sourceRow.Values.Count ? sourceRow.Values[columnIndex] : string.Empty;
                    row.Values[header] = value ?? string.Empty;
                }

                dataSet.Rows.Add(row);
            }

            if (dataSet.Rows.Count == 0)
            {
                throw new InvalidOperationException("Excel прочитан, но не найдено строк данных.");
            }

            return dataSet;
        }

        private static ZipArchiveEntry ResolveWorksheetWorkbookEntry(ZipArchive archive)
        {
            ZipArchiveEntry workbookEntry = archive.GetEntry("xl/workbook.xml");

            if (workbookEntry == null)
            {
                throw new InvalidOperationException("Excel файл не содержит xl/workbook.xml.");
            }

            return workbookEntry;
        }

        private static string ResolveFirstWorksheetPath(ZipArchive archive, string workbookPath, XDocument workbookDocument)
        {
            if (workbookDocument == null || workbookDocument.Root == null)
            {
                throw new InvalidOperationException("workbook.xml имеет некорректную структуру.");
            }

            XElement sheets = workbookDocument.Root != null ? workbookDocument.Root.Element(SpreadsheetNs + "sheets") : null;

            if (sheets == null)
            {
                throw new InvalidOperationException("В workbook.xml не найден блок sheets.");
            }

            XElement firstSheet = null;
            foreach (XElement sheet in sheets.Elements(SpreadsheetNs + "sheet"))
            {
                firstSheet = sheet;
                break;
            }

            if (firstSheet == null)
            {
                throw new InvalidOperationException("В workbook.xml отсутствуют листы.");
            }

            XAttribute relationshipIdAttribute = firstSheet.Attribute(RelationshipNs + "id");

            if (relationshipIdAttribute == null || string.IsNullOrWhiteSpace(relationshipIdAttribute.Value))
            {
                throw new InvalidOperationException("Первый лист не содержит relationship id.");
            }

            string relationshipId = relationshipIdAttribute.Value;
            string relationshipsPath = ResolvePath(workbookPath, "_rels/workbook.xml.rels");
            ZipArchiveEntry relationshipsEntry = archive.GetEntry(relationshipsPath);

            if (relationshipsEntry == null)
            {
                throw new InvalidOperationException("В Excel отсутствует workbook.xml.rels.");
            }

            XDocument relationshipsDocument = LoadXml(relationshipsEntry, "Не удалось прочитать workbook.xml.rels.");

            if (relationshipsDocument == null || relationshipsDocument.Root == null)
            {
                throw new InvalidOperationException("workbook.xml.rels имеет некорректную структуру.");
            }

            foreach (XElement relationship in relationshipsDocument.Root.Elements(PackageRelationshipNs + "Relationship"))
            {
                XAttribute idAttribute = relationship.Attribute("Id");

                if (idAttribute == null || !string.Equals(idAttribute.Value, relationshipId, StringComparison.Ordinal))
                {
                    continue;
                }

                XAttribute targetAttribute = relationship.Attribute("Target");

                if (targetAttribute == null || string.IsNullOrWhiteSpace(targetAttribute.Value))
                {
                    break;
                }

                string worksheetPath = ResolvePath(workbookPath, targetAttribute.Value);

                if (archive.GetEntry(worksheetPath) == null)
                {
                    throw new InvalidOperationException("Файл первого листа не найден: " + worksheetPath);
                }

                return worksheetPath;
            }

            throw new InvalidOperationException("Не удалось определить путь к первому листу Excel.");
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            List<string> sharedStrings = new List<string>();
            ZipArchiveEntry sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");

            if (sharedStringsEntry == null)
            {
                return sharedStrings;
            }

            XDocument sharedStringsDocument = LoadXml(sharedStringsEntry, "Не удалось прочитать sharedStrings.xml.");

            if (sharedStringsDocument == null || sharedStringsDocument.Root == null)
            {
                return sharedStrings;
            }

            foreach (XElement sharedItem in sharedStringsDocument.Root.Elements(SpreadsheetNs + "si"))
            {
                XElement singleText = sharedItem.Element(SpreadsheetNs + "t");

                if (singleText != null)
                {
                    sharedStrings.Add((singleText.Value ?? string.Empty).Trim());
                    continue;
                }

                string richTextValue = string.Empty;
                foreach (XElement textNode in sharedItem.Descendants(SpreadsheetNs + "t"))
                {
                    richTextValue += textNode.Value ?? string.Empty;
                }

                sharedStrings.Add(richTextValue.Trim());
            }

            return sharedStrings;
        }

        private static List<RowBuffer> ReadWorksheetRows(ZipArchive archive, string worksheetPath, List<string> sharedStrings)
        {
            ZipArchiveEntry worksheetEntry = archive.GetEntry(worksheetPath);

            if (worksheetEntry == null)
            {
                throw new InvalidOperationException("Файл листа не найден: " + worksheetPath);
            }

            XDocument worksheetDocument = LoadXml(worksheetEntry, "Не удалось прочитать XML первого листа.");
            XElement sheetData = worksheetDocument.Root != null ? worksheetDocument.Root.Element(SpreadsheetNs + "sheetData") : null;

            if (sheetData == null)
            {
                throw new InvalidOperationException("В первом листе отсутствует блок sheetData.");
            }

            List<RowBuffer> rows = new List<RowBuffer>();
            int fallbackRowNumber = 0;

            foreach (XElement rowElement in sheetData.Elements(SpreadsheetNs + "row"))
            {
                fallbackRowNumber++;

                RowBuffer row = new RowBuffer();
                XAttribute rowIndexAttribute = rowElement.Attribute("r");
                row.RowNumber = TryParseInt(rowIndexAttribute != null ? rowIndexAttribute.Value : null, fallbackRowNumber);

                int lastColumnIndex = -1;

                foreach (XElement cellElement in rowElement.Elements(SpreadsheetNs + "c"))
                {
                    string cellReference = null;
                    XAttribute cellRefAttribute = cellElement.Attribute("r");

                    if (cellRefAttribute != null)
                    {
                        cellReference = cellRefAttribute.Value;
                    }

                    int columnIndex = GetColumnIndex(cellReference);

                    if (columnIndex < 0)
                    {
                        columnIndex = lastColumnIndex + 1;
                    }

                    while (row.Values.Count <= columnIndex)
                    {
                        row.Values.Add(string.Empty);
                    }

                    row.Values[columnIndex] = ResolveCellValue(cellElement, sharedStrings);
                    lastColumnIndex = columnIndex;
                }

                rows.Add(row);
            }

            return rows;
        }

        private static string ResolveCellValue(XElement cellElement, List<string> sharedStrings)
        {
            if (cellElement == null)
            {
                return string.Empty;
            }

            XAttribute typeAttribute = cellElement.Attribute("t");
            string cellType = typeAttribute != null ? typeAttribute.Value : string.Empty;

            if (string.Equals(cellType, "inlineStr", StringComparison.OrdinalIgnoreCase))
            {
                XElement inlineString = cellElement.Element(SpreadsheetNs + "is");

                if (inlineString == null)
                {
                    return string.Empty;
                }

                string inlineValue = string.Empty;

                foreach (XElement textNode in inlineString.Descendants(SpreadsheetNs + "t"))
                {
                    inlineValue += textNode.Value ?? string.Empty;
                }

                return inlineValue.Trim();
            }

            XElement valueNode = cellElement.Element(SpreadsheetNs + "v");
            string rawValue = valueNode != null ? (valueNode.Value ?? string.Empty) : string.Empty;

            if (string.Equals(cellType, "s", StringComparison.OrdinalIgnoreCase))
            {
                int sharedIndex;

                if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out sharedIndex))
                {
                    if (sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
                    {
                        return sharedStrings[sharedIndex];
                    }
                }

                return string.Empty;
            }

            if (string.Equals(cellType, "b", StringComparison.OrdinalIgnoreCase))
            {
                return rawValue == "1" ? "TRUE" : "FALSE";
            }

            return rawValue.Trim();
        }

        private static XDocument LoadXml(ZipArchiveEntry entry, string errorMessage)
        {
            if (entry == null)
            {
                throw new InvalidOperationException(errorMessage);
            }

            using (Stream stream = entry.Open())
            {
                try
                {
                    return XDocument.Load(stream);
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(errorMessage, exception);
                }
            }
        }

        private static string ResolvePath(string basePath, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return string.Empty;
            }

            string cleanRelative = relativePath.Replace('\\', '/');

            if (cleanRelative.StartsWith("/", StringComparison.Ordinal))
            {
                cleanRelative = cleanRelative.TrimStart('/');
            }
            else
            {
                int lastSlashIndex = basePath.LastIndexOf('/');
                string baseDirectory = lastSlashIndex >= 0 ? basePath.Substring(0, lastSlashIndex + 1) : string.Empty;
                cleanRelative = baseDirectory + cleanRelative;
            }

            string[] parts = cleanRelative.Split('/');
            List<string> normalized = new List<string>();

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];

                if (string.IsNullOrWhiteSpace(part) || part == ".")
                {
                    continue;
                }

                if (part == "..")
                {
                    if (normalized.Count > 0)
                    {
                        normalized.RemoveAt(normalized.Count - 1);
                    }

                    continue;
                }

                normalized.Add(part);
            }

            return string.Join("/", normalized);
        }

        private static int GetColumnIndex(string cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference))
            {
                return -1;
            }

            int index = 0;

            for (int i = 0; i < cellReference.Length; i++)
            {
                char current = char.ToUpperInvariant(cellReference[i]);

                if (current < 'A' || current > 'Z')
                {
                    break;
                }

                index = (index * 26) + (current - 'A' + 1);
            }

            return index > 0 ? index - 1 : -1;
        }

        private static int FindFirstNonEmptyRowIndex(List<RowBuffer> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                RowBuffer row = rows[i];

                if (row != null && !IsRowEmpty(row.Values))
                {
                    return i;
                }
            }

            return -1;
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

        private static int TryParseInt(string text, int defaultValue)
        {
            int result;

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                return result;
            }

            return defaultValue;
        }

        private class RowBuffer
        {
            public RowBuffer()
            {
                Values = new List<string>();
            }

            public int RowNumber { get; set; }

            public List<string> Values { get; private set; }
        }
    }
}
