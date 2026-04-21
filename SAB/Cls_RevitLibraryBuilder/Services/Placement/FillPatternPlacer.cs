using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services.Csv;

namespace RevitLibraryBuilder.Services.Placement
{
    public class FillPatternPlacer
    {
        private const string TargetViewName = "Библиотека_Штриховки";
        private const int TargetViewScale = 20;
        private const string InvalidNamesReportFileName = "Штриховки с запрещенными символами.csv";

        // Запрещенные символы для имен типов Revit (ElementType.Duplicate)
        private static readonly char[] ProhibitedNameCharacters =
            { '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~' };

        public Result Execute(ExternalCommandData commandData, ref string message)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Активный UIDocument недоступен.";
                    TaskDialog.Show("Размещение штриховок", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null)
                {
                    message = "Документ недоступен.";
                    TaskDialog.Show("Размещение штриховок", message);
                    return Result.Failed;
                }

                // Block responsible for checking/creating Drafting View before placement logic
                ViewDrafting draftingView = GetOrCreateDraftingView(document);

                if (draftingView == null)
                {
                    message = "Не удалось получить или создать чертежный вид: " + TargetViewName;
                    TaskDialog.Show("Размещение штриховок", message);
                    return Result.Failed;
                }

                // Block responsible for switching Revit UI to the target view
                ActivateView(uiDocument, draftingView);

                View activeView = uiDocument.ActiveView;

                if (activeView == null || activeView.ViewType != ViewType.DraftingView)
                {
                    TaskDialog.Show("Размещение штриховок", "Активный вид должен быть чертежным видом.");
                    return Result.Cancelled;
                }

                CsvFillPatternExportService csvService = new CsvFillPatternExportService();
                string csvFilePath = csvService.RequestImportFilePath();

                if (string.IsNullOrWhiteSpace(csvFilePath))
                {
                    return Result.Cancelled;
                }

                List<FillPatternCsvRecord> importedRecords = csvService.ImportFillPatternRows(csvFilePath);

                if (importedRecords.Count == 0)
                {
                    TaskDialog.Show("Размещение штриховок", "В файле не найдено корректных строк штриховок.");
                    return Result.Cancelled;
                }

                TextNoteType textNoteType = GetTextNoteType(document);
                FilledRegionType baseFilledRegionType = GetBaseFilledRegionType(document);

                if (textNoteType == null)
                {
                    TaskDialog.Show("Размещение штриховок", "Не найден тип текстовых примечаний.");
                    return Result.Failed;
                }

                if (baseFilledRegionType == null)
                {
                    TaskDialog.Show("Размещение штриховок", "Не найден базовый тип залитой области.");
                    return Result.Failed;
                }

                List<FilledRegionType> resolvedTypes = new List<FilledRegionType>();
                List<InvalidNameReportRow> invalidNameReportRows = new List<InvalidNameReportRow>();

