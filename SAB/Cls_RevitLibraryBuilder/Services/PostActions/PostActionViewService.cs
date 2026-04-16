using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services.Views;
using SAB.Cls_RevitLibraryBuilder.UI.Dialogs;

namespace RevitLibraryBuilder.Services.PostActions
{
    /// <summary>
    /// Unified post-action service for placement commands.
    /// </summary>
    public static class PostActionViewService
    {
        public static void RunAfterPlacement(
            Document document,
            string categoryNameFromCsv,
            int placedCount,
            string commandName)
        {
            RunAfterPlacement(document, categoryNameFromCsv, placedCount, commandName, null, null, 0);
        }

        public static void RunAfterPlacement(
            Document document,
            string categoryNameFromCsv,
            int placedCount,
            string commandName,
            string sourceCsvFilePath,
            string typeNameOriginal,
            int rowIndex)
        {
            if (document == null)
            {
                TaskDialog.Show("Создание вида", "Документ недоступен.");
                return;
            }

            if (placedCount <= 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(categoryNameFromCsv))
            {
                TaskDialog.Show("Создание вида", "В файле отсутствует значение категории.");
                return;
            }

            View sourceView = document.ActiveView;

            if (sourceView == null)
            {
                TaskDialog.Show("Создание вида", "Активный вид недоступен.");
                return;
            }

            try
            {
                string generatedViewNamePreview = FloorPlanViewService.BuildViewNameByCategory(categoryNameFromCsv);
                bool createView = ViewCreationDialogService.Ask(generatedViewNamePreview);

                if (!createView)
                {
                    return;
                }

                CreateByCategory(
                    document,
                    categoryNameFromCsv,
                    sourceView,
                    sourceCsvFilePath,
                    typeNameOriginal,
                    rowIndex);
            }
            catch (Exception exception)
            {
                TaskDialog.Show("Создание вида", exception.ToString());
            }
        }

        public static ViewPlan CreateByCategory(
            Document document,
            string categoryNameFromCsv,
            View sourceView)
        {
            return CreateByCategory(document, categoryNameFromCsv, sourceView, null, null, 0);
        }

        public static ViewPlan CreateByCategory(
            Document document,
            string categoryNameFromCsv,
            View sourceView,
            string sourceCsvFilePath,
            string typeNameOriginal,
            int rowIndex)
        {
            if (document == null)
            {
                TaskDialog.Show("Создание вида", "Документ недоступен.");
                return null;
            }

            if (sourceView == null)
            {
                sourceView = document.ActiveView;
            }

            if (sourceView == null)
            {
                TaskDialog.Show("Создание вида", "Исходный вид недоступен.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(categoryNameFromCsv))
            {
                TaskDialog.Show("Создание вида", "В файле отсутствует значение категории.");
                return null;
            }

            try
            {
                ViewPlan createdView;

                using (Transaction transaction = new Transaction(document, "Create Placement Result View"))
                {
                    transaction.Start();

                    FloorPlanViewService service = new FloorPlanViewService();

                    // Block responsible for passing category into post-action workflow
                    createdView = service.CreateByCategory(
                        document,
                        categoryNameFromCsv,
                        sourceView,
                        sourceCsvFilePath,
                        typeNameOriginal,
                        rowIndex);

                    if (createdView == null)
                    {
                        transaction.RollBack();
                        TaskDialog.Show("Создание вида", "Не удалось создать вид плана этажа.");
                        return null;
                    }

                    transaction.Commit();
                }

                ShowSuccessNotification(
                    "Создание вида",
                    "Вид для категории " + categoryNameFromCsv + " создан");

                return createdView;
            }
            catch (Exception exception)
            {
                TaskDialog.Show("Создание вида", exception.ToString());
                return null;
            }
        }

        // Block responsible for post-execution notification with TaskDialog fallback
        private static void ShowSuccessNotification(string title, string message)
        {
            try
            {
                ToastNotifier.ShowSuccess(title, message, 10);
            }
            catch
            {
                TaskDialog.Show(title, message);
            }
        }
    }
}
