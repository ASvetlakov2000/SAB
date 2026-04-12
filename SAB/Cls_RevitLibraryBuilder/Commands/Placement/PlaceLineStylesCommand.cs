using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;

namespace RevitLibraryBuilder.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class PlaceLineStylesCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Active UIDocument is not available.";
                    TaskDialog.Show("Place Line Styles", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null)
                {
                    message = "Document is not available.";
                    TaskDialog.Show("Place Line Styles", message);
                    return Result.Failed;
                }

                Autodesk.Revit.DB.View activeView = document.ActiveView;

                if (activeView == null)
                {
                    message = "Active view is not available.";
                    TaskDialog.Show("Place Line Styles", message);
                    return Result.Failed;
                }

                if (activeView.ViewType != ViewType.DraftingView)
                {
                    TaskDialog.Show(
                        "Place Line Styles",
                        "Команда работает только в Drafting View.\n" +
                        "Текущий вид: " + activeView.Name + "\n" +
                        "Тип вида: " + activeView.ViewType);
                    return Result.Cancelled;
                }

                string csvFilePath = RequestCsvFilePath();

                if (string.IsNullOrWhiteSpace(csvFilePath))
                {
                    return Result.Cancelled;
                }

                List<string> styleNames = ParseLineStyleNames(csvFilePath);

                if (styleNames.Count == 0)
                {
                    TaskDialog.Show(
                        "Place Line Styles",
                        "В выбранном CSV не найдено ни одного имени стиля линий.");
                    return Result.Cancelled;
                }

                TextNoteType textNoteType = GetTextNoteType(document);

                if (textNoteType == null)
                {
                    TaskDialog.Show(
                        "Place Line Styles",
                        "В проекте не найден TextNoteType для создания подписей.");
                    return Result.Failed;
                }

                Category linesCategory = document.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);

                if (linesCategory == null)
                {
                    TaskDialog.Show(
                        "Place Line Styles",
                        "В проекте не найдена категория Lines.");
                    return Result.Failed;
                }

                int createdLineCount = 0;
                int skippedLineCount = 0;

                using (Transaction transaction = new Transaction(document, "Place Line Styles From CSV"))
                {
                    transaction.Start();

                    // Block responsible for base placement coordinates
                    XYZ basePoint = XYZ.Zero;

                    // Block responsible for fixed geometry parameters in millimeters
                    double lineLength = ConvertMillimetersToInternalUnits(1000.0);
                    double verticalSpacing = ConvertMillimetersToInternalUnits(500.0);

                    for (int index = 0; index < styleNames.Count; index++)
                    {
                        string styleName = styleNames[index];

                        GraphicsStyle targetLineStyle = FindLineStyle(document, linesCategory, styleName);

                        if (targetLineStyle == null)
                        {
                            skippedLineCount++;
                            continue;
                        }

                        // Block responsible for vertical spacing between lines
                        double currentY = 0.0 - (verticalSpacing * index);

                        XYZ lineStartPoint = new XYZ(basePoint.X, basePoint.Y + currentY, basePoint.Z);
                        XYZ lineEndPoint = new XYZ(basePoint.X + lineLength, basePoint.Y + currentY, basePoint.Z);

                        Line lineGeometry = Line.CreateBound(lineStartPoint, lineEndPoint);
                        DetailCurve detailCurve = document.Create.NewDetailCurve(activeView, lineGeometry);

                        if (detailCurve == null)
                        {
                            skippedLineCount++;
                            continue;
                        }

                        detailCurve.LineStyle = targetLineStyle;

                        // Block responsible for placing text annotation at the line position
                        XYZ textPoint = new XYZ(
                            lineStartPoint.X,
                            lineStartPoint.Y,
                            lineStartPoint.Z);

                        TextNote.Create(
                            document,
                            activeView.Id,
                            textPoint,
                            styleName,
                            textNoteType.Id);

                        createdLineCount++;
                    }

                    transaction.Commit();
                }

                ShowSuccessNotification(
                    "Place Line Styles",
                    "Создано линий: " + createdLineCount + "\n" +
                    "Пропущено стилей: " + skippedLineCount);

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Place Line Styles", exception.ToString());
                return Result.Failed;
            }
        }

        // Block responsible for manual CSV file selection
        private static string RequestCsvFilePath()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select CSV file with line styles";
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

        // Block responsible for parsing line style names from CSV
        private static List<string> ParseLineStyleNames(string csvFilePath)
        {
            List<string> styleNames = new List<string>();
            string[] lines = File.ReadAllLines(csvFilePath);

            for (int i = 0; i < lines.Length; i++)
            {
                string sourceLine = lines[i];

                if (string.IsNullOrWhiteSpace(sourceLine))
                {
                    continue;
                }

                string parsedStyleName = ExtractFirstCsvValue(sourceLine);

                if (string.IsNullOrWhiteSpace(parsedStyleName))
                {
                    continue;
                }

                if (IsHeaderValue(parsedStyleName))
                {
                    continue;
                }

                if (!ContainsValue(styleNames, parsedStyleName))
                {
                    styleNames.Add(parsedStyleName);
                }
            }

            return styleNames;
        }

        // Block responsible for simple first-column CSV parsing
        private static string ExtractFirstCsvValue(string csvLine)
        {
            if (string.IsNullOrWhiteSpace(csvLine))
            {
                return string.Empty;
            }

            string[] parts = csvLine.Split(',');

            if (parts.Length == 0)
            {
                return string.Empty;
            }

            string value = parts[0].Trim();

            if (value.StartsWith("\"", StringComparison.Ordinal))
            {
                value = value.Substring(1);
            }

            if (value.EndsWith("\"", StringComparison.Ordinal))
            {
                value = value.Substring(0, value.Length - 1);
            }

            return value.Trim();
        }

        // Block responsible for skipping header lines in CSV
        private static bool IsHeaderValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            string normalizedValue = value.Trim().ToLowerInvariant();

            if (normalizedValue == "name" ||
                normalizedValue == "style" ||
                normalizedValue == "line style" ||
                normalizedValue == "linestyle" ||
                normalizedValue == "style name" ||
                normalizedValue == "line style name")
            {
                return true;
            }

            return false;
        }

        private static bool ContainsValue(List<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        // Block responsible for locating the required line style in Revit
        private static GraphicsStyle FindLineStyle(Document document, Category linesCategory, string styleName)
        {
            if (document == null || linesCategory == null || string.IsNullOrWhiteSpace(styleName))
            {
                return null;
            }

            foreach (Category subCategory in linesCategory.SubCategories)
            {
                if (subCategory == null)
                {
                    continue;
                }

                if (!string.Equals(subCategory.Name, styleName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                GraphicsStyle graphicsStyle = subCategory.GetGraphicsStyle(GraphicsStyleType.Projection);

                if (graphicsStyle != null)
                {
                    return graphicsStyle;
                }
            }

            return null;
        }

        // Block responsible for retrieving a valid text note type
        private static TextNoteType GetTextNoteType(Document document)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(TextNoteType));

            foreach (Element element in collector)
            {
                TextNoteType textNoteType = element as TextNoteType;

                if (textNoteType != null)
                {
                    return textNoteType;
                }
            }

            return null;
        }

        private static double ConvertMillimetersToInternalUnits(double valueInMillimeters)
        {
            return UnitUtils.ConvertToInternalUnits(valueInMillimeters, UnitTypeId.Millimeters);
        }

        // Block responsible for post-execution notification
        private static void ShowSuccessNotification(string title, string message)
        {
            try
            {
                ToastNotifier.ShowSuccess(title, message, 5);
            }
            catch
            {
                TaskDialog.Show(title, message);
            }
        }
    }
}
