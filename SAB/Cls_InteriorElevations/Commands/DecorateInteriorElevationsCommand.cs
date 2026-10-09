using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Helpers.Notifications.ToastNotifications;
using SAB.CreateViewsAndSheets.Models;
using SAB.CreateViewsAndSheets.Views;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Annotations;
using SAB.InteriorElevations.Services.Selection;
using SAB.InteriorElevations.Services.Settings;
using SAB.InteriorElevations.Utils;
using SAB.InteriorElevations.Views;

namespace SAB.InteriorElevations.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class DecorateInteriorElevationsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            CreateViewsAndSheetsProgressWindow progressWindow = null;
            try
            {
                UIApplication uiApplication = commandData.Application;
                UIDocument uiDocument = uiApplication.ActiveUIDocument;
                if (uiDocument == null || uiDocument.Document == null)
                {
                    ToastNotifier.ShowError("SAB Развертки", "Не удалось получить активный документ Revit.");
                    return Result.Failed;
                }

                Document document = uiDocument.Document;
                List<string> warnings = new List<string>();
                ElevationDecorationSettingsStorageService storageService =
                    new ElevationDecorationSettingsStorageService();
                ElevationDecorationSettings savedSettings = null;
                try
                {
                    savedSettings = storageService.LoadSettings();
                }
                catch (Exception exception)
                {
                    warnings.Add("Не удалось загрузить настройки оформления: " + exception.Message);
                }

                ElevationDecorationWindow window;
                while (true)
                {
                    window = new ElevationDecorationWindow(document, savedSettings);
                    bool? dialogResult = window.ShowDialog();
                    if (window.OpenCatalogRequested)
                    {
                        if (document.ActiveView is ViewSheet)
                        {
                            ToastNotifier.ShowWarning(
                                "SAB Каталог оформления",
                                "Откройте 3D-вид, план, разрез или развертку, чтобы выбирать элементы каталога.");
                            return Result.Cancelled;
                        }

                        new ElevationDecorationCatalogWorkflowService().Run(uiDocument);
                        continue;
                    }

                    if (dialogResult != true || window.SelectedSettings == null)
                    {
                        return Result.Cancelled;
                    }

                    break;
                }

                ElevationDecorationSettings settings = window.SelectedSettings;
                try
                {
                    storageService.SaveSettings(settings);
                }
                catch (Exception exception)
                {
                    warnings.Add("Не удалось сохранить настройки оформления: " + exception.Message);
                }

                List<ViewSection> targetViews = ResolveTargetViews(uiDocument);
                if (targetViews == null || targetViews.Count == 0)
                {
                    return Result.Cancelled;
                }

                int totalProgressSteps = GetTotalProgressSteps(targetViews.Count, settings);
                progressWindow = ShowProgressWindow(uiApplication, totalProgressSteps, targetViews.Count);
                ElevationDecorationResult result;
                using (Transaction transaction = new Transaction(document, "SAB Оформление разверток"))
                {
                    transaction.Start();
                    ElevationDecorationService service = new ElevationDecorationService();
                    result = service.DecorateViews(
                        document,
                        targetViews,
                        settings,
                        warnings,
                        delegate(ElevationDecorationProgressInfo progressInfo)
                        {
                            ReportProgress(progressWindow, progressInfo);
                        });
                    transaction.Commit();
                }

                ReportProgress(
                    progressWindow,
                    new ElevationDecorationProgressInfo
                    {
                        CurrentStep = totalProgressSteps,
                        TotalSteps = totalProgressSteps,
                        Stage = "Готово",
                        Details = "Оформление разверток завершено.",
                        Result = result
                    });
                CloseProgressWindow(progressWindow);
                progressWindow = null;

                ShowResult(result, warnings);
                return result.ViewsProcessed > 0 ? Result.Succeeded : Result.Cancelled;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ToastNotifier.ShowError(
                    "SAB Развертки",
                    "Ошибка автоматического оформления: " + exception.Message);
                return Result.Failed;
            }
            finally
            {
                CloseProgressWindow(progressWindow);
            }
        }

        private static int GetTotalProgressSteps(
            int viewCount,
            ElevationDecorationSettings settings)
        {
            int stepsPerView = 2;
            if (settings != null && settings.PlaceSpotElevations)
            {
                stepsPerView++;
            }

            if (settings != null && settings.PlaceTags)
            {
                stepsPerView++;
            }

            if (settings != null && settings.PlaceDimensions)
            {
                stepsPerView++;
            }

            return Math.Max(1, viewCount * stepsPerView);
        }

        private CreateViewsAndSheetsProgressWindow ShowProgressWindow(
            UIApplication uiApplication,
            int totalSteps,
            int viewCount)
        {
            CreateViewsAndSheetsProgressWindow progressWindow = null;
            try
            {
                progressWindow = new CreateViewsAndSheetsProgressWindow(
                    new List<string>(),
                    "SAB Оформление разверток",
                    "Оформление разверток");
                if (uiApplication != null && uiApplication.MainWindowHandle != IntPtr.Zero)
                {
                    new WindowInteropHelper(progressWindow).Owner = uiApplication.MainWindowHandle;
                }

                progressWindow.Show();
                progressWindow.Report(new CreateViewsAndSheetsProgressInfo
                {
                    CurrentStep = 0,
                    TotalSteps = Math.Max(1, totalSteps),
                    ProcessedItems = 0,
                    TotalItems = Math.Max(1, totalSteps),
                    Stage = "Подготовка",
                    Details = "Подготовлено видов: " + viewCount + "."
                });
                return progressWindow;
            }
            catch (Exception exception)
            {
                CloseProgressWindow(progressWindow);
                ToastNotifier.ShowWarning(
                    "SAB Развертки",
                    "Не удалось открыть окно прогресса. Оформление будет продолжено: " +
                    exception.Message);
                return null;
            }
        }

        private void ReportProgress(
            CreateViewsAndSheetsProgressWindow progressWindow,
            ElevationDecorationProgressInfo progressInfo)
        {
            if (progressWindow == null || progressInfo == null)
            {
                return;
            }

            ElevationDecorationResult result = progressInfo.Result ?? new ElevationDecorationResult();
            int created = result.SpotElevationsCreated + result.TagsCreated + result.DimensionsCreated;
            progressWindow.Report(new CreateViewsAndSheetsProgressInfo
            {
                CurrentStep = progressInfo.CurrentStep,
                TotalSteps = Math.Max(1, progressInfo.TotalSteps),
                ProcessedItems = progressInfo.CurrentStep,
                TotalItems = Math.Max(1, progressInfo.TotalSteps),
                Stage = progressInfo.Stage,
                Details = progressInfo.Details,
                Counters = "Создано: " + created +
                           "  ·  Заменено: " + result.DeletedOwnedAnnotations +
                           "  ·  Ошибок: " + result.FailedItems
            });
        }

        private static void CloseProgressWindow(CreateViewsAndSheetsProgressWindow progressWindow)
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

        private List<ViewSection> ResolveTargetViews(UIDocument uiDocument)
        {
            Document document = uiDocument.Document;
            ViewSection activeSection = document.ActiveView as ViewSection;
            if (IsInteriorElevation(activeSection))
            {
                return new List<ViewSection> { activeSection };
            }

            ViewSheet activeSheet = document.ActiveView as ViewSheet;
            if (activeSheet == null)
            {
                ToastNotifier.ShowWarning(
                    "SAB Развертки",
                    "Откройте развертку или лист с развертками и повторите команду.");
                return new List<ViewSection>();
            }

            TaskDialog scopeDialog = new TaskDialog("SAB Оформление разверток");
            scopeDialog.MainInstruction = "Какие развертки оформить?";
            scopeDialog.MainContent = "Можно выбрать отдельные видовые экраны или обработать все развертки активного листа.";
            scopeDialog.AddCommandLink(
                TaskDialogCommandLinkId.CommandLink1,
                "Выбрать виды",
                "Укажите один или несколько видовых экранов на активном листе.");
            scopeDialog.AddCommandLink(
                TaskDialogCommandLinkId.CommandLink2,
                "Все виды листа",
                "Обработать все развертки, размещённые на активном листе.");
            scopeDialog.CommonButtons = TaskDialogCommonButtons.Cancel;

            TaskDialogResult scopeResult = scopeDialog.Show();
            if (scopeResult == TaskDialogResult.CommandLink1)
            {
                ToastNotifier.ShowInfo(
                    "SAB Развертки",
                    "Выберите видовые экраны разверток и нажмите Готово.");
                IList<Reference> picked = uiDocument.Selection.PickObjects(
                    ObjectType.Element,
                    new ElevationViewportSelectionFilter(document, activeSheet.Id),
                    "Выберите развертки для оформления");
                return ResolveViewsFromViewportReferences(document, picked);
            }

            if (scopeResult == TaskDialogResult.CommandLink2)
            {
                return activeSheet.GetAllViewports()
                    .Select(id => document.GetElement(id) as Viewport)
                    .Where(viewport => viewport != null)
                    .Select(viewport => document.GetElement(viewport.ViewId) as ViewSection)
                    .Where(IsInteriorElevation)
                    .GroupBy(view => RevitElementIdUtils.GetElementIdValue(view.Id))
                    .Select(group => group.First())
                    .ToList();
            }

            return new List<ViewSection>();
        }

        private List<ViewSection> ResolveViewsFromViewportReferences(
            Document document,
            IList<Reference> references)
        {
            if (references == null)
            {
                return new List<ViewSection>();
            }

            return references
                .Select(reference => document.GetElement(reference) as Viewport)
                .Where(viewport => viewport != null)
                .Select(viewport => document.GetElement(viewport.ViewId) as ViewSection)
                .Where(IsInteriorElevation)
                .GroupBy(view => RevitElementIdUtils.GetElementIdValue(view.Id))
                .Select(group => group.First())
                .ToList();
        }

        private static bool IsInteriorElevation(ViewSection view)
        {
            return view != null && !view.IsTemplate &&
                   (view.ViewType == ViewType.Elevation || view.ViewType == ViewType.Section);
        }

        private void ShowResult(ElevationDecorationResult result, IList<string> warnings)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Оформление завершено.");
            builder.AppendLine("Обработано видов: " + result.ViewsProcessed);
            builder.AppendLine("Заменено элементов SAB: " + result.DeletedOwnedAnnotations);
            builder.AppendLine("Высотных отметок: " + result.SpotElevationsCreated);
            builder.AppendLine("Марок: " + result.TagsCreated);
            builder.AppendLine("Размерных линий: " + result.DimensionsCreated);

            if (warnings != null && warnings.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Предупреждений: " + warnings.Count);
                for (int index = 0; index < Math.Min(4, warnings.Count); index++)
                {
                    builder.AppendLine((index + 1) + ". " + warnings[index]);
                }

                if (warnings.Count > 4)
                {
                    builder.AppendLine("... и ещё " + (warnings.Count - 4) + ".");
                }
            }

            if (result.ViewsProcessed > 0)
            {
                ToastNotifier.ShowSuccess("SAB Развертки", builder.ToString(), 14);
            }
            else
            {
                ToastNotifier.ShowWarning("SAB Развертки", builder.ToString(), 14);
            }
        }

        private class ElevationViewportSelectionFilter : ISelectionFilter
        {
            private readonly Document _document;
            private readonly ElementId _sheetId;

            public ElevationViewportSelectionFilter(Document document, ElementId sheetId)
            {
                _document = document;
                _sheetId = sheetId;
            }

            public bool AllowElement(Element element)
            {
                Viewport viewport = element as Viewport;
                if (viewport == null || !RevitElementIdUtils.AreEqual(viewport.SheetId, _sheetId))
                {
                    return false;
                }

                return IsInteriorElevation(_document.GetElement(viewport.ViewId) as ViewSection);
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
