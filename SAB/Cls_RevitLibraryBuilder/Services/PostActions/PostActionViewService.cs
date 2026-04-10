using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SAB.Cls_RevitLibraryBuilder.UI.Dialogs;
using System;
using Services.Views;

namespace RevitLibraryBuilder.Services.PostActions
{
    public static class PostActionViewService
    {
        // ------------------------------------------------------------
        // СТАРАЯ ВЕРСИЯ (для совместимости)
        // ------------------------------------------------------------
        public static void AskAndCreateView(Document doc)
        {
            Category fallback = doc.Settings.Categories.get_Item(BuiltInCategory.OST_GenericModel);
            AskAndCreateView(doc, fallback);
        }

        // ------------------------------------------------------------
        // ОСНОВНАЯ ВЕРСИЯ
        // ------------------------------------------------------------
        public static void AskAndCreateView(Document doc, Category category)
        {
            try
            {
                if (doc == null)
                    return;

                string categoryName = category?.Name ?? "БезКатегории";
                string viewName = $"Категория_{categoryName}";

                bool createView = ViewCreationDialogService.Ask(viewName);

                if (!createView)
                    return;

                using (Transaction t = new Transaction(doc, "Create View"))
                {
                    t.Start();

                    var service = new FloorPlanViewService();
                    service.Create(doc, viewName);

                    t.Commit();
                }

                Helpers.Notifications.ToastNotifications.ToastNotifier
                    .ShowSuccess("Готово", $"Вид '{viewName}' создан", 5);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("PostAction", ex.Message);
            }
        }
    }
}