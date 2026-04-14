using Autodesk.Revit.DB;
using RevitLibraryBuilder.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace RevitLibraryBuilder.Services.Csv
{
    /// <summary>
    /// Сервис XLSX/CSV импорта и XLSX экспорта для переименования типоразмеров.
    /// </summary>
    public class TypeNamingCsvService
    {
        private readonly CsvTableService _csvTableService;
        private readonly NamingSpreadsheetExportService _namingSpreadsheetExportService;
        private readonly NamingSpreadsheetImportService _namingSpreadsheetImportService;

        public TypeNamingCsvService()
        {
            _csvTableService = new CsvTableService();
            _namingSpreadsheetExportService = new NamingSpreadsheetExportService();
            _namingSpreadsheetImportService = new NamingSpreadsheetImportService();
        }

        public List<TypeNamingCsvModel> ImportRows(string filePath)
        {
            CsvTable table = ReadNamingTable(filePath);

            // Блок отвечает за чтение столбцов из XLSX файла
            table.ValidateRequiredColumns(new List<string>
            {
                "Category",
                "Family_Old",
                "Family_New",
                "TypeName_Old",
                "TypeName_New"
            });

            int categoryIndex = table.FindHeaderIndex("Category");
            int familyOldIndex = table.FindHeaderIndex("Family_Old");
            int familyNewIndex = table.FindHeaderIndex("Family_New");
            int typeOldIndex = table.FindHeaderIndex("TypeName_Old");
            int typeNewIndex = table.FindHeaderIndex("TypeName_New");

            List<TypeNamingCsvModel> result = new List<TypeNamingCsvModel>();

            // Здесь нельзя менять порядок строк, так как переименование идет построчно
            for (int i = 0; i < table.Rows.Count; i++)
            {
                CsvTableRow row = table.Rows[i];

                TypeNamingCsvModel model = new TypeNamingCsvModel
                {
                    RowIndex = row.RowIndex,
                    Category = row.GetValue(categoryIndex),
                    FamilyOld = row.GetValue(familyOldIndex),
                    FamilyNew = row.GetValue(familyNewIndex),
                    TypeNameOld = row.GetValue(typeOldIndex),
                    TypeNameNew = row.GetValue(typeNewIndex)
                };

                if (string.IsNullOrWhiteSpace(model.TypeNameOld))
                {
                    continue;
                }

                result.Add(model);
            }

            return result;
        }

        public string WriteTypeNamingXlsx(string outputFolder, string documentTitle, List<ElementType> types)
        {
            if (types == null || types.Count == 0)
            {
                throw new ArgumentException("Element type list is empty.");
            }

            List<TypeNamingCsvModel> namingRows = BuildNamingRows(types);

            // Блок отвечает за настройку состава столбцов выгрузки
            List<string> headers = new List<string>
            {
                "Category",
                "Family_Old",
                "Family_New",
                "TypeName_Old",
                "TypeName_New"
            };

            List<List<string>> rows = new List<List<string>>();

            for (int i = 0; i < namingRows.Count; i++)
            {
                TypeNamingCsvModel row = namingRows[i];

                rows.Add(new List<string>
                {
                    row.Category,
                    row.FamilyOld,
                    row.FamilyNew,
                    row.TypeNameOld,
                    row.TypeNameNew
                });
            }

            string safeDocumentName = MakeSafeFileName(documentTitle);
            string fileName = safeDocumentName + "_TYPE_NAMING.xlsx";

            return _namingSpreadsheetExportService.WriteNamingWorkbook(outputFolder, fileName, headers, rows);
        }

        public string WriteErrorReport(string importFilePath, List<NamingErrorCsvModel> errors)
        {
            if (errors == null || errors.Count == 0)
            {
                return string.Empty;
            }

            string folder = Path.GetDirectoryName(importFilePath);

            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            }

            string filePath = Path.Combine(folder, "Проблемные наименования.csv");

            List<string> header = new List<string> { "OldName", "NewName", "ErrorText" };
            List<List<string>> rows = new List<List<string>>();

            for (int i = 0; i < errors.Count; i++)
            {
                NamingErrorCsvModel error = errors[i];

                rows.Add(new List<string>
                {
                    error.OldName,
                    error.NewName,
                    error.ErrorText
                });
            }

            _csvTableService.Write(filePath, header, rows);
            return filePath;
        }

        private CsvTable ReadNamingTable(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Input file path is empty.");
            }

            string extension = Path.GetExtension(filePath) ?? string.Empty;

            if (string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return _namingSpreadsheetImportService.ReadAsTable(filePath);
            }

            // Поддержка CSV оставлена как безопасный fallback совместимости
            return _csvTableService.Read(filePath);
        }

        // Блок подготовки строк для XLSX с сохранением исходного порядка
        private static List<TypeNamingCsvModel> BuildNamingRows(List<ElementType> types)
        {
            List<TypeNamingCsvModel> result = new List<TypeNamingCsvModel>();

            for (int i = 0; i < types.Count; i++)
            {
                ElementType type = types[i];

                if (type == null || type.Category == null)
                {
                    continue;
                }

                string category = type.Category.Name ?? string.Empty;
                string family = type.FamilyName ?? string.Empty;
                string typeName = type.Name ?? string.Empty;

                result.Add(new TypeNamingCsvModel
                {
                    Category = category,
                    FamilyOld = family,
                    FamilyNew = family,
                    TypeNameOld = typeName,
                    TypeNameNew = typeName
                });
            }

            return result;
        }

        private static string MakeSafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Unnamed";
            }

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalidChar, '_');
            }

            return name;
        }
    }
}
