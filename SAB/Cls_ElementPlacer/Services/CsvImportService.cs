using RevitLibraryBuilder.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RevitLibraryBuilder.Services
{
    public class CsvImportService
    {
        public List<ElementTypeCsvModel> ImportFromCsv(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"CSV файл не найден: {filePath}");

            var lines = File.ReadAllLines(filePath);

            if (lines.Length <= 1)
                return new List<ElementTypeCsvModel>();

            var result = new List<ElementTypeCsvModel>();

            foreach (var line in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = ParseCsvLine(line);

                if (parts.Length < 4)
                    continue;

                bool include = parts[3].Trim().ToUpper() == "TRUE";

                result.Add(new ElementTypeCsvModel
                {
                    Category = parts[0].Trim(),
                    Family = parts[1].Trim(),
                    TypeName = parts[2].Trim(),
                    Include = include
                });
            }

            return result;
        }

        private string[] ParseCsvLine(string line)
        {
            var parts = line.Split(',');
            for (int i = 0; i < parts.Length; i++)
                parts[i] = parts[i].Trim().Trim('"');
            return parts;
        }
    }
}