using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using SAB.CreateViewsAndSheets.Models;
using SAB.CreateViewsAndSheets.Views;
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

        private class RoomExecutionData
        {
            public MultiRoomSelectionItem SelectionPackage { get; set; }

            public List<ElevationLineData> ElevationLines { get; set; }

            public ElevationViewCreationResult CreationResult { get; set; }

            public ViewPlan RoomPlanView { get; set; }

            public bool UsesExistingRoomPlanView { get; set; }
        }

        private class WindowChoiceState
        {
            public bool IsMultipleRoomsOnSheet { get; set; }

            public bool PickRoomFromLink { get; set; }

            public bool EnableRoomObjectCategory { get; set; }

            public bool CornerMarksOnlyCornerNumber { get; set; }

            public bool SheetCornerMarksBelowView { get; set; }

            public RevitElementOption SelectedPlanCornerMarkType { get; set; }

            public RevitElementOption SelectedSheetCornerMarkType { get; set; }

            public static WindowChoiceState Capture(ElevationSettingsViewModel viewModel)
            {
                if (viewModel == null)
                {
                    return null;
                }

                WindowChoiceState state = new WindowChoiceState();
                state.IsMultipleRoomsOnSheet = viewModel.IsMultipleRoomsOnSheet;
                state.PickRoomFromLink = viewModel.PickRoomFromLink;
                state.EnableRoomObjectCategory = viewModel.EnableRoomObjectCategory;
                state.CornerMarksOnlyCornerNumber = viewModel.CornerMarksOnlyCornerNumber;
                state.SheetCornerMarksBelowView = viewModel.SheetCornerMarksBelowView;
                state.SelectedPlanCornerMarkType = viewModel.SelectedPlanCornerMarkType;
                state.SelectedSheetCornerMarkType = viewModel.SelectedSheetCornerMarkType;
                return state;
            }

            public void Restore(ElevationSettingsViewModel viewModel)
            {
                if (viewModel == null)
                {
                    return;
                }

                viewModel.IsMultipleRoomsOnSheet = IsMultipleRoomsOnSheet;
                viewModel.PickRoomFromLink = PickRoomFromLink;
                viewModel.EnableRoomObjectCategory = EnableRoomObjectCategory;
                viewModel.CornerMarksOnlyCornerNumber = CornerMarksOnlyCornerNumber;
                viewModel.SheetCornerMarksBelowView = SheetCornerMarksBelowView;

                if (SelectedPlanCornerMarkType != null)
                {
                    viewModel.SelectedPlanCornerMarkType = SelectedPlanCornerMarkType;
                }

                if (SelectedSheetCornerMarkType != null)
                {
                    viewModel.SelectedSheetCornerMarkType = SelectedSheetCornerMarkType;
                }
            }
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            CreateViewsAndSheetsProgressWindow progressWindow = null;
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
                    string activeViewWarning = activeView is ViewSheet
                        ? "На листе сначала активируйте видовой экран план-схемы двойным щелчком, затем снова запустите команду."
                        : "Активный вид должен быть планом этажа или потолка.";
                    ToastNotifier.ShowWarning("SAB Развертки", activeViewWarning);
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
                try
                {
                    settingsViewModel.ApplyMarkPreferences(settingsStorageService.LoadMarkPreferences());
                }
                catch (Exception loadException)
                {
                    warnings.Add("Не удалось загрузить настройки марок: " + loadException.Message);
                }
                ViewSheet sheetContainingActivePlan = FindSheetContainingView(document, activePlanView.Id);
                if (sheetContainingActivePlan != null)
                {
                    settingsViewModel.SelectSuggestedExistingSheet(sheetContainingActivePlan.Id);
                }

                SheetPointSelectionService sheetPointSelectionService = new SheetPointSelectionService();
                ElevationCropByExampleService cropByExampleService = new ElevationCropByExampleService();
                string selectionStatusText = "Линии и помещение не выбраны.";
                string windowInfoText = sheetContainingActivePlan != null
                    ? "Активная план-схема размещена на листе '" +
                      sheetContainingActivePlan.SheetNumber + " — " + sheetContainingActivePlan.Name +
                      "'. Этот лист подставлен как целевой для режима добавления на существующий лист."
                    : GetDefaultWindowInfoText();
                if (warnings.Count > 0)
                {
                    windowInfoText = BuildWindowInfoText(warnings);
                }

                ObservableCollection<MultiRoomSelectionItem> selectionPackages =
                    new ObservableCollection<MultiRoomSelectionItem>();
                bool hasTransferredSelection = false;
                ElevationSettings settings = null;

                while (true)
                {
                    if (hasTransferredSelection && selectionPackages.Count > 1)
                    {
                        settingsViewModel.IsMultipleRoomsOnSheet = true;
                    }

                    WindowChoiceState choiceState = WindowChoiceState.Capture(settingsViewModel);
                    ElevationSettingsWindow settingsWindow = new ElevationSettingsWindow(
                        settingsViewModel,
                        hasTransferredSelection && HasCompletedSelections(selectionPackages),
                        selectionStatusText,
                        windowInfoText);
                    if (choiceState != null)
                    {
                        choiceState.Restore(settingsViewModel);
                    }

                    bool? dialogResult = settingsWindow.ShowDialog();
                    PersistCurrentWindowSettings(
                        settingsViewModel,
                        settingsStorageService,
                        warnings);

                    if (!dialogResult.HasValue || !dialogResult.Value)
                    {
                        // Семейства и подготовка видимости уже загружены в проект отдельными транзакциями.
                        // Возвращаем Succeeded, чтобы Revit не откатывал эти изменения при закрытии окна настроек.
                        return Result.Succeeded;
                    }

                    if (settingsWindow.RequestedAction == ElevationSettingsWindowAction.PickSelection)
                    {
                        roomVisibilityService.EnsureRoomsAndObjectVisible(
                            document,
                            activeView,
                            settingsViewModel.EnableRoomObjectCategory,
                            warnings);

                        List<string> selectionWarnings = new List<string>();

                        if (settingsViewModel.IsMultipleRoomsOnSheet)
                        {
                            hasTransferredSelection = false;
                            bool listTransferred = TryEditMultiRoomSelectionList(
                                uiDocument,
                                activeView,
                                activePlanLevelId,
                                settingsViewModel.PickRoomFromLink,
                                selectionPackages,
                                selectionWarnings);

                            AppendWarnings(warnings, selectionWarnings);
                            if (selectionWarnings.Count > 0)
                            {
                                windowInfoText = BuildWindowInfoText(selectionWarnings);
                            }

                            if (listTransferred)
                            {
                                hasTransferredSelection = true;
                                settingsViewModel.IsMultipleRoomsOnSheet = true;
                                UpdateNamingPreviewContexts(settingsViewModel, selectionPackages);
                                selectionStatusText = BuildSelectionStatusText(selectionPackages);
                                windowInfoText = "Список помещений передан. Проверьте параметры и нажмите Создать развертки.";
                                ToastNotifier.ShowInfo("SAB Развертки", windowInfoText);
                                TryPickRequiredExistingSheetPlacement(
                                    uiDocument,
                                    activeView,
                                    settingsViewModel,
                                    sheetPointSelectionService,
                                    warnings,
                                    ref windowInfoText);
                            }
                            else
                            {
                                selectionStatusText = "Список помещений не передан. Нажмите Выбрать линии, чтобы продолжить заполнение.";
                                if (selectionWarnings.Count == 0)
                                {
                                    windowInfoText = "Редактор списка закрыт без передачи. Заполненные строки сохранены.";
                                }
                            }

                            continue;
                        }

                        MultiRoomSelectionItem pickedSelectionPackage;
                        bool selectionPicked = TryPickLineGroupsAndRoom(
                            uiDocument,
                            activeView,
                            activePlanLevelId,
                            settingsViewModel.PickRoomFromLink,
                            selectionWarnings,
                            out pickedSelectionPackage);

                        AppendWarnings(warnings, selectionWarnings);
                        if (selectionWarnings.Count > 0)
                        {
                            windowInfoText = BuildWindowInfoText(selectionWarnings);
                        }

                        if (selectionPicked)
                        {
                            selectionPackages.Clear();
                            selectionPackages.Add(pickedSelectionPackage);
                            hasTransferredSelection = true;
                            UpdateNamingPreviewContexts(settingsViewModel, selectionPackages);
                            selectionStatusText = BuildSelectionStatusText(selectionPackages);
                            windowInfoText = "Стек собран. Проверьте параметры и нажмите Создать развертки.";
                            ToastNotifier.ShowInfo("SAB Развертки", windowInfoText);
                            TryPickRequiredExistingSheetPlacement(
                                uiDocument,
                                activeView,
                                settingsViewModel,
                                sheetPointSelectionService,
                                warnings,
                                ref windowInfoText);
                        }
                        else
                        {
                            selectionStatusText = hasTransferredSelection && HasCompletedSelections(selectionPackages)
                                ? BuildSelectionStatusText(selectionPackages)
                                : "Линии и помещение не выбраны. Нажмите Выбрать линии.";
                            if (selectionWarnings.Count == 0)
                            {
                                windowInfoText = hasTransferredSelection && HasCompletedSelections(selectionPackages)
                                    ? "Новый выбор отменен. Ранее добавленные помещения сохранены в стеке."
                                    : "Стек не собран. Нажмите Выбрать линии, затем укажите линии детализации и помещение.";
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

                        PickSheetPlacement(
                            uiDocument,
                            activeView,
                            sheetPointSettings,
                            settingsViewModel,
                            sheetPointSelectionService,
                            warnings,
                            ref windowInfoText);

                        continue;
                    }

                    if (settingsWindow.RequestedAction == ElevationSettingsWindowAction.PickRoomPlanPoint)
                    {
                        var planSettings = settingsWindow.SelectedSettings;
                        var planWarnings = new List<string>();
                        XYZ point;
                        ViewPlan ignoredPlan;
                        bool cancelled;
                        if (sheetPointSelectionService.TryPickStartPointOnSheet(uiDocument, activeView, planSettings,
                            planWarnings, out point, out ignoredPlan, out cancelled, true))
                        {
                            settingsViewModel.SetRoomPlanPosition(planSettings.SheetLayoutSettings.RoomPlanOffsetRightMm,
                                planSettings.SheetLayoutSettings.RoomPlanOffsetBottomMm);
                            windowInfoText = "Положение план-схемы сохранено. Отступы справа и снизу указаны на вкладке «План-схема».";
                        }
                        else windowInfoText = cancelled ? "Выбор точки план-схемы отменён; прежние отступы сохранены." : BuildWindowInfoText(planWarnings);
                        AppendWarnings(warnings, planWarnings);
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
                            continue;
                        }

                        roomVisibilityService.EnsureRoomsAndObjectVisible(
                            document,
                            activeView,
                            settingsViewModel.EnableRoomObjectCategory,
                            warnings);

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

                    settings.CornerMarksOnlyCornerNumber = settingsViewModel.CornerMarksOnlyCornerNumber;
                    settings.SheetCornerMarksBelowView = settingsViewModel.SheetCornerMarksBelowView;

                    if (!hasTransferredSelection || !HasCompletedSelections(selectionPackages))
                    {
                        string selectionMessage = settings.MultipleRoomsOnSheet
                            ? "Откройте список помещений, заполните все строки и нажмите 'Передать список помещений'."
                            : "Сначала нажмите 'Выбрать линии' и укажите линии детализации и помещение.";
                        ToastNotifier.ShowWarning("SAB Развертки", selectionMessage);
                        selectionStatusText = selectionMessage;
                        windowInfoText = selectionMessage;
                        continue;
                    }

                    if (selectionPackages.Count > 1)
                    {
                        settings.MultipleRoomsOnSheet = true;
                        settingsViewModel.IsMultipleRoomsOnSheet = true;
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

                try
                {
                    settingsStorageService.SaveSettings(settings);
                }
                catch (Exception saveException)
                {
                    warnings.Add("Не удалось сохранить настройки: " + saveException.Message);
                }

                ElevationGeometryService elevationGeometryService = new ElevationGeometryService();
                LineOrientationService lineOrientationService = new LineOrientationService();
                List<RoomExecutionData> roomExecutionDataList = new List<RoomExecutionData>();
                int selectedLinesCount = 0;

                for (int selectionIndex = 0; selectionIndex < selectionPackages.Count; selectionIndex++)
                {
                    MultiRoomSelectionItem currentSelectionPackage = selectionPackages[selectionIndex];
                    if (currentSelectionPackage == null || currentSelectionPackage.RoomData == null)
                    {
                        continue;
                    }

                    List<ElevationLineData> elevationLines = BuildElevationLinesWithGlobalCornerIndexing(
                        currentSelectionPackage.LineGroups,
                        elevationGeometryService,
                        warnings);

                    if (elevationLines.Count == 0)
                    {
                        warnings.Add(
                            "Помещение " + currentSelectionPackage.RoomData.RoomNumber + " " +
                            currentSelectionPackage.RoomData.RoomName +
                            ": выбранные линии не содержат корректной линейной геометрии.");
                        continue;
                    }

                    bool orientationAssigned = lineOrientationService.TryAssignInsideNormals(
                        document,
                        elevationLines,
                        currentSelectionPackage.RoomData,
                        settings.MarkerOffsetMm,
                        warnings);
                    if (!orientationAssigned)
                    {
                        warnings.Add(
                            "Помещение " + currentSelectionPackage.RoomData.RoomNumber + " " +
                            currentSelectionPackage.RoomData.RoomName +
                            ": не удалось определить направление разверток.");
                        continue;
                    }

                    RoomExecutionData roomExecutionData = new RoomExecutionData();
                    roomExecutionData.SelectionPackage = currentSelectionPackage;
                    roomExecutionData.ElevationLines = elevationLines;
                    roomExecutionData.CreationResult = new ElevationViewCreationResult();
                    roomExecutionDataList.Add(roomExecutionData);
                    selectedLinesCount += elevationLines.Count;
                }

                if (roomExecutionDataList.Count == 0)
                {
                    ToastNotifier.ShowWarning("SAB Развертки", "Ни для одного помещения не удалось подготовить линии разверток.");
                    return Result.Cancelled;
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
                AutomaticElevationSheetPlacementService viewportPlacementService = new AutomaticElevationSheetPlacementService();
                PlanCornerMarkPlacementService planCornerMarkPlacementService = new PlanCornerMarkPlacementService();
                RoomPlanRoomTagPlacementService roomPlanRoomTagPlacementService = new RoomPlanRoomTagPlacementService();
                ElevationCreationReportService reportService = new ElevationCreationReportService();

                ElevationViewCreationResult creationResult = new ElevationViewCreationResult();
                bool manualBoundaryRequired = false;
                ViewSheet targetSheet = null;
                bool targetSheetWasCreated = false;
                int placedViewportCount = 0;
                int placedPlanMarksCount = 0;
                int placedSheetMarksCount = 0;
                List<ViewSheet> placedSheets = new List<ViewSheet>();
                int unplacedViewsCount = 0;
                bool forcedPlacementUsed = false;
                bool automaticFallbackUsed = false;

                ToastNotifier.ShowInfo("SAB Развертки", "Создание разверток запущено. Дождитесь завершения операции.");

                bool placeViewsOnSheet = settings.CreateSheet || settings.UseExistingSheet;
                int totalProgressSteps = selectedLinesCount + roomExecutionDataList.Count * 2 +
                                         (placeViewsOnSheet
                                             ? roomExecutionDataList.Count * 2 + 1
                                             : 0) + 1;
                int progressStep = 0;
                progressWindow = ShowCreationProgressWindow(uiApplication, totalProgressSteps);
                ReportProgress(
                    progressWindow,
                    progressStep,
                    totalProgressSteps,
                    "Подготовка",
                    "Подготовлено помещений: " + roomExecutionDataList.Count + ".");

                using (TransactionGroup transactionGroup = new TransactionGroup(document, "SAB Развертки стен"))
                {
                    transactionGroup.Start();

                    using (Transaction transaction = new Transaction(document, "Создать развертки"))
                    {
                        transaction.Start();

                        for (int roomIndex = 0; roomIndex < roomExecutionDataList.Count; roomIndex++)
                        {
                            RoomExecutionData roomExecutionData = roomExecutionDataList[roomIndex];
                            MultiRoomSelectionItem currentSelectionPackage = roomExecutionData.SelectionPackage;
                            RoomData roomData = currentSelectionPackage.RoomData;

                            roomExecutionData.CreationResult = viewCreationService.CreateElevationViews(
                                document,
                                activePlanView,
                                roomExecutionData.ElevationLines,
                                settings,
                                warnings,
                                delegate(
                                    int currentLine,
                                    int totalLines,
                                    ElevationViewCreationResult partialResult)
                                {
                                    progressStep++;
                                    int createdViews = creationResult.CreatedViews.Count +
                                                       partialResult.CreatedViews.Count;
                                    int failedViews = creationResult.FailedViews.Count +
                                                      partialResult.FailedViews.Count;
                                    ReportProgress(
                                        progressWindow,
                                        progressStep,
                                        totalProgressSteps,
                                        "Создание разверток",
                                        BuildRoomProgressDetails(
                                            roomData,
                                            roomIndex,
                                            roomExecutionDataList.Count) +
                                        " Линия " + currentLine + " из " + totalLines + ".",
                                        "Создано видов: " + createdViews +
                                        "  ·  Ошибок: " + failedViews);
                                });

                            MergeCreationResults(creationResult, roomExecutionData.CreationResult);
                            if (roomExecutionData.CreationResult.CreatedViews.Count == 0)
                            {
                                progressStep++;
                                ReportProgress(
                                    progressWindow,
                                    progressStep,
                                    totalProgressSteps,
                                    "План-схема",
                                    settings.CreateRoomPlanScheme
                                        ? "План-схема пропущена: развертки не созданы."
                                        : "Создание план-схемы отключено.");
                                progressStep++;
                                ReportProgress(
                                    progressWindow,
                                    progressStep,
                                    totalProgressSteps,
                                    "Марки",
                                    "Марки пропущены для текущего помещения.");
                                continue;
                            }

                            Room roomForPlanScheme = null;
                            Transform roomToHost = Transform.Identity;
                            if (settings.CreateRoomPlanScheme)
                            {
                                bool useExistingRoomPlanView = settings.UseExistingSheet &&
                                    settings.ExistingRoomPlanViewId != null &&
                                    !RevitElementIdUtils.AreEqual(settings.ExistingRoomPlanViewId, ElementId.InvalidElementId);

                                if (useExistingRoomPlanView)
                                {
                                    roomExecutionData.RoomPlanView = document.GetElement(settings.ExistingRoomPlanViewId) as ViewPlan;
                                    roomExecutionData.UsesExistingRoomPlanView = roomExecutionData.RoomPlanView != null;

                                    if (roomExecutionData.RoomPlanView != null)
                                    {
                                        CopySelectedDetailLinesToPlanScheme(
                                            document,
                                            activePlanView,
                                            roomExecutionData.RoomPlanView,
                                            currentSelectionPackage.SelectedLines,
                                            warnings);
                                    }
                                    else
                                    {
                                        warnings.Add(
                                            "Выбранная существующая план-схема не найдена. Марки помещения " +
                                            roomData.RoomNumber + " на ней не размещены.");
                                    }
                                }
                                else if (roomData.TryResolveRoom(document, out roomForPlanScheme, out roomToHost))
                                {
                                    RoomPlanSchemeSettings roomPlanSettings = BuildRoomPlanSchemeSettings(settings);
                                    IList<Room> singleRoomList = new List<Room> { roomForPlanScheme };
                                    RoomPlanSchemeCreationSummary roomPlanSummary = roomPlanSchemeCreationService.CreateRoomPlanSchemes(
                                        document,
                                        activePlanView,
                                        singleRoomList,
                                        roomPlanSettings,
                                        null,
                                        roomToHost);

                                    if (roomPlanSummary != null)
                                    {
                                        AppendWarnings(warnings, roomPlanSummary.Warnings);
                                        manualBoundaryRequired = manualBoundaryRequired || roomPlanSummary.ManualBoundaryRequired;

                                        if (roomPlanSummary.CreatedViewIds.Count > 0)
                                        {
                                            roomExecutionData.RoomPlanView = document.GetElement(roomPlanSummary.CreatedViewIds[0]) as ViewPlan;
                                            if (roomExecutionData.RoomPlanView != null)
                                            {
                                                CopySelectedDetailLinesToPlanScheme(
                                                    document,
                                                    activePlanView,
                                                    roomExecutionData.RoomPlanView,
                                                    currentSelectionPackage.SelectedLines,
                                                    warnings);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    warnings.Add(
                                        "Помещение " + roomData.RoomNumber + " " + roomData.RoomName +
                                        " не удалось получить из документа для построения план-схемы.");
                                }
                            }

                            progressStep++;
                            ReportProgress(
                                progressWindow,
                                progressStep,
                                totalProgressSteps,
                                "План-схема",
                                settings.CreateRoomPlanScheme
                                    ? BuildRoomProgressDetails(roomData, roomIndex, roomExecutionDataList.Count)
                                    : "Создание план-схемы отключено.");

                            int placedPlanMarksOnSourcePlan = planCornerMarkPlacementService.PlacePlanCornerMarks(
                                document,
                                activePlanView,
                                roomExecutionData.ElevationLines,
                                roomData,
                                settings.PlanCornerMarkTypeId,
                                settings.CornerMarksOnlyCornerNumber,
                                warnings);

                            int placedPlanMarksOnPlanScheme = 0;
                            int placedRoomTagOnPlanScheme = 0;

                            bool roomPlanIsSourcePlan = roomExecutionData.RoomPlanView != null &&
                                RevitElementIdUtils.AreEqual(roomExecutionData.RoomPlanView.Id, activePlanView.Id);

                            if (roomExecutionData.RoomPlanView != null && !roomPlanIsSourcePlan)
                            {
                                placedPlanMarksOnPlanScheme = planCornerMarkPlacementService.PlacePlanCornerMarks(
                                    document,
                                    roomExecutionData.RoomPlanView,
                                    roomExecutionData.ElevationLines,
                                    roomData,
                                    settings.PlanCornerMarkTypeId,
                                    settings.CornerMarksOnlyCornerNumber,
                                    warnings);

                                if (!roomExecutionData.UsesExistingRoomPlanView && roomForPlanScheme != null)
                                {
                                    placedRoomTagOnPlanScheme = roomPlanRoomTagPlacementService.PlaceRoomTag(
                                        document,
                                        roomExecutionData.RoomPlanView,
                                        roomForPlanScheme,
                                        roomData.LinkInstanceId,
                                        roomToHost,
                                        settings.RoomPlanRoomTagTypeId,
                                        warnings);
                                }
                            }
                            else if (settings.CreateRoomPlanScheme)
                            {
                                warnings.Add(
                                    "Для помещения " + roomData.RoomNumber + " " + roomData.RoomName +
                                    " план-схема не создана. Марки на ней не размещены.");
                            }

                            placedPlanMarksCount += placedPlanMarksOnSourcePlan + placedPlanMarksOnPlanScheme;

                            if (settings.CreateRoomPlanScheme &&
                                !roomExecutionData.UsesExistingRoomPlanView &&
                                placedRoomTagOnPlanScheme == 0 &&
                                settings.RoomPlanRoomTagTypeId != null && settings.RoomPlanRoomTagTypeId != ElementId.InvalidElementId)
                            {
                                warnings.Add(
                                    "Марка помещения " + roomData.RoomNumber + " " + roomData.RoomName +
                                    " на план-схеме не была размещена.");
                            }

                            progressStep++;
                            ReportProgress(
                                progressWindow,
                                progressStep,
                                totalProgressSteps,
                                "Марки и аннотации",
                                BuildRoomProgressDetails(roomData, roomIndex, roomExecutionDataList.Count));
                        }

                        if (placeViewsOnSheet && creationResult.CreatedViews.Count > 0)
                        {
                            List<RoomData> roomDataList = new List<RoomData>();
                            List<IList<ElevationViewData>> roomViewGroups = new List<IList<ElevationViewData>>();
                            List<View> roomPlanViews = new List<View>();

                            for (int roomIndex = 0; roomIndex < roomExecutionDataList.Count; roomIndex++)
                            {
                                RoomExecutionData roomExecutionData = roomExecutionDataList[roomIndex];
                                if (roomExecutionData.CreationResult == null ||
                                    roomExecutionData.CreationResult.CreatedViews.Count == 0)
                                {
                                    continue;
                                }

                                roomDataList.Add(roomExecutionData.SelectionPackage.RoomData);
                                roomViewGroups.Add(roomExecutionData.CreationResult.CreatedViews);
                                roomPlanViews.Add(settings.CreateRoomPlanScheme
                                    ? roomExecutionData.RoomPlanView
                                    : null);
                            }

                            int sheetWarningCount = warnings.Count;
                            if (settings.UseExistingSheet)
                            {
                                targetSheet = document.GetElement(settings.ExistingSheetId) as ViewSheet;
                            }
                            else
                            {
                                targetSheet = sheetCreationService.CreateSheet(
                                    document,
                                    settings,
                                    roomDataList,
                                    namingService,
                                    warnings);
                                targetSheetWasCreated = targetSheet != null;
                            }

                            progressStep++;
                            ReportProgress(
                                progressWindow,
                                progressStep,
                                totalProgressSteps,
                                settings.UseExistingSheet ? "Выбор листа" : "Создание листа",
                                targetSheet != null
                                    ? (settings.UseExistingSheet
                                        ? "Существующий лист выбран."
                                        : "Общий лист создан.")
                                    : (settings.UseExistingSheet
                                        ? "Существующий лист не найден."
                                        : "Общий лист не создан."));

                            if (targetSheet != null)
                            {
                                ViewportPlacementResult placementResult = null;
                                int placementStepsCompleted = 0;
                                // Annotation families cannot be opened while the project is modifiable.
                                // Both project transactions remain in the same undo transaction group.
                                transaction.Commit();
                                Exception framePreparationError = null;
                                try
                                {
                                    if (settings.SheetLayoutSettings.UseAutomaticPlacement || (settings.CreateRoomPlanScheme && settings.PlaceRoomPlanSchemeOnSheet))
                                        viewportPlacementService.PrepareFamilyGeometry(document, targetSheet);
                                }
                                catch (Exception preparationException)
                                {
                                    framePreparationError = preparationException;
                                    SheetLayoutDiagnostics.Write("Frame preparation failed: " + preparationException);
                                }
                                transaction.Start("Разместить развертки на листах");
                                try
                                {
                                    placementResult = viewportPlacementService.Place(
                                        document,
                                        targetSheet,
                                        roomDataList,
                                        roomViewGroups,
                                        roomPlanViews,
                                        settings,
                                        namingService,
                                        targetSheetWasCreated,
                                        warnings,
                                        delegate(
                                            int currentGroup,
                                            int totalGroups,
                                            ViewportPlacementResult currentResult)
                                        {
                                            if (placementStepsCompleted < totalGroups)
                                            {
                                                progressStep++;
                                                placementStepsCompleted++;
                                            }
                                            ReportProgress(
                                                progressWindow,
                                                progressStep,
                                                totalProgressSteps,
                                                "Размещение видов",
                                                "Группа помещений " + currentGroup +
                                                " из " + totalGroups + ".",
                                                "Размещено видов: " + currentResult.PlacedCount);
                                        }, framePreparationError);
                                }
                                catch (Exception placementException)
                                {
                                    warnings.Add(
                                        "Размещение видов на листе завершилось с ошибкой: " +
                                        placementException.Message);
                                }

                                int remainingPlacementSteps = Math.Max(
                                    0,
                                    roomExecutionDataList.Count - placementStepsCompleted);
                                if (remainingPlacementSteps > 0)
                                {
                                    progressStep += remainingPlacementSteps;
                                    ReportProgress(
                                        progressWindow,
                                        progressStep,
                                        totalProgressSteps,
                                        "Размещение видов",
                                        "Оставшиеся группы не требуют размещения.",
                                        "Размещено видов: " +
                                        (placementResult == null ? 0 : placementResult.PlacedCount));
                                }

                                if (placementResult != null)
                                {
                                    placedViewportCount = placementResult.PlacedCount;
                                    placedSheetMarksCount = placementResult.PlacedSheetMarkCount;
                                    placedSheets.AddRange(placementResult.Sheets);
                                    unplacedViewsCount = placementResult.UnplacedViewIds.Count;
                                    forcedPlacementUsed = placementResult.ForcedPlacementUsed;
                                    automaticFallbackUsed = placementResult.AutomaticFallbackUsed;
                                }

                                for (int roomIndex = 0; roomIndex < roomExecutionDataList.Count; roomIndex++)
                                {
                                    progressStep++;
                                    ReportProgress(
                                        progressWindow,
                                        progressStep,
                                        totalProgressSteps,
                                        "Марки на листе",
                                        "Помещение " + (roomIndex + 1) + " из " +
                                        roomExecutionDataList.Count + ".",
                                        "Размещено марок: " + placedSheetMarksCount);
                                }
                            }
                            else
                            {
                                if (warnings.Count == sheetWarningCount)
                                {
                                    warnings.Add(settings.UseExistingSheet
                                        ? "Выбранный существующий лист не найден."
                                        : "Включено создание листа, но лист не был создан.");
                                }

                                progressStep += roomExecutionDataList.Count * 2;
                                ReportProgress(
                                    progressWindow,
                                    progressStep,
                                    totalProgressSteps,
                                    "Лист пропущен",
                                    "Размещение видов и марок пропущено.");
                            }
                        }
                        else if (placeViewsOnSheet)
                        {
                            progressStep += roomExecutionDataList.Count * 2 + 1;
                            ReportProgress(
                                progressWindow,
                                progressStep,
                                totalProgressSteps,
                                "Лист пропущен",
                                "Нет созданных разверток для размещения.");
                        }

                        transaction.Commit();
                    }

                    transactionGroup.Assimilate();
                    progressStep++;
                    ReportProgress(
                        progressWindow,
                        progressStep,
                        totalProgressSteps,
                        "Готово",
                        settings.CreateRoomPlanScheme
                            ? (settings.UseExistingSheet
                                ? "Развертки созданы, марки добавлены на выбранную план-схему."
                                : "Развертки и план-схемы созданы.")
                            : "Развертки созданы.");
                }

                CloseProgressWindow(progressWindow);
                progressWindow = null;

                if (settings.OpenCreatedSheet && targetSheet != null)
                {
                    ViewSheet sheetToOpen = placedSheets.Count > 0 ? placedSheets[0] : targetSheet;
                    try
                    {
                        if (uiDocument.ActiveView == null ||
                            !RevitElementIdUtils.AreEqual(uiDocument.ActiveView.Id, sheetToOpen.Id))
                        {
                            uiDocument.ActiveView = sheetToOpen;
                        }
                    }
                    catch (Exception openSheetException)
                    {
                        warnings.Add("Лист обработан, но открыть его автоматически не удалось: " + openSheetException.Message);
                    }
                }

                if (manualBoundaryRequired)
                {
                    ToastNotifier.ShowWarning(
                        "SAB Развертки",
                        "Границу вида создать не удалось. На активном виде созданы вспомогательные линии для ручной правки.",
                        15);
                }

                reportService.ShowFinalReport(
                    selectedLinesCount,
                    creationResult,
                    targetSheet,
                    targetSheetWasCreated,
                    placeViewsOnSheet,
                    placedViewportCount,
                    placedPlanMarksCount,
                    placedSheetMarksCount,
                    warnings,
                    placedSheets,
                    unplacedViewsCount,
                    forcedPlacementUsed,
                    automaticFallbackUsed,
                    settings.SheetLayoutSettings.ColumnsCount);

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
            finally
            {
                CloseProgressWindow(progressWindow);
            }
        }

        private CreateViewsAndSheetsProgressWindow ShowCreationProgressWindow(
            UIApplication uiApplication,
            int totalSteps)
        {
            CreateViewsAndSheetsProgressWindow progressWindow = null;
            try
            {
                progressWindow = new CreateViewsAndSheetsProgressWindow(
                    BuildCreationProgressMessages(),
                    "SAB Развертки",
                    "Создание разверток");
                if (uiApplication != null && uiApplication.MainWindowHandle != IntPtr.Zero)
                {
                    new WindowInteropHelper(progressWindow).Owner = uiApplication.MainWindowHandle;
                }

                progressWindow.Show();
                ReportProgress(
                    progressWindow,
                    0,
                    Math.Max(1, totalSteps),
                    "Подготовка",
                    "Запуск создания разверток.");
                return progressWindow;
            }
            catch (Exception exception)
            {
                CloseProgressWindow(progressWindow);
                ToastNotifier.ShowWarning(
                    "SAB Развертки",
                    "Не удалось открыть окно прогресса. Создание будет продолжено: " + exception.Message);
                return null;
            }
        }

        private IList<string> BuildCreationProgressMessages()
        {
            return new List<string>
            {
                "Строим развертки по выбранным линиям.",
                "Расставляем марки и при необходимости собираем план-схемы.",
                "Размещаем виды на листе по группам помещений."
            };
        }

        private string BuildRoomProgressDetails(
            RoomData roomData,
            int roomIndex,
            int totalRooms)
        {
            string roomIdentity = roomData != null
                ? (roomData.RoomNumber + " " + roomData.RoomName).Trim()
                : string.Empty;
            string suffix = string.IsNullOrWhiteSpace(roomIdentity)
                ? string.Empty
                : ": " + roomIdentity;
            return "Помещение " + (roomIndex + 1) + " из " + totalRooms + suffix + ".";
        }

        private void ReportProgress(
            CreateViewsAndSheetsProgressWindow progressWindow,
            int currentStep,
            int totalSteps,
            string stage,
            string details,
            string counters = null)
        {
            if (progressWindow == null)
            {
                return;
            }

            progressWindow.Report(new CreateViewsAndSheetsProgressInfo
            {
                CurrentStep = currentStep,
                TotalSteps = Math.Max(1, totalSteps),
                ProcessedItems = currentStep,
                TotalItems = totalSteps,
                Stage = stage,
                Details = details,
                Counters = counters
            });
        }

        private void CloseProgressWindow(CreateViewsAndSheetsProgressWindow progressWindow)
        {
            if (progressWindow == null)
            {
                return;
            }

            try
            {
                progressWindow.AllowCloseAndClose();
            }
            catch
            {
                // Окно прогресса не должно влиять на результат команды.
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

        private ViewSheet FindSheetContainingView(Document document, ElementId viewId)
        {
            if (document == null || viewId == null || RevitElementIdUtils.AreEqual(viewId, ElementId.InvalidElementId))
            {
                return null;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document).OfClass(typeof(Viewport));
            foreach (Element element in collector)
            {
                Viewport viewport = element as Viewport;
                if (viewport == null || !RevitElementIdUtils.AreEqual(viewport.ViewId, viewId))
                {
                    continue;
                }

                return document.GetElement(viewport.SheetId) as ViewSheet;
            }

            return null;
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

        private void PersistCurrentWindowSettings(
            ElevationSettingsViewModel settingsViewModel,
            ElevationSettingsStorageService settingsStorageService,
            IList<string> warnings)
        {
            if (settingsViewModel == null || settingsStorageService == null)
            {
                return;
            }

            try
            {
                settingsStorageService.SaveMarkPreferences(settingsViewModel.CaptureMarkPreferences());
                ElevationSettings currentSettings;
                string validationMessage;
                if (settingsViewModel.TryBuildSettings(
                        out currentSettings,
                        out validationMessage,
                        false))
                {
                    settingsStorageService.SaveSettings(currentSettings);
                    return;
                }

                // Если пользователь закрыл окно с незаполненным обязательным полем,
                // сохраняем независимые переключатели поверх последней валидной сессии.
                ElevationSettings savedSettings = settingsStorageService.LoadSettings();
                if (savedSettings == null)
                {
                    return;
                }

                savedSettings.MultipleRoomsOnSheet = settingsViewModel.IsMultipleRoomsOnSheet;
                savedSettings.PickRoomFromLink = settingsViewModel.PickRoomFromLink;
                savedSettings.EnableRoomObjectCategory = settingsViewModel.EnableRoomObjectCategory;
                savedSettings.CornerMarksOnlyCornerNumber = settingsViewModel.CornerMarksOnlyCornerNumber;
                savedSettings.SheetCornerMarksBelowView = settingsViewModel.SheetCornerMarksBelowView;
                savedSettings.CreateRoomPlanScheme = settingsViewModel.CreateRoomPlanScheme;
                savedSettings.PlaceRoomPlanSchemeOnSheet = settingsViewModel.CreateRoomPlanScheme &&
                    settingsViewModel.PlaceViewsOnSheet && settingsViewModel.PlaceRoomPlanSchemeOnSheet;
                savedSettings.SheetLayoutSettings.UseAutomaticPlacement = settingsViewModel.UseAutomaticSheetPlacement;
                int savedColumns;
                if (int.TryParse(settingsViewModel.ColumnsCountText, out savedColumns) && savedColumns > 0)
                    savedSettings.SheetLayoutSettings.ColumnsCount = savedColumns;
                savedSettings.PlanCornerMarkTypeId = settingsViewModel.SelectedPlanCornerMarkType != null
                    ? settingsViewModel.SelectedPlanCornerMarkType.Id
                    : savedSettings.PlanCornerMarkTypeId;
                savedSettings.SheetCornerMarkTypeId = settingsViewModel.SelectedSheetCornerMarkType != null
                    ? settingsViewModel.SelectedSheetCornerMarkType.Id
                    : savedSettings.SheetCornerMarkTypeId;
                settingsStorageService.SaveSettings(savedSettings);
            }
            catch (Exception exception)
            {
                if (warnings != null)
                {
                    warnings.Add("Не удалось сохранить текущие настройки окна: " + exception.Message);
                }
            }
        }

        private void TryPickRequiredExistingSheetPlacement(
            UIDocument uiDocument,
            View returnView,
            ElevationSettingsViewModel settingsViewModel,
            SheetPointSelectionService sheetPointSelectionService,
            IList<string> warnings,
            ref string windowInfoText)
        {
            if (settingsViewModel == null ||
                !settingsViewModel.UseExistingSheet ||
                !settingsViewModel.CreateRoomPlanScheme ||
                settingsViewModel.SelectedExistingSheet == null ||
                settingsViewModel.HasExistingRoomPlanSelection)
            {
                return;
            }

            ElevationSettings sheetPointSettings;
            string validationMessage;
            if (!settingsViewModel.TryBuildSettings(
                    out sheetPointSettings,
                    out validationMessage,
                    false))
            {
                windowInfoText = validationMessage;
                ToastNotifier.ShowWarning("SAB Развертки", validationMessage);
                return;
            }

            PickSheetPlacement(
                uiDocument,
                returnView,
                sheetPointSettings,
                settingsViewModel,
                sheetPointSelectionService,
                warnings,
                ref windowInfoText);
        }

        private void PickSheetPlacement(
            UIDocument uiDocument,
            View returnView,
            ElevationSettings sheetPointSettings,
            ElevationSettingsViewModel settingsViewModel,
            SheetPointSelectionService sheetPointSelectionService,
            IList<string> warnings,
            ref string windowInfoText)
        {
            XYZ pickedSheetPoint;
            ViewPlan pickedExistingRoomPlanView;
            bool pointSelectionCancelled;
            List<string> pointSelectionWarnings = new List<string>();
            bool pointPicked = sheetPointSelectionService.TryPickStartPointOnSheet(
                uiDocument,
                returnView,
                sheetPointSettings,
                pointSelectionWarnings,
                out pickedSheetPoint,
                out pickedExistingRoomPlanView,
                out pointSelectionCancelled);

            AppendWarnings(warnings, pointSelectionWarnings);
            if (pointSelectionWarnings.Count > 0)
            {
                windowInfoText = BuildWindowInfoText(pointSelectionWarnings);
            }

            if (pointPicked)
            {
                settingsViewModel.SetSheetStartPointFromRevitPoint(pickedSheetPoint);
                if (pickedExistingRoomPlanView != null)
                {
                    settingsViewModel.SetExistingRoomPlanViewSelection(pickedExistingRoomPlanView);
                    windowInfoText =
                        "Точка размещения и план-схема '" + pickedExistingRoomPlanView.Name + "' выбраны.";
                    ToastNotifier.ShowInfo("SAB Развертки", windowInfoText);
                }
                else if (sheetPointSettings.UseExistingSheet && sheetPointSettings.CreateRoomPlanScheme)
                {
                    windowInfoText = pointSelectionWarnings.Count > 0
                        ? pointSelectionWarnings[pointSelectionWarnings.Count - 1]
                        : "Точка сохранена, но план-схема не выбрана.";
                    ToastNotifier.ShowWarning("SAB Развертки", windowInfoText);
                }
                else
                {
                    windowInfoText = "Координата на листе выбрана и записана в поля Старт X/Y.";
                    ToastNotifier.ShowInfo("SAB Развертки", windowInfoText);
                }

                return;
            }

            if (pointSelectionCancelled)
            {
                windowInfoText = "Выбор точки на листе отменен. Линии и помещение сохранены.";
                return;
            }

            string warningText = pointSelectionWarnings.Count > 0
                ? pointSelectionWarnings[pointSelectionWarnings.Count - 1]
                : "Координата на листе не была выбрана.";

            windowInfoText = warningText;
            ToastNotifier.ShowWarning("SAB Развертки", warningText);
        }

        private void MergeCreationResults(
            ElevationViewCreationResult target,
            ElevationViewCreationResult source)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (int index = 0; index < source.CreatedViews.Count; index++)
            {
                target.CreatedViews.Add(source.CreatedViews[index]);
            }

            for (int index = 0; index < source.FailedViews.Count; index++)
            {
                target.FailedViews.Add(source.FailedViews[index]);
            }
        }

        private string GetDefaultWindowInfoText()
        {
            return "Перед созданием нажмите Выбрать линии, затем укажите линии детализации и помещение в активном плане.";
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
            bool pickRoomFromLink,
            IList<string> warnings,
            out MultiRoomSelectionItem selectionPackage)
        {
            selectionPackage = null;

            LineGroupSelectionMode lineGroupSelectionMode;
            if (!TrySelectLineGroupSelectionMode(out lineGroupSelectionMode))
            {
                return false;
            }

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
            if (!roomDetectionService.TryPickRoomData(uiDocument, pickRoomFromLink, out roomData, out roomSelectionError))
            {
                if (!string.IsNullOrWhiteSpace(roomSelectionError))
                {
                    ToastNotifier.ShowWarning("SAB Развертки", roomSelectionError);
                }

                return false;
            }

            if (!roomData.IsOnHostLevel(uiDocument.Document, activePlanLevelId))
            {
                ToastNotifier.ShowWarning(
                    "SAB Развертки",
                    "Выбранное помещение находится на другом уровне. Выберите помещение на уровне активного плана.");
                return false;
            }

            selectionPackage = new MultiRoomSelectionItem();
            selectionPackage.ApplySelection(lineGroups, selectedLines, roomData);
            return true;
        }

        private bool TryEditMultiRoomSelectionList(
            UIDocument uiDocument,
            View activeView,
            ElementId activePlanLevelId,
            bool pickRoomFromLink,
            ObservableCollection<MultiRoomSelectionItem> selectionPackages,
            IList<string> warnings)
        {
            if (selectionPackages == null)
            {
                return false;
            }

            while (true)
            {
                MultiRoomSelectionWindow selectionWindow = new MultiRoomSelectionWindow(selectionPackages);
                bool? dialogResult = selectionWindow.ShowDialog();
                if (!dialogResult.HasValue || !dialogResult.Value)
                {
                    return false;
                }

                if (selectionWindow.RequestedAction == MultiRoomSelectionWindowAction.Transfer)
                {
                    return true;
                }

                if (selectionWindow.RequestedAction != MultiRoomSelectionWindowAction.PickSelection ||
                    selectionWindow.RequestedRow == null)
                {
                    continue;
                }

                MultiRoomSelectionItem pickedSelectionPackage;
                List<string> selectionWarnings = new List<string>();
                bool selectionPicked = TryPickLineGroupsAndRoom(
                    uiDocument,
                    activeView,
                    activePlanLevelId,
                    pickRoomFromLink,
                    selectionWarnings,
                    out pickedSelectionPackage);

                AppendWarnings(warnings, selectionWarnings);
                if (selectionPicked && pickedSelectionPackage != null)
                {
                    selectionWindow.RequestedRow.ApplySelection(
                        pickedSelectionPackage.LineGroups,
                        pickedSelectionPackage.SelectedLines,
                        pickedSelectionPackage.RoomData);
                }
            }
        }

        private bool HasCompletedSelections(IList<MultiRoomSelectionItem> selectionPackages)
        {
            if (selectionPackages == null || selectionPackages.Count == 0)
            {
                return false;
            }

            for (int index = 0; index < selectionPackages.Count; index++)
            {
                if (selectionPackages[index] == null || !selectionPackages[index].IsCompleted)
                {
                    return false;
                }
            }

            return true;
        }

        private string BuildSelectionStatusText(IList<MultiRoomSelectionItem> selectionPackages)
        {
            if (selectionPackages == null || selectionPackages.Count == 0)
            {
                return "Линии и помещение не выбраны.";
            }

            int groupCount = 0;
            int lineCount = 0;
            List<string> roomNames = new List<string>();
            for (int index = 0; index < selectionPackages.Count; index++)
            {
                MultiRoomSelectionItem selectionPackage = selectionPackages[index];
                if (selectionPackage == null || selectionPackage.RoomData == null)
                {
                    continue;
                }

                groupCount += selectionPackage.LineGroups != null ? selectionPackage.LineGroups.Count : 0;
                lineCount += selectionPackage.SelectedLines != null ? selectionPackage.SelectedLines.Count : 0;
                roomNames.Add(selectionPackage.RoomData.RoomNumber + " " + selectionPackage.RoomData.RoomName);
            }

            return "Выбрано помещений: " + roomNames.Count +
                   ". Линий: " + lineCount +
                   ", контуров: " + groupCount +
                   ". Помещения: " + string.Join(", ", roomNames.ToArray()) + ".";
        }

        private void UpdateNamingPreviewContexts(
            ElevationSettingsViewModel viewModel,
            IList<MultiRoomSelectionItem> selectionPackages)
        {
            if (viewModel == null)
            {
                return;
            }

            List<ElevationNamingPreviewContext> contexts = new List<ElevationNamingPreviewContext>();
            ElevationGeometryService geometryService = new ElevationGeometryService();
            List<string> previewWarnings = new List<string>();

            if (selectionPackages != null)
            {
                for (int selectionIndex = 0; selectionIndex < selectionPackages.Count; selectionIndex++)
                {
                    MultiRoomSelectionItem selectionPackage = selectionPackages[selectionIndex];
                    if (selectionPackage == null || !selectionPackage.IsCompleted || selectionPackage.RoomData == null)
                    {
                        continue;
                    }

                    List<ElevationLineData> elevationLines = BuildElevationLinesWithGlobalCornerIndexing(
                        selectionPackage.LineGroups,
                        geometryService,
                        previewWarnings);

                    for (int lineIndex = 0; lineIndex < elevationLines.Count; lineIndex++)
                    {
                        ElevationLineData lineData = elevationLines[lineIndex];
                        if (lineData == null)
                        {
                            continue;
                        }

                        ElevationNamingPreviewContext context = new ElevationNamingPreviewContext();
                        context.RoomData = selectionPackage.RoomData;
                        context.StartPointNumber = lineData.Index;
                        context.EndPointNumber = lineData.EndIndex > 0
                            ? lineData.EndIndex
                            : lineData.Index + 1;
                        contexts.Add(context);
                    }
                }
            }

            viewModel.SetNamingPreviewContexts(contexts);
        }

        private bool TrySelectLineGroupSelectionMode(out LineGroupSelectionMode mode)
        {
            mode = LineGroupSelectionMode.Cancelled;

            LineGroupSelectionWindow selectionWindow = new LineGroupSelectionWindow();
            bool? dialogResult = selectionWindow.ShowDialog();
            if (!dialogResult.HasValue || !dialogResult.Value)
            {
                return false;
            }

            mode = selectionWindow.IsMultipleGroups
                ? LineGroupSelectionMode.MultipleGroups
                : LineGroupSelectionMode.SingleGroup;
            return true;
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
                ToastNotifier.ShowInfo(
                    "SAB Развертки",
                    "Выбирайте линии одной группы по очереди. Esc завершает выбор.");
                DetailLineSelectionResult singleGroupResult = selectionService.PickDetailLines(
                    uiDocument,
                    activeView,
                    "Выбирайте линии одной группы по очереди");

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
                    "Выберите линии группы №" + groupNumber +
                    " по очереди. Esc завершает группу.",
                    8);

                DetailLineSelectionResult groupResult = selectionService.PickDetailLines(
                    uiDocument,
                    activeView,
                    "Выберите линии группы №" + groupNumber +
                    " по очереди");

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

                    // Даже если две параллельные линии являются частями одной стены,
                    // их общая промежуточная точка сохраняет собственный номер угла.
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

            if (string.IsNullOrWhiteSpace(settings.ElevationNamePart1) &&
                string.IsNullOrWhiteSpace(settings.ElevationNamePart2) &&
                string.IsNullOrWhiteSpace(settings.ElevationNamePart3))
            {
                validationMessage = "Формула имени развертки не может быть пустой.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.ElevationTitlePart1) &&
                string.IsNullOrWhiteSpace(settings.ElevationTitlePart2) &&
                string.IsNullOrWhiteSpace(settings.ElevationTitlePart3))
            {
                validationMessage = "Формула заголовка на листе не может быть пустой.";
                return false;
            }

            if (settings.CreateRoomPlanScheme && !settings.UseExistingSheet)
            {
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

            if (settings.CreateSheet && settings.UseExistingSheet)
            {
                validationMessage = "Нельзя одновременно создать новый лист и использовать существующий.";
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

                if (string.IsNullOrWhiteSpace(settings.SheetNamePart1) &&
                    string.IsNullOrWhiteSpace(settings.SheetNamePart2) &&
                    string.IsNullOrWhiteSpace(settings.SheetNamePart3))
                {
                    validationMessage = "Формула имени листа не может быть пустой.";
                    return false;
                }

            }

            if (settings.UseExistingSheet)
            {
                ViewSheet existingSheet = document.GetElement(settings.ExistingSheetId) as ViewSheet;
                if (existingSheet == null || existingSheet.IsPlaceholder)
                {
                    validationMessage = "Выбранный существующий лист не найден или является листом-заполнителем.";
                    return false;
                }

                if (settings.CreateRoomPlanScheme)
                {
                    ViewPlan existingRoomPlanView = document.GetElement(settings.ExistingRoomPlanViewId) as ViewPlan;
                    if (existingRoomPlanView == null || existingRoomPlanView.IsTemplate)
                    {
                        validationMessage = "Выберите существующую план-схему на целевом листе.";
                        return false;
                    }

                    bool planViewIsPlacedOnSheet = false;
                    ICollection<ElementId> viewportIds = existingSheet.GetAllViewports();
                    foreach (ElementId viewportId in viewportIds)
                    {
                        Viewport viewport = document.GetElement(viewportId) as Viewport;
                        if (viewport != null && RevitElementIdUtils.AreEqual(viewport.ViewId, existingRoomPlanView.Id))
                        {
                            planViewIsPlacedOnSheet = true;
                            break;
                        }
                    }

                    if (!planViewIsPlacedOnSheet)
                    {
                        validationMessage = "Выбранная план-схема не размещена на целевом листе.";
                        return false;
                    }
                }
            }

            if (settings.CreateSheet || settings.UseExistingSheet)
            {
                if (settings.ViewportTypeId != null &&
                    RevitElementIdUtils.GetElementIdValue(settings.ViewportTypeId) >= 0 &&
                    (document.GetElement(settings.ViewportTypeId) as ElementType) == null)
                {
                    validationMessage = "Выбранный тип заголовка развертки не существует.";
                    return false;
                }

                FamilySymbol sheetMarkType = document.GetElement(settings.SheetCornerMarkTypeId) as FamilySymbol;
                if (sheetMarkType == null)
                {
                    validationMessage = "Не выбран тип марки угла на листе.";
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

            if (RevitElementIdUtils.AreEqual(sourcePlanView.Id, targetPlanSchemeView.Id))
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
