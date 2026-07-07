using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using SAB.CreateViewsAndSheets.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.CreateViewsAndSheets.Services
{
    public class CreateViewsAndSheetsValidationResult
    {
        public CreateViewsAndSheetsValidationResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
        }

        public List<string> Errors { get; private set; }

        public List<string> Warnings { get; private set; }

        public bool IsValid
        {
            get { return Errors.Count == 0; }
        }
    }

    public class ValidationService
    {
        private readonly RevitDataService _dataService;

        public ValidationService()
        {
            _dataService = new RevitDataService();
        }

        public CreateViewsAndSheetsValidationResult ValidateBeforeExecution(
            Document document,
            CreateViewsAndSheetsSettings settings,
            IList<SheetCreationItem> items)
        {
            CreateViewsAndSheetsValidationResult result = new CreateViewsAndSheetsValidationResult();

            if (document == null)
            {
                result.Errors.Add("Документ Revit недоступен.");
                return result;
            }

            if (document.ActiveView == null)
            {
                result.Errors.Add("Активный вид Revit недоступен.");
            }

            if (settings == null)
            {
                result.Errors.Add("Настройки команды не получены.");
                return result;
            }

            View sourceView = null;
            Dictionary<string, FloorSourceValidationData> sourceDataByFloorName = null;
            Dictionary<string, MultiViewZoneValidationData> sourceDataByZoneName = null;
            if (settings.StructureMode == CreateViewsAndSheetsStructureMode.MultiStory)
            {
                sourceDataByFloorName = ValidateFloorMappings(document, settings, result);
            }
            else if (settings.StructureMode == CreateViewsAndSheetsStructureMode.MultiView)
            {
                sourceDataByZoneName = ValidateMultiViewZoneMappings(document, settings, result);
            }

            bool useSourceSheetViewportPlacement = settings.Placement != null && settings.Placement.UseSourceSheetViewportPlacement;
            if (settings.StructureMode != CreateViewsAndSheetsStructureMode.MultiView && !useSourceSheetViewportPlacement)
            {
                ValidateViewportType(document, settings.ViewportTypeId, result);
            }

            if (settings.StructureMode != CreateViewsAndSheetsStructureMode.MultiView)
            {
                ValidateTitleBlockType(document, settings.TitleBlockTypeId, result);
            }

            ValidateSheetBrowserParameters(document, settings.SheetBrowserParameterIds, result);
            ValidatePlacement(settings, result);
            ValidateRows(document, settings, sourceView, sourceDataByFloorName, sourceDataByZoneName, items, result);

            return result;
        }

        private View ValidateSourceView(Document document, ElementId sourceViewId, CreateViewsAndSheetsValidationResult result)
        {
            if (sourceViewId == null || sourceViewId == ElementId.InvalidElementId)
            {
                result.Errors.Add("Не выбран вид-образец.");
                return null;
            }

            View sourceView = document.GetElement(sourceViewId) as View;
            if (sourceView == null)
            {
                result.Errors.Add("Вид-образец не найден в документе.");
                return null;
            }

            if (sourceView.IsTemplate)
            {
                result.Errors.Add("Вид-образец не должен быть шаблоном вида.");
                return null;
            }

            try
            {
                if (!sourceView.CanViewBeDuplicated(ViewDuplicateOption.WithDetailing))
                {
                    result.Errors.Add("Выбранный вид-образец нельзя дублировать с детализацией.");
                }
            }
            catch (Exception exception)
            {
                result.Errors.Add("Не удалось проверить возможность дублирования вида-образца: " + exception.Message);
            }

            return sourceView;
        }

        private ViewSheet ValidateSourceSheet(Document document, ElementId sourceSheetId, CreateViewsAndSheetsValidationResult result)
        {
            if (sourceSheetId == null || sourceSheetId == ElementId.InvalidElementId)
            {
                result.Errors.Add("Не выбран лист-образец.");
                return null;
            }

            ViewSheet sourceSheet = document.GetElement(sourceSheetId) as ViewSheet;
            if (sourceSheet == null)
            {
                result.Errors.Add("Лист-образец не найден в документе.");
                return null;
            }

            return sourceSheet;
        }

        private Dictionary<string, FloorSourceValidationData> ValidateFloorMappings(
            Document document,
            CreateViewsAndSheetsSettings settings,
            CreateViewsAndSheetsValidationResult result)
        {
            Dictionary<string, FloorSourceValidationData> sourceDataByFloorName = new Dictionary<string, FloorSourceValidationData>(StringComparer.Ordinal);
            if (settings.FloorMappings == null || settings.FloorMappings.Count == 0)
            {
                result.Errors.Add("Для многоэтажной структуры не заполнено сопоставление этажей.");
                return sourceDataByFloorName;
            }

            for (int i = 0; i < settings.FloorMappings.Count; i++)
            {
                FloorSourceMapping mapping = settings.FloorMappings[i];
                if (mapping == null)
                {
                    continue;
                }

                string floorName = (mapping.FloorName ?? string.Empty).Trim();
                string rowPrefix = "Сопоставление этажа " + (i + 1) + ": ";
                if (string.IsNullOrWhiteSpace(floorName))
                {
                    result.Errors.Add(rowPrefix + "не заполнено поле Этаж.");
                    continue;
                }

                if (sourceDataByFloorName.ContainsKey(floorName))
                {
                    result.Errors.Add(rowPrefix + "этаж повторяется.");
                    continue;
                }

                FloorSourceValidationData sourceData = new FloorSourceValidationData();
                sourceData.FloorName = floorName;

                bool hasStandardData = mapping.SourceViewId != null && mapping.SourceViewId != ElementId.InvalidElementId ||
                                       mapping.SourceSheetId != null && mapping.SourceSheetId != ElementId.InvalidElementId;
                bool hasCeilingData = mapping.CeilingSourceViewId != null && mapping.CeilingSourceViewId != ElementId.InvalidElementId ||
                                      mapping.CeilingSourceSheetId != null && mapping.CeilingSourceSheetId != ElementId.InvalidElementId;

                if (hasStandardData)
                {
                    sourceData.StandardSourceView = ValidateSourceView(document, mapping.SourceViewId, result);
                    sourceData.StandardSourceSheet = ValidateSourceSheet(document, mapping.SourceSheetId, result);
                }

                if (hasCeilingData)
                {
                    sourceData.CeilingSourceView = ValidateSourceView(document, mapping.CeilingSourceViewId, result);
                    sourceData.CeilingSourceSheet = ValidateSourceSheet(document, mapping.CeilingSourceSheetId, result);
                }

                sourceDataByFloorName[floorName] = sourceData;
            }

            if (sourceDataByFloorName.Count == 0)
            {
                result.Errors.Add("Для многоэтажной структуры нет ни одного сопоставления этажа.");
            }

            return sourceDataByFloorName;
        }

        private Dictionary<string, MultiViewZoneValidationData> ValidateMultiViewZoneMappings(
            Document document,
            CreateViewsAndSheetsSettings settings,
            CreateViewsAndSheetsValidationResult result)
        {
            Dictionary<string, MultiViewZoneValidationData> sourceDataByZoneName = new Dictionary<string, MultiViewZoneValidationData>(StringComparer.Ordinal);
            if (settings.MultiViewZoneMappings == null || settings.MultiViewZoneMappings.Count == 0)
            {
                result.Errors.Add("Для многовидовой структуры не заполнены зоны.");
                return sourceDataByZoneName;
            }

            for (int i = 0; i < settings.MultiViewZoneMappings.Count; i++)
            {
                MultiViewZoneMapping mapping = settings.MultiViewZoneMappings[i];
                if (mapping == null)
                {
                    continue;
                }

                string zoneName = (mapping.ZoneName ?? string.Empty).Trim();
                string rowPrefix = "Зона " + (i + 1) + ": ";
                if (string.IsNullOrWhiteSpace(zoneName))
                {
                    result.Errors.Add(rowPrefix + "не заполнено название зоны.");
                    continue;
                }

                if (sourceDataByZoneName.ContainsKey(zoneName))
                {
                    result.Errors.Add(rowPrefix + "зона повторяется.");
                    continue;
                }

                MultiViewZoneValidationData sourceData = new MultiViewZoneValidationData();
                sourceData.ZoneName = zoneName;
                sourceData.SourceSheet = ValidateSourceSheet(document, mapping.SourceSheetId, result);
                ValidateViewportType(document, mapping.ViewportTypeId, result);
                ValidateTitleBlockType(document, mapping.TitleBlockTypeId, result);

                if (mapping.Floors == null || mapping.Floors.Count == 0)
                {
                    result.Errors.Add(rowPrefix + "не добавлены этажи.");
                }
                else
                {
                    HashSet<string> floorNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int j = 0; j < mapping.Floors.Count; j++)
                    {
                        MultiViewZoneFloorMapping floorMapping = mapping.Floors[j];
                        if (floorMapping == null)
                        {
                            continue;
                        }

                        string floorName = (floorMapping.FloorName ?? string.Empty).Trim();
                        string floorPrefix = rowPrefix + "этаж " + (j + 1) + ": ";
                        if (string.IsNullOrWhiteSpace(floorName))
                        {
                            result.Errors.Add(floorPrefix + "не заполнено название этажа.");
                            continue;
                        }

                        if (!floorNames.Add(floorName))
                        {
                            result.Errors.Add(floorPrefix + "этаж повторяется внутри зоны.");
                            continue;
                        }

                        View sourceView = ValidateSourceView(document, floorMapping.SourceViewId, result);
                        if (sourceView != null)
                        {
                            sourceData.SourceViewsByFloorName[floorName] = sourceView;
                        }
                    }
                }

                sourceDataByZoneName[zoneName] = sourceData;
            }

            if (sourceDataByZoneName.Count == 0)
            {
                result.Errors.Add("Для многовидовой структуры нет ни одной корректной зоны.");
            }

            return sourceDataByZoneName;
        }

        private void ValidateViewportType(Document document, ElementId viewportTypeId, CreateViewsAndSheetsValidationResult result)
        {
            if (viewportTypeId == null || viewportTypeId == ElementId.InvalidElementId)
            {
                result.Errors.Add("Не выбран тип Viewport.");
                return;
            }

            ElementType viewportType = document.GetElement(viewportTypeId) as ElementType;
            if (viewportType == null)
            {
                result.Errors.Add("Выбранный тип Viewport не найден в документе.");
                return;
            }

            if (viewportType.Category == null ||
                viewportType.Category.Id.IntegerValue != (int)BuiltInCategory.OST_Viewports)
            {
                result.Warnings.Add("Категория выбранного типа Viewport не определена как OST_Viewports. Допустимость будет проверена Revit API при назначении типа.");
            }
        }

        private void ValidateTitleBlockType(Document document, ElementId titleBlockTypeId, CreateViewsAndSheetsValidationResult result)
        {
            if (titleBlockTypeId == null || titleBlockTypeId == ElementId.InvalidElementId)
            {
                result.Errors.Add("Не выбран тип основной надписи.");
                return;
            }

            FamilySymbol titleBlockType = document.GetElement(titleBlockTypeId) as FamilySymbol;
            if (titleBlockType == null)
            {
                result.Errors.Add("Выбранный тип основной надписи не найден в документе.");
                return;
            }

            if (titleBlockType.Category == null ||
                titleBlockType.Category.Id.IntegerValue != (int)BuiltInCategory.OST_TitleBlocks)
            {
                result.Errors.Add("Выбранный тип основной надписи относится к неверной категории.");
            }
        }

        private void ValidateSheetBrowserParameters(Document document, IList<ElementId> parameterIds, CreateViewsAndSheetsValidationResult result)
        {
            if (parameterIds == null)
            {
                return;
            }

            for (int i = 0; i < parameterIds.Count; i++)
            {
                ValidateSheetBrowserParameter(document, parameterIds[i], result);
            }
        }

        private void ValidateSheetBrowserParameter(Document document, ElementId parameterId, CreateViewsAndSheetsValidationResult result)
        {
            if (parameterId == null || parameterId == ElementId.InvalidElementId)
            {
                return;
            }

            Parameter parameter = FindFirstSheetParameterById(document, parameterId);
            if (parameter == null)
            {
                result.Warnings.Add("Один из параметров группировки/сортировки листов не найден на листах документа и не будет заполнен.");
                return;
            }

            if (parameter.IsReadOnly)
            {
                result.Warnings.Add("Параметр листа '" + GetParameterName(parameter) + "' доступен только для чтения и не будет заполнен.");
                return;
            }

            if (parameter.StorageType == StorageType.ElementId || parameter.StorageType == StorageType.None)
            {
                result.Warnings.Add("Параметр листа '" + GetParameterName(parameter) + "' имеет неподдерживаемый тип данных и не будет заполнен.");
            }
        }

        private Parameter FindFirstSheetParameterById(Document document, ElementId parameterId)
        {
            if (document == null || parameterId == null)
            {
                return null;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(ViewSheet));
            foreach (Element element in collector)
            {
                ViewSheet sheet = element as ViewSheet;
                if (sheet == null || sheet.IsTemplate || sheet.Parameters == null)
                {
                    continue;
                }

                foreach (Parameter parameter in sheet.Parameters)
                {
                    if (parameter != null && parameter.Id != null && RevitElementIdUtils.AreEqual(parameter.Id, parameterId))
                    {
                        return parameter;
                    }
                }
            }

            return null;
        }

        private string GetParameterName(Parameter parameter)
        {
            if (parameter == null || parameter.Definition == null)
            {
                return string.Empty;
            }

            return parameter.Definition.Name ?? string.Empty;
        }

        private void ValidatePlacement(CreateViewsAndSheetsSettings settings, CreateViewsAndSheetsValidationResult result)
        {
            if (settings.Placement == null)
            {
                result.Errors.Add("Настройки размещения не получены.");
                return;
            }

            if (settings.Placement.UseSourceSheetViewportPlacement)
            {
                return;
            }

            if (settings.SheetBounds == null)
            {
                result.Errors.Add("Не определены габариты листа для проверки координат.");
                return;
            }

            PlacementSettings placement = settings.Placement;
            if (!settings.SheetBounds.ContainsPointMm(placement.ViewCenterXmm, placement.ViewCenterYmm))
            {
                result.Errors.Add("Координаты центра Viewport выходят за габарит листа.");
            }

            if (!settings.SheetBounds.ContainsPointMm(placement.ViewTitleXmm, placement.ViewTitleYmm))
            {
                result.Errors.Add("Координаты заголовка вида выходят за габарит листа.");
            }

            if (placement.TitleLineLengthMm <= 0)
            {
                result.Errors.Add("Длина линии заголовка должна быть больше нуля.");
            }

        }

        private void ValidateRows(
            Document document,
            CreateViewsAndSheetsSettings settings,
            View sourceView,
            Dictionary<string, FloorSourceValidationData> sourceDataByFloorName,
            Dictionary<string, MultiViewZoneValidationData> sourceDataByZoneName,
            IList<SheetCreationItem> items,
            CreateViewsAndSheetsValidationResult result)
        {
            if (items == null || items.Count == 0)
            {
                result.Errors.Add("Нет строк для создания видов и листов.");
                return;
            }

            HashSet<string> existingViewNames = _dataService.CollectExistingViewNames(document);
            HashSet<string> existingSheetNumbers = _dataService.CollectExistingSheetNumbers(document);
            HashSet<string> tableViewNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> tableSheetNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < items.Count; i++)
            {
                SheetCreationItem item = items[i];
                if (item == null)
                {
                    result.Errors.Add("Строка " + (i + 1) + ": данные строки отсутствуют.");
                    continue;
                }

                string rowPrefix = "Строка " + item.RowNumber + ": ";
                string viewName = (item.ViewName ?? string.Empty).Trim();
                string sheetNumber = (item.SheetNumber ?? string.Empty).Trim();
                string sheetName = (item.SheetName ?? string.Empty).Trim();
                View templateSourceView = sourceView;
                ViewSheet templateSourceSheet = null;

                if (settings.StructureMode == CreateViewsAndSheetsStructureMode.SingleStory)
                {
                    bool isCeilingPlan = item.PlanKind == SheetPlanKind.CeilingPlan;
                    ElementId sourceViewId = isCeilingPlan ? settings.CeilingSourceViewId : settings.SourceViewId;
                    ElementId sourceSheetId = isCeilingPlan ? settings.CeilingSourceSheetId : settings.SourceSheetId;
                    string planName = isCeilingPlan ? "плана потолков" : "стандартного плана";

                    templateSourceView = ValidateSourceView(document, sourceViewId, result);
                    templateSourceSheet = ValidateSourceSheet(document, sourceSheetId, result);
                    if (templateSourceView == null)
                    {
                        result.Errors.Add(rowPrefix + "не выбран корректный вид-образец " + planName + ".");
                    }
                }
                else if (settings.StructureMode == CreateViewsAndSheetsStructureMode.MultiView)
                {
                    string zoneName = (item.FloorName ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(zoneName))
                    {
                        result.Errors.Add(rowPrefix + "не заполнена зона.");
                    }
                    else
                    {
                        MultiViewZoneValidationData sourceData;
                        if (sourceDataByZoneName == null || !sourceDataByZoneName.TryGetValue(zoneName, out sourceData))
                        {
                            result.Errors.Add(rowPrefix + "для зоны \"" + zoneName + "\" нет корректного сопоставления.");
                        }
                        else
                        {
                            templateSourceSheet = sourceData.SourceSheet;
                            foreach (KeyValuePair<string, View> pair in sourceData.SourceViewsByFloorName)
                            {
                                if (templateSourceView == null)
                                {
                                    templateSourceView = pair.Value;
                                }

                                string generatedViewName = BuildMultiViewGeneratedViewName(viewName, sourceData.ZoneName, pair.Key);
                                if (!string.IsNullOrWhiteSpace(generatedViewName))
                                {
                                    if (!tableViewNames.Add(generatedViewName))
                                    {
                                        result.Errors.Add(rowPrefix + "имя вида \"" + generatedViewName + "\" повторяется в таблице.");
                                    }

                                    if (existingViewNames.Contains(generatedViewName))
                                    {
                                        result.Errors.Add(rowPrefix + "вид \"" + generatedViewName + "\" уже существует в документе.");
                                    }
                                }

                                if (settings.Placement != null && settings.Placement.UseSourceSheetViewportPlacement)
                                {
                                    ValidateSourceViewportPlacement(document, templateSourceSheet, pair.Value, rowPrefix, result);
                                }
                            }

                            if (sourceData.SourceViewsByFloorName.Count == 0)
                            {
                                result.Errors.Add(rowPrefix + "в зоне \"" + zoneName + "\" нет этажей с видами-образцами.");
                            }
                        }
                    }
                }
                else if (settings.StructureMode == CreateViewsAndSheetsStructureMode.MultiStory)
                {
                    string floorName = (item.FloorName ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(floorName))
                    {
                        result.Errors.Add(rowPrefix + "не заполнен этаж.");
                    }
                    else
                    {
                        FloorSourceValidationData sourceData;
                        if (sourceDataByFloorName == null || !sourceDataByFloorName.TryGetValue(floorName, out sourceData))
                        {
                            result.Errors.Add(rowPrefix + "для этажа \"" + floorName + "\" нет корректного сопоставления.");
                        }
                        else
                        {
                            bool isCeilingPlan = item.PlanKind == SheetPlanKind.CeilingPlan;
                            templateSourceView = isCeilingPlan ? sourceData.CeilingSourceView : sourceData.StandardSourceView;
                            templateSourceSheet = isCeilingPlan ? sourceData.CeilingSourceSheet : sourceData.StandardSourceSheet;
                            if (templateSourceView == null)
                            {
                                result.Errors.Add(rowPrefix + "для этажа \"" + floorName + "\" не выбран вид-образец " + (isCeilingPlan ? "плана потолков" : "стандартного плана") + ".");
                            }
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(viewName))
                {
                    result.Errors.Add(rowPrefix + (settings.StructureMode == CreateViewsAndSheetsStructureMode.MultiView
                        ? "не заполнена часть имени вида."
                        : "не заполнено имя вида."));
                }

                if (string.IsNullOrWhiteSpace(sheetNumber))
                {
                    result.Errors.Add(rowPrefix + "не заполнен номер листа.");
                }

                if (string.IsNullOrWhiteSpace(sheetName))
                {
                    result.Errors.Add(rowPrefix + "не заполнено имя листа.");
                }

                if (item.ViewScale <= 0)
                {
                    result.Errors.Add(rowPrefix + "масштаб должен быть положительным целым числом.");
                }

                if (settings.StructureMode != CreateViewsAndSheetsStructureMode.MultiView && !string.IsNullOrWhiteSpace(viewName))
                {
                    if (!tableViewNames.Add(viewName))
                    {
                        result.Errors.Add(rowPrefix + "имя вида повторяется в таблице.");
                    }

                    if (existingViewNames.Contains(viewName))
                    {
                        result.Errors.Add(rowPrefix + "вид с таким именем уже существует в документе.");
                    }
                }

                if (!string.IsNullOrWhiteSpace(sheetNumber))
                {
                    if (!tableSheetNumbers.Add(sheetNumber))
                    {
                        result.Errors.Add(rowPrefix + "номер листа повторяется в таблице.");
                    }

                    if (existingSheetNumbers.Contains(sheetNumber))
                    {
                        result.Errors.Add(rowPrefix + "лист с таким номером уже существует в документе.");
                    }
                }

                ValidateTemplate(document, templateSourceView, item, rowPrefix, result);

                if (settings.StructureMode != CreateViewsAndSheetsStructureMode.MultiView &&
                    settings.Placement != null && settings.Placement.UseSourceSheetViewportPlacement)
                {
                    ValidateSourceViewportPlacement(document, templateSourceSheet, templateSourceView, rowPrefix, result);
                }
            }
        }

        private void ValidateTemplate(
            Document document,
            View sourceView,
            SheetCreationItem item,
            string rowPrefix,
            CreateViewsAndSheetsValidationResult result)
        {
            if (item.ViewTemplateId == null || item.ViewTemplateId == ElementId.InvalidElementId)
            {
                return;
            }

            View templateView = document.GetElement(item.ViewTemplateId) as View;
            if (templateView == null || !templateView.IsTemplate)
            {
                result.Errors.Add(rowPrefix + "выбранный шаблон вида не найден или не является шаблоном.");
                return;
            }

            if (sourceView != null && templateView.ViewType != sourceView.ViewType)
            {
                result.Warnings.Add(rowPrefix + "шаблон вида не совместим с типом вида-образца.");
            }
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

        private void ValidateSourceViewportPlacement(
            Document document,
            ViewSheet sourceSheet,
            View sourceView,
            string rowPrefix,
            CreateViewsAndSheetsValidationResult result)
        {
            if (document == null || sourceSheet == null || sourceView == null || result == null)
            {
                return;
            }

            Viewport sourceViewport = FindViewportOnSheet(document, sourceSheet, sourceView);
            if (sourceViewport != null)
            {
                return;
            }

            result.Warnings.Add(WarningMessageSeverity.MarkCritical(
                rowPrefix +
                "на листе-образце " + sourceSheet.SheetNumber +
                " не найден размещенный вид-образец \"" + sourceView.Name + "\". " +
                "Лист будет создан без размещенного вида."));
        }

        private Viewport FindViewportOnSheet(Document document, ViewSheet sheet, View view)
        {
            if (document == null || sheet == null || view == null)
            {
                return null;
            }

            try
            {
                System.Collections.Generic.ICollection<ElementId> viewportIds = sheet.GetAllViewports();
                if (viewportIds != null)
                {
                    foreach (ElementId viewportId in viewportIds)
                    {
                        Viewport viewport = document.GetElement(viewportId) as Viewport;
                        if (viewport != null && RevitElementIdUtils.AreEqual(viewport.ViewId, view.Id))
                        {
                            return viewport;
                        }
                    }
                }
            }
            catch
            {
                // Резервный поиск ниже нужен для устойчивости на нестандартных листах.
            }

            FilteredElementCollector collector = new FilteredElementCollector(document, sheet.Id).OfClass(typeof(Viewport));
            foreach (Element element in collector)
            {
                Viewport viewport = element as Viewport;
                if (viewport != null && RevitElementIdUtils.AreEqual(viewport.ViewId, view.Id))
                {
                    return viewport;
                }
            }

            return null;
        }

        private class FloorSourceValidationData
        {
            public string FloorName { get; set; }

            public View StandardSourceView { get; set; }

            public View CeilingSourceView { get; set; }

            public ViewSheet StandardSourceSheet { get; set; }

            public ViewSheet CeilingSourceSheet { get; set; }
        }

        private class MultiViewZoneValidationData
        {
            public MultiViewZoneValidationData()
            {
                ZoneName = string.Empty;
                SourceViewsByFloorName = new Dictionary<string, View>(StringComparer.OrdinalIgnoreCase);
            }

            public string ZoneName { get; set; }

            public ViewSheet SourceSheet { get; set; }

            public Dictionary<string, View> SourceViewsByFloorName { get; private set; }
        }
    }
}


