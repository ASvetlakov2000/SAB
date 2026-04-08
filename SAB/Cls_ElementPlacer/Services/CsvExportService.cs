using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RevitLibraryBuilder.Services
{
    public class CsvExportService
    {
        public void ExportToCsv(List<ElementType> types, string filePath)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Category,Family,TypeName,Include");

            var sortedTypes = types
                .Where(t => t.Category != null)
                .OrderBy(t => t.Category.Name)
                .ThenBy(t => GetFamilyName(t))
                .ThenBy(t => t.Name);

            foreach (ElementType type in sortedTypes)
            {
                string category = Escape(type.Category.Name);
                string family = Escape(GetFamilyName(type));
                string typeName = Escape(type.Name);
                string include = "TRUE"; 

                sb.AppendLine($"{category},{family},{typeName},{include}");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private string GetFamilyName(ElementType type)
        {
            return string.IsNullOrEmpty(type.FamilyName) ? string.Empty : type.FamilyName;
        }

        private string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\""))
                value = $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}