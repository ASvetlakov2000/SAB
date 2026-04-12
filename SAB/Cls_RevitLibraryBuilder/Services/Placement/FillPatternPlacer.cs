using System;
using System.Collections.Generic;
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

        public Result Execute(ExternalCommandData commandData, ref string message)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Active UIDocument is not available.";
                    TaskDialog.Show("Place Fill Patterns", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null)
                {
                    message = "Document is not available.";
                    TaskDialog.Show("Place Fill Patterns", message);
                    return Result.Failed;
                }

                // Block responsible for preparing the drafting view before filled region placement
                ViewDrafting draftingView = GetOrCreateDraftingView(document);

                if (draftingView == null)
                {
                    message = "Не удалось получить или создать вид \"" + TargetViewName + "\".";
                    TaskDialog.Show("Place Fill Patterns", message);
                    return Result.Failed;
                }

                // Block responsible for activating the prepared drafting view in Revit UI
                ActivateView(uiDocument, draftingView);

                View activeView = uiDocument.ActiveView;

                if (activeView == null)
                {
                    message = "Active view is not available.";
                    TaskDialog.Show("Place Fill Patterns", message);
                    return Result.Failed;
                }

                if (activeView.ViewType != ViewType.DraftingView)
                {
                    TaskDialog.Show(
                        "Place Fill Patterns",
                        "Команда работает только в Drafting View.\n" +
                        "Текущий вид: " + activeView.Name + "\n" +
                        "Тип вида: " + activeView.ViewType);
                    return Result.Cancelled;
                }

                CsvFillPatternExportService csvService = new CsvFillPatternExportService();
                string csvFilePath = csvService.RequestImportFilePath();

                if (string.IsNullOrWhiteSpace(csvFilePath))
                {
                    return Result.Cancelled;
                }

                List<FilledRegionTypeCsvModel> importedTypes = csvService.ImportFilledRegionTypes(csvFilePath);

                if (importedTypes.Count == 0)
                {
                    TaskDialog.Show(
                        "Place Fill Patterns",
                        "В выбранном CSV не найдено ни одного FilledRegionType.");
                    return Result.Cancelled;
                }

                TextNoteType textNoteType = GetTextNoteType(document);

                if (textNoteType == null)
                {
                    TaskDialog.Show(
                        "Place Fill Patterns",
                        "В проекте не найден TextNoteType для создания подписей.");
                    return Result.Failed;
                }

                FilledRegionType baseFilledRegionType = GetBaseFilledRegionType(document);

                if (baseFilledRegionType == null)
                {
                    TaskDialog.Show(
                        "Place Fill Patterns",
                        "В проекте не найден базовый FilledRegionType.");
                    return Result.Failed;
                }

                List<FilledRegionType> resolvedFilledRegionTypes = new List<FilledRegionType>();

                using (Transaction transaction = new Transaction(document, "Place Fill Patterns From CSV"))
                {
                    transaction.Start();

                    // Block responsible for ensuring that all imported types exist in the document
                    for (int i = 0; i < importedTypes.Count; i++)
                    {
                        FilledRegionTypeCsvModel importedType = importedTypes[i];
                        FilledRegionType resolvedType = GetOrCreateFilledRegionType(
                            document,
                            baseFilledRegionType,
                            importedType);

                        if (resolvedType != null)
                        {
                            resolvedFilledRegionTypes.Add(resolvedType);
                        }
                    }

                    // Block responsible for base placement coordinates and fixed geometry parameters
                    XYZ basePoint = XYZ.Zero;
                    double regionSize = ConvertMillimetersToInternalUnits(1000.0);
                    double regionSpacing = ConvertMillimetersToInternalUnits(500.0);
                    double textOffsetBelow = ConvertMillimetersToInternalUnits(250.0);

                    for (int index = 0; index < resolvedFilledRegionTypes.Count; index++)
                    {
                        FilledRegionType filledRegionType = resolvedFilledRegionTypes[index];

                        // Block responsible for horizontal placement of filled regions
                        double currentX = (regionSize + regionSpacing) * index;

                        XYZ p1 = new XYZ(basePoint.X + currentX, basePoint.Y, basePoint.Z);
                        XYZ p2 = new XYZ(basePoint.X + currentX + regionSize, basePoint.Y, basePoint.Z);
                        XYZ p3 = new XYZ(basePoint.X + currentX + regionSize, basePoint.Y + regionSize, basePoint.Z);
                        XYZ p4 = new XYZ(basePoint.X + currentX, basePoint.Y + regionSize, basePoint.Z);

                        CurveLoop curveLoop = new CurveLoop();
                        curveLoop.Append(Line.CreateBound(p1, p2));
                        curveLoop.Append(Line.CreateBound(p2, p3));
                        curveLoop.Append(Line.CreateBound(p3, p4));
                        curveLoop.Append(Line.CreateBound(p4, p1));

                        IList<CurveLoop> boundaries = new List<CurveLoop>();
                        boundaries.Add(curveLoop);

                        FilledRegion.Create(
                            document,
                            filledRegionType.Id,
                            activeView.Id,
                            boundaries);

                        // Block responsible for placing text annotation below the filled region
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

                ShowSuccessNotification(
                    "Place Fill Patterns",
                    "Создано FilledRegion: " + resolvedFilledRegionTypes.Count);

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Place Fill Patterns", exception.ToString());
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
                TaskDialog.Show("Place Fill Patterns", "Не найден ViewFamilyType для Drafting View.");
                return null;
            }

            using (Transaction transaction = new Transaction(document, "Create Drafting View For Fill Patterns"))
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

        // Block responsible for finding a drafting view by exact name
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

        // Block responsible for resolving an imported type to an existing or newly created FilledRegionType
        private static FilledRegionType GetOrCreateFilledRegionType(
            Document document,
            FilledRegionType baseFilledRegionType,
            FilledRegionTypeCsvModel importedType)
        {
            if (importedType == null || string.IsNullOrWhiteSpace(importedType.Name))
            {
                return null;
            }

            FilledRegionType existingType = FindFilledRegionTypeByName(document, importedType.Name);

            if (existingType != null)
            {
                return existingType;
            }

            FilledRegionType newType = baseFilledRegionType.Duplicate(importedType.Name) as FilledRegionType;

            if (newType == null)
            {
                return null;
            }

            FillPatternElement foregroundPattern = FindFillPatternByName(document, importedType.ForegroundPatternName);
            FillPatternElement backgroundPattern = FindFillPatternByName(document, importedType.BackgroundPatternName);

            if (foregroundPattern != null)
            {
                newType.ForegroundPatternId = foregroundPattern.Id;
            }

            if (backgroundPattern != null)
            {
                newType.BackgroundPatternId = backgroundPattern.Id;
            }

            try
            {
                newType.IsMasking = importedType.IsMasking;
            }
            catch
            {
            }

            return newType;
        }

        // Block responsible for finding a FilledRegionType by exact name
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

        // Block responsible for locating a base FilledRegionType for duplication
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

        // Block responsible for locating a fill pattern by exact name
        private static FillPatternElement FindFillPatternByName(Document document, string patternName)
        {
            if (string.IsNullOrWhiteSpace(patternName))
            {
                return null;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(FillPatternElement));

            foreach (Element element in collector)
            {
                FillPatternElement fillPatternElement = element as FillPatternElement;

                if (fillPatternElement == null)
                {
                    continue;
                }

                if (string.Equals(fillPatternElement.Name, patternName, StringComparison.OrdinalIgnoreCase))
                {
                    return fillPatternElement;
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
