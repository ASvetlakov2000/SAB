using Autodesk.Revit.DB;
using System.IO;
using System.Text;
using System.Linq;

namespace RevitLibraryBuilder.Services.Csv
{
    /// <summary>
    /// Экспорт Fill Patterns (Model + Drafting)
    /// </summary>
    public class CsvFillPatternExportService
    {
        public void Export(Document doc, string folderPath)
        {
            string filePath = Path.Combine(folderPath, "FillPatterns.csv");

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Name,Type,IsSolid,GridCount");

            var patterns = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>();

            foreach (var p in patterns)
            {
                FillPattern fp = p.GetFillPattern();

                if (fp == null)
                    continue;

                string type = fp.Target == FillPatternTarget.Model
                    ? "Model"
                    : "Drafting";

                int gridCount = 0;

                try
                {
                    gridCount = fp.GetFillGrids().Count;
                }
                catch
                {
                    gridCount = 0;
                }

                sb.AppendLine(
                    $"{Escape(p.Name)}," +
                    $"{type}," +
                    $"{fp.IsSolidFill}," +
                    $"{gridCount}"
                );
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            if (value.Contains(",") || value.Contains("\""))
                return $"\"{value.Replace("\"", "\"\"")}\"";

            return value;
        }
    }
}