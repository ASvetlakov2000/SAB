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
            out ViewPlan pickedExistingRoomPlanView,
            out bool wasCancelled,
            bool pickRoomPlanPosition = false)
        {
            pickedPoint = null;
            pickedExistingRoomPlanView = null;
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

            if (settings == null)
            {
                AddWarning(warnings, "Не заданы параметры выбора координаты на листе.");
                return false;
            }

            bool usesExistingSheet = settings.UseExistingSheet;
            ViewSheet coordinateSelectionSheet = usesExistingSheet
                ? document.GetElement(settings.ExistingSheetId) as ViewSheet
                : CreateCoordinateSelectionSheet(document, settings, warnings);

            if (usesExistingSheet && coordinateSelectionSheet == null)
            {
                AddWarning(warnings, "Выбранный существующий лист не найден в документе.");
                return false;
            }

            if (coordinateSelectionSheet == null)
            {
                return false;
            }

            try
            {
                // Переключение вида выполняется только после закрытия транзакции создания листа.
                // Если команда запущена из активированного видового экрана план-схемы,
                // назначение листа также завершает редактирование вида внутри листа.
                if (uiDocument.ActiveView == null ||
                    !RevitElementIdUtils.AreEqual(uiDocument.ActiveView.Id, coordinateSelectionSheet.Id))
                {
                    uiDocument.ActiveView = coordinateSelectionSheet;
                }
                ZoomSheetToFit(uiDocument, coordinateSelectionSheet.Id);

                SheetRectangle planArea = null;
                if (pickRoomPlanPosition)
                {
                    var workspace = new SheetWorkspaceService();
                    try { workspace.PrepareFamilyGeometry(document, coordinateSelectionSheet); }
                    catch (Exception exception) { SheetLayoutDiagnostics.Write("Plan point frame preparation: " + exception.Message); }
                    using (var measurement = new Transaction(document, "Измерить лист для точки план-схемы"))
                    {
                        measurement.Start();
                        IList<SheetRectangle> reserved;
                        planArea = workspace.ReadPlanWorkspace(document, coordinateSelectionSheet,
                            UnitConversionUtils.MillimetersToFeet(settings.SheetLayoutSettings.StartXmm),
                            UnitConversionUtils.MillimetersToFeet(settings.SheetLayoutSettings.StartYmm), out reserved);
                        measurement.RollBack();
                    }
                    if (planArea == null) throw new InvalidOperationException("Не удалось измерить границы листа для план-схемы.");
                }

                pickedPoint = uiDocument.Selection.PickPoint(
                    ObjectSnapTypes.None,
                    pickRoomPlanPosition ? "Укажите нижний правый угол план-схемы с оформлением вне штампа." :
                        "Укажите стартовую точку размещения нового комплекта разверток на листе.");

                if (pickedPoint != null && pickRoomPlanPosition)
                {
                    double right, bottom;
                    SheetPlanPosition.Offsets(planArea, pickedPoint.X, pickedPoint.Y, out right, out bottom);
                    settings.SheetLayoutSettings.RoomPlanOffsetRightMm = UnitConversionUtils.FeetToMillimeters(right);
                    settings.SheetLayoutSettings.RoomPlanOffsetBottomMm = UnitConversionUtils.FeetToMillimeters(bottom);
                    settings.SheetLayoutSettings.UseManualRoomPlanPosition = true;
                }

                if (pickedPoint != null && !pickRoomPlanPosition && usesExistingSheet && settings.CreateRoomPlanScheme)
                {
                    try
                    {
                        Reference viewportReference = uiDocument.Selection.PickObject(
                            ObjectType.Element,
                            new PlanViewportOnSheetSelectionFilter(document, coordinateSelectionSheet.Id),
                            "Выберите видовой экран существующей план-схемы на этом листе.");

                        Viewport selectedViewport = viewportReference != null
                            ? document.GetElement(viewportReference.ElementId) as Viewport
                            : null;
                        pickedExistingRoomPlanView = selectedViewport != null
                            ? document.GetElement(selectedViewport.ViewId) as ViewPlan
                            : null;

                        if (pickedExistingRoomPlanView == null)
                        {
                            AddWarning(
                                warnings,
                                "Точка размещения сохранена, но видовой экран план-схемы не выбран.");
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        AddWarning(
                            warnings,
                            "Точка размещения сохранена. Выбор план-схемы отменен — укажите точку и план-схему еще раз.");
                    }
                }

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
                RestoreSourceViewAndCloseSheetView(
                    uiDocument,
                    returnView,
                    coordinateSelectionSheet,
                    !usesExistingSheet,
                    warnings);
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
            bool closeSheetView,
            IList<string> warnings)
        {
            if (uiDocument == null)
            {
                return;
            }

            bool sourceViewRestored = TryRestoreSourceView(uiDocument, returnView, warnings);

            if (sourceViewRestored && closeSheetView &&
                coordinateSelectionSheet != null && coordinateSelectionSheet.IsValidObject)
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

        private sealed class PlanViewportOnSheetSelectionFilter : ISelectionFilter
        {
            private readonly Document _document;
            private readonly ElementId _sheetId;

            public PlanViewportOnSheetSelectionFilter(Document document, ElementId sheetId)
            {
                _document = document;
                _sheetId = sheetId;
            }

            public bool AllowElement(Element element)
            {
                Viewport viewport = element as Viewport;
                if (viewport == null || _document == null || _sheetId == null)
                {
                    return false;
                }

                if (!RevitElementIdUtils.AreEqual(viewport.SheetId, _sheetId))
                {
                    return false;
                }

                ViewPlan planView = _document.GetElement(viewport.ViewId) as ViewPlan;
                return planView != null && !planView.IsTemplate;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}

