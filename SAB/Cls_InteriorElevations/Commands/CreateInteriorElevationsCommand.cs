using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Elevations;
using SAB.InteriorElevations.Services.Geometry;
using SAB.InteriorElevations.Services.Marks;
using SAB.InteriorElevations.Services.Plans;
using SAB.InteriorElevations.Services.Reports;
using SAB.InteriorElevations.Services.Rooms;
using SAB.InteriorElevations.Services.Selection;
using SAB.InteriorElevations.Services.Settings;
using SAB.InteriorElevations.Services.Sheets;
using SAB.InteriorElevations.Utils;
using SAB.InteriorElevations.ViewModels;
using SAB.InteriorElevations.Views;
using SAB.Services.PluginResources;

namespace SAB.InteriorElevations.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreateInteriorElevationsCommand : IExternalCommand
    {
        private enum LineGroupSelectionMode
        {
            Cancelled = 0,
            SingleGroup = 1,
            MultipleGroups = 2
        }

        private class ElevationSelectionPackage
        {
            public ElevationSelectionPackage()
            {
                LineGroups = new List<List<DetailLine>>();
                SelectedLines = new List<DetailLine>();
            }

            public List<List<DetailLine>> LineGroups { get; set; }

            public List<DetailLine> SelectedLines { get; set; }

            public RoomData RoomData { get; set; }
        }

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
                PluginFamilyResourceService pluginFamilyResourceService = new PluginFamilyResourceService();
                pluginFamilyResourceService.EnsureCommandFamiliesLoaded(document, typeof(CreateInteriorElevationsCommand), warnings);

                RoomVisibilityService roomVisibilityService = new RoomVisibilityService();
                roomVisibilityService.EnsureRoomsAndObjectVisible(document, activeView, warnings);

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
                SheetPointSelectionService sheetPointSelectionService = new SheetPointSelectionService();
                ElevationCropByExampleService cropByExampleService = new ElevationCropByExampleService();
                bool useMultipleLineGroups = false;
                string selectionStatusText = "Линии и помещение не выбраны.";
                string windowInfoText = GetDefaultWindowInfoText();
                if (warnings.Count > 0)
                {
                    windowInfoText = BuildWindowInfoText(warnings);
                }

                ElevationSelectionPackage selectionPackage = null;
                ElevationSettings settings = null;

                while (true)
                {
                    ElevationSettingsWindow settingsWindow = new ElevationSettingsWindow(
                        settingsViewModel,
                        useMultipleLineGroups,
                        selectionPackage != null,
                        selectionStatusText,
                        windowInfoText);

                    bool? dialogResult = settingsWindow.ShowDialog();
                    if (!dialogResult.HasValue || !dialogResult.Value)
                    {
                        // Семейства и подготовка видимости уже загружены в проект отдельными транзакциями.
                        // Возвращаем Succeeded, чтобы Revit не откатывал эти изменения при закрытии окна настроек.
                        return Result.Succeeded;
                    }

                    useMultipleLineGroups = settingsWindow.IsMultipleGroupsMode;

                    if (settingsWindow.RequestedAction == ElevationSettingsWindowAction.PickSelection)
                    {
                        LineGroupSelectionMode lineGroupSelectionMode = useMultipleLineGroups
                            ? LineGroupSelectionMode.MultipleGroups
                            : LineGroupSelectionMode.SingleGroup;

                        ElevationSelectionPackage pickedSelectionPackage;
                        List<string> selectionWarnings = new List<string>();
                        bool selectionPicked = TryPickLineGroupsAndRoom(
                            uiDocument,
                            activeView,
                            activePlanLevelId,
                            lineGroupSelectionMode,
                            selectionWarnings,
                            out pickedSelectionPackage);

                        AppendWarnings(warnings, selectionWarnings);
                        if (selectionWarnings.Count > 0)
                        {
                            windowInfoText = BuildWindowInfoText(selectionWarnings);
                        }

                        if (selectionPicked)
                        {
                            selectionPackage = pickedSelectionPackage;
                            selectionStatusText = BuildSelectionStatusText(selectionPackage, lineGroupSelectionMode);
                            windowInfoText = "Стек собран. Проверьте параметры и нажмите Создать развертки.";
                            ToastNotifier.ShowInfo("SAB Развертки", "Линии и помещение выбраны. Проверьте настройки и нажмите Создать развертки.");
                        }
                        else
                        {
                            selectionPackage = null;
                            selectionStatusText = "Линии и помещение не выбраны. Нажмите Выбрать линии.";
                            if (selectionWarnings.Count == 0)
                            {
                                windowInfoText = "Стек не собран. Нажмите Выбрать линии, затем укажите линии детализации и помещение.";
                            }
                        }

                        continue;
                    }

                    if (settingsWindow.RequestedAction == ElevationSettingsWindowAction.PickSheetPoint)
                    {
                        ElevationSettings sheetPointSettings = settingsWindow.SelectedSettings;
                        if (sheetPointSettings == null)
                        {
                            windowInfoText = "Окно настроек не вернуло параметры листа.";
                            ToastNotifier.ShowWarning("SAB Развертки", "Окно настроек не вернуло параметры листа.");
                            continue;
                        }

                        XYZ pickedSheetPoint;
                        bool pointSelectionCancelled;
                        List<string> pointSelectionWarnings = new List<string>();
                        bool pointPicked = sheetPointSelectionService.TryPickStartPointOnSheet(
                            uiDocument,
                            activeView,
                            sheetPointSettings,
                            pointSelectionWarnings,
                            out pickedSheetPoint,
                            out pointSelectionCancelled);

                        AppendWarnings(warnings, pointSelectionWarnings);
                        if (pointSelectionWarnings.Count > 0)
                        {
                            windowInfoText = BuildWindowInfoText(pointSelectionWarnings);
                        }

                        if (pointPicked)
                        {
                            settingsViewModel.SetSheetStartPointFromRevitPoint(pickedSheetPoint);
                            windowInfoText = "Координата на листе выбрана и записана в поля Старт X/Y.";
                            ToastNotifier.ShowInfo("SAB Развертки", "Координата на листе выбрана и записана в поля Старт X/Y.");
                        }
                        else if (!pointSelectionCancelled)
                        {
                            string warningText = pointSelectionWarnings.Count > 0
                                ? pointSelectionWarnings[pointSelectionWarnings.Count - 1]
                                : "Координата на листе не была выбрана.";

                            windowInfoText = warningText;
                            ToastNotifier.ShowWarning("SAB Развертки", warningText);
                        }

                        continue;
                    }

                    if (settingsWindow.RequestedAction == ElevationSettingsWindowAction.PickCropByExample)
                    {
                        ElevationSettings cropByExampleSettings = settingsWindow.SelectedSettings;
                        if (cropByExampleSettings == null)
                        {
                            windowInfoText = "Окно настроек не вернуло параметры для вида-примера.";
                            ToastNotifier.ShowWarning("SAB Развертки", "Окно настроек не вернуло параметры для вида-примера.");
                            continue;
                        }

                        CropByExampleActionWindow cropByExampleActionWindow = new CropByExampleActionWindow();
                        bool? cropByExampleDialogResult = cropByExampleActionWindow.ShowDialog();
                        if (!cropByExampleDialogResult.HasValue || !cropByExampleDialogResult.Value)
                        {
                            settingsViewModel.IsCropManualMode = true;
                            continue;
                        }

                        bool workflowStarted = TryStartCropByExampleWorkflow(
                            uiApplication,
                            activePlanView,
                            activePlanLevelId,
                            cropByExampleSettings,
                            cropByExampleActionWindow.RequestedAction,
                            cropByExampleService,
                            warnings);

                        if (!workflowStarted)
                        {
                            settingsViewModel.IsCropManualMode = true;
                            windowInfoText = "Сценарий обрезки по виду-примеру не был запущен.";
                            ToastNotifier.ShowWarning("SAB Развертки", "Сценарий обрезки по виду-примеру не был запущен.");
                            continue;
                        }

                        return Result.Succeeded;
                    }

                    settings = settingsWindow.SelectedSettings;
                    if (settings == null)
                    {
                        windowInfoText = "Окно настроек не вернуло значения параметров.";
                        ToastNotifier.ShowWarning("SAB Развертки", "Окно настроек не вернуло значения параметров.");
                        continue;
                    }

                    if (selectionPackage == null ||
                        selectionPackage.SelectedLines == null ||
                        selectionPackage.SelectedLines.Count == 0 ||
                        selectionPackage.RoomData == null)
                    {
                        ToastNotifier.ShowWarning("SAB Развертки", "Сначала нажмите 'Выбрать линии' и укажите линии детализации и помещение.");
                        selectionStatusText = "Линии и помещение не выбраны. Нажмите Выбрать линии.";
                        windowInfoText = "Стек не собран. Нажмите Выбрать линии, затем укажите линии детализации и помещение.";
                        continue;
                    }

                    string settingsValidationMessage;
                    if (!ValidateSettings(document, settings, out settingsValidationMessage))
                    {
                        windowInfoText = settingsValidationMessage;
                        ToastNotifier.ShowWarning("SAB Развертки", settingsValidationMessage);
                        continue;
                    }

                    break;
                }

                List<List<DetailLine>> lineGroups = selectionPackage.LineGroups;
                List<DetailLine> selectedLines = selectionPackage.SelectedLines;
                RoomData roomData = selectionPackage.RoomData;

                try
                {
                    settingsStorageService.SaveSettings(settings);
                }
                catch (Exception saveException)
                {
                    warnings.Add("Не удалось сохранить настройки: " + saveException.Message);
                }

                ElevationGeometryService elevationGeometryService = new ElevationGeometryService();
                List<ElevationLineData> elevationLines = BuildElevationLinesWithGlobalCornerIndexing(
                    lineGroups,
                    elevationGeometryService,
                    warnings);

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

                Room roomForPlanScheme = document.GetElement(roomData.RoomElementId) as Room;
                if (roomForPlanScheme == null)
                {
                    warnings.Add("Выбранное помещение не удалось получить из документа для построения план-схемы.");
                }

                ElevationNamingService namingService = new ElevationNamingService(document);
                ElevationMarkerService markerService = new ElevationMarkerService();
                ElevationCropService cropService = new ElevationCropService();

                ElevationViewCreationService viewCreationService = new ElevationViewCreationService(
                    markerService,
                    cropService,
                    namingService);

                RoomPlanSchemeCreationService roomPlanSchemeCreationService = new RoomPlanSchemeCreationService();
                SheetCreationService sheetCreationService = new SheetCreationService();
                ViewportPlacementService viewportPlacementService = new ViewportPlacementService();
                PlanCornerMarkPlacementService planCornerMarkPlacementService = new PlanCornerMarkPlacementService();
                RoomPlanRoomTagPlacementService roomPlanRoomTagPlacementService = new RoomPlanRoomTagPlacementService();
                SheetCornerMarkPlacementService sheetCornerMarkPlacementService = new SheetCornerMarkPlacementService();
                ElevationCreationReportService reportService = new ElevationCreationReportService();

                ElevationViewCreationResult creationResult;
                RoomPlanSchemeCreationSummary roomPlanSummary = null;
                ViewPlan createdRoomPlanView = null;
                ViewSheet createdSheet = null;
                int placedViewportCount = 0;
                int placedPlanMarksCount = 0;
                int placedSheetMarksCount = 0;

                ToastNotifier.ShowInfo("SAB Развертки", "Создание разверток запущено. Дождитесь завершения операции.");

                using (TransactionGroup transactionGroup = new TransactionGroup(document, "SAB Развертки стен"))
                {
                    transactionGroup.Start();

                    using (Transaction transaction = new Transaction(document, "Создать развертки и план-схему"))
                    {
                        transaction.Start();

                        creationResult = viewCreationService.CreateElevationViews(
                            document,
                            activePlanView,
                            elevationLines,
                            settings,
                            warnings);

                        if (creationResult.CreatedViews.Count > 0 && roomForPlanScheme != null)
                        {
                            RoomPlanSchemeSettings roomPlanSettings = BuildRoomPlanSchemeSettings(settings);
                            IList<Room> singleRoomList = new List<Room> { roomForPlanScheme };

                            roomPlanSummary = roomPlanSchemeCreationService.CreateRoomPlanSchemes(
                                document,
                                activePlanView,
                                singleRoomList,
                                roomPlanSettings,
                                null);

                            if (roomPlanSummary != null)
                            {
                                AppendWarnings(warnings, roomPlanSummary.Warnings);

                                if (roomPlanSummary.CreatedViewIds.Count > 0)
                                {
                                    createdRoomPlanView = document.GetElement(roomPlanSummary.CreatedViewIds[0]) as ViewPlan;
                                    if (createdRoomPlanView != null)
                                    {
                                        CopySelectedDetailLinesToPlanScheme(
                                            document,
                                            activePlanView,
                                            createdRoomPlanView,
                                            selectedLines,
                                            warnings);
                                    }
                                }
                            }
                        }

                        if (creationResult.CreatedViews.Count > 0)
                        {
                            int placedPlanMarksOnSourcePlan = planCornerMarkPlacementService.PlacePlanCornerMarks(
                                document,
                                activePlanView,
                                elevationLines,
                                roomData,
                                settings.PlanCornerMarkTypeId,
                                warnings);

                            int placedPlanMarksOnPlanScheme = 0;
                            int placedRoomTagOnPlanScheme = 0;

                            if (createdRoomPlanView != null && roomForPlanScheme != null)
                            {
                                placedPlanMarksOnPlanScheme = planCornerMarkPlacementService.PlacePlanCornerMarks(
                                    document,
                                    createdRoomPlanView,
                                    elevationLines,
                                    roomData,
                                    settings.PlanCornerMarkTypeId,
                                    warnings);

                                placedRoomTagOnPlanScheme = roomPlanRoomTagPlacementService.PlaceRoomTag(
                                    document,
                                    createdRoomPlanView,
                                    roomForPlanScheme,
                                    settings.RoomPlanRoomTagTypeId,
                                    warnings);
                            }
                            else if (roomForPlanScheme == null)
                            {
                                warnings.Add("Не удалось получить помещение. Марка помещения на план-схеме не размещена.");
                            }
                            else
                            {
                                warnings.Add("План-схема не создана. Марки углов и марка помещения на план-схеме не размещены.");
                            }

                            placedPlanMarksCount = placedPlanMarksOnSourcePlan + placedPlanMarksOnPlanScheme;

                            if (placedRoomTagOnPlanScheme == 0 && settings.RoomPlanRoomTagTypeId != null && settings.RoomPlanRoomTagTypeId != ElementId.InvalidElementId)
                            {
                                warnings.Add("Марка помещения на план-схеме не была размещена.");
                            }
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

                                if (createdRoomPlanView != null)
                                {
                                    viewportPlacementService.TryPlaceAdditionalViewOnSheet(
                                        document,
                                        createdSheet,
                                        createdRoomPlanView,
                                        settings.SheetLayoutSettings,
                                        placementResult,
                                        warnings);
                                }

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

                if (roomPlanSummary != null && roomPlanSummary.ManualBoundaryRequired)
                {
                    ToastNotifier.ShowWarning(
                        "SAB Развертки",
                        "Границу вида создать не удалось. На активном виде созданы вспомогательные линии для ручной правки.",
                        15);
                }

                reportService.ShowFinalReport(
                    selectedLines.Count,
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

        private void AppendWarnings(IList<string> target, IList<string> source)
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

        private string GetDefaultWindowInfoText()
        {
            return "Перед созданием нажмите Выбрать линии, затем укажите линии детализации и помещение в активном плане. " +
                   "Параметры сохраняются и будут использованы при следующем запуске команды.";
        }

        private string BuildWindowInfoText(IList<string> warningMessages)
        {
            if (warningMessages == null || warningMessages.Count == 0)
            {
                return GetDefaultWindowInfoText();
            }

            int firstWarningIndex = Math.Max(0, warningMessages.Count - 3);
            List<string> visibleWarnings = new List<string>();

            for (int index = firstWarningIndex; index < warningMessages.Count; index++)
            {
                string warningMessage = warningMessages[index];
                if (string.IsNullOrWhiteSpace(warningMessage))
                {
                    continue;
                }

                visibleWarnings.Add(warningMessage.Trim());
            }

            if (visibleWarnings.Count == 0)
            {
                return GetDefaultWindowInfoText();
            }

            return "Предупреждения: " + string.Join(" ", visibleWarnings.ToArray());
        }

        private RoomPlanSchemeSettings BuildRoomPlanSchemeSettings(ElevationSettings settings)
        {
            RoomPlanSchemeSettings roomPlanSettings = new RoomPlanSchemeSettings();
            roomPlanSettings.NamePart1 = settings.RoomPlanNamePart1 ?? string.Empty;
            roomPlanSettings.NamePart2 = settings.RoomPlanNamePart2 ?? string.Empty;
            roomPlanSettings.NamePart3 = settings.RoomPlanNamePart3 ?? string.Empty;
            roomPlanSettings.ViewTemplateId = settings.RoomPlanViewTemplateId != null
                ? settings.RoomPlanViewTemplateId
                : ElementId.InvalidElementId;
            roomPlanSettings.ViewScale = settings.RoomPlanViewScale;
            roomPlanSettings.CropOffsetMm = settings.RoomPlanCropOffsetMm;
            return roomPlanSettings;
        }

        private bool TryPickLineGroupsAndRoom(
            UIDocument uiDocument,
            View activeView,
            ElementId activePlanLevelId,
            LineGroupSelectionMode lineGroupSelectionMode,
            IList<string> warnings,
            out ElevationSelectionPackage selectionPackage)
        {
            selectionPackage = null;

            DetailLineSelectionService selectionService = new DetailLineSelectionService();
            List<List<DetailLine>> lineGroups;
            bool linesPicked = TryCollectLineGroups(
                uiDocument,
                activeView,
                selectionService,
                lineGroupSelectionMode,
                warnings,
                out lineGroups);

            if (!linesPicked)
            {
                return false;
            }

            List<DetailLine> selectedLines = FlattenLineGroups(lineGroups);
            if (selectedLines.Count == 0)
            {
                ToastNotifier.ShowWarning(
                    "SAB Развертки",
                    "Не выбраны корректные линии детализации. Нажмите 'Выбрать линии' и повторите выбор.");
                return false;
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

                return false;
            }

            if (!RevitElementIdUtils.AreEqual(roomData.LevelId, activePlanLevelId))
            {
                ToastNotifier.ShowWarning(
                    "SAB Развертки",
                    "Выбранное помещение находится на другом уровне. Выберите помещение на уровне активного плана.");
                return false;
            }

            selectionPackage = new ElevationSelectionPackage();
            selectionPackage.LineGroups = lineGroups;
            selectionPackage.SelectedLines = selectedLines;
            selectionPackage.RoomData = roomData;
            return true;
        }

        private string BuildSelectionStatusText(
            ElevationSelectionPackage selectionPackage,
            LineGroupSelectionMode lineGroupSelectionMode)
        {
            if (selectionPackage == null || selectionPackage.RoomData == null)
            {
                return "Линии и помещение не выбраны.";
            }

            int groupCount = selectionPackage.LineGroups != null ? selectionPackage.LineGroups.Count : 0;
            int lineCount = selectionPackage.SelectedLines != null ? selectionPackage.SelectedLines.Count : 0;
            string modeText = lineGroupSelectionMode == LineGroupSelectionMode.MultipleGroups
                ? "несколько групп"
                : "одна группа";

            return "Выбрано: " + lineCount +
                   " линий, " + groupCount +
                   " групп (" + modeText +
                   "). Помещение: " + selectionPackage.RoomData.RoomNumber +
                   " " + selectionPackage.RoomData.RoomName + ".";
        }

        private bool TryCollectLineGroups(
            UIDocument uiDocument,
            View activeView,
            DetailLineSelectionService selectionService,
            LineGroupSelectionMode mode,
            IList<string> warnings,
            out List<List<DetailLine>> lineGroups)
        {
            lineGroups = new List<List<DetailLine>>();

            if (uiDocument == null || activeView == null || selectionService == null)
            {
                return false;
            }

            HashSet<int> usedLineIds = new HashSet<int>();

            if (mode == LineGroupSelectionMode.SingleGroup)
            {
                ToastNotifier.ShowInfo("SAB Развертки", "Выберите линии одной группы и нажмите «Готово».");
                DetailLineSelectionResult singleGroupResult = selectionService.PickDetailLines(
                    uiDocument,
                    activeView,
                    "Выберите линии одной группы и нажмите «Готово»");

                AppendWarnings(warnings, singleGroupResult.Warnings);
                if (singleGroupResult.IsCancelled)
                {
                    return false;
                }

                List<DetailLine> uniqueLines = FilterUniqueGroupLines(singleGroupResult.Lines, usedLineIds, warnings, 1);
                if (uniqueLines.Count > 0)
                {
                    lineGroups.Add(uniqueLines);
                }

                return lineGroups.Count > 0;
            }

            int groupNumber = 1;
            while (true)
            {
                ToastNotifier.ShowInfo(
                    "SAB Развертки",
                    "Выберите линии группы №" + groupNumber + " и нажмите «Готово».",
                    8);

                DetailLineSelectionResult groupResult = selectionService.PickDetailLines(
                    uiDocument,
                    activeView,
                    "Выберите линии группы №" + groupNumber + " и нажмите «Готово»");

                AppendWarnings(warnings, groupResult.Warnings);

                if (groupResult.IsCancelled)
                {
                    if (lineGroups.Count == 0)
                    {
                        return false;
                    }

                    TaskDialogResult finishAfterCancel = TaskDialog.Show(
                        "SAB Развертки",
                        "Выбор группы отменен. Завершить выбор групп и продолжить?",
                        TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                    if (finishAfterCancel == TaskDialogResult.Yes)
                    {
                        break;
                    }

                    continue;
                }

                List<DetailLine> uniqueGroupLines = FilterUniqueGroupLines(groupResult.Lines, usedLineIds, warnings, groupNumber);
                if (uniqueGroupLines.Count == 0)
                {
                    if (lineGroups.Count == 0)
                    {
                        ToastNotifier.ShowWarning("SAB Развертки", "В группе №" + groupNumber + " нет корректных уникальных линий.");
                    }

                    continue;
                }

                lineGroups.Add(uniqueGroupLines);

                TaskDialogResult nextAction = AskGroupSelectionNextAction(groupNumber);
                if (nextAction == TaskDialogResult.CommandLink1)
                {
                    groupNumber++;
                    continue;
                }

                if (nextAction == TaskDialogResult.CommandLink2)
                {
                    break;
                }

                return false;
            }

            return lineGroups.Count > 0;
        }

        private TaskDialogResult AskGroupSelectionNextAction(int currentGroupNumber)
        {
            LineGroupNextActionWindow actionWindow = new LineGroupNextActionWindow(currentGroupNumber);
            bool? dialogResult = actionWindow.ShowDialog();
            if (!dialogResult.HasValue || !dialogResult.Value)
            {
                return TaskDialogResult.Cancel;
            }

            return actionWindow.IsNextGroupAction
                ? TaskDialogResult.CommandLink1
                : TaskDialogResult.CommandLink2;
        }

        private bool TryStartCropByExampleWorkflow(
            UIApplication uiApplication,
            ViewPlan sourcePlanView,
            ElementId sourcePlanLevelId,
            ElevationSettings settings,
            CropByExampleAction requestedAction,
            ElevationCropByExampleService cropByExampleService,
            IList<string> warnings)
        {
            if (uiApplication == null || sourcePlanView == null || settings == null || cropByExampleService == null)
            {
                return false;
            }

            if (requestedAction == CropByExampleAction.None)
            {
                return false;
            }

            CropByExampleSession session = cropByExampleService.CreateSession(
                sourcePlanView,
                sourcePlanLevelId,
                settings);

            CropByExampleExternalEventHandler externalEventHandler =
                new CropByExampleExternalEventHandler(session, cropByExampleService);

            ExternalEvent externalEvent = ExternalEvent.Create(externalEventHandler);

            CropByExampleLineCreationWindow helperWindow =
                new CropByExampleLineCreationWindow(externalEventHandler, externalEvent);

            externalEventHandler.SetWindow(helperWindow);

            if (requestedAction == CropByExampleAction.PickLine)
            {
                helperWindow.SetExistingLineMode();
                helperWindow.Show();

                externalEventHandler.RequestOperation(CropByExampleOperation.PickExistingLineAndCreateView);
                externalEvent.Raise();
                return true;
            }

            if (requestedAction == CropByExampleAction.CreateLine)
            {
                helperWindow.SetCreateLineMode();
                helperWindow.Show();

                bool commandPosted = cropByExampleService.TryPostDetailLineCommand(uiApplication, warnings);
                if (!commandPosted && warnings != null && warnings.Count > 0)
                {
                    ToastNotifier.ShowWarning("SAB Развертки", warnings[warnings.Count - 1]);
                }

                return true;
            }

            return false;
        }

        private List<DetailLine> FilterUniqueGroupLines(
            IList<DetailLine> sourceLines,
            HashSet<int> usedLineIds,
            IList<string> warnings,
            int groupNumber)
        {
            List<DetailLine> result = new List<DetailLine>();
            if (sourceLines == null || sourceLines.Count == 0)
            {
                return result;
            }

            if (usedLineIds == null)
            {
                usedLineIds = new HashSet<int>();
            }

            for (int index = 0; index < sourceLines.Count; index++)
            {
                DetailLine line = sourceLines[index];
                if (line == null || line.Id == null || line.Id == ElementId.InvalidElementId)
                {
                    continue;
                }

                int lineIdValue = line.Id.IntegerValue;
                if (usedLineIds.Contains(lineIdValue))
                {
                    if (warnings != null)
                    {
                        warnings.Add(
                            "Линия " + lineIdValue +
                            " уже была выбрана в предыдущей группе и пропущена в группе №" + groupNumber + ".");
                    }

                    continue;
                }

                usedLineIds.Add(lineIdValue);
                result.Add(line);
            }

            return result;
        }

        private List<DetailLine> FlattenLineGroups(IList<List<DetailLine>> lineGroups)
        {
            List<DetailLine> flattenedLines = new List<DetailLine>();
            if (lineGroups == null || lineGroups.Count == 0)
            {
                return flattenedLines;
            }

            for (int groupIndex = 0; groupIndex < lineGroups.Count; groupIndex++)
            {
                List<DetailLine> group = lineGroups[groupIndex];
                if (group == null || group.Count == 0)
                {
                    continue;
                }

                for (int lineIndex = 0; lineIndex < group.Count; lineIndex++)
                {
                    DetailLine line = group[lineIndex];
                    if (line == null)
                    {
                        continue;
                    }

                    flattenedLines.Add(line);
                }
            }

            return flattenedLines;
        }

        private List<ElevationLineData> BuildElevationLinesWithGlobalCornerIndexing(
            IList<List<DetailLine>> lineGroups,
            ElevationGeometryService elevationGeometryService,
            IList<string> warnings)
        {
            List<ElevationLineData> result = new List<ElevationLineData>();
            if (lineGroups == null || lineGroups.Count == 0 || elevationGeometryService == null)
            {
                return result;
            }

            // Сквозная нумерация углов:
            // 1) для замкнутой группы последняя линия замыкается в первый угол группы;
            // 2) для незамкнутой группы последняя линия идет в новый угол;
            // 3) следующая группа начинается с корректного номера после предыдущей группы.
            int nextGroupStartCornerNumber = 1;

            for (int groupIndex = 0; groupIndex < lineGroups.Count; groupIndex++)
            {
                List<DetailLine> groupLines = lineGroups[groupIndex];
                List<ElevationLineData> groupLineData = elevationGeometryService.BuildElevationLineData(groupLines, warnings);
                if (groupLineData == null || groupLineData.Count == 0)
                {
                    if (warnings != null)
                    {
                        warnings.Add("Группа линий №" + (groupIndex + 1) + " не содержит валидной линейной геометрии.");
                    }

                    continue;
                }

                int groupStartCorner = nextGroupStartCornerNumber;
                bool isClosedGroup = IsClosedLineGroup(groupLineData);

                for (int lineIndex = 0; lineIndex < groupLineData.Count; lineIndex++)
                {
                    ElevationLineData lineData = groupLineData[lineIndex];
                    if (lineData == null)
                    {
                        continue;
                    }

                    int startCornerNumber = groupStartCorner + lineIndex;
                    int endCornerNumber = (lineIndex == groupLineData.Count - 1)
                        ? (isClosedGroup ? groupStartCorner : groupStartCorner + groupLineData.Count)
                        : groupStartCorner + lineIndex + 1;

                    lineData.Index = startCornerNumber;
                    lineData.EndIndex = endCornerNumber;
                    result.Add(lineData);
                }

                nextGroupStartCornerNumber += isClosedGroup
                    ? groupLineData.Count
                    : groupLineData.Count + 1;
            }

            return result;
        }

        private bool IsClosedLineGroup(IList<ElevationLineData> groupLineData)
        {
            if (groupLineData == null || groupLineData.Count < 2)
            {
                return false;
            }

            double pointTolerance = UnitConversionUtils.MillimetersToFeet(1.0);
            List<LineGroupNode> nodes = new List<LineGroupNode>();

            for (int index = 0; index < groupLineData.Count; index++)
            {
                ElevationLineData lineData = groupLineData[index];
                if (lineData == null || lineData.StartPoint == null || lineData.EndPoint == null)
                {
                    return false;
                }

                int startNodeIndex = GetOrCreateGroupNode(nodes, lineData.StartPoint, pointTolerance);
                int endNodeIndex = GetOrCreateGroupNode(nodes, lineData.EndPoint, pointTolerance);

                if (startNodeIndex == endNodeIndex)
                {
                    // Нулевая или вырожденная связь не может считаться корректным замкнутым контуром.
                    return false;
                }

                nodes[startNodeIndex].Degree++;
                nodes[endNodeIndex].Degree++;
            }

            if (nodes.Count < 3)
            {
                return false;
            }

            for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
            {
                if (nodes[nodeIndex].Degree != 2)
                {
                    return false;
                }
            }

            return true;
        }

        private int GetOrCreateGroupNode(IList<LineGroupNode> nodes, XYZ point, double tolerance)
        {
            for (int index = 0; index < nodes.Count; index++)
            {
                LineGroupNode node = nodes[index];
                if (node != null && node.Point != null && node.Point.DistanceTo(point) <= tolerance)
                {
                    return index;
                }
            }

            LineGroupNode createdNode = new LineGroupNode();
            createdNode.Point = point;
            createdNode.Degree = 0;
            nodes.Add(createdNode);
            return nodes.Count - 1;
        }

        private class LineGroupNode
        {
            public XYZ Point { get; set; }

            public int Degree { get; set; }
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

            if (string.IsNullOrWhiteSpace(settings.RoomPlanNamePart1) &&
                string.IsNullOrWhiteSpace(settings.RoomPlanNamePart2) &&
                string.IsNullOrWhiteSpace(settings.RoomPlanNamePart3))
            {
                validationMessage = "Формула имени план-схемы не может быть пустой.";
                return false;
            }

            if (settings.RoomPlanRoomTagTypeId != null && settings.RoomPlanRoomTagTypeId != ElementId.InvalidElementId)
            {
                FamilySymbol roomTagType = document.GetElement(settings.RoomPlanRoomTagTypeId) as FamilySymbol;
                if (roomTagType == null || roomTagType.Category == null || roomTagType.Category.Id.IntegerValue != (int)BuiltInCategory.OST_RoomTags)
                {
                    validationMessage = "Тип марки помещения план-схемы должен относиться к категории 'Марки помещений'.";
                    return false;
                }
            }

            FamilySymbol planMarkType = document.GetElement(settings.PlanCornerMarkTypeId) as FamilySymbol;
            if (planMarkType == null)
            {
                validationMessage = "Не выбран или не найден тип марки угла на плане.";
                return false;
            }

            if (!CornerMarkConstants.IsAnnotationSymbol(planMarkType))
            {
                validationMessage = "Тип марки угла на плане должен относиться к категории '" + CornerMarkConstants.GetAnnotationCategoryNameForMessage() + "'.";
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

                if (!CornerMarkConstants.IsAnnotationSymbol(sheetMarkType))
                {
                    validationMessage = "Тип марки угла на листе должен относиться к категории '" + CornerMarkConstants.GetAnnotationCategoryNameForMessage() + "'.";
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

        private void CopySelectedDetailLinesToPlanScheme(
            Document document,
            ViewPlan sourcePlanView,
            ViewPlan targetPlanSchemeView,
            IList<DetailLine> selectedDetailLines,
            IList<string> warnings)
        {
            if (document == null || sourcePlanView == null || targetPlanSchemeView == null || selectedDetailLines == null || selectedDetailLines.Count == 0)
            {
                return;
            }

            List<ElementId> sourceLineIds = new List<ElementId>();
            for (int index = 0; index < selectedDetailLines.Count; index++)
            {
                DetailLine detailLine = selectedDetailLines[index];
                if (detailLine == null || detailLine.Id == null || detailLine.Id == ElementId.InvalidElementId)
                {
                    continue;
                }

                if (!RevitElementIdUtils.AreEqual(detailLine.OwnerViewId, sourcePlanView.Id))
                {
                    continue;
                }

                sourceLineIds.Add(detailLine.Id);
            }

            if (sourceLineIds.Count == 0)
            {
                return;
            }

            try
            {
                CopyPasteOptions copyOptions = new CopyPasteOptions();
                ICollection<ElementId> copiedIds = ElementTransformUtils.CopyElements(
                    sourcePlanView,
                    sourceLineIds,
                    targetPlanSchemeView,
                    Transform.Identity,
                    copyOptions);

                if (copiedIds == null || copiedIds.Count == 0)
                {
                    warnings.Add("Линии детализации не были скопированы на план-схему.");
                }
            }
            catch (Exception copyException)
            {
                warnings.Add("Не удалось скопировать линии детализации на план-схему: " + copyException.Message);
            }
        }
    }
}
