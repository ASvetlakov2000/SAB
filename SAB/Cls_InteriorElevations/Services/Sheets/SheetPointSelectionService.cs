using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Sheets
{
    public class SheetPointSelectionService
    {
        private const string CoordinateSelectionSheetName = "SAB-Развертки по линии";

        private readonly SheetCreationService _sheetCreationService;

        public SheetPointSelectionService()
        {
            _sheetCreationService = new SheetCreationService();
        }

        public bool TryPickStartPointOnSheet(
            UIDocument uiDocument,
            View returnView,
            ElevationSettings settings,
            IList<string> warnings,
            out XYZ pickedPoint,
            out bool wasCancelled)
        {
            pickedPoint = null;
            wasCancelled = false;

            if (uiDocument == null)
            {
                AddWarning(warnings, "Не удалось получить UI-документ для выбора координаты на листе.");
                return false;
            }

            Document document = uiDocument.Document;
            if (document == null)
            {
                AddWarning(warnings, "Не удалось получить документ Revit для выбора координаты на листе.");
                return false;
            }

            if (document.ActiveView == null)
            {
                AddWarning(warnings, "Активный вид Revit не найден.");
                return false;
            }

            if (returnView == null || !returnView.IsValidObject)
            {
                returnView = document.ActiveView;
            }

            if (settings == null ||
                settings.TitleBlockTypeId == null ||
                settings.TitleBlockTypeId == ElementId.InvalidElementId)
            {
                AddWarning(warnings, "Для выбора координаты должен быть выбран тип основной надписи.");
                return false;
            }

            ViewSheet coordinateSelectionSheet = CreateCoordinateSelectionSheet(document, settings, warnings);
            if (coordinateSelectionSheet == null)
            {
                return false;
            }

            try
            {
                // Переключение вида выполняется только после закрытия транзакции создания листа.
                uiDocument.ActiveView = coordinateSelectionSheet;
                ZoomSheetToFit(uiDocument, coordinateSelectionSheet.Id);

                pickedPoint = uiDocument.Selection.PickPoint(
                    ObjectSnapTypes.None,
                    "Укажите стартовую точку размещения разверток на листе.");

                return pickedPoint != null;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                wasCancelled = true;
                return false;
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось выбрать координату на листе: " + exception.Message);
                return false;
            }
            finally
            {
                RestoreSourceViewAndCloseSheetView(uiDocument, returnView, coordinateSelectionSheet, warnings);
            }
        }

        private ViewSheet CreateCoordinateSelectionSheet(
            Document document,
            ElevationSettings settings,
            IList<string> warnings)
        {
            if (document == null || settings == null)
            {
                return null;
            }

            Transaction transaction = new Transaction(document, "SAB - создать лист выбора координаты");
            try
            {
                transaction.Start();

                ViewSheet sheet = _sheetCreationService.CreateCoordinateSelectionSheet(
                    document,
                    settings,
                    CoordinateSelectionSheetName);

                if (sheet == null)
                {
                    transaction.RollBack();
                    AddWarning(warnings, "Не удалось создать лист '" + CoordinateSelectionSheetName + "'.");
                    return null;
                }

                transaction.Commit();
                return sheet;
            }
            catch (Exception exception)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                {
                    transaction.RollBack();
                }

                AddWarning(warnings, "Ошибка создания листа для выбора координаты: " + exception.Message);
                return null;
            }
        }

        private void RestoreSourceViewAndCloseSheetView(
            UIDocument uiDocument,
            View returnView,
            ViewSheet coordinateSelectionSheet,
            IList<string> warnings)
        {
            if (uiDocument == null)
            {
                return;
            }

            bool sourceViewRestored = TryRestoreSourceView(uiDocument, returnView, warnings);

            if (sourceViewRestored && coordinateSelectionSheet != null && coordinateSelectionSheet.IsValidObject)
            {
                TryCloseOpenUIView(uiDocument, coordinateSelectionSheet.Id, warnings);
            }
        }

        private bool TryRestoreSourceView(UIDocument uiDocument, View returnView, IList<string> warnings)
        {
            if (uiDocument == null || returnView == null || !returnView.IsValidObject)
            {
                return false;
            }

            try
            {
                if (uiDocument.ActiveView == null ||
                    !RevitElementIdUtils.AreEqual(uiDocument.ActiveView.Id, returnView.Id))
                {
                    uiDocument.ActiveView = returnView;
                }

                return true;
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось вернуться на исходный вид: " + exception.Message);
                return false;
            }
        }

        private void TryCloseOpenUIView(UIDocument uiDocument, ElementId viewId, IList<string> warnings)
        {
            if (uiDocument == null || viewId == null || viewId == ElementId.InvalidElementId)
            {
                return;
            }

            try
            {
                IList<UIView> openViews = uiDocument.GetOpenUIViews();
                if (openViews == null)
                {
                    return;
                }

                for (int index = 0; index < openViews.Count; index++)
                {
                    UIView openView = openViews[index];
                    if (openView == null || !openView.IsValidObject)
                    {
                        continue;
                    }

                    if (!RevitElementIdUtils.AreEqual(openView.ViewId, viewId))
                    {
                        continue;
                    }

                    openView.Close();
                    return;
                }
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Лист создан, но вкладку листа не удалось закрыть: " + exception.Message);
            }
        }

        private void ZoomSheetToFit(UIDocument uiDocument, ElementId sheetId)
        {
            if (uiDocument == null || sheetId == null || sheetId == ElementId.InvalidElementId)
            {
                return;
            }

            IList<UIView> openViews = uiDocument.GetOpenUIViews();
            if (openViews == null)
            {
                return;
            }

            for (int index = 0; index < openViews.Count; index++)
            {
                UIView openView = openViews[index];
                if (openView == null || !openView.IsValidObject)
                {
                    continue;
                }

                if (!RevitElementIdUtils.AreEqual(openView.ViewId, sheetId))
                {
                    continue;
                }

                openView.ZoomSheetSize();
                return;
            }
        }

        private void AddWarning(IList<string> warnings, string warning)
        {
            if (warnings == null || string.IsNullOrWhiteSpace(warning))
            {
                return;
            }

            warnings.Add(warning);
        }
    }
}

