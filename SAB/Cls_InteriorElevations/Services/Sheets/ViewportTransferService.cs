using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Marks;

namespace SAB.InteriorElevations.Services.Sheets
{
    public class ViewportTransferService
    {
        public ViewportTransferResult TransferViewports(
            Document document,
            ViewSheet sourceSheet,
            IList<Viewport> selectedViewports,
            IList<FamilyInstance> selectedSheetMarks,
            IList<string> warnings)
        {
            ViewportTransferResult result = new ViewportTransferResult();
            result.SourceSheet = sourceSheet;
            result.SelectedViewportCount = selectedViewports != null ? selectedViewports.Count : 0;
            result.SelectedSheetMarkCount = selectedSheetMarks != null ? selectedSheetMarks.Count : 0;
            result.SelectedCount = result.SelectedViewportCount + result.SelectedSheetMarkCount;

            if (document == null || sourceSheet == null || result.SelectedCount == 0)
            {
                return result;
            }

            ElementId titleBlockTypeId = GetTitleBlockTypeId(document, sourceSheet);
            if (titleBlockTypeId == ElementId.InvalidElementId)
            {
                AddWarning(warnings, "Не удалось определить тип основной надписи исходного листа.");
                return result;
            }

            ViewSheet targetSheet = ViewSheet.Create(document, titleBlockTypeId);
            if (targetSheet == null)
            {
                AddWarning(warnings, "Не удалось создать новый лист для переноса элементов.");
                return result;
            }

            result.TargetSheet = targetSheet;

            HashSet<string> usedSheetNames = CollectUsedSheetNames(document);

            string sourceSheetOriginalName = sourceSheet.Name ?? string.Empty;
            string sourceSheetOriginalNumber = sourceSheet.SheetNumber ?? string.Empty;

            usedSheetNames.Remove(sourceSheetOriginalName);

            string baseName = RemovePartSuffix(sourceSheetOriginalName);
            string sourcePartName = GetUniqueValue(baseName + ". Часть 1-2", usedSheetNames, "_");
            sourceSheet.Name = sourcePartName;
            usedSheetNames.Add(sourcePartName);

            string targetPartName = GetUniqueValue(baseName + ". Часть 2-2", usedSheetNames, "_");
            targetSheet.Name = targetPartName;
            usedSheetNames.Add(targetPartName);

            string insertedTargetNumber = GetInsertedSheetNumber(
                document,
                sourceSheet,
                targetSheet,
                sourceSheetOriginalNumber,
                warnings);
            targetSheet.SheetNumber = insertedTargetNumber;

            TransferViewportInstances(document, sourceSheet, targetSheet, selectedViewports, result, warnings);
            TransferSheetMarkInstances(document, sourceSheet, targetSheet, selectedSheetMarks, result, warnings);

            result.MovedCount = result.MovedViewportCount + result.CopiedViewportCount + result.MovedSheetMarkCount;
            result.FailedCount = result.FailedViewportCount + result.FailedSheetMarkCount;
            return result;
        }

        private void TransferViewportInstances(
            Document document,
            ViewSheet sourceSheet,
            ViewSheet targetSheet,
            IList<Viewport> selectedViewports,
            ViewportTransferResult result,
            IList<string> warnings)
        {
            if (selectedViewports == null || selectedViewports.Count == 0)
            {
                return;
            }

            HashSet<long> processedViewportIds = new HashSet<long>();

            for (int index = 0; index < selectedViewports.Count; index++)
            {
                Viewport sourceViewport = selectedViewports[index];
                if (sourceViewport == null || !sourceViewport.IsValidObject)
                {
                    continue;
                }

                long viewportKey = sourceViewport.Id.IntegerValue;
                if (processedViewportIds.Contains(viewportKey))
                {
                    continue;
                }

                processedViewportIds.Add(viewportKey);

                if (!CanProcessViewport(sourceViewport, sourceSheet.Id))
                {
                    result.FailedViewportCount++;
                    AddWarning(warnings, "Выбранный viewport не принадлежит активному листу и был пропущен.");
                    continue;
                }

                ElementId viewId = sourceViewport.ViewId;
                ElementId viewportTypeId = sourceViewport.GetTypeId();
                XYZ viewportCenter = sourceViewport.GetBoxCenter();
                View sourceView = document.GetElement(viewId) as View;

                if (IsFloorPlanView(sourceView))
                {
                    bool copiedSuccessfully = TryCopyFloorPlanViewport(
                        document,
                        targetSheet,
                        sourceView,
                        viewportTypeId,
                        viewportCenter,
                        warnings);

                    if (copiedSuccessfully)
                    {
                        result.CopiedViewportCount++;
                    }
                    else
                    {
                        result.FailedViewportCount++;
                    }

                    continue;
                }

                try
                {
                    document.Delete(sourceViewport.Id);

                    if (!Viewport.CanAddViewToSheet(document, targetSheet.Id, viewId))
                    {
                        TryRestoreViewport(document, sourceSheet, viewId, viewportTypeId, viewportCenter, warnings);
                        result.FailedViewportCount++;
                        AddWarning(warnings, "Вид не удалось добавить на новый лист. Вид восстановлен на исходном листе.");
                        continue;
                    }

                    Viewport targetViewport = Viewport.Create(document, targetSheet.Id, viewId, viewportCenter);
                    if (targetViewport == null)
                    {
                        TryRestoreViewport(document, sourceSheet, viewId, viewportTypeId, viewportCenter, warnings);
                        result.FailedViewportCount++;
                        AddWarning(warnings, "Не удалось создать viewport на новом листе. Вид восстановлен на исходном листе.");
                        continue;
                    }

                    TryApplyViewportType(targetViewport, viewportTypeId);
                    result.MovedViewportCount++;
                }
                catch (Exception exception)
                {
                    TryRestoreViewport(document, sourceSheet, viewId, viewportTypeId, viewportCenter, warnings);
                    result.FailedViewportCount++;
                    AddWarning(warnings, "Ошибка переноса viewport: " + exception.Message);
                }
            }
        }

