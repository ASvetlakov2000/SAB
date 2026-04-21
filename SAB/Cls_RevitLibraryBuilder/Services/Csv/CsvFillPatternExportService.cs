using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services;
using asBIM;

namespace RevitLibraryBuilder.Services.Csv
{
    public class CsvFillPatternExportService
    {
        public Result ExecuteExport(ExternalCommandData commandData, ref string message)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Активный UIDocument недоступен.";
                    TaskDialog.Show("Экспорт линий и штриховок", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null)
                {
                    message = "Документ недоступен.";
                    TaskDialog.Show("Экспорт линий и штриховок", message);
                    return Result.Failed;
                }

                string selectedFolderPath = RequestExportFolderPath();

                if (string.IsNullOrWhiteSpace(selectedFolderPath))
                {
                    return Result.Cancelled;
                }

                string folderPath = ExportFolderRoutingService.ResolveCategoryExportFolder(selectedFolderPath);

                ExportLineStyles(document, folderPath);
                ExportFillPatterns(document, folderPath);

                ShowFolderSuccessNotification(
                    "Export completed",
                    "Line Styles and Fill Patterns were exported:",
                    folderPath);

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Экспорт линий и штриховок", exception.ToString());
                return Result.Failed;
            }
        }

        public string RequestImportFilePath()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select CSV file with fill patterns";
                dialog.Filter = "Файл CSV (*.csv)|*.csv";
                dialog.Multiselect = false;

                DialogResult dialogResult = dialog.ShowDialog();

                if (dialogResult != DialogResult.OK)
                {
                    return null;
                }

                return dialog.FileName;
            }
        }

        public List<FillPatternCsvRecord> ImportFillPatternRows(string csvFilePath)
        {
            List<FillPatternCsvRecord> result = new List<FillPatternCsvRecord>();

            if (string.IsNullOrWhiteSpace(csvFilePath) || !File.Exists(csvFilePath))
            {
                return result;
            }

            string[] lines = File.ReadAllLines(csvFilePath);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (i == 0 && line.IndexOf("Name", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                List<string> values = ParseCsvLine(line);

                if (values.Count < 5)
                {
                    continue;
                }

                FillPatternCsvRecord record = new FillPatternCsvRecord();
                record.RowIndex = i + 1;
                record.Name = GetValue(values, 0);
                record.ForegroundPattern = GetValue(values, 1);
                record.BackgroundPattern = GetValue(values, 2);
                record.IsMasking = ParseBoolean(GetValue(values, 3));
                record.Target = NormalizeTarget(GetValue(values, 4));

                if (string.IsNullOrWhiteSpace(record.Name))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(record.ForegroundPattern))
                {
                    record.ForegroundPattern = record.Name;
                }

                result.Add(record);
            }

            return result;
        }

        // Block responsible for exporting fill patterns to the required stable CSV schema
        public void ExportFillPatterns(Document document, string folderPath)
        {
            string filePath = Path.Combine(folderPath, "FillPatterns.csv");
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.AppendLine("Name,ForegroundPattern,BackgroundPattern,IsMasking,Target");
            List<FilledRegionType> regionTypes = CollectFilledRegionTypes(document);

            List<FillPatternElement> draftingPatterns = new List<FillPatternElement>();
            List<FillPatternElement> modelPatterns = new List<FillPatternElement>();

            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(FillPatternElement));

            foreach (Element element in collector)
            {
                FillPatternElement fillPatternElement = element as FillPatternElement;

                if (fillPatternElement == null)
                {
                    continue;
                }

                FillPattern fillPattern = fillPatternElement.GetFillPattern();

                if (fillPattern == null)
                {
                    continue;
                }

                if (fillPattern.Target == FillPatternTarget.Drafting)
                {
                    draftingPatterns.Add(fillPatternElement);
                }
                else
                {
                    modelPatterns.Add(fillPatternElement);
                }
            }

            WritePatternRows(stringBuilder, draftingPatterns, "Drafting", regionTypes, document);
            WritePatternRows(stringBuilder, modelPatterns, "Model", regionTypes, document);

            File.WriteAllText(filePath, stringBuilder.ToString(), Encoding.UTF8);
        }

        // Block responsible for exporting line styles into CSV using the current project structure
        private static void ExportLineStyles(Document document, string folderPath)
        {
            string filePath = Path.Combine(folderPath, "LineStyles.csv");
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.AppendLine("Name,Category,LineWeight,ColorR,ColorG,ColorB,Pattern");

            Category linesCategory = document.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);

            if (linesCategory == null)
            {
                File.WriteAllText(filePath, stringBuilder.ToString(), Encoding.UTF8);
                return;
            }

            foreach (Category subCategory in linesCategory.SubCategories)
            {
                if (subCategory == null)
                {
                    continue;
                }

                GraphicsStyle graphicsStyle = subCategory.GetGraphicsStyle(GraphicsStyleType.Projection);

                if (graphicsStyle == null)
                {
                    continue;
                }

                Category graphicsStyleCategory = graphicsStyle.GraphicsStyleCategory;

                if (graphicsStyleCategory == null)
                {
                    continue;
                }

                Color color = graphicsStyleCategory.LineColor;
                int red = 0;
                int green = 0;
                int blue = 0;

                if (color != null && color.IsValid)
                {
                    red = color.Red;
                    green = color.Green;
                    blue = color.Blue;
                }

                string patternName = "Solid";
                ElementId patternId = graphicsStyleCategory.GetLinePatternId(GraphicsStyleType.Projection);

                if (patternId != ElementId.InvalidElementId)
                {
                    LinePatternElement pattern = document.GetElement(patternId) as LinePatternElement;

                    if (pattern != null)
                    {
                        patternName = pattern.Name;
                    }
                }

                stringBuilder.AppendLine(
                    Escape(graphicsStyleCategory.Name) + "," +
                    Escape(graphicsStyleCategory.Name) + "," +
                    "0," +
                    red + "," +
                    green + "," +
                    blue + "," +
                    Escape(patternName));
            }

            File.WriteAllText(filePath, stringBuilder.ToString(), Encoding.UTF8);
        }

        private static void WritePatternRows(
            StringBuilder stringBuilder,
            List<FillPatternElement> patterns,
            string target,
            List<FilledRegionType> regionTypes,
            Document document)
        {
            for (int i = 0; i < patterns.Count; i++)
            {
                FillPatternElement pattern = patterns[i];
                string name = pattern.Name;
                string foregroundPattern = name;
                string backgroundPattern = string.Empty;
                bool isMasking = false;

                FilledRegionType relatedType = FindRelatedFilledRegionType(regionTypes, pattern.Id);

                if (relatedType != null)
                {
                    foregroundPattern = GetPatternName(document, relatedType.ForegroundPatternId, name);
                    backgroundPattern = GetPatternName(document, relatedType.BackgroundPatternId, string.Empty);
                    isMasking = relatedType.IsMasking;
                }

                stringBuilder.AppendLine(
                    Escape(name) + "," +
                    Escape(foregroundPattern) + "," +
                    Escape(backgroundPattern) + "," +
                    isMasking.ToString().ToLowerInvariant() + "," +
                    Escape(target));
            }
        }

        private static List<FilledRegionType> CollectFilledRegionTypes(Document document)
        {
            List<FilledRegionType> types = new List<FilledRegionType>();

            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(FilledRegionType));

            foreach (Element element in collector)
            {
                FilledRegionType type = element as FilledRegionType;

                if (type != null)
                {
                    types.Add(type);
                }
            }

            return types;
        }

        private static FilledRegionType FindRelatedFilledRegionType(
            List<FilledRegionType> regionTypes,
            ElementId patternId)
        {
            if (patternId == null || patternId == ElementId.InvalidElementId)
            {
                return null;
            }

            for (int i = 0; i < regionTypes.Count; i++)
            {
                FilledRegionType type = regionTypes[i];

                if (type.ForegroundPatternId == patternId || type.BackgroundPatternId == patternId)
                {
                    return type;
                }
            }

            return null;
        }

        private static string GetPatternName(Document document, ElementId patternId, string fallback)
        {
            if (patternId == null || patternId == ElementId.InvalidElementId)
            {
                return fallback;
            }

            FillPatternElement pattern = document.GetElement(patternId) as FillPatternElement;

            if (pattern == null)
            {
                return fallback;
            }

            return pattern.Name;
        }

        // Block responsible for selecting the folder for CSV export
        private static string RequestExportFolderPath()
        {
            return OpenFolder.SelectFolderPath(
                "Select folder for Line Styles and Fill Patterns export",
                "ctg");
        }

        private static string GetValue(List<string> values, int index)
        {
            if (index < 0 || index >= values.Count)
            {
                return string.Empty;
            }

            return values[index];
        }

        private static bool ParseBoolean(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            bool parsedValue;

            if (bool.TryParse(value, out parsedValue))
            {
                return parsedValue;
            }

            string normalizedValue = value.Trim().ToUpperInvariant();

            return normalizedValue == "1" ||
                   normalizedValue == "TRUE" ||
                   normalizedValue == "YES";
        }

        private static string NormalizeTarget(string value)
        {
            if (string.Equals(value, "Model", StringComparison.OrdinalIgnoreCase))
            {
                return "Model";
            }

            return "Drafting";
        }

        // Block responsible for parsing CSV lines with quoted values
        private static List<string> ParseCsvLine(string line)
        {
            List<string> values = new List<string>();
            StringBuilder currentValue = new StringBuilder();
            bool insideQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char currentChar = line[i];

                if (currentChar == '"')
                {
                    if (insideQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentValue.Append('"');
                        i++;
                    }
                    else
                    {
                        insideQuotes = !insideQuotes;
                    }

                    continue;
                }

                if (currentChar == ',' && !insideQuotes)
                {
                    values.Add(currentValue.ToString().Trim());
                    currentValue.Clear();
                    continue;
                }

                currentValue.Append(currentChar);
            }

            values.Add(currentValue.ToString().Trim());

            return values;
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Contains(",") || value.Contains("\""))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }

        // Block responsible for post-execution notification
        private static void ShowFolderSuccessNotification(string title, string message, string folderPath)
        {
            try
            {
                ToastNotifier.ShowFolderLinkSuccess(title, message, folderPath, 10);
            }
            catch
            {
                TaskDialog.Show(title, message + "\n" + folderPath);
            }
        }
    }

    public class FillPatternCsvRecord
    {
        public int RowIndex { get; set; }

        public string Name { get; set; }

        public string ForegroundPattern { get; set; }

        public string BackgroundPattern { get; set; }

        public bool IsMasking { get; set; }

        public string Target { get; set; }
    }
}
