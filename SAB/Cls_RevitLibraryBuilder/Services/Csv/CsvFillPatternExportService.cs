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
        private readonly CsvTableService _csvTableService = new CsvTableService();

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

                string folderPath = ExportFolderRoutingService.ResolveLineFillExportFolder(selectedFolderPath);

                ExportLineStylesCsv(document, folderPath);
                ExportFillPatternsCsv(document, folderPath);

                ShowFolderSuccessNotification(
                    "Экспорт завершен",
                    "Файлы линий и штриховок сохранены:",
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

        public string ExportLineStylesCsv(Document document, string selectedFolderPath)
        {
            string folderPath = ExportFolderRoutingService.ResolveLineFillExportFolder(selectedFolderPath);
            string modelPrefix = BuildSafeFileNamePart(document != null ? document.Title : string.Empty);
            string filePath = Path.Combine(folderPath, modelPrefix + "_Линии.csv");
            List<List<string>> rows = BuildLineStyleRows(document);

            List<string> header = new List<string>
            {
                "Наименование",
                "Миниатюра",
                "Категория",
                "Вес линии",
                "Цвет",
                "Образец"
            };

            _csvTableService.Write(filePath, header, rows);
            return filePath;
        }

        public string ExportFillPatternsCsv(Document document, string selectedFolderPath)
        {
            string folderPath = ExportFolderRoutingService.ResolveLineFillExportFolder(selectedFolderPath);
            string modelPrefix = BuildSafeFileNamePart(document != null ? document.Title : string.Empty);
            string filePath = Path.Combine(folderPath, modelPrefix + "_Штриховки.csv");

            List<string> header = new List<string>
            {
                "Наименование",
                "Миниатюра",
                "Штриховка переднего плана",
                "Штриховка заднего плана",
                "Маскирование",
                "Тип штриховки"
            };

            List<List<string>> rows = BuildFillPatternRows(document);
            _csvTableService.Write(filePath, header, rows);
            return filePath;
        }

        public string RequestImportFilePath()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Выберите CSV файл со штриховками";
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

            CsvTable table = _csvTableService.Read(csvFilePath);

            int nameIndex = FindColumnIndex(table, "Наименование", "Name");
            int foregroundIndex = FindColumnIndex(table, "Штриховка переднего плана", "ForegroundPattern");
            int backgroundIndex = FindColumnIndex(table, "Штриховка заднего плана", "BackgroundPattern");
            int maskingIndex = FindColumnIndex(table, "Маскирование", "IsMasking");
            int targetIndex = FindColumnIndex(table, "Тип штриховки", "Target");

            if (nameIndex < 0)
            {
                return result;
            }

            for (int i = 0; i < table.Rows.Count; i++)
            {
                CsvTableRow row = table.Rows[i];

                if (row == null)
                {
                    continue;
                }

                string name = row.GetValue(nameIndex);

                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                string foreground = foregroundIndex >= 0 ? row.GetValue(foregroundIndex) : string.Empty;
                string background = backgroundIndex >= 0 ? row.GetValue(backgroundIndex) : string.Empty;
                string maskingText = maskingIndex >= 0 ? row.GetValue(maskingIndex) : string.Empty;
                string targetText = targetIndex >= 0 ? row.GetValue(targetIndex) : string.Empty;

                if (string.IsNullOrWhiteSpace(foreground))
                {
                    foreground = name;
                }

                FillPatternCsvRecord record = new FillPatternCsvRecord
                {
                    RowIndex = row.RowIndex,
                    Name = name,
                    ForegroundPattern = foreground,
                    BackgroundPattern = background,
                    IsMasking = ParseBoolean(maskingText),
                    Target = NormalizeTarget(targetText)
                };

                result.Add(record);
            }

            return result;
        }

        private static int FindColumnIndex(CsvTable table, params string[] aliases)
        {
            if (table == null || aliases == null)
            {
                return -1;
            }

            for (int i = 0; i < aliases.Length; i++)
            {
                string alias = aliases[i];

                if (string.IsNullOrWhiteSpace(alias))
                {
                    continue;
                }

                int index = table.FindHeaderIndex(alias);

                if (index >= 0)
                {
                    return index;
                }
            }

            return -1;
        }

        private static List<List<string>> BuildLineStyleRows(Document document)
        {
            List<List<string>> rows = new List<List<string>>();

            if (document == null)
            {
                return rows;
            }

            Category linesCategory = document.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);

            if (linesCategory == null)
            {
                return rows;
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

                Color color = subCategory.LineColor;
                int red = 0;
                int green = 0;
                int blue = 0;

                if (color != null && color.IsValid)
                {
                    red = color.Red;
                    green = color.Green;
                    blue = color.Blue;
                }

                string colorText = red + ", " + green + ", " + blue;
                string patternName = "Сплошная";

                ElementId patternId = subCategory.GetLinePatternId(GraphicsStyleType.Projection);

                if (patternId != ElementId.InvalidElementId)
                {
                    LinePatternElement pattern = document.GetElement(patternId) as LinePatternElement;

                    if (pattern != null && !string.IsNullOrWhiteSpace(pattern.Name))
                    {
                        patternName = pattern.Name;
                    }
                }

                int lineWeight = 0;

                try
                {
                    int? nullableWeight = subCategory.GetLineWeight(GraphicsStyleType.Projection);
                    lineWeight = nullableWeight ?? 0;
                }
                catch
                {
                    lineWeight = 0;
                }

                string styleName = subCategory.Name ?? string.Empty;

                rows.Add(new List<string>
                {
                    styleName,
                    ThumbnailPathResolverService.ResolveForLineStyle(styleName),
                    linesCategory.Name,
                    lineWeight.ToString(),
                    colorText,
                    patternName
                });
            }

            return rows;
        }

        private static List<List<string>> BuildFillPatternRows(Document document)
        {
            List<List<string>> rows = new List<List<string>>();
            List<FilledRegionType> regionTypes = CollectFilledRegionTypes(document);

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

                string name = fillPatternElement.Name ?? string.Empty;
                string foreground = name;
                string background = string.Empty;
                bool masking = false;
                string target = fillPattern.Target == FillPatternTarget.Drafting ? "Чертежная" : "Модельная";

                FilledRegionType relatedType = FindRelatedFilledRegionType(regionTypes, fillPatternElement.Id);

                if (relatedType != null)
                {
                    foreground = GetPatternName(document, relatedType.ForegroundPatternId, name);
                    background = GetPatternName(document, relatedType.BackgroundPatternId, string.Empty);
                    masking = relatedType.IsMasking;
                }

                rows.Add(new List<string>
                {
                    name,
                    ThumbnailPathResolverService.ResolveForFillPattern(name),
                    foreground,
                    background,
                    masking ? "Да" : "Нет",
                    target
                });
            }

            return rows;
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

        private static FilledRegionType FindRelatedFilledRegionType(List<FilledRegionType> regionTypes, ElementId patternId)
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

        private static string RequestExportFolderPath()
        {
            return OpenFolder.SelectFolderPath(
                "Выберите папку для экспорта линий и штриховок",
                "ctg_lines-patterns");
        }

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

        private static string NormalizeTarget(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Drafting";
            }

            string normalized = value.Trim();

            if (normalized.Equals("Модельная", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Model", StringComparison.OrdinalIgnoreCase))
            {
                return "Model";
            }

            return "Drafting";
        }

        private static string BuildSafeFileNamePart(string value)
        {
            string safe = string.IsNullOrWhiteSpace(value) ? "Project" : value.Trim();

            char[] invalidChars = Path.GetInvalidFileNameChars();

            for (int i = 0; i < invalidChars.Length; i++)
            {
                safe = safe.Replace(invalidChars[i], '_');
            }

            return safe;
        }

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
