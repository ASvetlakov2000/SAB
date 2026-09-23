using System;
using System.Collections.Generic;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using SAB.CreateViewsAndSheets.Models;
using SAB.CreateViewsAndSheets.Views;
using SAB.DoorWindowExplanations.Models;
using SAB.DoorWindowExplanations.Services;
using SAB.DoorWindowExplanations.Services.Reports;
using SAB.DoorWindowExplanations.Views;

namespace SAB.DoorWindowExplanations.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreateDoorWindowViewsCommand : IExternalCommand
    {
        private const string CommandTitle = "SAB Экспликации дверей и окон";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            CreateViewsAndSheetsProgressWindow progressWindow = null;
            try
            {
                UIApplication uiApplication = commandData != null ? commandData.Application : null;
                UIDocument uiDocument = uiApplication != null ? uiApplication.ActiveUIDocument : null;
                Document document = uiDocument != null ? uiDocument.Document : null;
                if (uiDocument == null || document == null)
                {
                    message = "Не удалось получить активный документ Revit.";
                    ToastNotifier.ShowError(CommandTitle, message);
                    return Result.Failed;
                }

                DoorWindowSelectionService selectionService = new DoorWindowSelectionService();
                List<DoorWindowSelectionData> selections = new List<DoorWindowSelectionData>();
                AddUniqueSelections(selections, selectionService.GetPreselectedDoorsAndWindows(uiDocument));
                if (!CollectSelectionList(uiApplication, uiDocument, selectionService, selections))
                {
                    return Result.Cancelled;
                }

                DoorWindowRevitDataService dataService = new DoorWindowRevitDataService();
                IList<DoorWindowViewTemplateItem> templates = dataService.GetSectionViewTemplates(document);
                IList<DoorWindowNamedElementItem> titleBlockTypes = dataService.GetTitleBlockTypes(document);
                IList<DoorWindowNamedElementItem> viewportTypes = dataService.GetViewportTypes(document);
                dataService.GetSectionViewFamilyTypeId(document);
                if (titleBlockTypes.Count == 0)
                {
                    ToastNotifier.ShowWarning(CommandTitle, "В проекте не найден ни один тип основной надписи.");
                    return Result.Cancelled;
                }

                DoorWindowParameterService parameterService = new DoorWindowParameterService();
                IList<string> parameterNames = CollectParameterNames(parameterService, selections);
                DoorWindowSettingsStorageService storageService = new DoorWindowSettingsStorageService();
                DoorWindowViewSettings savedSettings = storageService.Load();

                DoorWindowViewsWindow settingsWindow = new DoorWindowViewsWindow(
                    selections,
                    savedSettings,
                    templates,
                    parameterNames,
                    titleBlockTypes,
                    viewportTypes);
                SetRevitOwner(settingsWindow, uiApplication);

                bool? dialogResult = settingsWindow.ShowDialog();
                if (dialogResult != true || settingsWindow.SelectedSettings == null)
                {
                    return Result.Cancelled;
                }

                DoorWindowViewSettings settings = settingsWindow.SelectedSettings;
                IList<DoorWindowOrientedBounds> bounds = PrepareBounds(
                    uiDocument,
                    selections,
                    settings);

                DoorWindowBatchCreationResult batchResult = new DoorWindowBatchCreationResult();
                HashSet<string> reservedViewNames = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
                int totalProgressSteps = selections.Count + 2;
                progressWindow = ShowCreationProgressWindow(uiApplication, totalProgressSteps);
                ReportProgress(
                    progressWindow,
                    0,
                    totalProgressSteps,
                    "Подготовка",
                    "Готовим имена и геометрию видов.");
                using (Transaction transaction = new Transaction(document, "SAB Экспликации дверей и окон"))
                {
                    transaction.Start();
                    DoorWindowNamingService namingService = new DoorWindowNamingService();
                    DoorWindowViewCreationService viewCreationService = new DoorWindowViewCreationService();

                    for (int i = 0; i < selections.Count; i++)
                    {
                        IList<string> viewNames = namingService.BuildUniqueNames(
                            document,
                            selections[i],
                            settings,
                            reservedViewNames);
                        DoorWindowViewCreationResult group = viewCreationService.CreateViews(
                            document,
                            bounds[i],
                            settings,
                            templates,
                            viewNames);
                        batchResult.ViewGroups.Add(group);
                        ReportProgress(
                            progressWindow,
                            i + 1,
                            totalProgressSteps,
                            "Создание видов",
                            "Элемент " + (i + 1) + " из " + selections.Count + ".");
                    }

                    DoorWindowSheetPlacementService sheetPlacementService = new DoorWindowSheetPlacementService();
                    batchResult.Sheet = sheetPlacementService.CreateSheetAndPlaceViews(
                        document,
                        settings,
                        titleBlockTypes,
                        viewportTypes,
                        batchResult.ViewGroups);
                    ReportProgress(
                        progressWindow,
                        selections.Count + 1,
                        totalProgressSteps,
                        "Размещение",
                        "Виды размещены на общем листе.");
                    transaction.Commit();
                    ReportProgress(
                        progressWindow,
                        totalProgressSteps,
                        totalProgressSteps,
                        "Готово",
                        "Изменения сохранены.");
                }

                CloseProgressWindow(progressWindow);
                progressWindow = null;

                if (settings.SaveSettings)
                {
                    try
                    {
                        storageService.Save(settings);
                    }
                    catch (Exception settingsException)
                    {
                        ToastNotifier.ShowWarning(
                            CommandTitle,
                            "Виды и лист созданы, но настройки не сохранены: " + settingsException.Message);
                    }
                }

                DoorWindowCreationReportService reportService = new DoorWindowCreationReportService();
                reportService.ShowFinalReport(selections.Count, batchResult);

                if (batchResult.Sheet != null)
                {
                    try
                    {
                        uiDocument.ActiveView = batchResult.Sheet;
                    }
                    catch
                    {
                        // Невозможность открыть лист не отменяет уже созданные элементы.
                    }
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ToastNotifier.ShowError(
                    CommandTitle,
                    "Не удалось создать виды и лист.\n\n" + exception.Message,
                    15);
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
                    CommandTitle,
                    "Создание экспликаций дверей и окон");
                SetRevitOwner(progressWindow, uiApplication);
                progressWindow.Show();
                ReportProgress(
                    progressWindow,
                    0,
                    Math.Max(1, totalSteps),
                    "Подготовка",
                    "Запуск создания видов.");
                return progressWindow;
            }
            catch (Exception exception)
            {
                CloseProgressWindow(progressWindow);
                ToastNotifier.ShowWarning(
                    CommandTitle,
                    "Не удалось открыть окно прогресса. Создание будет продолжено: " + exception.Message);
                return null;
            }
        }

        private IList<string> BuildCreationProgressMessages()
        {
            return new List<string>
            {
                "Создаём вид сверху, фасад и разрез для каждого элемента.",
                "Выравниваем виды и готовим компоновку листа.",
                "Размещаем заголовки и проверяем границы видов."
            };
        }

        private void ReportProgress(
            CreateViewsAndSheetsProgressWindow progressWindow,
            int currentStep,
            int totalSteps,
            string stage,
            string details)
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
                Details = details
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

        private bool CollectSelectionList(
            UIApplication uiApplication,
            UIDocument uiDocument,
            DoorWindowSelectionService selectionService,
            IList<DoorWindowSelectionData> selections)
        {
            while (true)
            {
                DoorWindowSelectionListWindow window = new DoorWindowSelectionListWindow(selections);
                SetRevitOwner(window, uiApplication);
                window.ShowDialog();

                if (window.RequestedAction == DoorWindowSelectionListAction.Cancel)
                {
                    return false;
                }

                if (window.RequestedAction == DoorWindowSelectionListAction.Continue)
                {
                    return selections.Count > 0;
                }

                if (window.RequestedAction == DoorWindowSelectionListAction.AddElements)
                {
                    try
                    {
                        IList<DoorWindowSelectionData> picked = selectionService.PickDoorsAndWindows(uiDocument);
                        AddUniqueSelections(selections, picked);
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        // Escape при выборе возвращает пользователя в окно накопителя.
                    }
                }
            }
        }

        private IList<DoorWindowOrientedBounds> PrepareBounds(
            UIDocument uiDocument,
            IList<DoorWindowSelectionData> selections,
            DoorWindowViewSettings settings)
        {
            List<DoorWindowOrientedBounds> result = new List<DoorWindowOrientedBounds>();
            DoorWindowGeometryService geometryService = new DoorWindowGeometryService();
            double manualDepth = UnitUtils.ConvertToInternalUnits(
                settings.ManualPlanDepthMm,
                UnitTypeId.Millimeters);

            for (int i = 0; i < selections.Count; i++)
            {
                DoorWindowSelectionData selection = selections[i];
                if (settings.BoundsSourceMode == DoorWindowBoundsSourceMode.ManualFrontContour)
                {
                    ToastNotifier.ShowInfo(
                        CommandTitle,
                        "Элемент " + (i + 1) + " из " + selections.Count +
                        ": выберите линии его контура на активной развертке.");
                    DoorWindowContourSelectionService contourSelectionService = new DoorWindowContourSelectionService();
                    IList<CurveElement> contourLines = contourSelectionService.PickContourLines(uiDocument);
                    result.Add(geometryService.GetManualBounds(selection, contourLines, manualDepth));
                    continue;
                }

                try
                {
                    result.Add(geometryService.GetAutomaticBounds(selection));
                }
                catch (InvalidOperationException exception)
                {
                    string suffix = selection.IsLinked
                        ? " Для таких элементов используйте режим габаритов по линиям контура."
                        : string.Empty;
                    throw new InvalidOperationException(
                        "Не удалось определить габариты элемента " + (i + 1) + ": " +
                        exception.Message + suffix,
                        exception);
                }
            }

            return result;
        }

        private IList<string> CollectParameterNames(
            DoorWindowParameterService parameterService,
            IList<DoorWindowSelectionData> selections)
        {
            SortedSet<string> names = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
            for (int i = 0; i < selections.Count; i++)
            {
                IList<string> itemNames = parameterService.GetAvailableParameterNames(selections[i]);
                for (int j = 0; j < itemNames.Count; j++)
                {
                    names.Add(itemNames[j]);
                }
            }

            return new List<string>(names);
        }

        private void AddUniqueSelections(
            IList<DoorWindowSelectionData> target,
            IList<DoorWindowSelectionData> source)
        {
            if (source == null)
            {
                return;
            }

            HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < target.Count; i++)
            {
                if (target[i] != null)
                {
                    keys.Add(target[i].SelectionKey);
                }
            }

            for (int i = 0; i < source.Count; i++)
            {
                DoorWindowSelectionData item = source[i];
                if (item != null && keys.Add(item.SelectionKey))
                {
                    target.Add(item);
                }
            }
        }

        private void SetRevitOwner(System.Windows.Window window, UIApplication uiApplication)
        {
            if (window != null && uiApplication != null && uiApplication.MainWindowHandle != IntPtr.Zero)
            {
                new WindowInteropHelper(window).Owner = uiApplication.MainWindowHandle;
            }
        }
    }
}