        private bool TryCopyFloorPlanViewport(
            Document document,
            ViewSheet targetSheet,
            View sourceView,
            ElementId viewportTypeId,
            XYZ viewportCenter,
            IList<string> warnings)
        {
            if (document == null || targetSheet == null || sourceView == null)
            {
                AddWarning(warnings, "Не удалось скопировать план-схему: недостаточно входных данных.");
                return false;
            }

            ElementId duplicatedViewId = ElementId.InvalidElementId;
            try
            {
                // Копируем план-схему с детализацией, чтобы вместе с видом переносились марки и аннотации.
                duplicatedViewId = sourceView.Duplicate(ViewDuplicateOption.WithDetailing);
                if (duplicatedViewId == ElementId.InvalidElementId)
                {
                    AddWarning(warnings, "Не удалось создать дубликат вида план-схемы.");
                    return false;
                }

                if (!Viewport.CanAddViewToSheet(document, targetSheet.Id, duplicatedViewId))
                {
                    AddWarning(warnings, "Дубликат план-схемы нельзя разместить на новом листе.");
                    TryDeleteElement(document, duplicatedViewId, warnings, "Не удалось удалить дубликат план-схемы после ошибки размещения.");
                    return false;
                }

                Viewport copiedViewport = Viewport.Create(document, targetSheet.Id, duplicatedViewId, viewportCenter);
                if (copiedViewport == null)
                {
                    AddWarning(warnings, "Не удалось создать viewport для дубликата план-схемы.");
                    TryDeleteElement(document, duplicatedViewId, warnings, "Не удалось удалить дубликат план-схемы после ошибки создания viewport.");
                    return false;
                }

                TryApplyViewportType(copiedViewport, viewportTypeId);
                return true;
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Ошибка копирования план-схемы: " + exception.Message);
                if (duplicatedViewId != ElementId.InvalidElementId)
                {
                    TryDeleteElement(document, duplicatedViewId, warnings, "Не удалось удалить дубликат план-схемы после исключения.");
                }

                return false;
            }
        }

        private bool IsFloorPlanView(View view)
        {
            if (view == null)
            {
                return false;
            }

            return view.ViewType == ViewType.FloorPlan;
        }

        private void TryDeleteElement(Document document, ElementId elementId, IList<string> warnings, string warningText)
        {
            if (document == null || elementId == null || elementId == ElementId.InvalidElementId)
            {
                return;
            }

            try
            {
                document.Delete(elementId);
            }
            catch
            {
                AddWarning(warnings, warningText);
            }
        }

