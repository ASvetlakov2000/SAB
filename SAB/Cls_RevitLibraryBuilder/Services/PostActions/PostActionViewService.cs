using Autodesk.Revit.DB;

using SAB.Cls_RevitLibraryBuilder.UI.Dialogs;
using Services.Views;

namespace RevitLibraryBuilder.Services.PostActions
{
    /// <summary>
    /// Единый post-action: вызывается после всех команд размещения
    /// </summary>
    public static class PostActionViewService
    {
        public static void AskAndCreateView(Document doc)
        {
            // ------------------------------------------------------------
            // 1. Диалог пользователя
            // ------------------------------------------------------------
            bool createView = ViewCreationDialogService.Ask("Результат размещения");

            if (!createView)
                return;

            // ------------------------------------------------------------
            // 2. Создание вида
            // ------------------------------------------------------------
            using (Transaction t = new Transaction(doc, "Create View"))
            {
                t.Start();

                var service = new FloorPlanViewService();
                service.Create(doc, "Результат размещения");

                t.Commit();
            }

            // ------------------------------------------------------------
            // 3. Уведомление
            // ------------------------------------------------------------
            Helpers.Notifications.ToastNotifications.ToastNotifier
                .ShowSuccess("Готово", "Создан вид результата размещения", 5);
        }
    }
}