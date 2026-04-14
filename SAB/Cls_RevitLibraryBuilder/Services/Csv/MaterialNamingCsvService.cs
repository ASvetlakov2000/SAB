using RevitLibraryBuilder.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace RevitLibraryBuilder.Services.Csv
{
    /// <summary>
    /// Сервис XLSX/CSV импорта и XLSX экспорта для материалов.
    /// </summary>
    public class MaterialNamingCsvService
    {
        private readonly CsvTableService _csvTableService;
        private readonly NamingSpreadsheetExportService _namingSpreadsheetExportService;
        private readonly NamingSpreadsheetImportService _namingSpreadsheetImportService;

        public MaterialNamingCsvService()
        {
            _csvTableService = new CsvTableService();
            _namingSpreadsheetExportService = new NamingSpreadsheetExportService();
            _namingSpreadsheetImportService = new NamingSpreadsheetImportService();
        }

        public List<MaterialNamingCsvModel> ImportRows(string filePath)
        {
            CsvTable table = ReadNamingTable(filePath);

            // Блок отвечает за чтение столбцов из XLSX файла
            table.ValidateRequiredColumns(new List<string>
            {
                "MaterialName_Old",
                "MaterialName_New",
                "Description_Old",
                "Description_New",
                "DeleteMaterial"
            });

            int nameOldIndex = table.FindHeaderIndex("MaterialName_Old");
            int nameNewIndex = table.FindHeaderIndex("MaterialName_New");
            int descriptionOldIndex = table.FindHeaderIndex("Description_Old");
            int descriptionNewIndex = table.FindHeaderIndex("Description_New");
            int deleteIndex = table.FindHeaderIndex("DeleteMaterial");

            // Блок сопоставления новых пользовательских столбцов
            int manufacturerIndex = FindAnyHeaderIndex(table, "Изготовитель", "Manufacturer");
            int modelIndex = FindAnyHeaderIndex(table, "Модель", "Model");
            int keynoteIndex = FindAnyHeaderIndex(table, "Ключевая заметка", "Keynote");
            int markingIndex = FindAnyHeaderIndex(table, "Маркировка", "Marking");

            List<MaterialNamingCsvModel> result = new List<MaterialNamingCsvModel>();

            // Здесь нельзя менять порядок строк, так как операции выполняются построчно
            for (int i = 0; i < table.Rows.Count; i++)
            {
                CsvTableRow row = table.Rows[i];

                MaterialNamingCsvModel model = new MaterialNamingCsvModel
                {
                    RowIndex = row.RowIndex,
                    MaterialNameOld = row.GetValue(nameOldIndex),
                    MaterialNameNew = row.GetValue(nameNewIndex),
                    DescriptionOld = row.GetValue(descriptionOldIndex),
                    DescriptionNew = row.GetValue(descriptionNewIndex),
                    Manufacturer = row.GetValue(manufacturerIndex),
                    Model = row.GetValue(modelIndex),
                    Keynote = row.GetValue(keynoteIndex),
                    Marking = row.GetValue(markingIndex),
                    DeleteMaterial = ParseBoolean(row.GetValue(deleteIndex))
                };

                if (string.IsNullOrWhiteSpace(model.MaterialNameOld))
                {
                    continue;
                }

                result.Add(model);
            }

            return result;
        }

        public string WriteMaterialXlsx(string outputFolder, string documentTitle, List<MaterialNamingCsvModel> rows)
        {
            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                throw new ArgumentException("Output folder is empty.");
            }

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            string safeDocumentName = MakeSafeFileName(documentTitle);
            string fileName = safeDocumentName + "_MATERIAL_NAMING.xlsx";

            // Блок отвечает за настройку состава столбцов выгрузки
            List<string> headers = new List<string>
            {
                "MaterialName_Old",
                "MaterialName_New",
                "Description_Old",
                "Description_New",
                "Изготовитель",
                "Модель",
                "Ключевая заметка",
                "Маркировка",
                "DeleteMaterial"
            };

            List<List<string>> dataRows = new List<List<string>>();

            for (int i = 0; i < rows.Count; i++)
            {
                MaterialNamingCsvModel row = rows[i];

                dataRows.Add(new List<string>
                {
                    row.MaterialNameOld,
                    row.MaterialNameNew,
                    row.DescriptionOld,
                    row.DescriptionNew,
                    row.Manufacturer,
                    row.Model,
                    row.Keynote,
                    row.Marking,
                    row.DeleteMaterial ? "TRUE" : "FALSE"
                });
            }

            return _namingSpreadsheetExportService.WriteNamingWorkbook(outputFolder, fileName, headers, dataRows);
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

        private static int FindAnyHeaderIndex(CsvTable table, params string[] names)
        {
            if (table == null || names == null)
            {
                return -1;
            }

            for (int i = 0; i < names.Length; i++)
            {
                int index = table.FindHeaderIndex(names[i]);

                if (index >= 0)
                {
                    return index;
                }
            }

            return -1;
        }

        // Блок преобразования признака удаления материала в bool
        // Здесь нельзя удалять по умолчанию: только явное положительное значение
        private static bool ParseBoolean(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim().ToUpperInvariant();

            return normalized == "TRUE" ||
                   normalized == "1" ||
                   normalized == "YES" ||
                   normalized == "ДА";
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
