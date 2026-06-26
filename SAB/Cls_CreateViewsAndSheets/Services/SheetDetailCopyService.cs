using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using SAB.CreateViewsAndSheets.Models;

namespace SAB.CreateViewsAndSheets.Services
{
    public class SheetDetailCopyService
    {
        private readonly SheetBoundsService _sheetBoundsService;

        public SheetDetailCopyService()
        {
            _sheetBoundsService = new SheetBoundsService();
        }

        public void CopyFromSourceSheet(
            Document document,
            ElementId sourceSheetId,
            ViewSheet targetSheet,
            SheetDetailCopySettings settings,
            IList<string> warnings)
        {
            if (document == null || sourceSheetId == null || targetSheet == null || settings == null)
            {
                return;
            }

            if (!settings.CopySheetWithDetailing)
            {
                return;
            }

            ViewSheet sourceSheet = document.GetElement(sourceSheetId) as ViewSheet;
            if (sourceSheet == null)
            {
                AddWarning(warnings, "Не удалось скопировать детализацию: лист-образец не найден.");
                return;
            }

            XYZ sheetTranslation = CalculateSheetCopyTranslation(document, sourceSheet, targetSheet);

            CopySheetOwnedElements(document, sourceSheet, targetSheet, settings, warnings, sheetTranslation);
            CopySchedules(document, sourceSheet, targetSheet, settings, warnings, sheetTranslation);
            CopyPlacedLegendAndDraftingViews(document, sourceSheet, targetSheet, settings, warnings, sheetTranslation);
        }

        private void CopySheetOwnedElements(
            Document document,
            ViewSheet sourceSheet,
            ViewSheet targetSheet,
            SheetDetailCopySettings settings,
            IList<string> warnings,
            XYZ sheetTranslation)
        {
            List<ElementId> elementIds = new List<ElementId>();

            if (settings.CopyDetailLines)
            {
                AddElementsByCategory(document, sourceSheet.Id, BuiltInCategory.OST_Lines, elementIds);
            }

            if (settings.CopyFilledRegions)
            {
                AddElementsByCategory(document, sourceSheet.Id, BuiltInCategory.OST_FilledRegion, elementIds);
            }

            if (settings.CopyTextNotes)
            {
                AddElementsByCategory(document, sourceSheet.Id, BuiltInCategory.OST_TextNotes, elementIds);
            }

            if (settings.CopyGenericAnnotations)
            {
                AddElementsByCategory(document, sourceSheet.Id, BuiltInCategory.OST_GenericAnnotation, elementIds);
            }

            if (settings.CopyImages)
            {
                AddElementsByCategory(document, sourceSheet.Id, BuiltInCategory.OST_RasterImages, elementIds);
            }

            if (elementIds.Count == 0)
            {
                return;
            }

            try
            {
                CopyPasteOptions copyOptions = CreateCopyPasteOptions();
                Transform transform = Transform.CreateTranslation(sheetTranslation ?? new XYZ(0.0, 0.0, 0.0));
                ElementTransformUtils.CopyElements(sourceSheet, elementIds, targetSheet, transform, copyOptions);
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось скопировать элементы детализации с листа-образца: " + exception.Message);
            }
        }

        private void CopySchedules(
            Document document,
            ViewSheet sourceSheet,
            ViewSheet targetSheet,
            SheetDetailCopySettings settings,
            IList<string> warnings,
            XYZ sheetTranslation)
        {
            if (!settings.CopySchedules)
            {
                return;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document, sourceSheet.Id);
            IList<Element> scheduleElements = collector.OfClass(typeof(ScheduleSheetInstance)).ToElements();

            for (int i = 0; i < scheduleElements.Count; i++)
            {
                ScheduleSheetInstance sourceSchedule = scheduleElements[i] as ScheduleSheetInstance;
                if (sourceSchedule == null)
                {
                    continue;
                }

                try
                {
                    XYZ targetPoint = TranslateSheetPoint(sourceSchedule.Point, sheetTranslation);
                    if (targetPoint == null)
                    {
                        AddWarning(warnings, "Не удалось определить точку размещения ведомости с листа-образца.");
                        continue;
                    }

                    ScheduleSheetInstance.Create(document, targetSheet.Id, sourceSchedule.ScheduleId, targetPoint);
                }
                catch (Exception exception)
                {
                    AddWarning(warnings, "Не удалось скопировать ведомость с листа-образца: " + exception.Message);
                }
            }
        }

