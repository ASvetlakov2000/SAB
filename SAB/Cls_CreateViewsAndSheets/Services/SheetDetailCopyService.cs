using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using SAB.CreateViewsAndSheets.Models;

namespace SAB.CreateViewsAndSheets.Services
{
    public class SheetDetailCopyService
    {
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

            CopySheetOwnedElements(document, sourceSheet, targetSheet, settings, warnings);
            CopySchedules(document, sourceSheet, targetSheet, settings, warnings);
            CopyPlacedLegendAndDraftingViews(document, sourceSheet, targetSheet, settings, warnings);
        }

        private void CopySheetOwnedElements(
            Document document,
            ViewSheet sourceSheet,
            ViewSheet targetSheet,
            SheetDetailCopySettings settings,
            IList<string> warnings)
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
                ElementTransformUtils.CopyElements(sourceSheet, elementIds, targetSheet, Transform.Identity, copyOptions);
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
            IList<string> warnings)
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
                    ScheduleSheetInstance.Create(document, targetSheet.Id, sourceSchedule.ScheduleId, sourceSchedule.Point);
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
            IList<string> warnings)
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
                    CopyLegendViewport(document, sourceViewport, sourceView, targetSheet, warnings);
                }
                else if (sourceView.ViewType == ViewType.DraftingView && settings.CopyDraftingViews)
                {
                    CopyDraftingViewport(document, sourceViewport, sourceView, targetSheet, warnings);
                }
            }
        }

        private void CopyLegendViewport(
            Document document,
            Viewport sourceViewport,
            View legendView,
            ViewSheet targetSheet,
            IList<string> warnings)
        {
            try
            {
                if (!Viewport.CanAddViewToSheet(document, targetSheet.Id, legendView.Id))
                {
                    AddWarning(warnings, "Легенда \"" + legendView.Name + "\" не может быть размещена на созданном листе.");
                    return;
                }

                Viewport targetViewport = Viewport.Create(document, targetSheet.Id, legendView.Id, sourceViewport.GetBoxCenter());
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
            IList<string> warnings)
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

                Viewport targetViewport = Viewport.Create(document, targetSheet.Id, duplicatedDraftingView.Id, sourceViewport.GetBoxCenter());
                TryCopyViewportType(sourceViewport, targetViewport);
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось скопировать чертежный вид \"" + draftingView.Name + "\": " + exception.Message);
            }
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
