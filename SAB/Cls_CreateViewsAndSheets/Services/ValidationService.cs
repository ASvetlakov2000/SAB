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
            if (settings.StructureMode == CreateViewsAndSheetsStructureMode.MultiStory)
            {
                sourceDataByFloorName = ValidateFloorMappings(document, settings, result);
            }

            ValidateViewportType(document, settings.ViewportTypeId, result);
            ValidateTitleBlockType(document, settings.TitleBlockTypeId, result);
            ValidateSheetBrowserParameters(document, settings.SheetBrowserParameterIds, result);
            ValidatePlacement(settings, result);
            ValidateRows(document, settings, sourceView, sourceDataByFloorName, items, result);

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

        private void ValidateSourceSheet(Document document, ElementId sourceSheetId, CreateViewsAndSheetsValidationResult result)
        {
            if (sourceSheetId == null || sourceSheetId == ElementId.InvalidElementId)
            {
                result.Errors.Add("Не выбран лист-образец.");
                return;
            }

            ViewSheet sourceSheet = document.GetElement(sourceSheetId) as ViewSheet;
            if (sourceSheet == null)
            {
                result.Errors.Add("Лист-образец не найден в документе.");
            }
        }

        private Dictionary<string, FloorSourceValidationData> ValidateFloorMappings(
            Document document,
            CreateViewsAndSheetsSettings settings,
            CreateViewsAndSheetsValidationResult result)
        {
            Dictionary<string, FloorSourceValidationData> sourceDataByFloorName = new Dictionary<string, FloorSourceValidationData>(StringComparer.OrdinalIgnoreCase);
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
                    ValidateSourceSheet(document, mapping.SourceSheetId, result);
                }

                if (hasCeilingData)
                {
                    sourceData.CeilingSourceView = ValidateSourceView(document, mapping.CeilingSourceViewId, result);
                    ValidateSourceSheet(document, mapping.CeilingSourceSheetId, result);
                }

                sourceDataByFloorName[floorName] = sourceData;
            }

            if (sourceDataByFloorName.Count == 0)
            {
                result.Errors.Add("Для многоэтажной структуры нет ни одного сопоставления этажа.");
            }

            return sourceDataByFloorName;
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

                if (settings.StructureMode == CreateViewsAndSheetsStructureMode.SingleStory)
                {
                    bool isCeilingPlan = item.PlanKind == SheetPlanKind.CeilingPlan;
                    ElementId sourceViewId = isCeilingPlan ? settings.CeilingSourceViewId : settings.SourceViewId;
                    ElementId sourceSheetId = isCeilingPlan ? settings.CeilingSourceSheetId : settings.SourceSheetId;
                    string planName = isCeilingPlan ? "плана потолков" : "стандартного плана";

                    templateSourceView = ValidateSourceView(document, sourceViewId, result);
                    ValidateSourceSheet(document, sourceSheetId, result);
                    if (templateSourceView == null)
                    {
                        result.Errors.Add(rowPrefix + "не выбран корректный вид-образец " + planName + ".");
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
                            if (templateSourceView == null)
                            {
                                result.Errors.Add(rowPrefix + "для этажа \"" + floorName + "\" не выбран вид-образец " + (isCeilingPlan ? "плана потолков" : "стандартного плана") + ".");
                            }
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(viewName))
                {
                    result.Errors.Add(rowPrefix + "не заполнено имя вида.");
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

                if (!string.IsNullOrWhiteSpace(viewName))
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
                result.Errors.Add(rowPrefix + "не выбран шаблон вида.");
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
                result.Errors.Add(rowPrefix + "шаблон вида несовместим с типом вида-образца.");
            }
        }

        private class FloorSourceValidationData
        {
            public string FloorName { get; set; }

            public View StandardSourceView { get; set; }

            public View CeilingSourceView { get; set; }
        }
    }
}


