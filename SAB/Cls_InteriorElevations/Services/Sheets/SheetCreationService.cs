using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Elevations;
using SAB.InteriorElevations.Utils;
using System;

namespace SAB.InteriorElevations.Services.Sheets
{
    public class SheetCreationService
    {
        public ViewSheet CreateSheet(
            Document document,
            ElevationSettings settings,
            RoomData roomData,
            ElevationNamingService namingService)
        {
            if (document == null || settings == null || namingService == null)
            {
                return null;
            }

            if (settings.TitleBlockTypeId == null || settings.TitleBlockTypeId == ElementId.InvalidElementId)
            {
                return null;
            }

            FamilySymbol titleBlockType = document.GetElement(settings.TitleBlockTypeId) as FamilySymbol;
            if (titleBlockType == null)
            {
                return null;
            }

            ViewSheet sheet = ViewSheet.Create(document, settings.TitleBlockTypeId);
            if (sheet == null)
            {
                return null;
            }

            // Блок уникальных параметров листа.
            sheet.Name = namingService.GenerateUniqueSheetName(roomData);
            sheet.SheetNumber = namingService.GenerateUniqueSheetNumber(roomData);
            TrySetSheetFormatAParameter(document, sheet, settings);

            return sheet;
        }

        public ViewSheet CreateCoordinateSelectionSheet(
            Document document,
            ElevationSettings settings,
            string baseSheetName)
        {
            if (document == null || settings == null)
            {
                return null;
            }

            if (settings.TitleBlockTypeId == null || settings.TitleBlockTypeId == ElementId.InvalidElementId)
            {
                return null;
            }

            FamilySymbol titleBlockType = document.GetElement(settings.TitleBlockTypeId) as FamilySymbol;
            if (titleBlockType == null)
            {
                return null;
            }

            // Блок имени служебного листа для ручного выбора координаты.
            string preparedBaseName = string.IsNullOrWhiteSpace(baseSheetName)
                ? "SAB-Развертки по линии"
                : baseSheetName.Trim();

            ViewSheet existingSheet = FindCoordinateSelectionSheet(document, preparedBaseName);
            if (existingSheet != null)
            {
                TryApplyTitleBlockType(document, existingSheet, settings);
                TrySetSheetFormatAParameter(document, existingSheet, settings);
                return existingSheet;
            }

            ViewSheet sheet = ViewSheet.Create(document, settings.TitleBlockTypeId);
            if (sheet == null)
            {
                return null;
            }

            sheet.Name = preparedBaseName;
            sheet.SheetNumber = preparedBaseName;
            TrySetSheetFormatAParameter(document, sheet, settings);

            return sheet;
        }

        private ViewSheet FindCoordinateSelectionSheet(Document document, string sheetNameOrNumber)
        {
            if (document == null || string.IsNullOrWhiteSpace(sheetNameOrNumber))
            {
                return null;
            }

            ViewSheet sheetWithSameName = null;
            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(ViewSheet));

            foreach (Element element in collector)
            {
                ViewSheet sheet = element as ViewSheet;
                if (sheet == null)
                {
                    continue;
                }

                if (string.Equals(sheet.SheetNumber, sheetNameOrNumber, StringComparison.OrdinalIgnoreCase))
                {
                    return sheet;
                }

                if (sheetWithSameName == null &&
                    string.Equals(sheet.Name, sheetNameOrNumber, StringComparison.OrdinalIgnoreCase))
                {
                    sheetWithSameName = sheet;
                }
            }

            return sheetWithSameName;
        }

        private void TryApplyTitleBlockType(Document document, ViewSheet sheet, ElevationSettings settings)
        {
            if (document == null || sheet == null || settings == null)
            {
                return;
            }

            if (settings.TitleBlockTypeId == null || settings.TitleBlockTypeId == ElementId.InvalidElementId)
            {
                return;
            }

            FamilySymbol titleBlockType = document.GetElement(settings.TitleBlockTypeId) as FamilySymbol;
            if (titleBlockType == null)
            {
                return;
            }

            if (!titleBlockType.IsActive)
            {
                titleBlockType.Activate();
            }

            bool titleBlockInstanceFound = false;
            FilteredElementCollector titleBlockCollector =
                new FilteredElementCollector(document, sheet.Id)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .WhereElementIsNotElementType();

            foreach (Element element in titleBlockCollector)
            {
                FamilyInstance titleBlockInstance = element as FamilyInstance;
                if (titleBlockInstance == null)
                {
                    continue;
                }

                titleBlockInstanceFound = true;
                if (!RevitElementIdUtils.AreEqual(titleBlockInstance.GetTypeId(), settings.TitleBlockTypeId))
                {
                    titleBlockInstance.ChangeTypeId(settings.TitleBlockTypeId);
                }
            }

            if (!titleBlockInstanceFound)
            {
                document.Create.NewFamilyInstance(XYZ.Zero, titleBlockType, sheet);
            }
        }

        private void TrySetSheetFormatAParameter(Document document, ViewSheet sheet, ElevationSettings settings)
        {
            if (document == null || sheet == null || settings == null || !settings.SheetFormatAValue.HasValue)
            {
                return;
            }

            int formatAValue = settings.SheetFormatAValue.Value;

            // Сначала пробуем записать параметр напрямую в лист.
            Parameter sheetParameter = sheet.LookupParameter("Формат А");
            if (TrySetIntegerParameterValue(sheetParameter, formatAValue))
            {
                return;
            }

            // Если у листа параметр отсутствует, пробуем записать в экземпляр основной надписи на этом листе.
            FilteredElementCollector titleBlockCollector =
                new FilteredElementCollector(document, sheet.Id)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .WhereElementIsNotElementType();

            foreach (Element element in titleBlockCollector)
            {
                FamilyInstance titleBlockInstance = element as FamilyInstance;
                if (titleBlockInstance == null)
                {
                    continue;
                }

                Parameter titleBlockParameter = titleBlockInstance.LookupParameter("Формат А");
                if (TrySetIntegerParameterValue(titleBlockParameter, formatAValue))
                {
                    return;
                }
            }

            // Если параметр является параметром типа основной надписи, пробуем записать его в выбранный тип.
            FamilySymbol titleBlockType = document.GetElement(settings.TitleBlockTypeId) as FamilySymbol;
            if (titleBlockType == null)
            {
                return;
            }

            Parameter titleBlockTypeParameter = titleBlockType.LookupParameter("Формат А");
            TrySetIntegerParameterValue(titleBlockTypeParameter, formatAValue);
        }

        private bool TrySetIntegerParameterValue(Parameter parameter, int value)
        {
            if (parameter == null || parameter.IsReadOnly)
            {
                return false;
            }

            if (parameter.StorageType != StorageType.Integer)
            {
                return false;
            }

            parameter.Set(value);
            return true;
        }
    }
}