                using (Transaction transaction = new Transaction(document, "Place Fill Patterns From CSV"))
                {
                    transaction.Start();

                    // Block responsible for resolving FilledRegionType for every CSV row in source order
                    for (int i = 0; i < importedRecords.Count; i++)
                    {
                        FillPatternCsvRecord record = importedRecords[i];
                        FilledRegionType resolvedType = CreateOrGetFilledRegionType(
                            document,
                            baseFilledRegionType,
                            record,
                            invalidNameReportRows);

                        if (resolvedType != null)
                        {
                            resolvedTypes.Add(resolvedType);
                        }
                    }

                    // Block responsible for geometry values that can be tuned later
                    XYZ basePoint = XYZ.Zero;
                    // Настраиваемый размер одной штриховки по стороне (мм)
                    double regionSize = ConvertMillimetersToInternalUnits(1000.0);
                    // Настраиваемый горизонтальный отступ между соседними штриховками (мм)
                    double regionSpacing = ConvertMillimetersToInternalUnits(500.0);
                    // Настраиваемое смещение подписи вниз от штриховки (мм)
                    double textOffsetBelow = ConvertMillimetersToInternalUnits(250.0);

                    for (int index = 0; index < resolvedTypes.Count; index++)
                    {
                        FilledRegionType filledRegionType = resolvedTypes[index];

                        // Block responsible for horizontal spacing between FilledRegions
                        double currentX = (regionSize + regionSpacing) * index;

                        XYZ p1 = new XYZ(basePoint.X + currentX, basePoint.Y, basePoint.Z);
                        XYZ p2 = new XYZ(basePoint.X + currentX + regionSize, basePoint.Y, basePoint.Z);
                        XYZ p3 = new XYZ(basePoint.X + currentX + regionSize, basePoint.Y + regionSize, basePoint.Z);
                        XYZ p4 = new XYZ(basePoint.X + currentX, basePoint.Y + regionSize, basePoint.Z);

                        CurveLoop loop = new CurveLoop();
                        loop.Append(Line.CreateBound(p1, p2));
                        loop.Append(Line.CreateBound(p2, p3));
                        loop.Append(Line.CreateBound(p3, p4));
                        loop.Append(Line.CreateBound(p4, p1));

                        IList<CurveLoop> loops = new List<CurveLoop> { loop };

                        FilledRegion.Create(document, filledRegionType.Id, activeView.Id, loops);

                        XYZ textPoint = new XYZ(
                            basePoint.X + currentX + (regionSize / 2.0),
                            basePoint.Y - textOffsetBelow,
                            basePoint.Z);

                        TextNote.Create(
                            document,
                            activeView.Id,
                            textPoint,
                            filledRegionType.Name,
                            textNoteType.Id);
                    }

                    transaction.Commit();
                }

                if (invalidNameReportRows.Count > 0)
                {
                    WriteInvalidNamesReport(csvFilePath, invalidNameReportRows);
                }