        private void TransferSheetMarkInstances(
            Document document,
            ViewSheet sourceSheet,
            ViewSheet targetSheet,
            IList<FamilyInstance> selectedSheetMarks,
            ViewportTransferResult result,
            IList<string> warnings)
        {
            if (selectedSheetMarks == null || selectedSheetMarks.Count == 0)
            {
                return;
            }

            HashSet<long> processedMarkIds = new HashSet<long>();

            for (int index = 0; index < selectedSheetMarks.Count; index++)
            {
                FamilyInstance sourceMark = selectedSheetMarks[index];
                if (sourceMark == null || !sourceMark.IsValidObject)
                {
                    continue;
                }

                long markKey = sourceMark.Id.IntegerValue;
                if (processedMarkIds.Contains(markKey))
                {
                    continue;
                }

                processedMarkIds.Add(markKey);

                if (!CanProcessSheetMark(sourceMark, sourceSheet.Id))
                {
                    result.FailedSheetMarkCount++;
                    AddWarning(warnings, "Выбранная марка не принадлежит активному листу или имеет неверное семейство.");
                    continue;
                }

                LocationPoint locationPoint = sourceMark.Location as LocationPoint;
                if (locationPoint == null)
                {
                    result.FailedSheetMarkCount++;
                    AddWarning(warnings, "У марки угла отсутствует точка размещения.");
                    continue;
                }

                XYZ markPoint = locationPoint.Point;
                ElementId markTypeId = sourceMark.GetTypeId();
                string roomNumberValue = GetTextParameterValue(sourceMark, CornerMarkConstants.RoomNumberParameterName);
                string cornerNumberValue = GetTextParameterValue(sourceMark, CornerMarkConstants.CornerNumberParameterName);
                FamilySymbol markSymbol = document.GetElement(markTypeId) as FamilySymbol;

                if (markSymbol == null)
                {
                    result.FailedSheetMarkCount++;
                    AddWarning(warnings, "Не удалось определить тип выбранной марки угла.");
                    continue;
                }

                try
                {
                    document.Delete(sourceMark.Id);

                    if (!markSymbol.IsActive)
                    {
                        markSymbol.Activate();
                        document.Regenerate();
                    }

                    FamilyInstance targetMark = document.Create.NewFamilyInstance(markPoint, markSymbol, targetSheet);
                    if (targetMark == null)
                    {
                        result.FailedSheetMarkCount++;
                        AddWarning(warnings, "Не удалось создать марку угла на новом листе.");
                        TryRestoreSheetMark(document, sourceSheet, markSymbol, markPoint, roomNumberValue, cornerNumberValue, warnings);
                        continue;
                    }

                    TrySetTextParameter(targetMark, CornerMarkConstants.RoomNumberParameterName, roomNumberValue);
                    TrySetTextParameter(targetMark, CornerMarkConstants.CornerNumberParameterName, cornerNumberValue);
                    result.MovedSheetMarkCount++;
                }
                catch (Exception exception)
                {
                    result.FailedSheetMarkCount++;
                    AddWarning(warnings, "Ошибка переноса марки угла: " + exception.Message);
                    TryRestoreSheetMark(document, sourceSheet, markSymbol, markPoint, roomNumberValue, cornerNumberValue, warnings);
                }
            }
        }

        private bool CanProcessViewport(Viewport viewport, ElementId sourceSheetId)
        {
            if (viewport == null || sourceSheetId == null || sourceSheetId == ElementId.InvalidElementId)
            {
                return false;
            }

            return viewport.OwnerViewId == sourceSheetId;
        }

        private bool CanProcessSheetMark(FamilyInstance markInstance, ElementId sourceSheetId)
        {
            if (markInstance == null || sourceSheetId == null || sourceSheetId == ElementId.InvalidElementId)
            {
                return false;
            }

            if (markInstance.OwnerViewId != sourceSheetId || markInstance.Symbol == null)
            {
                return false;
            }

            string familyName = markInstance.Symbol.Family != null
                ? markInstance.Symbol.Family.Name
                : markInstance.Symbol.FamilyName;

            return string.Equals(familyName, CornerMarkConstants.SheetFamilyName, StringComparison.OrdinalIgnoreCase);
        }

        private void TryRestoreViewport(
            Document document,
            ViewSheet sourceSheet,
            ElementId viewId,
            ElementId viewportTypeId,
            XYZ center,
            IList<string> warnings)
        {
            try
            {
                if (!Viewport.CanAddViewToSheet(document, sourceSheet.Id, viewId))
                {
                    return;
                }

                Viewport restoredViewport = Viewport.Create(document, sourceSheet.Id, viewId, center);
                if (restoredViewport != null)
                {
                    TryApplyViewportType(restoredViewport, viewportTypeId);
                }
            }
            catch (Exception restoreException)
            {
                AddWarning(warnings, "Не удалось восстановить viewport на исходном листе: " + restoreException.Message);
            }
        }

