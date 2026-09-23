using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.Revit.DB;
using SAB.CreateViewsAndSheets.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.CreateViewsAndSheets.Services
{
    public class SheetCreationService
    {
        public ViewSheet CreateSheet(
            Document document,
            ElementId titleBlockTypeId,
            ElementId sourceSheetId,
            IList<SheetBrowserParameterValueItem> sheetBrowserParameterValues,
            string sheetNumber,
            string sheetName,
            IList<string> warnings)
        {
            if (document == null)
            {
                throw new InvalidOperationException("Документ Revit недоступен.");
            }

            if (titleBlockTypeId == null || titleBlockTypeId == ElementId.InvalidElementId)
            {
                throw new InvalidOperationException("Не выбран тип основной надписи.");
            }

            ViewSheet sheet = ViewSheet.Create(document, titleBlockTypeId);
            if (sheet == null)
            {
                throw new InvalidOperationException("Revit API не создал новый лист.");
            }

            // Сначала наследуем параметры листа-образца. Явные значения строки применяются последними.
            document.Regenerate();
            CopyParametersFromSourceSheet(document, sourceSheetId, sheet, warnings);

            sheet.SheetNumber = sheetNumber;
            sheet.Name = sheetName;
            SetSheetBrowserParameterValues(sheet, sheetBrowserParameterValues, warnings);

            return sheet;
        }

        private void SetSheetBrowserParameterValues(
            ViewSheet sheet,
            IList<SheetBrowserParameterValueItem> parameterValues,
            IList<string> warnings)
        {
            if (sheet == null || parameterValues == null)
            {
                return;
            }

            for (int i = 0; i < parameterValues.Count; i++)
            {
                SheetBrowserParameterValueItem parameterValue = parameterValues[i];
                if (parameterValue == null)
                {
                    continue;
                }

                SetSheetBrowserParameterValue(
                    sheet,
                    parameterValue.ParameterId,
                    parameterValue.Value,
                    warnings);
            }
        }

        private void SetSheetBrowserParameterValue(
            ViewSheet sheet,
            ElementId parameterId,
            string parameterValue,
            IList<string> warnings)
        {
            if (sheet == null || parameterId == null || parameterId == ElementId.InvalidElementId)
            {
                return;
            }

            Parameter parameter = FindParameterById(sheet, parameterId);
            if (parameter == null)
            {
                AddWarning(warnings, "Параметр листа для диспетчера проекта не найден на созданном листе.");
                return;
            }

            if (parameter.IsReadOnly)
            {
                AddWarning(warnings, "Параметр листа '" + GetParameterName(parameter) + "' доступен только для чтения.");
                return;
            }

            try
            {
                SetParameterValueFromText(parameter, parameterValue, warnings);
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось заполнить параметр листа '" + GetParameterName(parameter) + "': " + exception.Message);
            }
        }

        private void SetParameterValueFromText(Parameter parameter, string valueText, IList<string> warnings)
        {
            string cleanValue = valueText ?? string.Empty;
            if (parameter.StorageType == StorageType.String)
            {
                parameter.Set(cleanValue);
                return;
            }

            if (string.IsNullOrWhiteSpace(cleanValue))
            {
                return;
            }

            if (parameter.StorageType == StorageType.Integer)
            {
                int intValue;
                if (int.TryParse(cleanValue, NumberStyles.Integer, CultureInfo.CurrentCulture, out intValue) ||
                    int.TryParse(cleanValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out intValue))
                {
                    parameter.Set(intValue);
                    return;
                }

                AddWarning(warnings, "Значение '" + cleanValue + "' не удалось записать в целочисленный параметр листа '" + GetParameterName(parameter) + "'.");
                return;
            }

            if (parameter.StorageType == StorageType.Double)
            {
                double doubleValue;
                if (double.TryParse(cleanValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out doubleValue) ||
                    double.TryParse(cleanValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out doubleValue))
                {
                    parameter.Set(doubleValue);
                    return;
                }

                AddWarning(warnings, "Значение '" + cleanValue + "' не удалось записать в числовой параметр листа '" + GetParameterName(parameter) + "'.");
                return;
            }

            AddWarning(warnings, "Параметр листа '" + GetParameterName(parameter) + "' имеет неподдерживаемый тип данных и не был заполнен.");
        }

        private Parameter FindParameterById(Element element, ElementId parameterId)
        {
            if (element == null || parameterId == null || element.Parameters == null)
            {
                return null;
            }

            foreach (Parameter parameter in element.Parameters)
            {
                if (parameter != null && parameter.Id != null && RevitElementIdUtils.AreEqual(parameter.Id, parameterId))
                {
                    return parameter;
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

        private void CopyParametersFromSourceSheet(
            Document document,
            ElementId sourceSheetId,
            ViewSheet targetSheet,
            IList<string> warnings)
        {
            if (document == null || sourceSheetId == null || sourceSheetId == ElementId.InvalidElementId || targetSheet == null)
            {
                return;
            }

            ViewSheet sourceSheet = document.GetElement(sourceSheetId) as ViewSheet;
            if (sourceSheet == null)
            {
                AddWarning(warnings, "Не удалось скопировать параметры листа: лист-образец не найден.");
                return;
            }

            CopyWritableParameterValues(sourceSheet, targetSheet, "листа", warnings);

            FamilyInstance sourceTitleBlock = FindTitleBlockInstance(document, sourceSheetId);
            FamilyInstance targetTitleBlock = FindTitleBlockInstance(document, targetSheet.Id);

            if (sourceTitleBlock == null || targetTitleBlock == null)
            {
                AddWarning(warnings, "Не удалось скопировать параметры основной надписи: основная надпись не найдена на эталонном или созданном листе.");
                return;
            }

            CopyWritableParameterValues(sourceTitleBlock, targetTitleBlock, "основной надписи", warnings);
        }

        private FamilyInstance FindTitleBlockInstance(Document document, ElementId sheetId)
        {
            if (document == null || sheetId == null || sheetId == ElementId.InvalidElementId)
            {
                return null;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document, sheetId)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType();

            foreach (Element element in collector)
            {
                FamilyInstance titleBlockInstance = element as FamilyInstance;
                if (titleBlockInstance != null)
                {
                    return titleBlockInstance;
                }
            }

            return null;
        }

        private void CopyWritableParameterValues(
            Element sourceElement,
            Element targetElement,
            string elementDescription,
            IList<string> warnings)
        {
            if (sourceElement == null || targetElement == null || sourceElement.Parameters == null)
            {
                return;
            }

            foreach (Parameter sourceParameter in sourceElement.Parameters)
            {
                if (!CanCopyTemplateParameter(sourceParameter))
                {
                    continue;
                }

                Parameter targetParameter = FindMatchingParameter(targetElement, sourceParameter);
                if (targetParameter == null ||
                    targetParameter.IsReadOnly ||
                    targetParameter.StorageType != sourceParameter.StorageType)
                {
                    continue;
                }

                try
                {
                    SetParameterValue(targetParameter, sourceParameter);
                }
                catch (Exception exception)
                {
                    AddWarning(
                        warnings,
                        "Не удалось скопировать параметр " + elementDescription + " '" +
                        GetParameterName(sourceParameter) + "': " + exception.Message);
                }
            }
        }

        private bool CanCopyTemplateParameter(Parameter parameter)
        {
            if (parameter == null ||
                parameter.StorageType == StorageType.None ||
                !parameter.HasValue)
            {
                return false;
            }

            long parameterIdValue = RevitElementIdUtils.GetElementIdValue(parameter.Id);
            return parameterIdValue != (int)BuiltInParameter.SHEET_NUMBER &&
                   parameterIdValue != (int)BuiltInParameter.SHEET_NAME &&
                   parameterIdValue != (int)BuiltInParameter.VIEW_NAME;
        }

        private Parameter FindMatchingParameter(Element targetElement, Parameter sourceParameter)
        {
            if (targetElement == null || sourceParameter == null)
            {
                return null;
            }

            Parameter parameter = FindParameterById(targetElement, sourceParameter.Id);
            if (parameter != null)
            {
                return parameter;
            }

            string parameterName = GetParameterName(sourceParameter);
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                return null;
            }

            try
            {
                return targetElement.LookupParameter(parameterName);
            }
            catch
            {
                return null;
            }
        }

        private void SetParameterValue(Parameter targetParameter, Parameter sourceParameter)
        {
            if (targetParameter.StorageType == StorageType.String)
            {
                targetParameter.Set(sourceParameter.AsString() ?? string.Empty);
                return;
            }

            if (targetParameter.StorageType == StorageType.Integer)
            {
                targetParameter.Set(sourceParameter.AsInteger());
                return;
            }

            if (targetParameter.StorageType == StorageType.Double)
            {
                targetParameter.Set(sourceParameter.AsDouble());
                return;
            }

            if (targetParameter.StorageType == StorageType.ElementId)
            {
                targetParameter.Set(sourceParameter.AsElementId());
            }
        }

        private void AddWarning(IList<string> warnings, string text)
        {
            if (warnings == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            warnings.Add(text);
        }
    }
}