                ShowSuccessNotification(
                    "Размещение элементов",
                    "Элементов размещено " + resolvedTypes.Count);

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Размещение штриховок", exception.ToString());
                return Result.Failed;
            }
        }

        // Block responsible for creating a new FilledRegionType or using existing one
        private static FilledRegionType CreateOrGetFilledRegionType(
            Document document,
            FilledRegionType baseFilledRegionType,
            FillPatternCsvRecord record,
            List<InvalidNameReportRow> invalidNameReportRows)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.Name))
            {
                return null;
            }

            string originalName = record.Name;
            string invalidCharactersFound = GetInvalidCharactersFound(originalName);
            string sanitizedName = SanitizeTypeName(originalName);

            InvalidNameReportRow reportRow = null;

            if (!string.IsNullOrWhiteSpace(invalidCharactersFound) || !string.Equals(originalName, sanitizedName, StringComparison.Ordinal))
            {
                reportRow = new InvalidNameReportRow
                {
                    RowIndex = record.RowIndex,
                    OriginalName = originalName,
                    SanitizedName = sanitizedName,
                    InvalidCharactersFound = invalidCharactersFound
                };
            }

            if (string.IsNullOrWhiteSpace(sanitizedName))
            {
                AddReportRowIfNeeded(invalidNameReportRows, reportRow, record, originalName, sanitizedName, "Санитизированное имя пустое");
                return null;
            }

            FilledRegionType existingType = FindFilledRegionTypeByName(document, sanitizedName);

            if (existingType != null)
            {
                AddReportRowIfNeeded(invalidNameReportRows, reportRow, record, originalName, sanitizedName, "Использован существующий FilledRegionType");
                return existingType;
            }

            FillPatternTarget target = IsDraftingTarget(record.Target)
                ? FillPatternTarget.Drafting
                : FillPatternTarget.Model;

            FillPatternElement foregroundPattern = GetOrCreateFillPatternElement(
                document,
                record.ForegroundPattern,
                target);

            FillPatternElement backgroundPattern = null;

            if (!string.IsNullOrWhiteSpace(record.BackgroundPattern))
            {
                backgroundPattern = GetOrCreateFillPatternElement(
                    document,
                    record.BackgroundPattern,
                    target);
            }

            if (foregroundPattern == null)
            {
                AddReportRowIfNeeded(invalidNameReportRows, reportRow, record, originalName, sanitizedName, "Не найден или не создан штриховой образец переднего плана");
                return null;
            }

            FilledRegionType newType;

            try
            {
                newType = baseFilledRegionType.Duplicate(sanitizedName) as FilledRegionType;
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException exception)
            {
                AddReportRowIfNeeded(invalidNameReportRows, reportRow, record, originalName, sanitizedName, "Ошибка имени при создании типа: " + exception.Message);
                return null;
            }

            if (newType == null)
            {
                AddReportRowIfNeeded(invalidNameReportRows, reportRow, record, originalName, sanitizedName, "Не удалось дублировать FilledRegionType");
                return null;
            }

            newType.ForegroundPatternId = foregroundPattern.Id;

            if (backgroundPattern != null)
            {
                newType.BackgroundPatternId = backgroundPattern.Id;
            }

            try
            {
                newType.IsMasking = record.IsMasking;
            }
            catch
            {
                // Пропускаем ошибку параметра маскирования и продолжаем размещение.
            }

            AddReportRowIfNeeded(invalidNameReportRows, reportRow, record, originalName, sanitizedName, "Создан и обработан с санитизированным именем");
            return newType;
        }

        private static void AddReportRowIfNeeded(
            List<InvalidNameReportRow> invalidNameReportRows,
            InvalidNameReportRow reportRow,
            FillPatternCsvRecord record,
            string originalName,
            string sanitizedName,
            string reason)
        {
            if (invalidNameReportRows == null)
            {
                return;
            }

            InvalidNameReportRow rowToAdd = reportRow;

            if (rowToAdd == null)
            {
                rowToAdd = new InvalidNameReportRow
                {
                    RowIndex = record != null ? record.RowIndex : 0,
                    OriginalName = originalName ?? string.Empty,
                    SanitizedName = sanitizedName ?? string.Empty,
                    InvalidCharactersFound = string.Empty
                };
            }

            rowToAdd.SkipReason = reason;
            invalidNameReportRows.Add(rowToAdd);
        }

        // Block responsible for finding or creating FillPatternElement
        private static FillPatternElement GetOrCreateFillPatternElement(
            Document document,
            string patternName,
            FillPatternTarget target)
        {
            if (string.IsNullOrWhiteSpace(patternName))
            {
                return null;
            }

            FillPatternElement existing = FillPatternElement.GetFillPatternElementByName(
                document,
                target,
                patternName);

            if (existing != null)
            {
                return existing;
            }

            // Настраиваемый шаг штриха для автоматически создаваемого FillPattern (мм)
            double defaultSpacing = ConvertMillimetersToInternalUnits(5.0);

            FillPattern fillPattern = new FillPattern(
                patternName,
                target,
                FillPatternHostOrientation.ToView,
                0.0,
                defaultSpacing);

            try
            {
                return FillPatternElement.Create(document, fillPattern);
            }
            catch
            {
                return null;
            }
        }

        // Block responsible for checking the existence of Drafting View and creating it when needed
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

            ViewFamilyType draftingType = GetDraftingViewFamilyType(document);

            if (draftingType == null)
            {
                return null;
            }

            using (Transaction transaction = new Transaction(document, "Create Drafting View For Fill Patterns"))
            {
                transaction.Start();

                ViewDrafting newView = ViewDrafting.Create(document, draftingType.Id);

                if (newView == null)
                {
                    transaction.RollBack();
                    return null;
                }

                newView.Name = TargetViewName;
                newView.Scale = TargetViewScale;

                transaction.Commit();
                return newView;
            }
        }

        private static ViewDrafting FindDraftingViewByName(Document document, string viewName)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(ViewDrafting));

            foreach (Element element in collector)
            {
                ViewDrafting viewDrafting = element as ViewDrafting;

                if (viewDrafting == null || viewDrafting.IsTemplate)
                {
                    continue;
                }

                if (string.Equals(viewDrafting.Name, viewName, StringComparison.OrdinalIgnoreCase))
                {
                    return viewDrafting;
                }
            }

            return null;
        }

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

        private static void ActivateView(UIDocument uiDocument, ViewDrafting draftingView)
        {
            if (uiDocument.ActiveView != null && uiDocument.ActiveView.Id == draftingView.Id)
            {
                return;
            }

            uiDocument.ActiveView = draftingView;
        }

        private static FilledRegionType FindFilledRegionTypeByName(Document document, string typeName)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(FilledRegionType));

            foreach (Element element in collector)
            {
                FilledRegionType filledRegionType = element as FilledRegionType;

                if (filledRegionType == null)
                {
                    continue;
                }

                if (string.Equals(filledRegionType.Name, typeName, StringComparison.OrdinalIgnoreCase))
                {
                    return filledRegionType;
                }
            }

            return null;
        }

        private static FilledRegionType GetBaseFilledRegionType(Document document)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(FilledRegionType));

            foreach (Element element in collector)
            {
                FilledRegionType filledRegionType = element as FilledRegionType;

                if (filledRegionType != null)
                {
                    return filledRegionType;
                }
            }

            return null;
        }

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

        // Block responsible for converting imported CSV names into valid Revit type names
        private static string SanitizeTypeName(string originalName)
        {
            if (string.IsNullOrWhiteSpace(originalName))
            {
                return string.Empty;
            }

            StringBuilder result = new StringBuilder();
            string source = originalName.Trim();

            for (int i = 0; i < source.Length; i++)
            {
                char character = source[i];

                if (char.IsControl(character))
                {
                    continue;
                }

                if (IsProhibitedCharacter(character))
                {
                    result.Append('_');
                    continue;
                }

                result.Append(character);
            }

            string sanitized = result.ToString();

            while (sanitized.Contains("__"))
            {
                sanitized = sanitized.Replace("__", "_");
            }

            return sanitized.Trim();
        }

        private static string GetInvalidCharactersFound(string originalName)
        {
            if (string.IsNullOrEmpty(originalName))
            {
                return string.Empty;
            }

            StringBuilder found = new StringBuilder();

            for (int i = 0; i < originalName.Length; i++)
            {
                char character = originalName[i];

                if (!IsProhibitedCharacter(character))
                {
                    continue;
                }

                if (found.ToString().IndexOf(character) >= 0)
                {
                    continue;
                }

                found.Append(character);
            }

            return found.ToString();
        }

        private static bool IsProhibitedCharacter(char character)
        {
            for (int i = 0; i < ProhibitedNameCharacters.Length; i++)
            {
                if (ProhibitedNameCharacters[i] == character)
                {
                    return true;
                }
            }

            return false;
        }

        private static void WriteInvalidNamesReport(
            string sourceCsvFilePath,
            List<InvalidNameReportRow> invalidRows)
        {
            if (invalidRows == null || invalidRows.Count == 0)
            {
                return;
            }

            string sourceFolder = Path.GetDirectoryName(sourceCsvFilePath);

            if (string.IsNullOrWhiteSpace(sourceFolder))
            {
                return;
            }

            string reportPath = Path.Combine(sourceFolder, InvalidNamesReportFileName);
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.AppendLine("RowIndex,OriginalName,SanitizedName,InvalidCharactersFound,SkipReason");

            for (int i = 0; i < invalidRows.Count; i++)
            {
                InvalidNameReportRow row = invalidRows[i];

                stringBuilder.AppendLine(
                    row.RowIndex + "," +
                    EscapeCsv(row.OriginalName) + "," +
                    EscapeCsv(row.SanitizedName) + "," +
                    EscapeCsv(row.InvalidCharactersFound) + "," +
                    EscapeCsv(row.SkipReason));
            }

            File.WriteAllText(reportPath, stringBuilder.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsv(string value)
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

        private static bool IsDraftingTarget(string target)
        {
            return !string.Equals(target, "Model", StringComparison.OrdinalIgnoreCase);
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

        private class InvalidNameReportRow
        {
            public int RowIndex { get; set; }

            public string OriginalName { get; set; }

            public string SanitizedName { get; set; }

            public string InvalidCharactersFound { get; set; }

            public string SkipReason { get; set; }
        }
    }
}
