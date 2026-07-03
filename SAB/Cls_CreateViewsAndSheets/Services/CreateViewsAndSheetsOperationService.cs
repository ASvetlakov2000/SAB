using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using SAB.CreateViewsAndSheets.Models;

namespace SAB.CreateViewsAndSheets.Services
{
    public class RowProcessingException : Exception
    {
        public RowProcessingException(SheetCreationItem item, Exception innerException)
            : base(BuildMessage(item, innerException), innerException)
        {
            Item = item;
        }

        public SheetCreationItem Item { get; private set; }

        private static string BuildMessage(SheetCreationItem item, Exception innerException)
        {
            string rowNumber = item != null ? item.RowNumber.ToString() : "?";
            string viewName = item != null ? item.ViewName : string.Empty;
            string sheetNumber = item != null ? item.SheetNumber : string.Empty;
            string reason = innerException != null ? innerException.Message : "неизвестная ошибка";

            return "Ошибка в строке " + rowNumber +
                   ". Вид: " + viewName +
                   ". Лист: " + sheetNumber +
                   ". Причина: " + reason;
        }
    }

    public class CreateViewsAndSheetsOperationService
    {
        private readonly ViewDuplicationService _viewDuplicationService;
        private readonly SheetCreationService _sheetCreationService;
        private readonly SheetDetailCopyService _sheetDetailCopyService;
        private readonly SheetBoundsService _sheetBoundsService;
        private readonly ViewportPlacementService _viewportPlacementService;

        public CreateViewsAndSheetsOperationService()
        {
            _viewDuplicationService = new ViewDuplicationService();
            _sheetCreationService = new SheetCreationService();
            _sheetDetailCopyService = new SheetDetailCopyService();
            _sheetBoundsService = new SheetBoundsService();
            _viewportPlacementService = new ViewportPlacementService();
        }

        public CreateViewsAndSheetsResult Execute(
            Document document,
            CreateViewsAndSheetsSettings settings,
            IList<SheetCreationItem> items)
        {
            if (document == null)
            {
                throw new InvalidOperationException("Документ Revit недоступен.");
            }

            if (settings == null)
            {
                throw new InvalidOperationException("Настройки создания видов и листов не получены.");
            }

            if (items == null || items.Count == 0)
            {
                throw new InvalidOperationException("Нет строк для создания видов и листов.");
            }

            View sourceView = settings.StructureMode == CreateViewsAndSheetsStructureMode.SingleStory
                ? null
                : null;

            CreateViewsAndSheetsResult result = new CreateViewsAndSheetsResult();

            ValidateSettingsBeforeTransaction(settings, items);

            using (TransactionGroup transactionGroup = new TransactionGroup(document, "SAB Создание видов и листов"))
            {
                transactionGroup.Start();

                try
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        SheetCreationItem item = items[i];
                        RowSourceSelection rowSource = ResolveRowSource(document, settings, item, sourceView);
                        ProcessSingleRow(document, rowSource.SourceView, rowSource.SourceSheetId, settings, item, result);
                    }

                    transactionGroup.Assimilate();
                }
                catch (RowProcessingException)
                {
                    transactionGroup.RollBack();
                    throw;
                }
                catch (Exception exception)
                {
                    transactionGroup.RollBack();
                    throw new RowProcessingException(null, exception);
                }
            }

