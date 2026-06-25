using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SAB.CreateViewsAndSheets.Services
{
    public class SheetCreationService
    {
        public ViewSheet CreateSheet(
            Document document,
            ElementId titleBlockTypeId,
            ElementId sourceSheetId,
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

            sheet.SheetNumber = sheetNumber;
            sheet.Name = sheetName;

            // Блок копирования второстепенных параметров основной надписи с эталонного листа.
            document.Regenerate();
            CopyTitleBlockParametersFromSourceSheet(document, sourceSheetId, sheet, warnings);

            return sheet;
        }

        private void CopyTitleBlockParametersFromSourceSheet(
            Document document,
            ElementId sourceSheetId,
            ViewSheet targetSheet,
            IList<string> warnings)
        {
            if (document == null || sourceSheetId == null || sourceSheetId == ElementId.InvalidElementId || targetSheet == null)
            {
                return;
            }

            FamilyInstance sourceTitleBlock = FindTitleBlockInstance(document, sourceSheetId);
            FamilyInstance targetTitleBlock = FindTitleBlockInstance(document, targetSheet.Id);

            if (sourceTitleBlock == null || targetTitleBlock == null)
            {
                AddWarning(warnings, "Не удалось скопировать параметры основной надписи: основная надпись не найдена на эталонном или созданном листе.");
                return;
            }

            CopyParameterValue(sourceTitleBlock, targetTitleBlock, "Формат", warnings);
            CopyParameterValue(sourceTitleBlock, targetTitleBlock, "Кратность", warnings);
            CopyParameterValue(sourceTitleBlock, targetTitleBlock, "Книжная ориентация", warnings);
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

        private void CopyParameterValue(
            FamilyInstance sourceTitleBlock,
            FamilyInstance targetTitleBlock,
            string parameterName,
            IList<string> warnings)
        {
            if (sourceTitleBlock == null || targetTitleBlock == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            Parameter sourceParameter = FindParameter(sourceTitleBlock, parameterName);
            Parameter targetParameter = FindParameter(targetTitleBlock, parameterName);

            if (sourceParameter == null || targetParameter == null)
            {
                AddWarning(warnings, "Параметр основной надписи '" + parameterName + "' не найден на эталонном или созданном листе.");
                return;
            }

            if (targetParameter.IsReadOnly)
            {
                AddWarning(warnings, "Параметр основной надписи '" + parameterName + "' доступен только для чтения.");
                return;
            }

            if (sourceParameter.StorageType != targetParameter.StorageType)
            {
                AddWarning(warnings, "Параметр основной надписи '" + parameterName + "' имеет разный тип данных на эталонном и созданном листе.");
                return;
            }

            try
            {
                SetParameterValue(targetParameter, sourceParameter);
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось скопировать параметр основной надписи '" + parameterName + "': " + exception.Message);
            }
        }

        private Parameter FindParameter(FamilyInstance titleBlockInstance, string parameterName)
        {
            if (titleBlockInstance == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return null;
            }

            Parameter parameter = titleBlockInstance.LookupParameter(parameterName);
            if (parameter != null)
            {
                return parameter;
            }

            FamilySymbol symbol = titleBlockInstance.Symbol;
            if (symbol == null)
            {
                return null;
            }

            return symbol.LookupParameter(parameterName);
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
