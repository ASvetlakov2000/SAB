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

                UIDocument uiDocument = commandData.Application.ActiveUIDocument;
                RoomGeometryToolsViewModel viewModel = new RoomGeometryToolsViewModel(uiDocument, startupAction);
                RoomGeometryToolsWindow window = new RoomGeometryToolsWindow(viewModel);
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ToastNotifier.ShowError("Проверка геометрии помещений", "Ошибка запуска окна: " + exception.Message, 12);
                return Result.Failed;
            }
        }
    }
}

