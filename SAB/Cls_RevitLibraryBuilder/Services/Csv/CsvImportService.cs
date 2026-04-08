using RevitLibraryBuilder.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RevitLibraryBuilder.Services.Csv
{
    public class CsvImportService
    {
        public List<ElementTypeCsvModel> ImportFromCsv(string path)
        {
            var lines = File.ReadAllLines(path);

            return lines
                .Skip(1)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l =>
                {
                    var p = l.Split(',');

                    return new ElementTypeCsvModel
                    {
                        Category = p[0],
                        Family = p[1],
                        TypeName = p[2],
                        Include = p[3].ToUpper() == "TRUE"
                    };
                })
                .ToList();
        }
    }
}