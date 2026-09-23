using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using SAB.RoomGeometryTools.UI;
using SAB.RoomGeometryTools.ViewModels;
using System;

namespace SAB.RoomGeometryTools.Services
{
    /// <summary>
    /// Оркестратор запуска окна Room Geometry Tools и стартовых операций.
    /// </summary>
    public class RoomGeometryToolsOrchestratorService
    {
        private static RoomGeometryToolsWindow _window;
        private static RoomGeometryToolsViewModel _viewModel;
        private static RoomGeometryExternalEventHandler _externalEventHandler;
        private static ExternalEvent _externalEvent;

        public Result Run(ExternalCommandData commandData, ref string message, RoomGeometryStartupAction startupAction)
        {
            try
            {
                if (commandData == null || commandData.Application == null || commandData.Application.ActiveUIDocument == null)
                {
                    message = "Не удалось получить активный документ Revit.";
                    ToastNotifier.ShowError("Проверка геометрии помещений", message, 12);
                    return Result.Failed;
                }

                // Если окно уже открыто, просто активируем его и при необходимости запускаем действие.
                if (_window != null && _window.IsLoaded)
                {
                    if (_viewModel != null)
                    {
                        _viewModel.RequestStartupAction(startupAction);
                    }

                    _window.Show();
                    _window.Activate();
                    return Result.Succeeded;
                }

                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                // В modeless-режиме операции Revit API должны выполняться через ExternalEvent.
                _externalEventHandler = new RoomGeometryExternalEventHandler();
                _externalEvent = ExternalEvent.Create(_externalEventHandler);
                _viewModel = new RoomGeometryToolsViewModel(uiDocument, startupAction, _externalEvent, _externalEventHandler);
                _externalEventHandler.AttachViewModel(_viewModel);

                _window = new RoomGeometryToolsWindow(_viewModel);
                _window.Closed += Window_Closed;
                _window.Show();
                _window.Activate();
                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ToastNotifier.ShowError("Проверка геометрии помещений", "Ошибка запуска окна: " + exception.Message, 12);
                return Result.Failed;
            }
        }

        private static void Window_Closed(object sender, EventArgs e)
        {
            if (_window != null)
            {
                _window.Closed -= Window_Closed;
            }

            _window = null;
            _viewModel = null;
            _externalEventHandler = null;

            if (_externalEvent != null)
            {
                _externalEvent.Dispose();
                _externalEvent = null;
            }
        }
    }
}
