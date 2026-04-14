using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;

namespace RevitLibraryBuilder.Services.Placement
{
    public class LineStylePlacer
    {
        private const string TargetViewName = "Библиотека_Стили линий";
        private const int TargetViewScale = 20;

        public Result Execute(ExternalCommandData commandData, ref string message)
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

                // Block responsible for preparing the drafting view before main placement logic
                ViewDrafting draftingView = GetOrCreateDraftingView(document);

                if (draftingView == null)
                {
                    message = "Не удалось получить или создать вид \"" + TargetViewName + "\".";
                    TaskDialog.Show("Place Line Styles", message);
                    return Result.Failed;
                }

                // Block responsible for activating the prepared drafting view in Revit UI
                ActivateView(uiDocument, draftingView);

                Autodesk.Revit.DB.View activeView = uiDocument.ActiveView;

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
                    // Настраиваемая длина создаваемой линии (мм)
                    double lineLength = ConvertMillimetersToInternalUnits(1000.0);
                    // Настраиваемый шаг между строками линий по оси Y (мм)
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
                    "Размещение элементов",
                    "Элементов размещено " + createdLineCount);

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Place Line Styles", exception.ToString());
                return Result.Failed;
            }
        }

        // Block responsible for checking the existence of the drafting view and creating it when needed
        private static ViewDrafting GetOrCreateDraftingView(Document document)
        {
            ViewDrafting existingView = FindDraftingViewByName(document, TargetViewName);

            if (existingView != null)
            {
                if (existingView.Scale != TargetViewScale)
                {
                    using (Transaction transaction = new Transaction(document, "Update Drafting View Scale"))
                    {
                        transaction.Start();
                        existingView.Scale = TargetViewScale;
                        transaction.Commit();
                    }
                }

                return existingView;
            }

            ViewFamilyType draftingViewFamilyType = GetDraftingViewFamilyType(document);

            if (draftingViewFamilyType == null)
            {
                TaskDialog.Show("Place Line Styles", "Не найден ViewFamilyType для Drafting View.");
                return null;
            }

            using (Transaction transaction = new Transaction(document, "Create Drafting View For Line Styles"))
            {
                transaction.Start();

                ViewDrafting draftingView = ViewDrafting.Create(document, draftingViewFamilyType.Id);

                if (draftingView == null)
                {
                    transaction.RollBack();
                    return null;
                }

                draftingView.Name = TargetViewName;
                draftingView.Scale = TargetViewScale;

                transaction.Commit();

                return draftingView;
            }
        }

        // Block responsible for searching a drafting view by exact name
        private static ViewDrafting FindDraftingViewByName(Document document, string viewName)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(ViewDrafting));

            foreach (Element element in collector)
            {
                ViewDrafting draftingView = element as ViewDrafting;

                if (draftingView == null)
                {
                    continue;
                }

                if (draftingView.IsTemplate)
                {
                    continue;
                }

                if (string.Equals(draftingView.Name, viewName, StringComparison.OrdinalIgnoreCase))
                {
                    return draftingView;
                }
            }

            return null;
        }

        // Block responsible for finding the required ViewFamilyType for Drafting View creation
        private static ViewFamilyType GetDraftingViewFamilyType(Document document)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(ViewFamilyType));

            foreach (Element element in collector)
            {
                ViewFamilyType viewFamilyType = element as ViewFamilyType;

                if (viewFamilyType == null)
                {
                    continue;
                }

                if (viewFamilyType.ViewFamily == ViewFamily.Drafting)
                {
                    return viewFamilyType;
                }
            }

            return null;
        }

        // Block responsible for switching Revit UI to the target drafting view
        private static void ActivateView(UIDocument uiDocument, ViewDrafting draftingView)
        {
            if (uiDocument.ActiveView != null &&
                uiDocument.ActiveView.Id == draftingView.Id)
            {
                return;
            }

            uiDocument.ActiveView = draftingView;
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
                ToastNotifier.ShowSuccess(title, message, 10);
            }
            catch
            {
                TaskDialog.Show(title, message);
            }
        }
    }
}
