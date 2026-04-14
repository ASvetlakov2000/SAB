using RevitLibraryBuilder.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace RevitLibraryBuilder.Services.Csv
{
    /// <summary>
    /// Сервис экспорта/импорта CSV для переименования типоразмеров.
    /// </summary>
    public class TypeNamingCsvService
    {
        private readonly CsvTableService _csvTableService;

        public TypeNamingCsvService()
        {
            _csvTableService = new CsvTableService();
        }

        public List<TypeNamingCsvModel> ImportRows(string filePath)
        {
            CsvTable table = _csvTableService.Read(filePath);
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
    }
}
