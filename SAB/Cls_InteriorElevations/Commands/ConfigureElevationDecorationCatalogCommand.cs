using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using SAB.InteriorElevations.Services.Selection;

namespace SAB.InteriorElevations.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ConfigureElevationDecorationCatalogCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;
                if (uiDocument == null || uiDocument.Document == null)
                {
                    return Result.Cancelled;
                }

                new ElevationDecorationCatalogWorkflowService().Run(uiDocument);
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (System.Exception exception)
            {
                message = exception.Message;
                ToastNotifier.ShowError(
                    "SAB Каталог оформления",
                    "Не удалось изменить каталог: " + exception.Message);
                return Result.Failed;
            }
        }
    }
}
