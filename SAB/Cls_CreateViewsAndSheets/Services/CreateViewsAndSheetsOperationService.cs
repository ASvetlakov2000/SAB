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
        private readonly SheetBoundsService _sheetBoundsService;
        private readonly ViewportPlacementService _viewportPlacementService;

        public CreateViewsAndSheetsOperationService()
        {
            _viewDuplicationService = new ViewDuplicationService();
            _sheetCreationService = new SheetCreationService();
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

            View sourceView = document.GetElement(settings.SourceViewId) as View;
            if (sourceView == null)
            {
                throw new InvalidOperationException("Вид-образец не найден в документе.");
            }

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
                        ProcessSingleRow(document, sourceView, settings, item, result);
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

        private void ProcessSingleRow(
            Document document,
            View sourceView,
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
                    settings.SourceSheetId,
                    item.SheetNumber,
                    item.SheetName,
                    result.Warnings);

                document.Regenerate();

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

            if (settings.SheetBounds == null)
            {
                throw new InvalidOperationException("Не определены габариты листа для создания.");
            }

            ValidatePlacementBeforeTransaction(settings.SheetBounds, settings.Placement);

            if (items == null || items.Count == 0)
            {
                throw new InvalidOperationException("Нет строк для создания видов и листов.");
            }

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

        private void ValidatePlacementBeforeTransaction(SheetBounds sheetBounds, PlacementSettings placement)
        {
            if (placement == null)
            {
                throw new InvalidOperationException("Настройки размещения не получены.");
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

        private bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
