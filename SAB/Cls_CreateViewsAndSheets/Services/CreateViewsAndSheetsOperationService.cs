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
            return Execute(document, settings, items, null);
        }

        public CreateViewsAndSheetsResult Execute(
            Document document,
            CreateViewsAndSheetsSettings settings,
            IList<SheetCreationItem> items,
            IProgress<CreateViewsAndSheetsProgressInfo> progress)
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
            OperationProgressTracker progressTracker = new OperationProgressTracker(
                CalculateTotalProgressSteps(settings, items),
                items.Count);

            progressTracker.Report(
                progress,
                "Проверка настроек",
                "Проверяются выбранные строки, образцы видов, образцы листов и параметры размещения.");
            ValidateSettingsBeforeTransaction(settings, items);
            progressTracker.CompleteStep();

            using (TransactionGroup transactionGroup = new TransactionGroup(document, "SAB Создание видов и листов"))
            {
                transactionGroup.Start();

                try
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        SheetCreationItem item = items[i];
                        if (settings.StructureMode == CreateViewsAndSheetsStructureMode.MultiView)
                        {
                            ProcessMultiViewRow(document, settings, item, result, progress, progressTracker, i + 1, items.Count);
                        }
                        else
                        {
                            progressTracker.Report(
                                progress,
                                "Подбор образцов",
                                BuildRowProgressDetails(item, i + 1, items.Count, "поиск вида-образца и листа-образца"));
                            RowSourceSelection rowSource = ResolveRowSource(document, settings, item, sourceView);
                            progressTracker.CompleteStep();

                            ProcessSingleRow(
                                document,
                                rowSource.SourceView,
                                rowSource.SourceSheetId,
                                settings,
                                item,
                                result,
                                progress,
                                progressTracker,
                                i + 1,
                                items.Count);
                        }
                    }

                    transactionGroup.Assimilate();
                    progressTracker.Report(
                        progress,
                        "Готово",
                        "Создание видов и листов завершено.");
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
                if (string.Equals(mappingFloorName, cleanFloorName, StringComparison.Ordinal))
                {
                    return mapping;
                }
            }

            return null;
        }

        private MultiViewZoneMapping FindMultiViewZoneMapping(IList<MultiViewZoneMapping> mappings, string zoneName)
        {
            if (mappings == null)
            {
                return null;
            }

            string cleanZoneName = (zoneName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(cleanZoneName))
            {
                return null;
            }

            for (int i = 0; i < mappings.Count; i++)
            {
                MultiViewZoneMapping mapping = mappings[i];
                if (mapping == null)
                {
                    continue;
                }

                string mappingZoneName = (mapping.ZoneName ?? string.Empty).Trim();
                if (string.Equals(mappingZoneName, cleanZoneName, StringComparison.Ordinal))
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
            CreateViewsAndSheetsResult result,
            IProgress<CreateViewsAndSheetsProgressInfo> progress,
            OperationProgressTracker progressTracker,
            int currentRowIndex,
            int totalRows)
        {
            Transaction transaction = null;

            try
            {
                transaction = new Transaction(document, "Создать вид и лист: строка " + item.RowNumber);
                transaction.Start();

                progressTracker.Report(
                    progress,
                    "Дублирование вида",
                    BuildRowProgressDetails(item, currentRowIndex, totalRows, "создание копии вида \"" + item.ViewName + "\""));
                View duplicatedView = _viewDuplicationService.DuplicateView(
                    document,
                    sourceView,
                    item,
                    result.Warnings);
                progressTracker.CompleteStep();

                progressTracker.Report(
                    progress,
                    "Создание листа",
                    BuildRowProgressDetails(item, currentRowIndex, totalRows, "создание листа " + item.SheetNumber));
                ViewSheet createdSheet = _sheetCreationService.CreateSheet(
                    document,
                    settings.TitleBlockTypeId,
                    sourceSheetId,
                    item.SheetBrowserParameterValues,
                    item.SheetNumber,
                    item.SheetName,
                    result.Warnings);
                progressTracker.CompleteStep();

                document.Regenerate();

                progressTracker.Report(
                    progress,
                    "Копирование оформления",
                    BuildRowProgressDetails(item, currentRowIndex, totalRows, "копирование оформления с листа-образца"));
                _sheetDetailCopyService.CopyFromSourceSheet(
                    document,
                    sourceSheetId,
                    createdSheet,
                    settings.DetailCopy,
                    result.Warnings);
                progressTracker.CompleteStep();

                document.Regenerate();

                progressTracker.Report(
                    progress,
                    "Размещение вида",
                    BuildRowProgressDetails(item, currentRowIndex, totalRows, "размещение вида на созданном листе"));
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
                progressTracker.CompleteStep();

                CreatedViewSheetInfo info = new CreatedViewSheetInfo();
                info.RowNumber = item.RowNumber;
                info.ViewId = duplicatedView.Id;
                info.ViewName = duplicatedView.Name;
                info.SheetId = createdSheet.Id;
                info.SheetNumber = createdSheet.SheetNumber;
                info.SheetName = createdSheet.Name;
                result.CreatedItems.Add(info);

                progressTracker.Report(
                    progress,
                    "Сохранение строки",
                    BuildRowProgressDetails(item, currentRowIndex, totalRows, "сохранение изменений строки"));
                transaction.Commit();
                progressTracker.CompleteStep();
                progressTracker.CompleteRow();
                progressTracker.Report(
                    progress,
                    "Строка готова",
                    BuildRowProgressDetails(item, currentRowIndex, totalRows, "создание завершено"));
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

        private void ProcessMultiViewRow(
            Document document,
            CreateViewsAndSheetsSettings settings,
            SheetCreationItem item,
            CreateViewsAndSheetsResult result,
            IProgress<CreateViewsAndSheetsProgressInfo> progress,
            OperationProgressTracker progressTracker,
            int currentRowIndex,
            int totalRows)
        {
            Transaction transaction = null;

            try
            {
                progressTracker.Report(
                    progress,
                    "Подбор зоны",
                    BuildRowProgressDetails(item, currentRowIndex, totalRows, "поиск зоны и листа-образца"));
                MultiViewZoneMapping zoneMapping = FindMultiViewZoneMapping(settings.MultiViewZoneMappings, item != null ? item.FloorName : string.Empty);
                if (zoneMapping == null)
                {
                    throw new InvalidOperationException("Строка " + item.RowNumber + ": зона \"" + item.FloorName + "\" не найдена.");
                }

                ViewSheet sourceSheet = document.GetElement(zoneMapping.SourceSheetId) as ViewSheet;
                if (sourceSheet == null)
                {
                    throw new InvalidOperationException("Строка " + item.RowNumber + ": лист-образец зоны \"" + zoneMapping.ZoneName + "\" не найден.");
                }

                ElementId titleBlockTypeId = zoneMapping.TitleBlockTypeId != null && zoneMapping.TitleBlockTypeId != ElementId.InvalidElementId
                    ? zoneMapping.TitleBlockTypeId
                    : settings.TitleBlockTypeId;
                ElementId viewportTypeId = zoneMapping.ViewportTypeId != null && zoneMapping.ViewportTypeId != ElementId.InvalidElementId
                    ? zoneMapping.ViewportTypeId
                    : settings.ViewportTypeId;
                progressTracker.CompleteStep();

                transaction = new Transaction(document, "Создать многовидовой лист: строка " + item.RowNumber);
                transaction.Start();

                progressTracker.Report(
                    progress,
                    "Создание листа",
                    BuildRowProgressDetails(item, currentRowIndex, totalRows, "создание многовидового листа " + item.SheetNumber));
                ViewSheet createdSheet = _sheetCreationService.CreateSheet(
                    document,
                    titleBlockTypeId,
                    zoneMapping.SourceSheetId,
                    item.SheetBrowserParameterValues,
                    item.SheetNumber,
                    item.SheetName,
                    result.Warnings);
                progressTracker.CompleteStep();

                document.Regenerate();

                progressTracker.Report(
                    progress,
                    "Копирование оформления",
                    BuildRowProgressDetails(item, currentRowIndex, totalRows, "копирование оформления с листа-образца зоны"));
                _sheetDetailCopyService.CopyFromSourceSheet(
                    document,
                    zoneMapping.SourceSheetId,
                    createdSheet,
                    settings.DetailCopy,
                    result.Warnings);
                progressTracker.CompleteStep();

                document.Regenerate();

                for (int i = 0; i < zoneMapping.Floors.Count; i++)
                {
                    MultiViewZoneFloorMapping floorMapping = zoneMapping.Floors[i];
                    if (floorMapping == null)
                    {
                        continue;
                    }

                    View sourceView = document.GetElement(floorMapping.SourceViewId) as View;
                    if (sourceView == null)
                    {
                        throw new InvalidOperationException("Строка " + item.RowNumber + ": вид-образец этажа \"" + floorMapping.FloorName + "\" не найден.");
                    }

                    SheetCreationItem viewItem = CloneItemForMultiViewFloor(item, zoneMapping.ZoneName, floorMapping.FloorName);
                    progressTracker.Report(
                        progress,
                        "Дублирование вида",
                        BuildRowProgressDetails(item, currentRowIndex, totalRows, "создание вида этажа \"" + floorMapping.FloorName + "\""));
                    View duplicatedView = _viewDuplicationService.DuplicateView(
                        document,
                        sourceView,
                        viewItem,
                        result.Warnings);
                    progressTracker.CompleteStep();

                    document.Regenerate();

                    progressTracker.Report(
                        progress,
                        "Размещение вида",
                        BuildRowProgressDetails(item, currentRowIndex, totalRows, "размещение вида этажа \"" + floorMapping.FloorName + "\""));
                    _viewportPlacementService.PlaceViewOnSheetBySourceViewport(
                        document,
                        sourceSheet,
                        sourceView,
                        createdSheet,
                        duplicatedView,
                        viewportTypeId,
                        result.Warnings);
                    progressTracker.CompleteStep();

                    CreatedViewSheetInfo info = new CreatedViewSheetInfo();
                    info.RowNumber = item.RowNumber;
                    info.ViewId = duplicatedView.Id;
                    info.ViewName = duplicatedView.Name;
                    info.SheetId = createdSheet.Id;
                    info.SheetNumber = createdSheet.SheetNumber;
                    info.SheetName = createdSheet.Name;
                    result.CreatedItems.Add(info);
                }

                progressTracker.Report(
                    progress,
                    "Сохранение строки",
                    BuildRowProgressDetails(item, currentRowIndex, totalRows, "сохранение многовидового листа"));
                transaction.Commit();
                progressTracker.CompleteStep();
                progressTracker.CompleteRow();
                progressTracker.Report(
                    progress,
                    "Строка готова",
                    BuildRowProgressDetails(item, currentRowIndex, totalRows, "создание завершено"));
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

        private SheetCreationItem CloneItemForMultiViewFloor(SheetCreationItem sourceItem, string zoneName, string floorName)
        {
            SheetCreationItem item = new SheetCreationItem();
            item.RowNumber = sourceItem.RowNumber;
            item.PlanKind = sourceItem.PlanKind;
            item.FloorId = sourceItem.FloorId;
            item.FloorName = sourceItem.FloorName;
            item.ViewName = BuildMultiViewGeneratedViewName(sourceItem.ViewName, zoneName, floorName);
            item.ViewScale = sourceItem.ViewScale;
            item.ViewTemplateId = sourceItem.ViewTemplateId;
            item.SheetNumber = sourceItem.SheetNumber;
            item.SheetName = sourceItem.SheetName;
            item.SheetBrowserParameterValue = sourceItem.SheetBrowserParameterValue;
            item.SheetBrowserParameterValues = sourceItem.SheetBrowserParameterValues;
            return item;
        }

        private string BuildMultiViewGeneratedViewName(string sectionName, string zoneName, string floorName)
        {
            string cleanSectionName = (sectionName ?? string.Empty).Trim();
            string cleanZoneName = (zoneName ?? string.Empty).Trim();
            string cleanFloorName = (floorName ?? string.Empty).Trim();
            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(cleanSectionName))
            {
                parts.Add(cleanSectionName);
            }

            if (!string.IsNullOrWhiteSpace(cleanZoneName))
            {
                parts.Add(cleanZoneName);
            }

            if (!string.IsNullOrWhiteSpace(cleanFloorName))
            {
                parts.Add(cleanFloorName);
            }

            return string.Join(" ", parts);
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
                    throw new InvalidOperationException("Строка " + item.RowNumber + ": " +
                                                        (settings.StructureMode == CreateViewsAndSheetsStructureMode.MultiView
                                                            ? "не заполнена часть имени вида."
                                                            : "не заполнено имя вида."));
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
            if (settings.StructureMode == CreateViewsAndSheetsStructureMode.MultiView)
            {
                ValidateMultiViewSourceMappingsBeforeTransaction(settings, items);
                return;
            }

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

            Dictionary<string, FloorSourceMapping> mappingsByFloorName = new Dictionary<string, FloorSourceMapping>(StringComparer.Ordinal);
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

        private void ValidateMultiViewSourceMappingsBeforeTransaction(CreateViewsAndSheetsSettings settings, IList<SheetCreationItem> items)
        {
            if (settings.MultiViewZoneMappings == null || settings.MultiViewZoneMappings.Count == 0)
            {
                throw new InvalidOperationException("Для многовидовой структуры не заполнены зоны.");
            }

            Dictionary<string, MultiViewZoneMapping> mappingsByZoneName = new Dictionary<string, MultiViewZoneMapping>(StringComparer.Ordinal);
            for (int i = 0; i < settings.MultiViewZoneMappings.Count; i++)
            {
                MultiViewZoneMapping mapping = settings.MultiViewZoneMappings[i];
                if (mapping == null)
                {
                    continue;
                }

                string zoneName = (mapping.ZoneName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(zoneName))
                {
                    throw new InvalidOperationException("В сопоставлении зон есть строка без названия зоны.");
                }

                if (mappingsByZoneName.ContainsKey(zoneName))
                {
                    throw new InvalidOperationException("Зона \"" + zoneName + "\" повторяется в сопоставлении зон.");
                }

                if (mapping.SourceSheetId == null || mapping.SourceSheetId == ElementId.InvalidElementId)
                {
                    throw new InvalidOperationException("Для зоны \"" + zoneName + "\" не выбран лист-образец.");
                }

                if (mapping.ViewportTypeId == null || mapping.ViewportTypeId == ElementId.InvalidElementId)
                {
                    throw new InvalidOperationException("Для зоны \"" + zoneName + "\" не выбран тип Viewport.");
                }

                if (mapping.TitleBlockTypeId == null || mapping.TitleBlockTypeId == ElementId.InvalidElementId)
                {
                    throw new InvalidOperationException("Для зоны \"" + zoneName + "\" не выбрана основная надпись.");
                }

                if (mapping.Floors == null || mapping.Floors.Count == 0)
                {
                    throw new InvalidOperationException("Для зоны \"" + zoneName + "\" не добавлены этажи.");
                }

                mappingsByZoneName[zoneName] = mapping;
            }

            for (int i = 0; i < items.Count; i++)
            {
                SheetCreationItem item = items[i];
                string zoneName = item != null ? (item.FloorName ?? string.Empty).Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(zoneName))
                {
                    throw new InvalidOperationException("Строка " + (item != null ? item.RowNumber : i + 1) + ": не заполнена зона.");
                }

                MultiViewZoneMapping mapping;
                if (!mappingsByZoneName.TryGetValue(zoneName, out mapping))
                {
                    throw new InvalidOperationException("Строка " + item.RowNumber + ": для зоны \"" + zoneName + "\" нет сопоставления.");
                }

                int completeFloorCount = 0;
                for (int j = 0; j < mapping.Floors.Count; j++)
                {
                    MultiViewZoneFloorMapping floor = mapping.Floors[j];
                    if (floor == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(floor.FloorName))
                    {
                        throw new InvalidOperationException("Зона \"" + zoneName + "\": один из этажей без названия.");
                    }

                    if (floor.SourceViewId == null || floor.SourceViewId == ElementId.InvalidElementId)
                    {
                        throw new InvalidOperationException("Зона \"" + zoneName + "\": для этажа \"" + floor.FloorName + "\" не выбран вид-образец.");
                    }

                    completeFloorCount++;
                }

                if (completeFloorCount == 0)
                {
                    throw new InvalidOperationException("Строка " + item.RowNumber + ": в зоне \"" + zoneName + "\" нет этажей с видами-образцами.");
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

        private int CalculateTotalProgressSteps(CreateViewsAndSheetsSettings settings, IList<SheetCreationItem> items)
        {
            // Блок настройки общего количества шагов прогресса.
            // Если позже добавятся новые этапы создания, их нужно отразить здесь и в местах progressTracker.CompleteStep().
            int totalSteps = 1;

            if (settings == null || items == null)
            {
                return totalSteps;
            }

            for (int i = 0; i < items.Count; i++)
            {
                SheetCreationItem item = items[i];
                if (settings.StructureMode == CreateViewsAndSheetsStructureMode.MultiView)
                {
                    int floorCount = CountMultiViewFloorsForProgress(settings, item);
                    totalSteps += 4 + floorCount * 2;
                }
                else
                {
                    totalSteps += 6;
                }
            }

            return totalSteps > 0 ? totalSteps : 1;
        }

        private int CountMultiViewFloorsForProgress(CreateViewsAndSheetsSettings settings, SheetCreationItem item)
        {
            if (settings == null || item == null)
            {
                return 0;
            }

            MultiViewZoneMapping mapping = FindMultiViewZoneMapping(settings.MultiViewZoneMappings, item.FloorName);
            if (mapping == null || mapping.Floors == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < mapping.Floors.Count; i++)
            {
                if (mapping.Floors[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private string BuildRowProgressDetails(SheetCreationItem item, int currentRowIndex, int totalRows, string actionText)
        {
            string details = "Строка " + currentRowIndex + " из " + totalRows;
            if (item != null)
            {
                details += " (табличная строка " + item.RowNumber + ")";
                if (!string.IsNullOrWhiteSpace(item.SheetNumber))
                {
                    details += ", лист " + item.SheetNumber;
                }
            }

            if (!string.IsNullOrWhiteSpace(actionText))
            {
                details += ": " + actionText + ".";
            }

            return details;
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

        private class OperationProgressTracker
        {
            public OperationProgressTracker(int totalSteps, int totalItems)
            {
                TotalSteps = totalSteps > 0 ? totalSteps : 1;
                TotalItems = totalItems > 0 ? totalItems : 0;
            }

            public int CurrentStep { get; private set; }

            public int TotalSteps { get; private set; }

            public int ProcessedItems { get; private set; }

            public int TotalItems { get; private set; }

            public void CompleteStep()
            {
                if (CurrentStep < TotalSteps)
                {
                    CurrentStep++;
                }
            }

            public void CompleteRow()
            {
                if (ProcessedItems < TotalItems)
                {
                    ProcessedItems++;
                }
            }

            public void Report(IProgress<CreateViewsAndSheetsProgressInfo> progress, string stage, string details)
            {
                if (progress == null)
                {
                    return;
                }

                CreateViewsAndSheetsProgressInfo progressInfo = new CreateViewsAndSheetsProgressInfo();
                progressInfo.CurrentStep = CurrentStep;
                progressInfo.TotalSteps = TotalSteps;
                progressInfo.ProcessedItems = ProcessedItems;
                progressInfo.TotalItems = TotalItems;
                progressInfo.Stage = stage;
                progressInfo.Details = details;

                progress.Report(progressInfo);
            }
        }

        private bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}