            return result;
        }

        private RowSourceSelection ResolveRowSource(
            Document document,
            CreateViewsAndSheetsSettings settings,
            SheetCreationItem item,
            View singleStorySourceView)
        {
            if (settings.StructureMode == CreateViewsAndSheetsStructureMode.SingleStory)
            {
                bool isSingleStoryCeilingPlan = item != null && item.PlanKind == SheetPlanKind.CeilingPlan;
                ElementId singleStorySourceViewId = isSingleStoryCeilingPlan ? settings.CeilingSourceViewId : settings.SourceViewId;
                ElementId singleStorySourceSheetId = isSingleStoryCeilingPlan ? settings.CeilingSourceSheetId : settings.SourceSheetId;
                string singleStoryPlanName = isSingleStoryCeilingPlan ? "плана потолков" : "стандартного плана";

                View singleStorySource = document.GetElement(singleStorySourceViewId) as View;
                if (singleStorySource == null)
                {
                    throw new InvalidOperationException("Вид-образец " + singleStoryPlanName + " не найден в документе.");
                }

                ViewSheet singleStorySheet = document.GetElement(singleStorySourceSheetId) as ViewSheet;
                if (singleStorySheet == null)
                {
                    throw new InvalidOperationException("Лист-образец " + singleStoryPlanName + " не найден в документе.");
                }

                return new RowSourceSelection(singleStorySource, singleStorySourceSheetId);
            }

            FloorSourceMapping mapping = FindFloorMapping(settings.FloorMappings, item != null ? item.FloorName : string.Empty);
            if (mapping == null)
            {
                throw new InvalidOperationException("Для этажа \"" + (item != null ? item.FloorName : string.Empty) + "\" не найдено сопоставление вида-образца и листа-образца.");
            }

            bool isCeilingPlan = item != null && item.PlanKind == SheetPlanKind.CeilingPlan;
            ElementId sourceViewId = isCeilingPlan ? mapping.CeilingSourceViewId : mapping.SourceViewId;
            ElementId sourceSheetId = isCeilingPlan ? mapping.CeilingSourceSheetId : mapping.SourceSheetId;

            View sourceView = document.GetElement(sourceViewId) as View;
            if (sourceView == null)
            {
                throw new InvalidOperationException("Для этажа \"" + mapping.FloorName + "\" вид-образец не найден в документе.");
            }

            ViewSheet sourceSheet = document.GetElement(sourceSheetId) as ViewSheet;
            if (sourceSheet == null)
            {
                throw new InvalidOperationException("Для этажа \"" + mapping.FloorName + "\" лист-образец не найден в документе.");
            }

            return new RowSourceSelection(sourceView, sourceSheetId);
        }

        private FloorSourceMapping FindFloorMapping(IList<FloorSourceMapping> mappings, string floorName)
        {
            if (mappings == null)
            {
                return null;
            }

            string cleanFloorName = (floorName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(cleanFloorName))
            {
                return null;
            }

            for (int i = 0; i < mappings.Count; i++)
            {
                FloorSourceMapping mapping = mappings[i];
                if (mapping == null)
                {
                    continue;
                }

                string mappingFloorName = (mapping.FloorName ?? string.Empty).Trim();
                if (string.Equals(mappingFloorName, cleanFloorName, StringComparison.OrdinalIgnoreCase))
                {
                    return mapping;
                }
            }

            return null;
        }

        private void ProcessSingleRow(
            Document document,
            View sourceView,
            ElementId sourceSheetId,
            CreateViewsAndSheetsSettings settings,
            SheetCreationItem item,
            CreateViewsAndSheetsResult result)
        {
            Transaction transaction = null;

            try
            {
                transaction = new Transaction(document, "Создать вид и лист: строка " + item.RowNumber);
                transaction.Start();

                View duplicatedView = _viewDuplicationService.DuplicateView(
                    document,
                    sourceView,
                    item,
                    result.Warnings);

                ViewSheet createdSheet = _sheetCreationService.CreateSheet(
                    document,
                    settings.TitleBlockTypeId,
                    sourceSheetId,
                    item.SheetBrowserParameterValues,
                    item.SheetNumber,
                    item.SheetName,
                    result.Warnings);

                document.Regenerate();

                _sheetDetailCopyService.CopyFromSourceSheet(
                    document,
                    sourceSheetId,
                    createdSheet,
                    settings.DetailCopy,
                    result.Warnings);

                document.Regenerate();

                if (settings.Placement != null && settings.Placement.UseSourceSheetViewportPlacement)
                {
                    ViewSheet sourceSheet = document.GetElement(sourceSheetId) as ViewSheet;
                    _viewportPlacementService.PlaceViewOnSheetBySourceViewport(
                        document,
                        sourceSheet,
                        sourceView,
                        createdSheet,
                        duplicatedView,
                        settings.ViewportTypeId,
                        result.Warnings);
                }
                else
                {
                    SheetBounds actualSheetBounds;
                    if (!_sheetBoundsService.TryGetSheetBounds(document, createdSheet, out actualSheetBounds))
                    {
                        actualSheetBounds = settings.SheetBounds;
                        result.Warnings.Add(
                            "Строка " + item.RowNumber +
                            ": габарит созданного листа не определен, использован габарит из окна настроек.");
                    }

                    _viewportPlacementService.PlaceViewOnSheet(
                        document,
                        createdSheet,
                        duplicatedView,
                        settings.ViewportTypeId,
                        actualSheetBounds,
                        settings.Placement,
                        result.Warnings);
                }

                CreatedViewSheetInfo info = new CreatedViewSheetInfo();
                info.RowNumber = item.RowNumber;
                info.ViewId = duplicatedView.Id;
                info.ViewName = duplicatedView.Name;
                info.SheetId = createdSheet.Id;
                info.SheetNumber = createdSheet.SheetNumber;
                info.SheetName = createdSheet.Name;
                result.CreatedItems.Add(info);

                transaction.Commit();
            }
            catch (Exception exception)
            {
                if (transaction != null && transaction.GetStatus() == TransactionStatus.Started)
                {
                    transaction.RollBack();
                }

                throw new RowProcessingException(item, exception);
            }
            finally
            {
                if (transaction != null)
                {
                    transaction.Dispose();
                }
            }
        }

        private void ValidateSettingsBeforeTransaction(CreateViewsAndSheetsSettings settings, IList<SheetCreationItem> items)
        {
            if (settings == null)
            {
                throw new InvalidOperationException("Настройки создания видов и листов не получены.");
            }

            bool useSourceSheetViewportPlacement = settings.Placement != null && settings.Placement.UseSourceSheetViewportPlacement;
            if (!useSourceSheetViewportPlacement && settings.SheetBounds == null)
            {
                throw new InvalidOperationException("Не определены габариты листа для создания.");
            }

            ValidatePlacementBeforeTransaction(settings.SheetBounds, settings.Placement);

            if (items == null || items.Count == 0)
            {
                throw new InvalidOperationException("Нет строк для создания видов и листов.");
            }

            ValidateSourceMappingsBeforeTransaction(settings, items);

            for (int i = 0; i < items.Count; i++)
            {
                SheetCreationItem item = items[i];
                if (item == null)
                {
                    throw new InvalidOperationException("Одна из строк создания не содержит данных.");
                }

                if (string.IsNullOrWhiteSpace(item.ViewName))
                {
                    throw new InvalidOperationException("Строка " + item.RowNumber + ": не заполнено имя вида.");
                }

                if (string.IsNullOrWhiteSpace(item.SheetNumber))
                {
                    throw new InvalidOperationException("Строка " + item.RowNumber + ": не заполнен номер листа.");
                }

                if (string.IsNullOrWhiteSpace(item.SheetName))
                {
                    throw new InvalidOperationException("Строка " + item.RowNumber + ": не заполнено имя листа.");
                }

                if (item.ViewScale <= 0)
                {
                    throw new InvalidOperationException("Строка " + item.RowNumber + ": масштаб должен быть больше нуля.");
                }
            }
        }

        private void ValidateSourceMappingsBeforeTransaction(CreateViewsAndSheetsSettings settings, IList<SheetCreationItem> items)
        {
            if (settings.StructureMode == CreateViewsAndSheetsStructureMode.SingleStory)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    SheetCreationItem item = items[i];
                    bool isCeilingPlan = item != null && item.PlanKind == SheetPlanKind.CeilingPlan;
                    ElementId sourceViewId = isCeilingPlan ? settings.CeilingSourceViewId : settings.SourceViewId;
                    ElementId sourceSheetId = isCeilingPlan ? settings.CeilingSourceSheetId : settings.SourceSheetId;
                    string planName = isCeilingPlan ? "плана потолков" : "стандартного плана";
                    int rowNumber = item != null ? item.RowNumber : i + 1;

                    if (sourceViewId == null || sourceViewId == ElementId.InvalidElementId)
                    {
                        throw new InvalidOperationException("Строка " + rowNumber + ": не выбран вид-образец " + planName + ".");
                    }

                    if (sourceSheetId == null || sourceSheetId == ElementId.InvalidElementId)
                    {
                        throw new InvalidOperationException("Строка " + rowNumber + ": не выбран лист-образец " + planName + ".");
                    }
                }

                return;
            }

            if (settings.FloorMappings == null || settings.FloorMappings.Count == 0)
            {
                throw new InvalidOperationException("Для многоэтажной структуры не заполнено сопоставление этажей.");
            }

            Dictionary<string, FloorSourceMapping> mappingsByFloorName = new Dictionary<string, FloorSourceMapping>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < settings.FloorMappings.Count; i++)
            {
                FloorSourceMapping mapping = settings.FloorMappings[i];
                if (mapping == null)
                {
                    continue;
                }

                string floorName = (mapping.FloorName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(floorName))
                {
                    throw new InvalidOperationException("В сопоставлении этажей есть строка без названия этажа.");
                }

                if (mappingsByFloorName.ContainsKey(floorName))
                {
                    throw new InvalidOperationException("Этаж \"" + floorName + "\" повторяется в сопоставлении этажей.");
                }

                mappingsByFloorName[floorName] = mapping;
            }

            for (int i = 0; i < items.Count; i++)
            {
                SheetCreationItem item = items[i];
                string floorName = item != null ? (item.FloorName ?? string.Empty).Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(floorName))
                {
                    throw new InvalidOperationException("Строка " + (item != null ? item.RowNumber : i + 1) + ": не заполнен этаж.");
                }

                FloorSourceMapping mapping;
                if (!mappingsByFloorName.TryGetValue(floorName, out mapping))
                {
                    throw new InvalidOperationException("Строка " + item.RowNumber + ": для этажа \"" + floorName + "\" нет сопоставления.");
                }

                bool isCeilingPlan = item != null && item.PlanKind == SheetPlanKind.CeilingPlan;
                ElementId sourceViewId = isCeilingPlan ? mapping.CeilingSourceViewId : mapping.SourceViewId;
                ElementId sourceSheetId = isCeilingPlan ? mapping.CeilingSourceSheetId : mapping.SourceSheetId;
                string planName = isCeilingPlan ? "плана потолков" : "стандартного плана";

                if (sourceViewId == null || sourceViewId == ElementId.InvalidElementId)
                {
                    throw new InvalidOperationException("Строка " + item.RowNumber + ": для этажа \"" + floorName + "\" не выбран вид-образец " + planName + ".");
                }

                if (sourceSheetId == null || sourceSheetId == ElementId.InvalidElementId)
                {
                    throw new InvalidOperationException("Строка " + item.RowNumber + ": для этажа \"" + floorName + "\" не выбран лист-образец " + planName + ".");
                }
            }
        }

        private void ValidatePlacementBeforeTransaction(SheetBounds sheetBounds, PlacementSettings placement)
        {
            if (placement == null)
            {
                throw new InvalidOperationException("Настройки размещения не получены.");
            }

            if (placement.UseSourceSheetViewportPlacement)
            {
                return;
            }

            if (sheetBounds == null)
            {
                throw new InvalidOperationException("Не определены габариты листа для создания.");
            }

            if (!IsFinite(sheetBounds.MinXFeet) ||
                !IsFinite(sheetBounds.MinYFeet) ||
                !IsFinite(sheetBounds.WidthFeet) ||
                !IsFinite(sheetBounds.HeightFeet) ||
                sheetBounds.WidthFeet <= 1e-9 ||
                sheetBounds.HeightFeet <= 1e-9)
            {
                throw new InvalidOperationException("Габарит листа содержит некорректные значения.");
            }

            if (!IsFinite(placement.ViewCenterXmm) ||
                !IsFinite(placement.ViewCenterYmm) ||
                !IsFinite(placement.ViewTitleXmm) ||
                !IsFinite(placement.ViewTitleYmm) ||
                !IsFinite(placement.TitleLineLengthMm))
            {
                throw new InvalidOperationException("Координаты размещения содержат некорректные значения.");
            }

            if (!sheetBounds.ContainsPointMm(placement.ViewCenterXmm, placement.ViewCenterYmm))
            {
                throw new InvalidOperationException("Координаты центра Viewport выходят за габарит листа.");
            }

            if (!sheetBounds.ContainsPointMm(placement.ViewTitleXmm, placement.ViewTitleYmm))
            {
                throw new InvalidOperationException("Координаты заголовка Viewport выходят за габарит листа.");
            }

            if (placement.TitleLineLengthMm <= 0)
            {
                throw new InvalidOperationException("Длина линии заголовка должна быть больше нуля.");
            }
        }

        private class RowSourceSelection
        {
            public RowSourceSelection(View sourceView, ElementId sourceSheetId)
            {
                SourceView = sourceView;
                SourceSheetId = sourceSheetId;
            }

            public View SourceView { get; private set; }

            public ElementId SourceSheetId { get; private set; }
        }

        private bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}

