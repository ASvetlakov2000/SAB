using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Plans;
using SAB.InteriorElevations.Services.Rooms;
using SAB.InteriorElevations.Services.Settings;
using SAB.InteriorElevations.Utils;
using SAB.InteriorElevations.ViewModels;
using SAB.InteriorElevations.Views;

namespace SAB.InteriorElevations.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreateRoomPlanSchemesCommand : IExternalCommand
    {
        // Блок допуска сравнения точек при сборке замкнутого контура из выбранных линий.
        private const double PointToleranceFeet = 1e-4;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApplication = commandData.Application;
                UIDocument uiDocument = uiApplication != null ? uiApplication.ActiveUIDocument : null;
                if (uiDocument == null)
                {
                    ToastNotifier.ShowError("SAB План-схемы", "Не удалось получить активный UI-документ Revit.");
                    return Result.Failed;
                }

                Document document = uiDocument.Document;
                if (document == null)
                {
                    ToastNotifier.ShowError("SAB План-схемы", "Не удалось получить активный документ Revit.");
                    return Result.Failed;
                }

                if (document.IsFamilyDocument)
                {
                    ToastNotifier.ShowWarning("SAB План-схемы", "Команда доступна только в проектном документе Revit.");
                    return Result.Cancelled;
                }

                View activeView = document.ActiveView;
                if (!IsSupportedPlanView(activeView))
                {
                    ToastNotifier.ShowWarning("SAB План-схемы", "Активный вид должен быть планом этажа или потолка.");
                    return Result.Cancelled;
                }

                ViewPlan sourcePlanView = activeView as ViewPlan;
                if (sourcePlanView == null)
                {
                    ToastNotifier.ShowWarning("SAB План-схемы", "Не удалось получить активный плановый вид.");
                    return Result.Cancelled;
                }

                // Блок загрузки последних настроек окна из файла пользователя.
                RoomPlanSchemeSettingsStorageService settingsStorageService = new RoomPlanSchemeSettingsStorageService();
                RoomPlanSchemeSettings savedSettings = null;
                try
                {
                    savedSettings = settingsStorageService.LoadSettings();
                }
                catch (Exception loadException)
                {
                    ToastNotifier.ShowWarning("SAB План-схемы", "Не удалось загрузить сохраненные настройки: " + loadException.Message);
                }

                RoomPlanSchemeSettingsViewModel viewModel = new RoomPlanSchemeSettingsViewModel(document, activeView, savedSettings);
                RoomPlanSchemeSettingsWindow settingsWindow = new RoomPlanSchemeSettingsWindow(viewModel);
                bool? dialogResult = settingsWindow.ShowDialog();
                if (!dialogResult.HasValue || !dialogResult.Value)
                {
                    return Result.Cancelled;
                }

                RoomPlanSchemeSettings settings = settingsWindow.SelectedSettings;
                if (settings == null)
                {
                    ToastNotifier.ShowWarning("SAB План-схемы", "Окно настроек не вернуло параметры построения.");
                    return Result.Cancelled;
                }

                // Блок сохранения настроек для следующего запуска команды.
                try
                {
                    settingsStorageService.SaveSettings(settings);
                }
                catch (Exception saveException)
                {
                    ToastNotifier.ShowWarning("SAB План-схемы", "Не удалось сохранить настройки: " + saveException.Message);
                }

                // Блок чтения уже выбранных пользователем линий. Это используется для ручного контура fallback.
                CurveLoop manualBoundaryLoop;
                int preselectedLineCount;
                string preselectedValidationMessage;
                if (!TryBuildManualBoundaryFromCurrentSelection(
                        uiDocument,
                        activeView,
                        out manualBoundaryLoop,
                        out preselectedLineCount,
                        out preselectedValidationMessage))
                {
                    ToastNotifier.ShowWarning("SAB План-схемы", preselectedValidationMessage);
                    return Result.Cancelled;
                }

                RoomPlanSelectionService roomSelectionService = new RoomPlanSelectionService();
                bool isSelectionCancelled;
                string selectionErrorMessage;
                IList<Room> selectedRooms = roomSelectionService.PickSingleRoom(uiDocument, out isSelectionCancelled, out selectionErrorMessage);
                if (isSelectionCancelled)
                {
                    return Result.Cancelled;
                }

                if (selectedRooms == null || selectedRooms.Count == 0)
                {
                    if (!string.IsNullOrWhiteSpace(selectionErrorMessage))
                    {
                        ToastNotifier.ShowWarning("SAB План-схемы", selectionErrorMessage);
                    }

                    return Result.Cancelled;
                }

                if (preselectedLineCount > 0)
                {
                    ToastNotifier.ShowInfo("SAB План-схемы", "Будет использован ручной контур из выбранных линий.");
                }

                RoomPlanSchemeCreationSummary summary;
                RoomPlanSchemeCreationService creationService = new RoomPlanSchemeCreationService();

                // Блок изменения модели Revit выполняется в отдельной транзакции.
                using (Transaction transaction = new Transaction(document, "SAB План-схемы помещений"))
                {
                    transaction.Start();
                    summary = creationService.CreateRoomPlanSchemes(document, sourcePlanView, selectedRooms, settings, manualBoundaryLoop);
                    transaction.Commit();
                }

                if (summary == null)
                {
                    ToastNotifier.ShowWarning("SAB План-схемы", "Сервис не вернул результат построения.");
                    return Result.Cancelled;
                }

                // Блок сообщения для сценария, когда автоматический контур не удалось применить.
                if (summary.ManualBoundaryRequired && manualBoundaryLoop == null)
                {
                    string manualBoundaryMessage =
                        "Границу вида создать не удалось.\n" +
                        "На виде созданы линии, которые необходимо исправить.\n" +
                        "Исправленные линии станут границами вида.";

                    ToastNotifier.ShowWarning("SAB План-схемы", manualBoundaryMessage, 18);

                    if (summary.HelperBoundaryLinesCount > 0)
                    {
                        ToastNotifier.ShowInfo(
                            "SAB План-схемы",
                            "Создано вспомогательных линий: " + summary.HelperBoundaryLinesCount +
                            ". Исправьте их, выделите и запустите команду повторно.",
                            12);
                    }
                }

                // Блок итогового уведомления по результатам команды.
                string resultMessage = BuildResultMessage(summary);
                if (summary.CreatedViewsCount > 0)
                {
                    ToastNotifier.ShowSuccess("SAB План-схемы", resultMessage, 14);
                    return Result.Succeeded;
                }

                ToastNotifier.ShowWarning("SAB План-схемы", resultMessage, 14);
                return Result.Cancelled;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ToastNotifier.ShowError("SAB План-схемы", "Ошибка выполнения команды: " + exception.Message, 20);
                return Result.Failed;
            }
        }

        private static bool IsSupportedPlanView(View view)
        {
            if (view == null)
            {
                return false;
            }

            return view.ViewType == ViewType.FloorPlan || view.ViewType == ViewType.CeilingPlan;
        }

        /// <summary>
        /// Преобразует текущий выбор пользователя (DetailLine на активном виде) в замкнутый CurveLoop.
        /// Если пользователь ничего не выбрал, возвращается null-контур без ошибки.
        /// </summary>
        private static bool TryBuildManualBoundaryFromCurrentSelection(
            UIDocument uiDocument,
            View activeView,
            out CurveLoop manualBoundaryLoop,
            out int selectedLineCount,
            out string validationMessage)
        {
            manualBoundaryLoop = null;
            selectedLineCount = 0;
            validationMessage = string.Empty;

            if (uiDocument == null || uiDocument.Document == null || activeView == null)
            {
                validationMessage = "Не удалось прочитать текущий выбор линий для ручного контура.";
                return false;
            }

            ICollection<ElementId> selectedIds = uiDocument.Selection.GetElementIds();
            if (selectedIds == null || selectedIds.Count == 0)
            {
                return true;
            }

            List<Curve> sourceCurves = new List<Curve>();
            Document document = uiDocument.Document;

            foreach (ElementId selectedId in selectedIds)
            {
                if (selectedId == null || selectedId == ElementId.InvalidElementId)
                {
                    continue;
                }

                Element selectedElement = document.GetElement(selectedId);
                DetailLine detailLine = selectedElement as DetailLine;
                if (detailLine == null)
                {
                    continue;
                }

                if (!RevitElementIdUtils.AreEqual(detailLine.OwnerViewId, activeView.Id))
                {
                    continue;
                }

                Curve curve = detailLine.GeometryCurve;
                if (curve == null)
                {
                    continue;
                }

                sourceCurves.Add(curve.Clone());
            }

            selectedLineCount = sourceCurves.Count;
            if (selectedLineCount == 0)
            {
                return true;
            }

            if (selectedLineCount < 3)
            {
                validationMessage = "Для ручной границы выбрано недостаточно линий. Нужно минимум 3 замкнутых сегмента.";
                return false;
            }

            if (!TryOrderCurvesAsClosedLoop(sourceCurves, out List<Curve> orderedCurves, out validationMessage))
            {
                return false;
            }

            CurveLoop loop = new CurveLoop();
            for (int i = 0; i < orderedCurves.Count; i++)
            {
                loop.Append(orderedCurves[i]);
            }

            manualBoundaryLoop = loop;
            return true;
        }

        /// <summary>
        /// Упорядочивает набор кривых в единый замкнутый контур по совпадающим конечным точкам.
        /// </summary>
        private static bool TryOrderCurvesAsClosedLoop(IList<Curve> curves, out List<Curve> orderedCurves, out string errorMessage)
        {
            orderedCurves = new List<Curve>();
            errorMessage = string.Empty;

            if (curves == null || curves.Count == 0)
            {
                errorMessage = "Список линий для ручного контура пуст.";
                return false;
            }

            List<Curve> remaining = new List<Curve>();
            for (int i = 0; i < curves.Count; i++)
            {
                if (curves[i] != null)
                {
                    remaining.Add(curves[i]);
                }
            }

            if (remaining.Count < 3)
            {
                errorMessage = "Недостаточно валидных линий для построения замкнутого контура.";
                return false;
            }

            Curve firstCurve = remaining[0];
            remaining.RemoveAt(0);
            orderedCurves.Add(firstCurve);

            XYZ startPoint = firstCurve.GetEndPoint(0);
            XYZ currentEndPoint = firstCurve.GetEndPoint(1);

            while (remaining.Count > 0)
            {
                bool foundNext = false;

                for (int i = 0; i < remaining.Count; i++)
                {
                    Curve nextCurve = remaining[i];
                    XYZ nextStart = nextCurve.GetEndPoint(0);
                    XYZ nextEnd = nextCurve.GetEndPoint(1);

                    if (ArePointsEqual(currentEndPoint, nextStart))
                    {
                        orderedCurves.Add(nextCurve);
                        currentEndPoint = nextEnd;
                        remaining.RemoveAt(i);
                        foundNext = true;
                        break;
                    }

                    if (ArePointsEqual(currentEndPoint, nextEnd))
                    {
                        Curve reversed = nextCurve.CreateReversed();
                        orderedCurves.Add(reversed);
                        currentEndPoint = reversed.GetEndPoint(1);
                        remaining.RemoveAt(i);
                        foundNext = true;
                        break;
                    }
                }

                if (!foundNext)
                {
                    errorMessage = "Выбранные линии не образуют последовательную замкнутую цепочку.";
                    return false;
                }
            }

            if (!ArePointsEqual(currentEndPoint, startPoint))
            {
                errorMessage = "Выбранные линии не замкнуты в контур.";
                return false;
            }

            return true;
        }

        private static bool ArePointsEqual(XYZ first, XYZ second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            double dx = first.X - second.X;
            double dy = first.Y - second.Y;
            double dz = first.Z - second.Z;
            double squaredDistance = (dx * dx) + (dy * dy) + (dz * dz);
            return squaredDistance <= PointToleranceFeet * PointToleranceFeet;
        }

        private static string BuildResultMessage(RoomPlanSchemeCreationSummary summary)
        {
            if (summary == null)
            {
                return "Результат выполнения команды не получен.";
            }

            string message =
                "Обработано помещений: " + summary.ProcessedRoomsCount +
                "\nСоздано видов: " + summary.CreatedViewsCount +
                "\nПропущено помещений: " + summary.SkippedRoomsCount;

            if (summary.Warnings != null && summary.Warnings.Count > 0)
            {
                int previewCount = summary.Warnings.Count > 3 ? 3 : summary.Warnings.Count;
                for (int i = 0; i < previewCount; i++)
                {
                    message += "\n- " + summary.Warnings[i];
                }

                if (summary.Warnings.Count > previewCount)
                {
                    message += "\n... и еще: " + (summary.Warnings.Count - previewCount);
                }
            }

            return message;
        }
    }
}