        private void CopyPlacedLegendAndDraftingViews(
            Document document,
            ViewSheet sourceSheet,
            ViewSheet targetSheet,
            SheetDetailCopySettings settings,
            IList<string> warnings,
            XYZ sheetTranslation)
        {
            if (!settings.CopyLegends && !settings.CopyDraftingViews)
            {
                return;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document, sourceSheet.Id);
            IList<Element> viewportElements = collector.OfClass(typeof(Viewport)).ToElements();

            for (int i = 0; i < viewportElements.Count; i++)
            {
                Viewport sourceViewport = viewportElements[i] as Viewport;
                if (sourceViewport == null)
                {
                    continue;
                }

                View sourceView = document.GetElement(sourceViewport.ViewId) as View;
                if (sourceView == null)
                {
                    continue;
                }

                if (sourceView.ViewType == ViewType.Legend && settings.CopyLegends)
                {
                    CopyLegendViewport(document, sourceViewport, sourceView, targetSheet, warnings, sheetTranslation);
                }
                else if (sourceView.ViewType == ViewType.DraftingView && settings.CopyDraftingViews)
                {
                    CopyDraftingViewport(document, sourceViewport, sourceView, targetSheet, warnings, sheetTranslation);
                }
            }
        }

        private void CopyLegendViewport(
            Document document,
            Viewport sourceViewport,
            View legendView,
            ViewSheet targetSheet,
            IList<string> warnings,
            XYZ sheetTranslation)
        {
            try
            {
                if (!Viewport.CanAddViewToSheet(document, targetSheet.Id, legendView.Id))
                {
                    AddWarning(warnings, "Легенда \"" + legendView.Name + "\" не может быть размещена на созданном листе.");
                    return;
                }

                XYZ targetPoint = TranslateSheetPoint(sourceViewport.GetBoxCenter(), sheetTranslation);
                if (targetPoint == null)
                {
                    AddWarning(warnings, "Не удалось определить точку размещения легенды \"" + legendView.Name + "\".");
                    return;
                }

                Viewport targetViewport = Viewport.Create(document, targetSheet.Id, legendView.Id, targetPoint);
                TryCopyViewportType(sourceViewport, targetViewport);
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось скопировать легенду \"" + legendView.Name + "\": " + exception.Message);
            }
        }

