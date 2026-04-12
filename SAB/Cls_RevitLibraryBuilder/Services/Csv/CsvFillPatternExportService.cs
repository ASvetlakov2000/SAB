using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;

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
                    message = "Active UIDocument is not available.";
                    TaskDialog.Show("Export Line And Fill Patterns", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null)
                {
                    message = "Document is not available.";
                    TaskDialog.Show("Export Line And Fill Patterns", message);
                    return Result.Failed;
                }

                string folderPath = RequestExportFolderPath();

                if (string.IsNullOrWhiteSpace(folderPath))
                {
                    return Result.Cancelled;
                }

                ExportLineStyles(document, folderPath);
                ExportFilledRegionTypes(document, folderPath);

                ShowFolderSuccessNotification(
                    "Экспорт завершён",
                    "Line Styles и FilledRegionTypes экспортированы:",
                    folderPath);

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Export Line And Fill Patterns", exception.ToString());
                return Result.Failed;
            }
        }

        public string RequestImportFilePath()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select CSV file with filled region types";
                dialog.Filter = "CSV (*.csv)|*.csv";
                dialog.Multiselect = false;

                DialogResult dialogResult = dialog.ShowDialog();

                if (dialogResult != DialogResult.OK)
                {
                    return null;
                }

                return dialog.FileName;
            }
        }

        public void ExportFilledRegionTypes(Document document, string folderPath)
        {
            string filePath = Path.Combine(folderPath, "FilledRegionTypes.csv");
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.AppendLine("Name,ForegroundPattern,BackgroundPattern,IsMasking");

            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(FilledRegionType));

            foreach (Element element in collector)
            {
                FilledRegionType filledRegionType = element as FilledRegionType;

                if (filledRegionType == null)
                {
                    continue;
                }

                string foregroundPatternName = GetFillPatternName(document, filledRegionType.ForegroundPatternId);
                string backgroundPatternName = GetFillPatternName(document, filledRegionType.BackgroundPatternId);

                stringBuilder.AppendLine(
                    Escape(filledRegionType.Name) + "," +
                    Escape(foregroundPatternName) + "," +
                    Escape(backgroundPatternName) + "," +
                    filledRegionType.IsMasking);
            }

            File.WriteAllText(filePath, stringBuilder.ToString(), Encoding.UTF8);
        }

        public List<FilledRegionTypeCsvModel> ImportFilledRegionTypes(string csvFilePath)
        {
            List<FilledRegionTypeCsvModel> result = new List<FilledRegionTypeCsvModel>();

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

                if (values.Count == 0)
                {
                    continue;
                }

                FilledRegionTypeCsvModel model = new FilledRegionTypeCsvModel();
                model.Name = GetValue(values, 0);
                model.ForegroundPatternName = GetValue(values, 1);
                model.BackgroundPatternName = GetValue(values, 2);
                model.IsMasking = ParseBoolean(GetValue(values, 3));

                if (string.IsNullOrWhiteSpace(model.Name))
                {
                    continue;
                }

                result.Add(model);
            }

            return result;
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

        // Block responsible for selecting the folder for CSV export
        private static string RequestExportFolderPath()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Выберите папку для экспорта Line Styles и Filled Region Types";

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return null;
                }

                return dialog.SelectedPath;
            }
        }

        private static string GetFillPatternName(Document document, ElementId patternId)
        {
            if (patternId == null || patternId == ElementId.InvalidElementId)
            {
                return string.Empty;
            }

            FillPatternElement fillPatternElement = document.GetElement(patternId) as FillPatternElement;

            if (fillPatternElement == null)
            {
                return string.Empty;
            }

            return fillPatternElement.Name;
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

    public class FilledRegionTypeCsvModel
    {
        public string Name { get; set; }

        public string ForegroundPatternName { get; set; }

        public string BackgroundPatternName { get; set; }

        public bool IsMasking { get; set; }
    }
}
