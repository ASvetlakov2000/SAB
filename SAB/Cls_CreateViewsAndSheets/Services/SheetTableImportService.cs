using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using SAB.CreateViewsAndSheets.Models;

namespace SAB.CreateViewsAndSheets.Services
{
    public class SheetTableImportService
    {
        private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
        private static readonly XNamespace OfficeDocumentRelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        public IList<SheetTableImportRow> ReadRows(string filePath)
        {
            ValidateFilePath(filePath);

            using (FileStream fileStream = File.OpenRead(filePath))
            using (ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Read))
            {
                List<string> sharedStrings = ReadSharedStrings(archive);
                string firstWorksheetPath = GetFirstWorksheetPath(archive);
                ZipArchiveEntry worksheetEntry = archive.GetEntry(firstWorksheetPath);
                if (worksheetEntry == null)
                {
                    throw new InvalidDataException("В файле Excel не найден первый лист книги.");
                }

                List<Dictionary<int, string>> worksheetRows = ReadWorksheetRows(worksheetEntry, sharedStrings);
                return ConvertWorksheetRowsToImportRows(worksheetRows);
            }
        }

        private void ValidateFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Путь к файлу Excel не задан.");
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Файл Excel не найден.", filePath);
            }

            string extension = Path.GetExtension(filePath);
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Поддерживается только формат .xlsx.");
            }
        }

        private List<string> ReadSharedStrings(ZipArchive archive)
        {
            List<string> result = new List<string>();
            ZipArchiveEntry sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
            if (sharedStringsEntry == null)
            {
                return result;
            }

            XDocument document = LoadXml(sharedStringsEntry);
            foreach (XElement item in document.Descendants(SpreadsheetNamespace + "si"))
            {
                StringBuilder builder = new StringBuilder();
                foreach (XElement textElement in item.Descendants(SpreadsheetNamespace + "t"))
                {
                    builder.Append(textElement.Value);
                }

                result.Add(builder.ToString());
            }

            return result;
        }

        private string GetFirstWorksheetPath(ZipArchive archive)
        {
            ZipArchiveEntry workbookEntry = archive.GetEntry("xl/workbook.xml");
            ZipArchiveEntry relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
            if (workbookEntry == null || relationshipsEntry == null)
            {
                throw new InvalidDataException("Файл Excel поврежден: не найдена структура книги.");
            }

            XDocument workbookDocument = LoadXml(workbookEntry);
            XElement firstSheet = null;
            foreach (XElement sheet in workbookDocument.Descendants(SpreadsheetNamespace + "sheet"))
            {
                firstSheet = sheet;
                break;
            }

            if (firstSheet == null)
            {
                throw new InvalidDataException("В файле Excel нет листов.");
            }

            XAttribute relationshipIdAttribute = firstSheet.Attribute(OfficeDocumentRelationshipsNamespace + "id");
            if (relationshipIdAttribute == null || string.IsNullOrWhiteSpace(relationshipIdAttribute.Value))
            {
                throw new InvalidDataException("В файле Excel не найден идентификатор первого листа.");
            }

            XDocument relationshipsDocument = LoadXml(relationshipsEntry);
            foreach (XElement relationship in relationshipsDocument.Descendants(RelationshipsNamespace + "Relationship"))
            {
                XAttribute idAttribute = relationship.Attribute("Id");
                if (idAttribute == null || !string.Equals(idAttribute.Value, relationshipIdAttribute.Value, StringComparison.Ordinal))
                {
                    continue;
                }

                XAttribute targetAttribute = relationship.Attribute("Target");
                if (targetAttribute == null || string.IsNullOrWhiteSpace(targetAttribute.Value))
                {
                    break;
                }

                return ResolveWorkbookRelationshipTarget(targetAttribute.Value);
            }

            throw new InvalidDataException("В файле Excel не найден путь к первому листу.");
        }

        private string ResolveWorkbookRelationshipTarget(string target)
        {
            string normalizedTarget = (target ?? string.Empty).Replace('\\', '/').Trim();
            if (string.IsNullOrWhiteSpace(normalizedTarget))
            {
                throw new InvalidDataException("В файле Excel указан пустой путь к листу.");
            }

            if (normalizedTarget.StartsWith("/", StringComparison.Ordinal))
            {
                return normalizedTarget.TrimStart('/');
            }

            if (normalizedTarget.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedTarget;
            }

            return "xl/" + normalizedTarget;
        }

        private List<Dictionary<int, string>> ReadWorksheetRows(ZipArchiveEntry worksheetEntry, IList<string> sharedStrings)
        {
            List<Dictionary<int, string>> result = new List<Dictionary<int, string>>();
            XDocument document = LoadXml(worksheetEntry);
            foreach (XElement rowElement in document.Descendants(SpreadsheetNamespace + "row"))
            {
                Dictionary<int, string> rowValues = new Dictionary<int, string>();
                int nextColumnIndex = 0;

                foreach (XElement cellElement in rowElement.Elements(SpreadsheetNamespace + "c"))
                {
                    int columnIndex = GetColumnIndex(cellElement, nextColumnIndex);
                    string value = ReadCellValue(cellElement, sharedStrings);
                    rowValues[columnIndex] = value;
                    nextColumnIndex = columnIndex + 1;
                }

                if (rowValues.Count > 0)
                {
                    result.Add(rowValues);
                }
            }

            return result;
        }

        private IList<SheetTableImportRow> ConvertWorksheetRowsToImportRows(List<Dictionary<int, string>> worksheetRows)
        {
            if (worksheetRows == null || worksheetRows.Count == 0)
            {
                throw new InvalidDataException("В таблице Excel нет строк.");
            }

            int headerRowIndex = FindHeaderRowIndex(worksheetRows);
            if (headerRowIndex < 0)
            {
                throw new InvalidDataException("В таблице Excel не найдена строка заголовков.");
            }

            Dictionary<int, string> headerRow = worksheetRows[headerRowIndex];
            int sheetNumberColumnIndex = FindHeaderColumnIndex(headerRow, SheetTableHeaderKind.SheetNumber);
            int sheetNameColumnIndex = FindHeaderColumnIndex(headerRow, SheetTableHeaderKind.SheetName);
            int sectionColumnIndex = FindHeaderColumnIndex(headerRow, SheetTableHeaderKind.Section);
            ValidateRequiredColumns(sheetNumberColumnIndex, sheetNameColumnIndex, sectionColumnIndex);

            List<SheetTableImportRow> result = new List<SheetTableImportRow>();
            for (int i = headerRowIndex + 1; i < worksheetRows.Count; i++)
            {
                Dictionary<int, string> worksheetRow = worksheetRows[i];
                SheetTableImportRow importRow = new SheetTableImportRow();
                importRow.SheetNumber = GetCellValue(worksheetRow, sheetNumberColumnIndex);
                importRow.SheetName = GetCellValue(worksheetRow, sheetNameColumnIndex);
                importRow.SectionName = GetCellValue(worksheetRow, sectionColumnIndex);

                if (IsImportRowEmpty(importRow))
                {
                    continue;
                }

                result.Add(importRow);
            }

            if (result.Count == 0)
            {
                throw new InvalidDataException("В таблице Excel нет строк с данными.");
            }

            return result;
        }

        private int FindHeaderRowIndex(List<Dictionary<int, string>> worksheetRows)
        {
            for (int i = 0; i < worksheetRows.Count; i++)
            {
                Dictionary<int, string> row = worksheetRows[i];
                if (row == null)
                {
                    continue;
                }

                bool hasSheetNumberHeader = FindHeaderColumnIndex(row, SheetTableHeaderKind.SheetNumber) >= 0;
                bool hasSheetNameHeader = FindHeaderColumnIndex(row, SheetTableHeaderKind.SheetName) >= 0;
                bool hasSectionHeader = FindHeaderColumnIndex(row, SheetTableHeaderKind.Section) >= 0;
                if (hasSheetNumberHeader && hasSheetNameHeader && hasSectionHeader)
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindHeaderColumnIndex(Dictionary<int, string> headerRow, SheetTableHeaderKind headerKind)
        {
            if (headerRow == null)
            {
                return -1;
            }

            foreach (KeyValuePair<int, string> pair in headerRow)
            {
                if (IsHeaderMatch(pair.Value, headerKind))
                {
                    return pair.Key;
                }
            }

            return -1;
        }

        private bool IsHeaderMatch(string headerText, SheetTableHeaderKind headerKind)
        {
            string normalizedHeader = NormalizeHeader(headerText);
            if (string.IsNullOrWhiteSpace(normalizedHeader))
            {
                return false;
            }

            if (headerKind == SheetTableHeaderKind.SheetNumber)
            {
                return normalizedHeader == "номерлиста" ||
                       normalizedHeader == "листномер" ||
                       normalizedHeader == "sheetnumber" ||
                       normalizedHeader == "number" ||
                       ((headerText ?? string.Empty).Contains("№") && normalizedHeader.Contains("листа"));
            }

            if (headerKind == SheetTableHeaderKind.SheetName)
            {
                return normalizedHeader == "имялиста" ||
                       normalizedHeader == "листимя" ||
                       normalizedHeader == "sheetname" ||
                       normalizedHeader == "name";
            }

            return normalizedHeader == "раздел" ||
                   normalizedHeader == "разделлиста" ||
                   normalizedHeader == "sheetsection" ||
                   normalizedHeader == "section" ||
                   normalizedHeader.Contains("раздел");
        }

        private string NormalizeHeader(string headerText)
        {
            string value = (headerText ?? string.Empty).Trim().ToLowerInvariant();
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        private void ValidateRequiredColumns(int sheetNumberColumnIndex, int sheetNameColumnIndex, int sectionColumnIndex)
        {
            List<string> missingColumns = new List<string>();
            if (sheetNumberColumnIndex < 0)
            {
                missingColumns.Add("Номер листа");
            }

            if (sheetNameColumnIndex < 0)
            {
                missingColumns.Add("Имя листа");
            }

            if (sectionColumnIndex < 0)
            {
                missingColumns.Add("Раздел");
            }

            if (missingColumns.Count > 0)
            {
                throw new InvalidDataException("В таблице Excel не найдены обязательные колонки: " + string.Join(", ", missingColumns));
            }
        }

        private string GetCellValue(Dictionary<int, string> row, int columnIndex)
        {
            if (row == null || columnIndex < 0)
            {
                return string.Empty;
            }

            string value;
            if (!row.TryGetValue(columnIndex, out value))
            {
                return string.Empty;
            }

            return (value ?? string.Empty).Trim();
        }

        private bool IsImportRowEmpty(SheetTableImportRow importRow)
        {
            if (importRow == null)
            {
                return true;
            }

            return string.IsNullOrWhiteSpace(importRow.SheetNumber) &&
                   string.IsNullOrWhiteSpace(importRow.SheetName) &&
                   string.IsNullOrWhiteSpace(importRow.SectionName);
        }

        private int GetColumnIndex(XElement cellElement, int fallbackColumnIndex)
        {
            XAttribute referenceAttribute = cellElement.Attribute("r");
            if (referenceAttribute == null || string.IsNullOrWhiteSpace(referenceAttribute.Value))
            {
                return fallbackColumnIndex;
            }

            string reference = referenceAttribute.Value;
            int columnIndex = 0;
            bool hasColumnLetters = false;
            for (int i = 0; i < reference.Length; i++)
            {
                char character = reference[i];
                if (!char.IsLetter(character))
                {
                    break;
                }

                hasColumnLetters = true;
                columnIndex *= 26;
                columnIndex += char.ToUpperInvariant(character) - 'A' + 1;
            }

            return hasColumnLetters ? columnIndex - 1 : fallbackColumnIndex;
        }

        private string ReadCellValue(XElement cellElement, IList<string> sharedStrings)
        {
            XAttribute typeAttribute = cellElement.Attribute("t");
            string cellType = typeAttribute != null ? typeAttribute.Value : string.Empty;

            if (string.Equals(cellType, "inlineStr", StringComparison.OrdinalIgnoreCase))
            {
                return ReadInlineStringValue(cellElement);
            }

            XElement valueElement = cellElement.Element(SpreadsheetNamespace + "v");
            if (valueElement == null)
            {
                return string.Empty;
            }

            string rawValue = valueElement.Value ?? string.Empty;
            if (string.Equals(cellType, "s", StringComparison.OrdinalIgnoreCase))
            {
                int sharedStringIndex;
                if (int.TryParse(rawValue, out sharedStringIndex) &&
                    sharedStringIndex >= 0 &&
                    sharedStringIndex < sharedStrings.Count)
                {
                    return sharedStrings[sharedStringIndex] ?? string.Empty;
                }

                return string.Empty;
            }

            if (string.Equals(cellType, "b", StringComparison.OrdinalIgnoreCase))
            {
                return rawValue == "1" ? "TRUE" : "FALSE";
            }

            return rawValue.Trim();
        }

        private string ReadInlineStringValue(XElement cellElement)
        {
            StringBuilder builder = new StringBuilder();
            foreach (XElement textElement in cellElement.Descendants(SpreadsheetNamespace + "t"))
            {
                builder.Append(textElement.Value);
            }

            return builder.ToString();
        }

        private XDocument LoadXml(ZipArchiveEntry entry)
        {
            using (Stream stream = entry.Open())
            {
                return XDocument.Load(stream);
            }
        }

        private enum SheetTableHeaderKind
        {
            SheetNumber,
            SheetName,
            Section
        }
    }
}
