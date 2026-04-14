using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Security;

namespace RevitLibraryBuilder.Services.Csv
{
    /// <summary>
    /// Сервис записи XLSX для таблиц переименования с форматированием.
    /// </summary>
    public class NamingSpreadsheetExportService
    {
        // Блок отвечает за настройку цветов таблицы
        // Здесь можно вручную изменить цвета групп столбцов
        // Здесь настраивается цвет фона для старых и новых наименований
        // Блок отвечает за чередование цвета строк по категориям
        private static class NamingTableColors
        {
            // Цвет заголовка для колонки Category
            public const string HeaderGroupCategory = "FFDDEBF7";

            // Цвет заголовка для колонок со старыми значениями
            public const string HeaderGroupOldValues = "FFFCE4D6";

            // Цвет заголовка для колонок с новыми значениями
            public const string HeaderGroupNewValues = "FFE2F0D9";

            // Цвет строк без переключения категории
            public const string RowBaseWhite = "FFFFFFFF";

            // Цвет строк после переключения категории
            public const string RowBaseGray = "FFF2F2F2";

            // Цвет для колонки Category (белый блок категории)
            public const string CategoryColumnWhite = "FFEAF3FB";

            // Цвет для колонки Category (серый блок категории)
            public const string CategoryColumnGray = "FFD9E8F5";

            // Цвет для столбцов со старыми значениями (белый блок)
            public const string OldValuesWhite = "FFFDF1E8";

            // Цвет для столбцов со старыми значениями (серый блок)
            public const string OldValuesGray = "FFF8E4D7";

            // Цвет для столбцов с новыми значениями (белый блок)
            public const string NewValuesWhite = "FFECF7E8";

            // Цвет для столбцов с новыми значениями (серый блок)
            public const string NewValuesGray = "FFDFEFD9";
        }

        private enum ColumnGroup
        {
            Default = 0,
            Category = 1,
            OldValues = 2,
            NewValues = 3
        }

        private static class StyleIndex
        {
            public const int HeaderCategory = 1;
            public const int HeaderOld = 2;
            public const int HeaderNew = 3;

            public const int DataDefaultWhite = 4;
            public const int DataDefaultGray = 5;

            public const int DataCategoryWhite = 6;
            public const int DataCategoryGray = 7;

            public const int DataOldWhite = 8;
            public const int DataOldGray = 9;

            public const int DataNewWhite = 10;
            public const int DataNewGray = 11;
        }

        public string WriteNamingWorkbook(
            string outputFolder,
            string fileName,
            List<string> headers,
            List<List<string>> rows)
        {
            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                throw new ArgumentException("Output folder is empty.");
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name is empty.");
            }

            if (headers == null || headers.Count == 0)
            {
                throw new ArgumentException("Headers are empty.");
            }

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            string fullPath = Path.Combine(outputFolder, fileName);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            List<ColumnGroup> groups = ResolveColumnGroups(headers);
            List<bool> rowToggleFlags = BuildRowToggleFlags(headers, rows);

