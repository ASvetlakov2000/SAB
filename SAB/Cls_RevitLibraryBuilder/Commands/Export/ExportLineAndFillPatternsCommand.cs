using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Windows.Forms;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services.Csv;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Экспорт стилей линий и штриховок (Model / Drafting)
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ExportLineAndFillPatternsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Document doc = commandData.Application.ActiveUIDocument.Document;

                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Выберите папку для экспорта Line Styles и Fill Patterns";

                    if (dialog.ShowDialog() != DialogResult.OK)
                        return Result.Cancelled;

                    string folder = dialog.SelectedPath;

                    // =========================
                    // 🔹 1. LINE STYLES
                    // =========================
                    ExportLineStyles(doc, folder);

                    // =========================
                    // 🔹 2. FILL PATTERNS
                    // =========================
                    new CsvFillPatternExportService().Export(doc, folder);

                    // =========================
                    // 🔹 SUCCESS
                    // =========================
                    ToastNotifier.ShowFolderLinkSuccess(
                        "Экспорт завершён",
                        "\nLine Styles + Fill Patterns экспортированы:",
                        folder,
                        durationSeconds: 10
                    );
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        /// <summary>
        /// Экспорт стилей линий (100% Revit API safe)
        /// </summary>
        private void ExportLineStyles(Document doc, string folder)
        {
            string filePath = System.IO.Path.Combine(folder, "LineStyles.csv");

            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            sb.AppendLine("Name,Category,LineWeight,ColorR,ColorG,ColorB,Pattern");

            // 🔹 Получаем категории линий
            Category linesCategory = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);

            if (linesCategory == null)
                return;

            foreach (Category subCat in linesCategory.SubCategories)
            {
                if (subCat == null)
                    continue;

                // =========================
                // 🔹 GraphicsStyle (основной источник данных)
                // =========================
                GraphicsStyle gs = subCat.GetGraphicsStyle(GraphicsStyleType.Projection);

                if (gs == null)
                    continue;

                Category cat = gs.GraphicsStyleCategory;

                if (cat == null)
                    continue;

                // =========================
                // 🔹 Name
                // =========================
                string name = cat.Name;

                // =========================
                // 🔹 Color (safe)
                // =========================
                Color color = cat.LineColor;

                int r = 0, g = 0, b = 0;

                if (color != null && color.IsValid)
                {
                    r = color.Red;
                    g = color.Green;
                    b = color.Blue;
                }

                // =========================
                // 🔹 Line Weight (ВАЖНО: только через Category)
                // =========================
                
                
                // int lineWeight = cat.GetLineWeight(GraphicsStyleType.Projection);

                // =========================
                // 🔹 Line Pattern
                // =========================
                string patternName = "Solid";

                ElementId patternId = cat.GetLinePatternId(GraphicsStyleType.Projection);

                if (patternId != ElementId.InvalidElementId)
                {
                    LinePatternElement pattern = doc.GetElement(patternId) as LinePatternElement;

                    if (pattern != null)
                        patternName = pattern.Name;
                }

                // =========================
                // 🔹 Write CSV
                // =========================
                sb.AppendLine(
                    $"{Escape(name)}," +
                    $"{Escape(cat.Name)}," +
                    $"{r},{g},{b}," +
                    $"{Escape(patternName)}"
                );
            }

            System.IO.File.WriteAllText(filePath, sb.ToString());
        }

        /// <summary>
        /// Escape CSV
        /// </summary>
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