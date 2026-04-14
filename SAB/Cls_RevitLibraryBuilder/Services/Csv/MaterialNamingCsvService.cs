using RevitLibraryBuilder.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace RevitLibraryBuilder.Services.Csv
{
    /// <summary>
    /// Сервис экспорта/импорта CSV для материалов.
    /// </summary>
    public class MaterialNamingCsvService
    {
        private readonly CsvTableService _csvTableService;

        public MaterialNamingCsvService()
        {
            _csvTableService = new CsvTableService();
        }

        public List<MaterialNamingCsvModel> ImportRows(string filePath)
        {
            CsvTable table = _csvTableService.Read(filePath);
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

            List<MaterialNamingCsvModel> result = new List<MaterialNamingCsvModel>();

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

        public string WriteMaterialCsv(string outputFolder, string documentTitle, List<MaterialNamingCsvModel> rows)
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
            string filePath = Path.Combine(outputFolder, safeDocumentName + "_MATERIAL_NAMING.csv");

            List<string> header = new List<string>
            {
                "MaterialName_Old",
                "MaterialName_New",
                "Description_Old",
                "Description_New",
                "DeleteMaterial"
            };

            List<List<string>> csvRows = new List<List<string>>();

            for (int i = 0; i < rows.Count; i++)
            {
                MaterialNamingCsvModel row = rows[i];

                csvRows.Add(new List<string>
                {
                    row.MaterialNameOld,
                    row.MaterialNameNew,
                    row.DescriptionOld,
                    row.DescriptionNew,
                    row.DeleteMaterial ? "TRUE" : "FALSE"
                });
            }

            _csvTableService.Write(filePath, header, csvRows);
            return filePath;
        }

        public string WriteErrorReport(string importCsvPath, List<NamingErrorCsvModel> errors)
        {
            if (errors == null || errors.Count == 0)
            {
                return string.Empty;
            }

            string folder = Path.GetDirectoryName(importCsvPath);

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

        // Блок преобразования строкового признака удаления в bool
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