            using (FileStream stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create, false))
            {
                WriteEntry(archive, "[Content_Types].xml", BuildContentTypesXml());
                WriteEntry(archive, "_rels/.rels", BuildRootRelsXml());
                WriteEntry(archive, "xl/workbook.xml", BuildWorkbookXml());
                WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelsXml());
                WriteEntry(archive, "xl/styles.xml", BuildStylesXml());
                WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(headers, rows, groups, rowToggleFlags));
            }

            return fullPath;
        }

        private static List<ColumnGroup> ResolveColumnGroups(List<string> headers)
        {
            List<ColumnGroup> groups = new List<ColumnGroup>();

            for (int i = 0; i < headers.Count; i++)
            {
                string header = headers[i] ?? string.Empty;

                if (string.Equals(header, "Category", StringComparison.OrdinalIgnoreCase))
                {
                    groups.Add(ColumnGroup.Category);
                    continue;
                }

                if (header.EndsWith("_Old", StringComparison.OrdinalIgnoreCase))
                {
                    groups.Add(ColumnGroup.OldValues);
                    continue;
                }

                if (header.EndsWith("_New", StringComparison.OrdinalIgnoreCase))
                {
                    groups.Add(ColumnGroup.NewValues);
                    continue;
                }

                groups.Add(ColumnGroup.Default);
            }

            return groups;
        }

        private static List<bool> BuildRowToggleFlags(List<string> headers, List<List<string>> rows)
        {
            List<bool> result = new List<bool>();

            if (rows == null || rows.Count == 0)
            {
                return result;
            }

            int categoryIndex = -1;

            for (int i = 0; i < headers.Count; i++)
            {
                if (string.Equals(headers[i], "Category", StringComparison.OrdinalIgnoreCase))
                {
                    categoryIndex = i;
                    break;
                }
            }

            bool gray = false;
            string previousCategory = null;

            for (int i = 0; i < rows.Count; i++)
            {
                if (categoryIndex >= 0)
                {
                    string currentCategory = GetCellValue(rows[i], categoryIndex);

                    if (i > 0 && !string.Equals(previousCategory, currentCategory, StringComparison.OrdinalIgnoreCase))
                    {
                        gray = !gray;
                    }

                    previousCategory = currentCategory;
                }

                result.Add(gray);
            }

            return result;
        }

        private static string BuildWorksheetXml(
            List<string> headers,
            List<List<string>> rows,
            List<ColumnGroup> groups,
            List<bool> rowToggleFlags)
        {
            int dataRowCount = rows != null ? rows.Count : 0;
            int totalRows = dataRowCount + 1;
            int lastColumnIndex = headers.Count - 1;
            string lastColumnName = ToColumnName(lastColumnIndex + 1);
            string dimensionRange = "A1:" + lastColumnName + totalRows;
            string autoFilterRange = dimensionRange;

            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            stringBuilder.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
            stringBuilder.Append("<dimension ref=\"").Append(dimensionRange).Append("\"/>");
            stringBuilder.Append("<sheetViews><sheetView workbookViewId=\"0\"/></sheetViews>");
            stringBuilder.Append("<sheetFormatPr defaultRowHeight=\"15\"/>");
            stringBuilder.Append("<sheetData>");

            // Заголовок таблицы
            stringBuilder.Append("<row r=\"1\">");

            for (int col = 0; col < headers.Count; col++)
            {
                string cellRef = ToColumnName(col + 1) + "1";
                int styleIndex = ResolveHeaderStyle(groups[col]);
                AppendInlineStringCell(stringBuilder, cellRef, headers[col], styleIndex);
            }

            stringBuilder.Append("</row>");

            // Данные таблицы
            for (int row = 0; row < dataRowCount; row++)
            {
                int excelRow = row + 2;
                bool gray = rowToggleFlags.Count > row && rowToggleFlags[row];

                stringBuilder.Append("<row r=\"").Append(excelRow).Append("\">");

                for (int col = 0; col < headers.Count; col++)
                {
                    string cellRef = ToColumnName(col + 1) + excelRow;
                    string value = GetCellValue(rows[row], col);
                    int styleIndex = ResolveDataStyle(groups[col], gray);
                    AppendInlineStringCell(stringBuilder, cellRef, value, styleIndex);
                }

                stringBuilder.Append("</row>");
            }

            stringBuilder.Append("</sheetData>");
            stringBuilder.Append("<autoFilter ref=\"").Append(autoFilterRange).Append("\"/>");
            stringBuilder.Append("</worksheet>");

            return stringBuilder.ToString();
        }

        private static int ResolveHeaderStyle(ColumnGroup group)
        {
            switch (group)
            {
                case ColumnGroup.Category:
                    return StyleIndex.HeaderCategory;
                case ColumnGroup.OldValues:
                    return StyleIndex.HeaderOld;
                case ColumnGroup.NewValues:
                    return StyleIndex.HeaderNew;
                default:
                    return StyleIndex.HeaderCategory;
            }
        }

        private static int ResolveDataStyle(ColumnGroup group, bool gray)
        {
            switch (group)
            {
                case ColumnGroup.Category:
                    return gray ? StyleIndex.DataCategoryGray : StyleIndex.DataCategoryWhite;
                case ColumnGroup.OldValues:
                    return gray ? StyleIndex.DataOldGray : StyleIndex.DataOldWhite;
                case ColumnGroup.NewValues:
                    return gray ? StyleIndex.DataNewGray : StyleIndex.DataNewWhite;
                default:
                    return gray ? StyleIndex.DataDefaultGray : StyleIndex.DataDefaultWhite;
            }
        }

        private static string BuildStylesXml()
        {
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            stringBuilder.Append("<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            stringBuilder.Append("<numFmts count=\"0\"/>");
            stringBuilder.Append("<fonts count=\"1\">\n");
            stringBuilder.Append("<font><sz val=\"11\"/><color theme=\"1\"/><name val=\"Calibri\"/><family val=\"2\"/></font>");
            stringBuilder.Append("</fonts>");

            stringBuilder.Append("<fills count=\"14\">");
            stringBuilder.Append("<fill><patternFill patternType=\"none\"/></fill>");
            stringBuilder.Append("<fill><patternFill patternType=\"gray125\"/></fill>");
            AppendFill(stringBuilder, NamingTableColors.HeaderGroupCategory); //2
            AppendFill(stringBuilder, NamingTableColors.HeaderGroupOldValues); //3
            AppendFill(stringBuilder, NamingTableColors.HeaderGroupNewValues); //4
            AppendFill(stringBuilder, NamingTableColors.RowBaseWhite); //5
            AppendFill(stringBuilder, NamingTableColors.RowBaseGray); //6
            AppendFill(stringBuilder, NamingTableColors.CategoryColumnWhite); //7
            AppendFill(stringBuilder, NamingTableColors.CategoryColumnGray); //8
            AppendFill(stringBuilder, NamingTableColors.OldValuesWhite); //9
            AppendFill(stringBuilder, NamingTableColors.OldValuesGray); //10
            AppendFill(stringBuilder, NamingTableColors.NewValuesWhite); //11
            AppendFill(stringBuilder, NamingTableColors.NewValuesGray); //12
            AppendFill(stringBuilder, NamingTableColors.HeaderGroupCategory); //13 fallback
            stringBuilder.Append("</fills>");

            stringBuilder.Append("<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>");
            stringBuilder.Append("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");

            stringBuilder.Append("<cellXfs count=\"12\">");
            AppendXf(stringBuilder, 0);  //0 default
            AppendXf(stringBuilder, 2);  //1 header category
            AppendXf(stringBuilder, 3);  //2 header old
            AppendXf(stringBuilder, 4);  //3 header new
            AppendXf(stringBuilder, 5);  //4 data default white
            AppendXf(stringBuilder, 6);  //5 data default gray
            AppendXf(stringBuilder, 7);  //6 data category white
            AppendXf(stringBuilder, 8);  //7 data category gray
            AppendXf(stringBuilder, 9);  //8 data old white
            AppendXf(stringBuilder, 10); //9 data old gray
            AppendXf(stringBuilder, 11); //10 data new white
            AppendXf(stringBuilder, 12); //11 data new gray
            stringBuilder.Append("</cellXfs>");

            stringBuilder.Append("<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>");
            stringBuilder.Append("</styleSheet>");

            return stringBuilder.ToString();
        }

        private static void AppendXf(StringBuilder stringBuilder, int fillId)
        {
            stringBuilder.Append("<xf numFmtId=\"0\" fontId=\"0\" fillId=\"")
                         .Append(fillId)
                         .Append("\" borderId=\"0\" xfId=\"0\" applyFill=\"1\" applyAlignment=\"1\"><alignment vertical=\"center\"/></xf>");
        }

        private static void AppendFill(StringBuilder stringBuilder, string argb)
        {
            stringBuilder.Append("<fill><patternFill patternType=\"solid\"><fgColor rgb=\"")
                         .Append(argb)
                         .Append("\"/><bgColor indexed=\"64\"/></patternFill></fill>");
        }

        private static string BuildWorkbookXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                   "<sheets><sheet name=\"Naming\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
                   "</workbook>";
        }

        private static string BuildWorkbookRelsXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                   "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                   "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                   "</Relationships>";
        }

        private static string BuildRootRelsXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                   "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                   "</Relationships>";
        }

        private static string BuildContentTypesXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                   "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                   "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                   "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                   "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                   "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                   "</Types>";
        }

        private static void AppendInlineStringCell(StringBuilder stringBuilder, string cellReference, string value, int styleIndex)
        {
            stringBuilder.Append("<c r=\"")
                         .Append(cellReference)
                         .Append("\" t=\"inlineStr\" s=\"")
                         .Append(styleIndex)
                         .Append("\"><is><t>")
                         .Append(EscapeXmlPreservingSpaces(value))
                         .Append("</t></is></c>");
        }

        private static string EscapeXmlPreservingSpaces(string value)
        {
            string text = value ?? string.Empty;
            return SecurityElement.Escape(text) ?? string.Empty;
        }

        private static string GetCellValue(List<string> row, int index)
        {
            if (row == null || index < 0 || index >= row.Count)
            {
                return string.Empty;
            }

            return row[index] ?? string.Empty;
        }

        private static string ToColumnName(int number)
        {
            StringBuilder result = new StringBuilder();

            while (number > 0)
            {
                int modulo = (number - 1) % 26;
                result.Insert(0, (char)('A' + modulo));
                number = (number - modulo - 1) / 26;
            }

            return result.ToString();
        }

        private static void WriteEntry(ZipArchive archive, string path, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);

            using (Stream stream = entry.Open())
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(content);
            }
        }
    }
}
