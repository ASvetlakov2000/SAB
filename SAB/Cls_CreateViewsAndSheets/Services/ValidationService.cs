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

            View sourceView = ValidateSourceView(document, settings.SourceViewId, result);
            ValidateSourceSheet(document, settings.SourceSheetId, result);
            ValidateViewportType(document, settings.ViewportTypeId, result);
            ValidateTitleBlockType(document, settings.TitleBlockTypeId, result);
            ValidatePlacement(settings, result);
            ValidateRows(document, sourceView, items, result);

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
            View sourceView,
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

                ValidateTemplate(document, sourceView, item, rowPrefix, result);
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
    }
}