        private void TryRestoreSheetMark(
            Document document,
            ViewSheet sourceSheet,
            FamilySymbol symbol,
            XYZ point,
            string roomNumberValue,
            string cornerNumberValue,
            IList<string> warnings)
        {
            try
            {
                if (symbol == null)
                {
                    return;
                }

                if (!symbol.IsActive)
                {
                    symbol.Activate();
                    document.Regenerate();
                }

                FamilyInstance restoredMark = document.Create.NewFamilyInstance(point, symbol, sourceSheet);
                if (restoredMark != null)
                {
                    TrySetTextParameter(restoredMark, CornerMarkConstants.RoomNumberParameterName, roomNumberValue);
                    TrySetTextParameter(restoredMark, CornerMarkConstants.CornerNumberParameterName, cornerNumberValue);
                }
            }
            catch (Exception restoreException)
            {
                AddWarning(warnings, "Не удалось восстановить марку на исходном листе: " + restoreException.Message);
            }
        }

        private void TryApplyViewportType(Viewport viewport, ElementId viewportTypeId)
        {
            if (viewport == null || viewportTypeId == null || viewportTypeId == ElementId.InvalidElementId)
            {
                return;
            }

            try
            {
                viewport.ChangeTypeId(viewportTypeId);
            }
            catch
            {
            }
        }

        private string GetTextParameterValue(FamilyInstance markInstance, string parameterName)
        {
            if (markInstance == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return string.Empty;
            }

            Parameter parameter = markInstance.LookupParameter(parameterName);
            if (parameter == null)
            {
                return string.Empty;
            }

            if (parameter.StorageType == StorageType.String)
            {
                return parameter.AsString() ?? string.Empty;
            }

            if (parameter.StorageType == StorageType.Integer)
            {
                return parameter.AsInteger().ToString();
            }

            return parameter.AsValueString() ?? string.Empty;
        }

        private void TrySetTextParameter(FamilyInstance markInstance, string parameterName, string value)
        {
            if (markInstance == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            Parameter parameter = markInstance.LookupParameter(parameterName);
            if (parameter == null || parameter.IsReadOnly)
            {
                return;
            }

            if (parameter.StorageType == StorageType.String)
            {
                parameter.Set(value ?? string.Empty);
                return;
            }

            if (parameter.StorageType == StorageType.Integer)
            {
                int intValue;
                if (int.TryParse(value, out intValue))
                {
                    parameter.Set(intValue);
                }

                return;
            }

            parameter.SetValueString(value ?? string.Empty);
        }

        private ElementId GetTitleBlockTypeId(Document document, ViewSheet sourceSheet)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document, sourceSheet.Id)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType();

            foreach (Element element in collector)
            {
                FamilyInstance titleBlock = element as FamilyInstance;
                if (titleBlock == null || titleBlock.Symbol == null)
                {
                    continue;
                }

                return titleBlock.Symbol.Id;
            }

            return ElementId.InvalidElementId;
        }

