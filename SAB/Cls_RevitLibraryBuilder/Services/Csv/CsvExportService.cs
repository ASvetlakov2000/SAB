using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RevitLibraryBuilder.Services.Csv
{
    public class CsvExportService
    {
        public void ExportToCsv(List<ElementType> types, string path)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Category,Family,TypeName,Include");

            foreach (var t in types.Where(x => x.Category != null))
            {
                sb.AppendLine($"{t.Category.Name},{t.FamilyName},{t.Name},TRUE");
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
    }
}