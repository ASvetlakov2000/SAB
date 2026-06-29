using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Views;

namespace SAB.InteriorElevations.Services.Elevations
{
    public enum CropByExampleOperation
    {
        None = 0,
        PickLine = 1,
        PickExistingLineAndCreateView = 2,
        CreateView = 3,
        AcceptCrop = 4
    }

    public class CropByExampleExternalEventHandler : IExternalEventHandler
    {
        private readonly CropByExampleSession _session;
        private readonly ElevationCropByExampleService _service;
        private CropByExampleLineCreationWindow _window;
        private CropByExampleOperation _pendingOperation;

        public CropByExampleExternalEventHandler(
            CropByExampleSession session,
            ElevationCropByExampleService service)
        {
            _session = session;
            _service = service;
            _pendingOperation = CropByExampleOperation.None;
        }

        public void SetWindow(CropByExampleLineCreationWindow window)
        {
            _window = window;
        }

        public void RequestOperation(CropByExampleOperation operation)
        {
            _pendingOperation = operation;
        }

        public void Execute(UIApplication app)
        {
            CropByExampleOperation operation = _pendingOperation;
            _pendingOperation = CropByExampleOperation.None;

            if (operation == CropByExampleOperation.None)
            {
                return;
            }

            List<string> warnings = new List<string>();

            try
            {
                UIDocument uiDocument = app != null ? app.ActiveUIDocument : null;
                if (uiDocument == null)
                {
                    ShowWarning("Не удалось получить активный документ Revit.");
                    return;
                }

                if (operation == CropByExampleOperation.PickLine)
                {
                    PickLine(uiDocument, warnings);
                    return;
                }

                if (operation == CropByExampleOperation.PickExistingLineAndCreateView)
                {
                    PickLine(uiDocument, warnings);
                    if (HasSelectedLine())
                    {
                        CreateSampleView(uiDocument, warnings);
                    }

                    return;
                }

                if (operation == CropByExampleOperation.CreateView)
                {
                    CreateSampleView(uiDocument, warnings);
                    return;
                }

                if (operation == CropByExampleOperation.AcceptCrop)
                {
                    AcceptCrop(uiDocument, warnings);
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                ShowStatus("Действие отменено.", "Команда Revit была отменена пользователем.", false, false);
            }
            catch (Exception exception)
            {
                ShowWarning("Ошибка сценария вида-примера: " + exception.Message);
            }
        }

        public string GetName()
        {
            return "SAB Crop By Example External Event";
        }

        private void PickLine(UIDocument uiDocument, IList<string> warnings)
        {
            DetailLine selectedLine;
            bool picked = _service.TryPickSingleDetailLine(uiDocument, _session, warnings, out selectedLine);
            if (!picked || selectedLine == null)
            {
                ShowWarningsOrDefault(warnings, "Линия не выбрана.");
                return;
            }

            ShowStatus(
                "Линия выбрана.",
                "Линия выбрана. Нажмите Создать вид.",
                true,
                false);
        }

        private void CreateSampleView(UIDocument uiDocument, IList<string> warnings)
        {
            if (!HasSelectedLine())
            {
                ShowWarning("Сначала выберите линию детализации.");
                return;
            }

            ShowStatus(
                "Выберите помещение.",
                "Укажите помещение, когда Revit запросит выбор.",
                true,
                false);

            RoomData roomData;
            bool roomPicked = _service.TryPickRoomDataForSampleView(uiDocument, _session, warnings, out roomData);
            if (!roomPicked || roomData == null)
            {
                ShowWarningsOrDefault(warnings, "Помещение не выбрано.");
                return;
            }

            ViewSection sampleView;
            bool created = _service.TryCreateSampleView(uiDocument, _session, roomData, warnings, out sampleView);
            if (!created || sampleView == null)
            {
                ShowWarningsOrDefault(warnings, "Вид-пример не создан.");
                return;
            }

            ShowStatus(
                "Вид-пример создан.",
                "Вид-пример создан и открыт. Настройте верхнюю и нижнюю границу вида, нажмите зеленую галочку Revit, затем нажмите Принять границы.",
                true,
                true);

            ToastNotifier.ShowInfo("SAB Развертки", "Вид-пример создан. Настройте верхнюю и нижнюю границу вида.");
        }

        private void AcceptCrop(UIDocument uiDocument, IList<string> warnings)
        {
            double topOffsetMm;
            double bottomOffsetMm;
            bool accepted = _service.TryAcceptCropFromSampleView(
                uiDocument,
                _session,
                warnings,
                out topOffsetMm,
                out bottomOffsetMm);

            if (!accepted)
            {
                ShowWarningsOrDefault(warnings, "Границы вида-примера не удалось принять.");
                return;
            }

            ShowStatus(
                "Границы приняты.",
                "Верхний отступ: " + topOffsetMm.ToString("0.###") + " мм. Нижний отступ: " + bottomOffsetMm.ToString("0.###") + " мм. Настройки сохранены.",
                true,
                true);

            ToastNotifier.ShowInfo("SAB Развертки", "Границы вида по высоте заданы и сохранены.");
        }

        private bool HasSelectedLine()
        {
            return _session != null &&
                   _session.SourceLineId != null &&
                   _session.SourceLineId != ElementId.InvalidElementId;
        }

        private void ShowWarningsOrDefault(IList<string> warnings, string defaultMessage)
        {
            if (warnings != null && warnings.Count > 0)
            {
                ShowWarning(warnings[warnings.Count - 1]);
                return;
            }

            ShowWarning(defaultMessage);
        }

        private void ShowWarning(string message)
        {
            ShowStatus("Внимание.", message, HasSelectedLine(), HasSampleView());
            ToastNotifier.ShowWarning("SAB Развертки", message);
        }

        private bool HasSampleView()
        {
            return _session != null &&
                   _session.SampleViewId != null &&
                   _session.SampleViewId != ElementId.InvalidElementId;
        }

        private void ShowStatus(string header, string description, bool lineSelected, bool sampleViewCreated)
        {
            if (_window == null)
            {
                return;
            }

            _window.Dispatcher.Invoke(delegate
            {
                _window.UpdateStatus(header, description, lineSelected, sampleViewCreated);
            });
        }
    }
}
