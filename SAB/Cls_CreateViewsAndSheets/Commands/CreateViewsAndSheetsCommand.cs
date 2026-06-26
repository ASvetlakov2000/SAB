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
            try
            {
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

                List<string> warnings = new List<string>();
                RevitDataService dataService = new RevitDataService();

                List<RevitElementItem> sourceViews = dataService.GetDuplicatableViews(document);
                List<RevitElementItem> sourceSheets = dataService.GetSheets(document);
                List<RevitElementItem> viewportTypes = dataService.GetViewportTypes(document);
                List<RevitElementItem> titleBlockTypes = dataService.GetTitleBlockTypes(document);
                List<RevitElementItem> sheetBrowserParameters = dataService.GetSheetBrowserParameters(document);
                Dictionary<long, List<string>> sheetBrowserParameterValuesById = dataService.GetSheetBrowserParameterValues(document, sheetBrowserParameters);
                List<RevitElementItem> viewTemplates = dataService.GetViewTemplates(document);

                string startupValidationMessage;
                if (!ValidateStartupData(sourceViews, sourceSheets, viewportTypes, titleBlockTypes, viewTemplates, out startupValidationMessage))
                {
                    TaskDialog.Show(CommandTitle, startupValidationMessage);
                    return Result.Cancelled;
                }

                SettingsService settingsService = new SettingsService();
                CreateViewsAndSheetsSettings savedSettings = settingsService.LoadSettings(document, warnings);

                CreateViewsAndSheetsViewModel viewModel = new CreateViewsAndSheetsViewModel(
                    sourceViews,
                    sourceSheets,
                    viewportTypes,
                    titleBlockTypes,
                    sheetBrowserParameters,
                    sheetBrowserParameterValuesById,
                    viewTemplates,
                    dataService.CollectExistingViewNames(document),
                    dataService.CollectExistingSheetNumbers(document),
                    savedSettings);

                bool dialogAccepted = ShowCreationWindowUntilAccepted(uiDocument, document, viewModel);
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
                CreateViewsAndSheetsResult result = operationService.Execute(document, settings, items);
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
                message = rowException.Message;
                TaskDialog.Show(
                    CommandTitle,
                    rowException.Message + "\n\nОперация работает по принципу \"всё или ничего\". Все изменения отменены.");
                return Result.Failed;
            }
            catch (InvalidOperationException operationException)
            {
                message = operationException.Message;
                TaskDialog.Show(CommandTitle, operationException.Message);
                return Result.Cancelled;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show(CommandTitle, "Неожиданная ошибка:\n\n" + exception);
                return Result.Failed;
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

            if (warnings != null && warnings.Count > 0)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.AppendLine("Предупреждения:");
                for (int i = 0; i < warnings.Count; i++)
                {
                    builder.AppendLine("- " + warnings[i]);
                }
            }

            return builder.ToString().Trim();
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
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.SourceInitialized += CreateViewsAndSheetsWindow_SourceInitialized;

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

        private bool ShowCreationWindowUntilAccepted(
            UIDocument uiDocument,
            Document document,
            CreateViewsAndSheetsViewModel viewModel)
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

            while (true)
            {
                CreateViewsAndSheetsWindow window = new CreateViewsAndSheetsWindow(viewModel);
                PrepareWindowForRevit(window);

                bool? dialogResult = window.ShowDialog();
                PlacementPointSelectionRequestEventArgs pointRequest = window.PendingPointSelectionRequest;
                if (pointRequest != null)
                {
                    TryPickPlacementPoint(uiDocument, document, viewModel, pointRequest);
                    continue;
                }

                return dialogResult.HasValue && dialogResult.Value;
            }
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

        private void CreateViewsAndSheetsWindow_SourceInitialized(object sender, EventArgs e)
        {
            try
            {
                CreateViewsAndSheetsWindow window = sender as CreateViewsAndSheetsWindow;
                if (window == null)
                {
                    return;
                }

                HwndSource source = PresentationSource.FromVisual(window) as HwndSource;
                if (source != null && source.CompositionTarget != null)
                {
                    source.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
                }

                window.SourceInitialized -= CreateViewsAndSheetsWindow_SourceInitialized;
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
