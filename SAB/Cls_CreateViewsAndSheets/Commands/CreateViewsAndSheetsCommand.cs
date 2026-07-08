using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using SAB.CreateViewsAndSheets.Models;
using SAB.CreateViewsAndSheets.Services;
using SAB.CreateViewsAndSheets.ViewModels;
using SAB.CreateViewsAndSheets.Views;
using SAB.InteriorElevations.Utils;

namespace SAB.CreateViewsAndSheets.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreateViewsAndSheetsCommand : IExternalCommand
    {
        private const string CommandTitle = "SAB Создание видов и листов";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string debugStep = "Старт команды";
            Stopwatch stopwatch = Stopwatch.StartNew();
            CreateViewsAndSheetsProgressWindow openingProgressWindow = null;

            try
            {
                debugStep = "Получение активного документа";
                UIApplication uiApplication = commandData != null ? commandData.Application : null;
                UIDocument uiDocument = uiApplication != null ? uiApplication.ActiveUIDocument : null;
                if (uiDocument == null)
                {
                    message = "Не удалось получить активный UI-документ Revit.";
                    TaskDialog.Show(CommandTitle, message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;
                if (document == null)
                {
                    message = "Не удалось получить активный документ Revit.";
                    TaskDialog.Show(CommandTitle, message);
                    return Result.Failed;
                }

                if (document.ActiveView == null)
                {
                    message = "Активный вид Revit недоступен.";
                    TaskDialog.Show(CommandTitle, message);
                    return Result.Failed;
                }

                debugStep = "Открытие окна прогресса загрузки";
                openingProgressWindow = ShowOpeningProgressWindow();

                List<string> warnings = new List<string>();
                RevitDataService dataService = new RevitDataService();

                debugStep = "Сбор данных для окна создания";
                ReportProgressWindow(
                    openingProgressWindow,
                    1,
                    11,
                    "Сбор данных",
                    "Ищем виды, которые можно дублировать.");
                List<RevitElementItem> sourceViews = dataService.GetDuplicatableViews(document);

                ReportProgressWindow(
                    openingProgressWindow,
                    2,
                    11,
                    "Сбор данных",
                    "Ищем листы-образцы.");
                List<RevitElementItem> sourceSheets = dataService.GetSheets(document);

                ReportProgressWindow(
                    openingProgressWindow,
                    3,
                    11,
                    "Сбор данных",
                    "Загружаем типы видовых экранов.");
                List<RevitElementItem> viewportTypes = dataService.GetViewportTypes(document);

                ReportProgressWindow(
                    openingProgressWindow,
                    4,
                    11,
                    "Сбор данных",
                    "Загружаем типы основных надписей.");
                List<RevitElementItem> titleBlockTypes = dataService.GetTitleBlockTypes(document);

                ReportProgressWindow(
                    openingProgressWindow,
                    5,
                    11,
                    "Сбор данных",
                    "Читаем параметры диспетчера проекта.");
                List<RevitElementItem> sheetBrowserParameters = dataService.GetSheetBrowserParameters(document);

                ReportProgressWindow(
                    openingProgressWindow,
                    6,
                    11,
                    "Сбор данных",
                    "Собираем значения параметров листов.");
                Dictionary<long, List<string>> sheetBrowserParameterValuesById = dataService.GetSheetBrowserParameterValues(document, sheetBrowserParameters);

                ReportProgressWindow(
                    openingProgressWindow,
                    7,
                    11,
                    "Сбор данных",
                    "Загружаем шаблоны видов.");
                List<RevitElementItem> viewTemplates = dataService.GetViewTemplates(document);

                ReportProgressWindow(
                    openingProgressWindow,
                    8,
                    11,
                    "Проверка",
                    "Проверяем, хватает ли данных для открытия окна.");
                string startupValidationMessage;
                if (!ValidateStartupData(sourceViews, sourceSheets, viewportTypes, titleBlockTypes, viewTemplates, out startupValidationMessage))
                {
                    CloseProgressWindow(openingProgressWindow);
                    openingProgressWindow = null;
                    TaskDialog.Show(CommandTitle, startupValidationMessage);
                    return Result.Cancelled;
                }

                ReportProgressWindow(
                    openingProgressWindow,
                    9,
                    11,
                    "Настройки",
                    "Загружаем сохраненные настройки команды.");
                SettingsService settingsService = new SettingsService();
                CreateViewsAndSheetsSettings savedSettings = settingsService.LoadSettings(document, warnings);

                debugStep = "Создание модели окна";
                ReportProgressWindow(
                    openingProgressWindow,
                    10,
                    11,
                    "Подготовка окна",
                    "Собираем строки, списки и проверки для интерфейса.");
                CreateViewsAndSheetsViewModel viewModel = new CreateViewsAndSheetsViewModel(
                    sourceViews,
                    sourceSheets,
                    viewportTypes,
                    titleBlockTypes,
                    sheetBrowserParameters,
                    sheetBrowserParameterValuesById,
                    new Dictionary<long, HashSet<long>>(),
                    viewTemplates,
                    dataService.CollectExistingViewNames(document),
                    dataService.CollectExistingSheetNumbers(document),
                    savedSettings);

                debugStep = "Открытие окна создания";
                ReportProgressWindow(
                    openingProgressWindow,
                    11,
                    11,
                    "Подготовка окна",
                    "Дорисовываем основное окно.");
                bool dialogAccepted = ShowCreationWindowUntilAccepted(uiDocument, document, viewModel, settingsService, ref openingProgressWindow);
                if (!dialogAccepted)
                {
                    return Result.Cancelled;
                }

                CreateViewsAndSheetsSettings settings;
                List<SheetCreationItem> items;
                string uiValidationMessage;
                if (!viewModel.TryBuildRequest(out settings, out items, out uiValidationMessage))
                {
                    TaskDialog.Show(CommandTitle, uiValidationMessage);
                    return Result.Cancelled;
                }

                AppendWarnings(warnings, ValidateBeforeExecution(document, settings, items));

                CreateViewsAndSheetsOperationService operationService = new CreateViewsAndSheetsOperationService();
                CreateViewsAndSheetsProgressWindow progressWindow = ShowProgressWindow(items.Count);
                CreateViewsAndSheetsResult result;
                try
                {
                    result = operationService.Execute(document, settings, items, progressWindow);
                }
                finally
                {
                    CloseProgressWindow(progressWindow);
                }

                AppendWarnings(result.Warnings, warnings);

                if (settings.Placement != null && settings.Placement.SaveSettings)
                {
                    settingsService.SaveSettings(settings);
                }

                ShowFinalReport(result);
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (RowProcessingException rowException)
            {
                CloseProgressWindow(openingProgressWindow);
                openingProgressWindow = null;
                message = rowException.Message;
                TaskDialog.Show(
                    CommandTitle,
                    rowException.Message + "\n\nОперация работает по принципу \"всё или ничего\". Все изменения отменены.");
                return Result.Failed;
            }
            catch (InvalidOperationException operationException)
            {
                CloseProgressWindow(openingProgressWindow);
                openingProgressWindow = null;
                message = operationException.Message;
                TaskDialog.Show(
                    CommandTitle,
                    operationException.Message + "\n\n" +
                    "Шаг: " + debugStep + "\n" +
                    "Время: " + stopwatch.ElapsedMilliseconds + " мс");
                return Result.Cancelled;
            }
            catch (Exception exception)
            {
                CloseProgressWindow(openingProgressWindow);
                openingProgressWindow = null;
                message = exception.Message;
                TaskDialog.Show(
                    CommandTitle,
                    "Неожиданная ошибка:\n\n" +
                    "Шаг: " + debugStep + "\n" +
                    "Время: " + stopwatch.ElapsedMilliseconds + " мс\n\n" +
                    exception);
                return Result.Failed;
            }
            finally
            {
                CloseProgressWindow(openingProgressWindow);
            }
        }

        private bool ValidateStartupData(
            IList<RevitElementItem> sourceViews,
            IList<RevitElementItem> sourceSheets,
            IList<RevitElementItem> viewportTypes,
            IList<RevitElementItem> titleBlockTypes,
            IList<RevitElementItem> viewTemplates,
            out string validationMessage)
        {
            StringBuilder builder = new StringBuilder();

            if (sourceViews == null || sourceViews.Count == 0)
            {
                builder.AppendLine("- В документе не найдены виды, которые можно дублировать с детализацией.");
            }

            if (sourceSheets == null || sourceSheets.Count == 0)
            {
                builder.AppendLine("- В документе не найдены листы-образцы с основной надписью.");
            }

            if (viewportTypes == null || viewportTypes.Count == 0)
            {
                builder.AppendLine("- В документе не найдены типы Viewport.");
            }

            if (titleBlockTypes == null || titleBlockTypes.Count == 0)
            {
                builder.AppendLine("- В документе не найдены типы основных надписей.");
            }

            if (viewTemplates == null || viewTemplates.Count <= 1)
            {
                builder.AppendLine("- В документе не найдены шаблоны видов.");
            }

            validationMessage = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(validationMessage);
        }

        private IList<string> ValidateBeforeExecution(Document document, CreateViewsAndSheetsSettings settings, IList<SheetCreationItem> items)
        {
            ValidationService validationService = new ValidationService();
            CreateViewsAndSheetsValidationResult validationResult = validationService.ValidateBeforeExecution(document, settings, items);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException(BuildValidationMessage(validationResult.Errors, validationResult.Warnings));
            }

            return validationResult.Warnings;
        }

        private string BuildValidationMessage(IList<string> errors, IList<string> warnings)
        {
            StringBuilder builder = new StringBuilder();

            if (errors != null && errors.Count > 0)
            {
                builder.AppendLine("Ошибки проверки:");
                for (int i = 0; i < errors.Count; i++)
                {
                    builder.AppendLine("- " + errors[i]);
                }
            }

            if (HasDialogWarnings(warnings))
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.AppendLine("Предупреждения:");
                for (int i = 0; i < warnings.Count; i++)
                {
                    if (WarningMessageSeverity.IsCritical(warnings[i]))
                    {
                        continue;
                    }

                    builder.AppendLine("- " + WarningMessageSeverity.Clean(warnings[i]));
                }
            }

            return builder.ToString().Trim();
        }

        private bool HasDialogWarnings(IList<string> warnings)
        {
            if (warnings == null)
            {
                return false;
            }

            for (int i = 0; i < warnings.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(warnings[i]) &&
                    !WarningMessageSeverity.IsCritical(warnings[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private void ShowFinalReport(CreateViewsAndSheetsResult result)
        {
            int createdSheetsCount = result != null ? result.CreatedSheetsCount : 0;
            ToastNotifier.ShowSuccess(CommandTitle, "Создано листов: " + createdSheetsCount, 10);
        }

        private void PrepareWindowForRevit(CreateViewsAndSheetsWindow window)
        {
            if (window == null)
            {
                return;
            }

            window.ShowInTaskbar = false;
            window.Opacity = 0.0;
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.SourceInitialized += RevitOwnedWindow_SourceInitialized;

            AttachRevitWindowOwner(window);
        }

        private void PrepareProgressWindowForRevit(Window window)
        {
            if (window == null)
            {
                return;
            }

            window.ShowInTaskbar = false;
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.SourceInitialized += RevitOwnedWindow_SourceInitialized;
            AttachRevitWindowOwner(window);
        }

        private void AttachRevitWindowOwner(Window window)
        {
            if (window == null)
            {
                return;
            }

            IntPtr ownerHandle = IntPtr.Zero;
            try
            {
                ownerHandle = Process.GetCurrentProcess().MainWindowHandle;
            }
            catch
            {
                ownerHandle = IntPtr.Zero;
            }

            if (ownerHandle != IntPtr.Zero)
            {
                WindowInteropHelper helper = new WindowInteropHelper(window);
                helper.Owner = ownerHandle;
            }

        }

        private CreateViewsAndSheetsProgressWindow ShowProgressWindow(int totalItems)
        {
            CreateViewsAndSheetsProgressWindow progressWindow = null;
            try
            {
                progressWindow = new CreateViewsAndSheetsProgressWindow(BuildCreationProgressMessages());
                PrepareProgressWindowForRevit(progressWindow);
                progressWindow.Show();

                CreateViewsAndSheetsProgressInfo progressInfo = new CreateViewsAndSheetsProgressInfo();
                progressInfo.CurrentStep = 0;
                progressInfo.TotalSteps = 1;
                progressInfo.ProcessedItems = 0;
                progressInfo.TotalItems = totalItems > 0 ? totalItems : 0;
                progressInfo.Stage = "Подготовка";
                progressInfo.Details = "Запуск создания видов и листов.";
                progressWindow.Report(progressInfo);
                ShowProgressWindowDebugDialogIfInvalid("Проверка окна прогресса создания после первого обновления", progressWindow);

                return progressWindow;
            }
            catch (Exception exception)
            {
                ShowProgressWindowDebugDialog("Ошибка открытия окна прогресса создания", progressWindow, exception);
                TaskDialog.Show(
                    CommandTitle,
                    "Не удалось открыть окно прогресса. Операция будет выполнена без полосы прогресса.\n\n" +
                    exception.Message);
                return null;
            }
        }

        private CreateViewsAndSheetsProgressWindow ShowOpeningProgressWindow()
        {
            CreateViewsAndSheetsProgressWindow progressWindow = null;
            try
            {
                progressWindow = new CreateViewsAndSheetsProgressWindow(BuildOpeningProgressMessages());
                PrepareProgressWindowForRevit(progressWindow);
                progressWindow.Show();
                ReportProgressWindow(
                    progressWindow,
                    0,
                    11,
                    "Открытие",
                    "Показываем пустое окно и готовим данные.");
                ShowProgressWindowDebugDialogIfInvalid("Проверка окна прогресса загрузки после первого обновления", progressWindow);
                return progressWindow;
            }
            catch (Exception exception)
            {
                ShowProgressWindowDebugDialog("Ошибка открытия окна прогресса загрузки", progressWindow, exception);
                TaskDialog.Show(
                    CommandTitle,
                    "Не удалось открыть окно прогресса загрузки. Операция продолжит открываться без полосы прогресса.\n\n" +
                    exception.Message);
                return null;
            }
        }

        private void ShowProgressWindowDebugDialogIfInvalid(string debugStep, CreateViewsAndSheetsProgressWindow progressWindow)
        {
            if (progressWindow == null)
            {
                return;
            }

            try
            {
                progressWindow.UpdateLayout();
                bool hasInvalidHeight =
                    progressWindow.IsLoaded &&
                    progressWindow.ActualHeight > 0.0 &&
                    progressWindow.MinHeight > 0.0 &&
                    progressWindow.ActualHeight + 1.0 < progressWindow.MinHeight;

                bool hasMissingContent = progressWindow.Content == null;
                if (hasInvalidHeight || hasMissingContent)
                {
                    ShowProgressWindowDebugDialog(debugStep, progressWindow, null);
                }
            }
            catch (Exception exception)
            {
                ShowProgressWindowDebugDialog(debugStep + ". Ошибка проверки размеров", progressWindow, exception);
            }
        }

        private void ShowProgressWindowDebugDialog(string debugStep, CreateViewsAndSheetsProgressWindow progressWindow, Exception exception)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Шаг: " + debugStep);
            builder.AppendLine("Окно создано: " + (progressWindow != null ? "да" : "нет"));

            if (progressWindow != null)
            {
                builder.AppendLine("Width: " + progressWindow.Width);
                builder.AppendLine("Height: " + progressWindow.Height);
                builder.AppendLine("MinWidth: " + progressWindow.MinWidth);
                builder.AppendLine("MinHeight: " + progressWindow.MinHeight);
                builder.AppendLine("ActualWidth: " + progressWindow.ActualWidth);
                builder.AppendLine("ActualHeight: " + progressWindow.ActualHeight);
                builder.AppendLine("SizeToContent: " + progressWindow.SizeToContent);
                builder.AppendLine("IsLoaded: " + progressWindow.IsLoaded);
                builder.AppendLine("Visibility: " + progressWindow.Visibility);
                builder.AppendLine("Content: " + (progressWindow.Content != null ? progressWindow.Content.GetType().FullName : "null"));
            }

            if (exception != null)
            {
                builder.AppendLine();
                builder.AppendLine("Ошибка:");
                builder.AppendLine(exception.ToString());
            }

            TaskDialog.Show(CommandTitle + " - debug", builder.ToString());
        }

        private IList<string> BuildOpeningProgressMessages()
        {
            List<string> messages = new List<string>();
            messages.Add("Загружаем потерянные буквы");
            messages.Add("Еще чуть-чуть и я откроюсь");
            messages.Add("Ты давно протирал компьютер?");
            messages.Add("Если на меня не смотреть - то я откроюсь быстрее");
            return messages;
        }

        private IList<string> BuildCreationProgressMessages()
        {
            List<string> messages = new List<string>();
            messages.Add("Какие у тебя красивые чертежи! Копировать одно удовольствие!");
            messages.Add("Как много листов для копирования... Только Гретте объем бумаги не показывайте");
            messages.Add("А что, ручками копировать листы уже не модно?");
            messages.Add("Я на одном из скопированных листов написать пакость:), дать подсказку, где именно?");
            return messages;
        }

        private void ReportProgressWindow(
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

            CreateViewsAndSheetsProgressInfo progressInfo = new CreateViewsAndSheetsProgressInfo();
            progressInfo.CurrentStep = currentStep;
            progressInfo.TotalSteps = totalSteps;
            progressInfo.ProcessedItems = 0;
            progressInfo.TotalItems = 0;
            progressInfo.Stage = stage;
            progressInfo.Details = details;
            progressWindow.Report(progressInfo);
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
                // Закрытие окна прогресса вспомогательное и не должно менять результат команды.
            }
        }

        private bool ShowCreationWindowUntilAccepted(
            UIDocument uiDocument,
            Document document,
            CreateViewsAndSheetsViewModel viewModel,
            SettingsService settingsService,
            ref CreateViewsAndSheetsProgressWindow openingProgressWindow)
        {
            if (uiDocument == null)
            {
                TaskDialog.Show(CommandTitle, "UI-документ Revit недоступен.");
                return false;
            }

            if (document == null)
            {
                TaskDialog.Show(CommandTitle, "Документ Revit недоступен.");
                return false;
            }

            if (viewModel == null)
            {
                TaskDialog.Show(CommandTitle, "Модель данных окна недоступна.");
                return false;
            }

            bool reopenSettingsWindowAfterPointSelection = false;
            while (true)
            {
                CreateViewsAndSheetsWindow window = new CreateViewsAndSheetsWindow(viewModel, reopenSettingsWindowAfterPointSelection);
                reopenSettingsWindowAfterPointSelection = false;
                PrepareWindowForRevit(window);
                CloseOpeningProgressWindowAfterFullWindowReady(ref openingProgressWindow);

                bool? dialogResult = window.ShowDialog();
                SaveLastWindowSession(settingsService, viewModel);
                PlacementPointSelectionRequestEventArgs pointRequest = window.PendingPointSelectionRequest;
                if (pointRequest != null)
                {
                    bool shouldReopenSettingsWindow = window.OpenSettingsAfterPointSelection;
                    TryPickPlacementPoint(uiDocument, document, viewModel, pointRequest);
                    reopenSettingsWindowAfterPointSelection = shouldReopenSettingsWindow;
                    continue;
                }

                return dialogResult.HasValue && dialogResult.Value;
            }
        }

        private void CloseOpeningProgressWindowAfterFullWindowReady(ref CreateViewsAndSheetsProgressWindow openingProgressWindow)
        {
            if (openingProgressWindow == null)
            {
                return;
            }

            ReportProgressWindow(
                openingProgressWindow,
                11,
                11,
                "Окно готово",
                "Показываем настройки создания видов и листов.");
            CloseProgressWindow(openingProgressWindow);
            openingProgressWindow = null;
        }

        private void SaveLastWindowSession(SettingsService settingsService, CreateViewsAndSheetsViewModel viewModel)
        {
            if (settingsService == null || viewModel == null)
            {
                return;
            }

            settingsService.SaveSettings(viewModel.BuildSessionSettings());
        }

        private bool TryPickPlacementPoint(
            UIDocument uiDocument,
            Document document,
            CreateViewsAndSheetsViewModel viewModel,
            PlacementPointSelectionRequestEventArgs request)
        {
            if (uiDocument == null || document == null || viewModel == null || request == null)
            {
                TaskDialog.Show(CommandTitle, "Не удалось запустить выбор точки: не хватает входных данных.");
                return false;
            }

            View activeViewBeforePick = uiDocument.ActiveView;
            ViewSheet sourceSheet = GetSelectedSourceSheet(document, viewModel);
            if (sourceSheet == null)
            {
                TaskDialog.Show(CommandTitle, "Не удалось открыть лист-образец для выбора точки.");
                return false;
            }

            SheetBounds sourceSheetBounds = GetSelectedSourceSheetBounds(viewModel);
            if (sourceSheetBounds == null)
            {
                TaskDialog.Show(CommandTitle, "Не удалось определить габарит листа-образца для расчета координат.");
                return false;
            }

            try
            {
                ActivateRevitView(uiDocument, sourceSheet);

                XYZ pickedPoint = uiDocument.Selection.PickPoint(request.Prompt);
                if (pickedPoint == null)
                {
                    return false;
                }

                double xMm = UnitConversionUtils.FeetToMillimeters(pickedPoint.X - sourceSheetBounds.MinXFeet);
                double yMm = UnitConversionUtils.FeetToMillimeters(pickedPoint.Y - sourceSheetBounds.MinYFeet);

                viewModel.ApplyPickedPoint(request.Target, xMm, yMm);
                return true;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return false;
            }
            catch (Exception exception)
            {
                TaskDialog.Show(CommandTitle, "Не удалось выбрать точку:\n\n" + exception.Message);
                return false;
            }
            finally
            {
                RestoreRevitView(uiDocument, activeViewBeforePick);
            }
        }

        private ViewSheet GetSelectedSourceSheet(Document document, CreateViewsAndSheetsViewModel viewModel)
        {
            if (document == null ||
                viewModel == null ||
                viewModel.GetPointSelectionSourceSheet() == null ||
                viewModel.GetPointSelectionSourceSheet().Id == null)
            {
                return null;
            }

            return document.GetElement(viewModel.GetPointSelectionSourceSheet().Id) as ViewSheet;
        }

        private SheetBounds GetSelectedSourceSheetBounds(CreateViewsAndSheetsViewModel viewModel)
        {
            if (viewModel == null)
            {
                return null;
            }

            return viewModel.GetPointSelectionSourceSheetBounds();
        }

        private void ActivateRevitView(UIDocument uiDocument, View view)
        {
            if (uiDocument == null || view == null || uiDocument.ActiveView == null || uiDocument.ActiveView.Id == view.Id)
            {
                return;
            }

            uiDocument.ActiveView = view;
        }

        private void RestoreRevitView(UIDocument uiDocument, View view)
        {
            try
            {
                if (uiDocument != null &&
                    view != null &&
                    uiDocument.ActiveView != null &&
                    uiDocument.ActiveView.Id != view.Id)
                {
                    uiDocument.ActiveView = view;
                }
            }
            catch
            {
                // Восстановление активного вида вспомогательное и не должно прерывать работу команды.
            }
        }

        private void RevitOwnedWindow_SourceInitialized(object sender, EventArgs e)
        {
            try
            {
                Window window = sender as Window;
                if (window == null)
                {
                    return;
                }

                HwndSource source = PresentationSource.FromVisual(window) as HwndSource;
                if (source != null && source.CompositionTarget != null)
                {
                    source.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
                }

                window.SourceInitialized -= RevitOwnedWindow_SourceInitialized;
            }
            catch
            {
            }
        }

        private void AppendWarnings(IList<string> target, IList<string> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(source[i]))
                {
                    target.Add(source[i]);
                }
            }
        }
    }
}
