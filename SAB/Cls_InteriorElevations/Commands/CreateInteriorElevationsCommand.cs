using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Elevations;
using SAB.InteriorElevations.Services.Geometry;
using SAB.InteriorElevations.Services.Marks;
using SAB.InteriorElevations.Services.Reports;
using SAB.InteriorElevations.Services.Rooms;
using SAB.InteriorElevations.Services.Selection;
using SAB.InteriorElevations.Services.Settings;
using SAB.InteriorElevations.Services.Sheets;
using SAB.InteriorElevations.Utils;
using SAB.InteriorElevations.ViewModels;
using SAB.InteriorElevations.Views;

namespace SAB.InteriorElevations.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreateInteriorElevationsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApplication = commandData.Application;
                UIDocument uiDocument = uiApplication.ActiveUIDocument;
                if (uiDocument == null)
                {
                    ToastNotifier.ShowError("SAB Развертки", "Не удалось получить активный UI-документ Revit.");
                    return Result.Failed;
                }

                Document document = uiDocument.Document;
                if (document == null)
                {
                    ToastNotifier.ShowError("SAB Развертки", "Не удалось получить активный документ Revit.");
                    return Result.Failed;
                }

                View activeView = document.ActiveView;
                if (!IsSupportedPlanView(activeView))
                {
                    ToastNotifier.ShowWarning("SAB Развертки", "Активный вид должен быть планом этажа или потолка.");
                    return Result.Cancelled;
                }

                ViewPlan activePlanView = activeView as ViewPlan;
                if (activePlanView == null)
                {
                    ToastNotifier.ShowWarning("SAB Развертки", "Активный плановый вид некорректен.");
                    return Result.Cancelled;
                }

                ElementId activePlanLevelId;
                if (!TryGetPlanLevelId(activePlanView, out activePlanLevelId))
                {
                    ToastNotifier.ShowWarning(
                        "SAB Развертки",
                        "Не удалось определить уровень активного плана. Создание разверток отменено.");
                    return Result.Cancelled;
                }

                List<string> warnings = new List<string>();

                ToastNotifier.ShowInfo("SAB Развертки", "Выберите линии, вдоль которых будут созданы развертки.");

                DetailLineSelectionService selectionService = new DetailLineSelectionService();
                DetailLineSelectionResult selectionResult = selectionService.PickDetailLines(uiDocument, activeView);
                AppendWarnings(warnings, selectionResult.Warnings);

                if (selectionResult.IsCancelled)
                {
                    return Result.Cancelled;
                }

                if (selectionResult.Lines.Count == 0)
                {
                    ToastNotifier.ShowWarning(
                        "SAB Развертки",
                        "Не выбраны корректные линии детализации. Выберите линии и запустите команду снова.");
                    return Result.Cancelled;
                }

                ToastNotifier.ShowInfo("SAB Развертки", "Выберите помещение, для которого будут созданы развертки.");

                RoomDetectionService roomDetectionService = new RoomDetectionService();
                RoomData roomData;
                string roomSelectionError;
                if (!roomDetectionService.TryPickRoomData(uiDocument, out roomData, out roomSelectionError))
                {
                    if (!string.IsNullOrWhiteSpace(roomSelectionError))
                    {
                        ToastNotifier.ShowWarning("SAB Развертки", roomSelectionError);
                    }

                    return Result.Cancelled;
                }

                if (!RevitElementIdUtils.AreEqual(roomData.LevelId, activePlanLevelId))
                {
                    ToastNotifier.ShowWarning(
                        "SAB Развертки",
                        "Выбранное помещение находится на другом уровне. Выберите помещение на уровне активного плана.");
                    return Result.Cancelled;
                }

                ElevationSettingsStorageService settingsStorageService = new ElevationSettingsStorageService();
                ElevationSettings savedSettings = null;
                try
                {
                    savedSettings = settingsStorageService.LoadSettings();
                }
                catch (Exception loadException)
                {
                    warnings.Add("Не удалось загрузить сохраненные настройки: " + loadException.Message);
                }

                ElevationSettingsViewModel settingsViewModel = new ElevationSettingsViewModel(document, savedSettings);
                ElevationSettingsWindow settingsWindow = new ElevationSettingsWindow(settingsViewModel);

                bool? dialogResult = settingsWindow.ShowDialog();
                if (!dialogResult.HasValue || !dialogResult.Value)
                {
                    return Result.Cancelled;
                }

                ElevationSettings settings = settingsWindow.SelectedSettings;
                if (settings == null)
                {
                    ToastNotifier.ShowWarning("SAB Развертки", "Окно настроек не вернуло значения параметров.");
                    return Result.Cancelled;
                }

                string settingsValidationMessage;
                if (!ValidateSettings(document, settings, out settingsValidationMessage))
                {
                    ToastNotifier.ShowWarning("SAB Развертки", settingsValidationMessage);
                    return Result.Cancelled;
                }

                try
                {
                    settingsStorageService.SaveSettings(settings);
                }
                catch (Exception saveException)
                {
                    warnings.Add("Не удалось сохранить настройки: " + saveException.Message);
                }

                ElevationGeometryService elevationGeometryService = new ElevationGeometryService();
                List<ElevationLineData> elevationLines = elevationGeometryService.BuildElevationLineData(selectionResult.Lines, warnings);
                if (elevationLines.Count == 0)
                {
                    ToastNotifier.ShowWarning("SAB Развертки", "Выбранные линии не содержат корректной линейной геометрии.");
                    return Result.Cancelled;
                }

                LineOrientationService lineOrientationService = new LineOrientationService();
                bool orientationAssigned = lineOrientationService.TryAssignInsideNormals(document, elevationLines, roomData, settings.MarkerOffsetMm, warnings);
                if (!orientationAssigned)
                {
                    ToastNotifier.ShowWarning("SAB Развертки", "Не удалось определить направление разверток для выбранных линий.");
                    return Result.Cancelled;
                }

                ElevationNamingService namingService = new ElevationNamingService(document);
                ElevationMarkerService markerService = new ElevationMarkerService();
                ElevationCropService cropService = new ElevationCropService();

                ElevationViewCreationService viewCreationService = new ElevationViewCreationService(
                    markerService,
                    cropService,
                    namingService);

                SheetCreationService sheetCreationService = new SheetCreationService();
                ViewportPlacementService viewportPlacementService = new ViewportPlacementService();
                PlanCornerMarkPlacementService planCornerMarkPlacementService = new PlanCornerMarkPlacementService();
                SheetCornerMarkPlacementService sheetCornerMarkPlacementService = new SheetCornerMarkPlacementService();
                ElevationCreationReportService reportService = new ElevationCreationReportService();

                ElevationViewCreationResult creationResult;
                ViewSheet createdSheet = null;
                int placedViewportCount = 0;
                int placedPlanMarksCount = 0;
                int placedSheetMarksCount = 0;

                ToastNotifier.ShowInfo("SAB Развертки", "Создание разверток запущено. Дождитесь завершения операции.");

                using (TransactionGroup transactionGroup = new TransactionGroup(document, "SAB Развертки стен"))
                {
                    transactionGroup.Start();

                    using (Transaction transaction = new Transaction(document, "Создать развертки стен"))
                    {
                        transaction.Start();

                        creationResult = viewCreationService.CreateElevationViews(
                            document,
                            activePlanView,
                            elevationLines,
                            settings,
                            warnings);

                        if (creationResult.CreatedViews.Count > 0)
                        {
                            placedPlanMarksCount = planCornerMarkPlacementService.PlacePlanCornerMarks(
                                document,
                                activePlanView,
                                elevationLines,
                                roomData,
                                settings.PlanCornerMarkTypeId,
                                warnings);
                        }

                        if (settings.CreateSheet && creationResult.CreatedViews.Count > 0)
                        {
                            createdSheet = sheetCreationService.CreateSheet(document, settings, roomData, namingService);

                            if (createdSheet != null)
                            {
                                ViewportPlacementResult placementResult = viewportPlacementService.PlaceViewsOnSheet(
                                    document,
                                    createdSheet,
                                    creationResult.CreatedViews,
                                    settings.SheetLayoutSettings,
                                    warnings);

                                if (placementResult != null)
                                {
                                    placedViewportCount = placementResult.PlacedCount;

                                    placedSheetMarksCount = sheetCornerMarkPlacementService.PlaceSheetCornerMarks(
                                        document,
                                        createdSheet,
                                        roomData,
                                        settings.SheetCornerMarkTypeId,
                                        creationResult.CreatedViews,
                                        placementResult,
                                        warnings);
                                }
                            }
                            else
                            {
                                warnings.Add("Включено создание листа, но лист не был создан.");
                            }
                        }

                        transaction.Commit();
                    }

                    transactionGroup.Assimilate();
                }

                reportService.ShowFinalReport(
                    selectionResult.Lines.Count,
                    creationResult,
                    createdSheet,
                    placedViewportCount,
                    placedPlanMarksCount,
                    placedSheetMarksCount,
                    warnings);

                return creationResult.CreatedViews.Count > 0 ? Result.Succeeded : Result.Cancelled;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ToastNotifier.ShowError("SAB Развертки", "Неожиданная ошибка: " + exception.Message);
                return Result.Failed;
            }
        }

        private bool IsSupportedPlanView(View view)
        {
            if (view == null)
            {
                return false;
            }

            return view.ViewType == ViewType.FloorPlan || view.ViewType == ViewType.CeilingPlan;
        }

        private void AppendWarnings(List<string> target, IList<string> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                target.Add(source[i]);
            }
        }

        private bool ValidateSettings(Document document, ElevationSettings settings, out string validationMessage)
        {
            validationMessage = string.Empty;

            if (settings.ElevationViewFamilyTypeId == null || settings.ElevationViewFamilyTypeId == ElementId.InvalidElementId)
            {
                validationMessage = "Не выбран тип вида развертки.";
                return false;
            }

            ViewFamilyType viewFamilyType = document.GetElement(settings.ElevationViewFamilyTypeId) as ViewFamilyType;
            if (viewFamilyType == null || viewFamilyType.ViewFamily != ViewFamily.Elevation)
            {
                validationMessage = "Выбран некорректный тип вида развертки.";
                return false;
            }

            if (settings.ViewTemplateId != null && settings.ViewTemplateId != ElementId.InvalidElementId)
            {
                View templateView = document.GetElement(settings.ViewTemplateId) as View;
                if (templateView == null || !templateView.IsTemplate)
                {
                    validationMessage = "Выбранный шаблон вида не существует или не является шаблоном.";
                    return false;
                }
            }

            if (settings.ViewScale <= 0)
            {
                validationMessage = "Масштаб вида должен быть больше нуля.";
                return false;
            }

            if (settings.ViewDepthMm <= 0)
            {
                validationMessage = "Смещение дальнего предела должно быть больше нуля.";
                return false;
            }

            if (settings.MarkerOffsetMm < 0)
            {
                validationMessage = "Отступ вида от линии должен быть неотрицательным.";
                return false;
            }

            if (settings.TopOffsetMm < 0 || settings.BottomOffsetMm < 0 || settings.LeftOffsetMm < 0 || settings.RightOffsetMm < 0)
            {
                validationMessage = "Отступы обрезки должны быть неотрицательными.";
                return false;
            }

            FamilySymbol planMarkType = document.GetElement(settings.PlanCornerMarkTypeId) as FamilySymbol;
            if (planMarkType == null)
            {
                validationMessage = "Не выбран или не найден тип марки угла на плане.";
                return false;
            }

            if (!IsSymbolFromFamily(planMarkType, CornerMarkConstants.PlanFamilyName))
            {
                validationMessage = "Тип марки угла на плане должен относиться к семейству '" + CornerMarkConstants.PlanFamilyName + "'.";
                return false;
            }

            if (settings.CreateSheet)
            {
                if (settings.TitleBlockTypeId == null || settings.TitleBlockTypeId == ElementId.InvalidElementId)
                {
                    validationMessage = "Включено создание листа, но не выбран тип основной надписи.";
                    return false;
                }

                FamilySymbol titleBlockType = document.GetElement(settings.TitleBlockTypeId) as FamilySymbol;
                if (titleBlockType == null)
                {
                    validationMessage = "Выбранный тип основной надписи не существует.";
                    return false;
                }

                FamilySymbol sheetMarkType = document.GetElement(settings.SheetCornerMarkTypeId) as FamilySymbol;
                if (sheetMarkType == null)
                {
                    validationMessage = "Включено создание листа, но не выбран тип марки угла на листе.";
                    return false;
                }

                if (!IsSymbolFromFamily(sheetMarkType, CornerMarkConstants.SheetFamilyName))
                {
                    validationMessage = "Тип марки угла на листе должен относиться к семейству '" + CornerMarkConstants.SheetFamilyName + "'.";
                    return false;
                }

                if (settings.SheetLayoutSettings == null)
                {
                    validationMessage = "Не заданы параметры размещения видов на листе.";
                    return false;
                }

                if (settings.SheetLayoutSettings.ColumnsCount <= 0)
                {
                    validationMessage = "Количество колонок должно быть больше нуля.";
                    return false;
                }

                if (settings.SheetLayoutSettings.StepXmm <= 0 || settings.SheetLayoutSettings.StepYmm <= 0)
                {
                    validationMessage = "Шаги размещения на листе должны быть больше нуля.";
                    return false;
                }
            }

            return true;
        }

        private bool IsSymbolFromFamily(FamilySymbol symbol, string familyName)
        {
            if (symbol == null || string.IsNullOrWhiteSpace(familyName))
            {
                return false;
            }

            string currentFamilyName = symbol.Family != null ? symbol.Family.Name : symbol.FamilyName;
            return string.Equals(currentFamilyName, familyName, StringComparison.OrdinalIgnoreCase);
        }

        private bool TryGetPlanLevelId(ViewPlan viewPlan, out ElementId levelId)
        {
            levelId = ElementId.InvalidElementId;

            if (viewPlan == null)
            {
                return false;
            }

            if (viewPlan.GenLevel != null && viewPlan.GenLevel.Id != null && viewPlan.GenLevel.Id != ElementId.InvalidElementId)
            {
                levelId = viewPlan.GenLevel.Id;
                return true;
            }

            Parameter levelParameter = viewPlan.get_Parameter(BuiltInParameter.PLAN_VIEW_LEVEL);
            if (levelParameter != null)
            {
                ElementId parameterLevelId = levelParameter.AsElementId();
                if (parameterLevelId != null && parameterLevelId != ElementId.InvalidElementId)
                {
                    levelId = parameterLevelId;
                    return true;
                }
            }

            return false;
        }
    }
}
