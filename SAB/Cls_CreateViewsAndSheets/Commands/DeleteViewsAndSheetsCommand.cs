using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace SAB.CreateViewsAndSheets.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class DeleteViewsAndSheetsCommand : IExternalCommand
    {
        private const string CommandTitle = "SAB Удаление видов и листов";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string debugStep = "Старт команды";
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                debugStep = "Получение активного документа";
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

                debugStep = "Сбор листов и размещенных видов";
                RevitDataService dataService = new RevitDataService();
                List<SheetDeletionItem> deletionItems = dataService.GetSheetDeletionItems(document, null);
                if (deletionItems.Count == 0)
                {
                    TaskDialog.Show(CommandTitle, "В проекте не найдены листы для удаления.");
                    return Result.Cancelled;
                }

                debugStep = "Создание модели окна";
                DeleteViewsAndSheetsViewModel viewModel = new DeleteViewsAndSheetsViewModel(deletionItems);
                DeleteViewsAndSheetsWindow window = new DeleteViewsAndSheetsWindow(viewModel);
                PrepareWindowForRevit(window);

                debugStep = "Открытие окна";
                bool dialogAccepted = window.ShowDialog() == true;
                if (!dialogAccepted)
                {
                    return Result.Cancelled;
                }

                debugStep = "Подготовка списка удаления";
                List<SheetDeletionItem> selectedItems;
                string validationMessage;
                if (!viewModel.TryBuildDeleteRequest(out selectedItems, out validationMessage))
                {
                    TaskDialog.Show(CommandTitle, validationMessage);
                    return Result.Cancelled;
                }

                if (!ConfirmDeletion(selectedItems))
                {
                    return Result.Cancelled;
                }

                debugStep = "Удаление листов и видов";
                SheetDeletionService deletionService = new SheetDeletionService();
                SheetDeletionResult result = deletionService.Execute(document, selectedItems);

                debugStep = "Показ результата";
                ShowDeletionReport(result);
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show(
                    CommandTitle,
                    "Ошибка при удалении видов и листов.\n\n" +
                    "Шаг: " + debugStep + "\n" +
                    "Время: " + stopwatch.ElapsedMilliseconds + " мс\n\n" +
                    exception);
                return Result.Failed;
            }
        }

        private bool ConfirmDeletion(IList<SheetDeletionItem> selectedItems)
        {
            int sheetsCount = selectedItems != null ? selectedItems.Count : 0;
            int viewsCount = 0;
            if (selectedItems != null)
            {
                for (int i = 0; i < selectedItems.Count; i++)
                {
                    SheetDeletionItem item = selectedItems[i];
                    if (item != null && item.PlacedViewIds != null)
                    {
                        viewsCount += item.PlacedViewIds.Count;
                    }
                }
            }

            TaskDialog dialog = new TaskDialog(CommandTitle);
            dialog.MainInstruction = "Удалить выбранные листы и размещенные на них виды?";
            dialog.MainContent =
                "Листов: " + sheetsCount + "\n" +
                "Видов: " + viewsCount + "\n\n" +
                "Будут удалены сами виды из проекта, а не только их размещение на листах.";
            dialog.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
            dialog.DefaultButton = TaskDialogResult.No;
            return dialog.Show() == TaskDialogResult.Yes;
        }

        private void ShowDeletionReport(SheetDeletionResult result)
        {
            int deletedSheetsCount = result != null ? result.DeletedSheetsCount : 0;
            int deletedViewsCount = result != null ? result.DeletedViewsCount : 0;
            ToastNotifier.ShowSuccess(
                CommandTitle,
                "Удалено листов: " + deletedSheetsCount + "\nУдалено видов: " + deletedViewsCount,
                10);

            if (result == null || result.Warnings == null || result.Warnings.Count == 0)
            {
                return;
            }

            TaskDialog.Show(CommandTitle, string.Join("\n", result.Warnings));
        }

        private void PrepareWindowForRevit(DeleteViewsAndSheetsWindow window)
        {
            if (window == null)
            {
                return;
            }

            window.ShowInTaskbar = false;
            window.Opacity = 0.0;
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

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
    }
}