        private HashSet<string> CollectUsedSheetNames(Document document)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(ViewSheet));

            foreach (Element element in collector)
            {
                ViewSheet sheet = element as ViewSheet;
                if (sheet == null || string.IsNullOrWhiteSpace(sheet.Name))
                {
                    continue;
                }

                names.Add(sheet.Name);
            }

            return names;
        }

        private HashSet<string> CollectUsedSheetNumbers(Document document)
        {
            HashSet<string> numbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(ViewSheet));

            foreach (Element element in collector)
            {
                ViewSheet sheet = element as ViewSheet;
                if (sheet == null || string.IsNullOrWhiteSpace(sheet.SheetNumber))
                {
                    continue;
                }

                numbers.Add(sheet.SheetNumber);
            }

            return numbers;
        }

        private string RemovePartSuffix(string sourceName)
        {
            if (string.IsNullOrWhiteSpace(sourceName))
            {
                return "Развертки";
            }

            return Regex.Replace(sourceName.Trim(), @"\.\s*Часть\s*\d+\s*-\s*\d+$", string.Empty, RegexOptions.IgnoreCase);
        }

        private string BuildNextSheetNumber(string sourceNumber)
        {
            if (string.IsNullOrWhiteSpace(sourceNumber))
            {
                return "ELV-2";
            }

            Match match = Regex.Match(sourceNumber, @"(\d+)$");
            if (!match.Success)
            {
                return sourceNumber + "-2";
            }

            string numericText = match.Groups[1].Value;
            int parsedValue;
            if (!int.TryParse(numericText, out parsedValue))
            {
                return sourceNumber + "-2";
            }

            int incrementedValue = parsedValue + 1;
            string incrementedText = incrementedValue.ToString().PadLeft(numericText.Length, '0');

            int startIndex = match.Groups[1].Index;
            string prefix = sourceNumber.Substring(0, startIndex);
            return prefix + incrementedText;
        }

        private string GetUniqueIncrementedSheetNumber(string sourceNumber, HashSet<string> usedSheetNumbers)
        {
            string candidate = BuildNextSheetNumber(sourceNumber);
            if (usedSheetNumbers == null || usedSheetNumbers.Count == 0)
            {
                return candidate;
            }

            while (usedSheetNumbers.Contains(candidate))
            {
                candidate = BuildNextSheetNumber(candidate);
            }

            return candidate;
        }

        private string GetInsertedSheetNumber(
            Document document,
            ViewSheet sourceSheet,
            ViewSheet targetSheet,
            string sourceSheetOriginalNumber,
            IList<string> warnings)
        {
            string desiredNumber = BuildNextSheetNumber(sourceSheetOriginalNumber);
            if (string.IsNullOrWhiteSpace(desiredNumber))
            {
                desiredNumber = "ELV-2";
            }

            Dictionary<string, ViewSheet> occupiedSheetNumbers = CollectOccupiedSheetNumberMap(document, sourceSheet, targetSheet);
            if (!occupiedSheetNumbers.ContainsKey(desiredNumber))
            {
                return desiredNumber;
            }

            // Block responsible for sheet number insertion:
            // all conflicting sheets are shifted one step forward, so new target keeps number n+1.
            List<ViewSheet> sheetsToShift = new List<ViewSheet>();
            List<string> occupiedNumbers = new List<string>();
            string nextFreeNumber = desiredNumber;
            int safetyCounter = 0;

            while (occupiedSheetNumbers.ContainsKey(nextFreeNumber))
            {
                sheetsToShift.Add(occupiedSheetNumbers[nextFreeNumber]);
                occupiedNumbers.Add(nextFreeNumber);

                nextFreeNumber = BuildNextSheetNumber(nextFreeNumber);
                safetyCounter++;

                if (safetyCounter > 500)
                {
                    AddWarning(warnings, "Не удалось освободить номер листа n+1: превышен лимит итераций.");
                    return GetUniqueIncrementedSheetNumber(sourceSheetOriginalNumber, CollectUsedSheetNumbers(document));
                }
            }

            for (int index = sheetsToShift.Count - 1; index >= 0; index--)
            {
                ViewSheet sheetToShift = sheetsToShift[index];
                string oldNumber = occupiedNumbers[index];
                string newNumber = index == sheetsToShift.Count - 1
                    ? nextFreeNumber
                    : occupiedNumbers[index + 1];

                try
                {
                    sheetToShift.SheetNumber = newNumber;
                }
                catch (Exception exception)
                {
                    AddWarning(
                        warnings,
                        "Не удалось перенумеровать лист " + oldNumber + " в " + newNumber + ": " + exception.Message);

                    return GetUniqueIncrementedSheetNumber(sourceSheetOriginalNumber, CollectUsedSheetNumbers(document));
                }
            }

            return desiredNumber;
        }

        private Dictionary<string, ViewSheet> CollectOccupiedSheetNumberMap(
            Document document,
            ViewSheet sourceSheet,
            ViewSheet targetSheet)
        {
            Dictionary<string, ViewSheet> numberMap = new Dictionary<string, ViewSheet>(StringComparer.OrdinalIgnoreCase);
            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(ViewSheet));

            ElementId sourceSheetId = sourceSheet != null ? sourceSheet.Id : ElementId.InvalidElementId;
            ElementId targetSheetId = targetSheet != null ? targetSheet.Id : ElementId.InvalidElementId;

            foreach (Element element in collector)
            {
                ViewSheet sheet = element as ViewSheet;
                if (sheet == null || string.IsNullOrWhiteSpace(sheet.SheetNumber))
                {
                    continue;
                }

                if (sheet.Id == sourceSheetId || sheet.Id == targetSheetId)
                {
                    continue;
                }

                if (!numberMap.ContainsKey(sheet.SheetNumber))
                {
                    numberMap.Add(sheet.SheetNumber, sheet);
                }
            }

            return numberMap;
        }

        private string GetUniqueValue(string baseValue, HashSet<string> usedValues, string separator)
        {
            if (string.IsNullOrWhiteSpace(baseValue))
            {
                baseValue = "ELV";
            }

            if (!usedValues.Contains(baseValue))
            {
                return baseValue;
            }

            int index = 1;
            while (true)
            {
                string candidate = baseValue + separator + index.ToString("00");
                if (!usedValues.Contains(candidate))
                {
                    return candidate;
                }

                index++;
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