        private void CopyDraftingViewport(
            Document document,
            Viewport sourceViewport,
            View draftingView,
            ViewSheet targetSheet,
            IList<string> warnings,
            XYZ sheetTranslation)
        {
            try
            {
                ElementId duplicatedViewId = draftingView.Duplicate(ViewDuplicateOption.WithDetailing);
                View duplicatedDraftingView = document.GetElement(duplicatedViewId) as View;
                if (duplicatedDraftingView == null)
                {
                    AddWarning(warnings, "Не удалось создать копию чертежного вида \"" + draftingView.Name + "\".");
                    return;
                }

                TrySetUniqueDraftingViewName(document, duplicatedDraftingView, draftingView.Name, targetSheet);

                if (!Viewport.CanAddViewToSheet(document, targetSheet.Id, duplicatedDraftingView.Id))
                {
                    AddWarning(warnings, "Чертежный вид \"" + duplicatedDraftingView.Name + "\" не может быть размещен на созданном листе.");
                    return;
                }

                XYZ targetPoint = TranslateSheetPoint(sourceViewport.GetBoxCenter(), sheetTranslation);
                if (targetPoint == null)
                {
                    AddWarning(warnings, "Не удалось определить точку размещения чертежного вида \"" + draftingView.Name + "\".");
                    return;
                }

                Viewport targetViewport = Viewport.Create(document, targetSheet.Id, duplicatedDraftingView.Id, targetPoint);
                TryCopyViewportType(sourceViewport, targetViewport);
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось скопировать чертежный вид \"" + draftingView.Name + "\": " + exception.Message);
            }
        }

        private XYZ CalculateSheetCopyTranslation(Document document, ViewSheet sourceSheet, ViewSheet targetSheet)
        {
            SheetBounds sourceBounds;
            SheetBounds targetBounds;
            if (!_sheetBoundsService.TryGetSheetBounds(document, sourceSheet, out sourceBounds) ||
                !_sheetBoundsService.TryGetSheetBounds(document, targetSheet, out targetBounds) ||
                sourceBounds == null ||
                targetBounds == null)
            {
                return new XYZ(0.0, 0.0, 0.0);
            }

            return new XYZ(
                targetBounds.MinXFeet - sourceBounds.MinXFeet,
                targetBounds.MinYFeet - sourceBounds.MinYFeet,
                0.0);
        }

        private XYZ TranslateSheetPoint(XYZ sourcePoint, XYZ sheetTranslation)
        {
            if (sourcePoint == null)
            {
                return null;
            }

            XYZ translation = sheetTranslation ?? new XYZ(0.0, 0.0, 0.0);
            return new XYZ(
                sourcePoint.X + translation.X,
                sourcePoint.Y + translation.Y,
                sourcePoint.Z + translation.Z);
        }

        private void AddElementsByCategory(
            Document document,
            ElementId sourceSheetId,
            BuiltInCategory category,
            IList<ElementId> elementIds)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document, sourceSheetId);
            IList<Element> elements = collector
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .ToElements();

            for (int i = 0; i < elements.Count; i++)
            {
                Element element = elements[i];
                if (element == null || elementIds.Contains(element.Id))
                {
                    continue;
                }

                elementIds.Add(element.Id);
            }
        }

        private CopyPasteOptions CreateCopyPasteOptions()
        {
            CopyPasteOptions options = new CopyPasteOptions();
            options.SetDuplicateTypeNamesHandler(new UseDestinationDuplicateTypeNamesHandler());
            return options;
        }

        private void TryCopyViewportType(Viewport sourceViewport, Viewport targetViewport)
        {
            if (sourceViewport == null || targetViewport == null)
            {
                return;
            }

            try
            {
                ElementId sourceTypeId = sourceViewport.GetTypeId();
                if (sourceTypeId != null && sourceTypeId != ElementId.InvalidElementId)
                {
                    targetViewport.ChangeTypeId(sourceTypeId);
                }
            }
            catch
            {
                // Тип Viewport второстепенен для копирования детализации, поэтому ошибка не должна отменять создание листа.
            }
        }

        private void TrySetUniqueDraftingViewName(Document document, View duplicatedDraftingView, string sourceName, ViewSheet targetSheet)
        {
            if (document == null || duplicatedDraftingView == null)
            {
                return;
            }

            try
            {
                string baseName = (sourceName ?? "Drafting View") + " - " + CleanViewNamePart(targetSheet.SheetNumber);
                HashSet<string> existingNames = CollectExistingViewNames(document);
                string uniqueName = BuildUniqueName(baseName, existingNames);
                duplicatedDraftingView.Name = uniqueName;
            }
            catch
            {
                // Если имя занято или содержит запрещенный символ, оставляем имя, которое Revit назначил при дублировании.
            }
        }

        private HashSet<string> CollectExistingViewNames(Document document)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            FilteredElementCollector collector = new FilteredElementCollector(document);
            IList<Element> views = collector.OfClass(typeof(View)).ToElements();

            for (int i = 0; i < views.Count; i++)
            {
                View view = views[i] as View;
                if (view == null || string.IsNullOrWhiteSpace(view.Name))
                {
                    continue;
                }

                if (!names.Contains(view.Name))
                {
                    names.Add(view.Name);
                }
            }

            return names;
        }

        private string BuildUniqueName(string baseName, HashSet<string> existingNames)
        {
            string cleanBaseName = string.IsNullOrWhiteSpace(baseName) ? "Drafting View Copy" : baseName.Trim();
            if (existingNames == null || !existingNames.Contains(cleanBaseName))
            {
                return cleanBaseName;
            }

            int index = 1;
            while (index < 10000)
            {
                string candidate = cleanBaseName + " " + index;
                if (!existingNames.Contains(candidate))
                {
                    return candidate;
                }

                index++;
            }

            return cleanBaseName + " " + Guid.NewGuid().ToString("N");
        }

        private string CleanViewNamePart(string text)
        {
            string value = string.IsNullOrWhiteSpace(text) ? "Sheet" : text.Trim();
            char[] invalidCharacters = new[] { '\\', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?' };

            for (int i = 0; i < invalidCharacters.Length; i++)
            {
                value = value.Replace(invalidCharacters[i], '_');
            }

            return value;
        }

        private void AddWarning(IList<string> warnings, string message)
        {
            if (warnings == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            warnings.Add(message);
        }

        private class UseDestinationDuplicateTypeNamesHandler : IDuplicateTypeNamesHandler
        {
            public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args)
            {
                return DuplicateTypeAction.UseDestinationTypes;
            }
        }
    }
}
