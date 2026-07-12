using System;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Models;
using RevitLibraryBuilder.Services;
using SAB.BimDashboard.Models;
using SAB.BimDashboard.Services;
using SAB.Cls_RevitLibraryBuilder.UI;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Opens the unified Library Builder window and executes the selected operation.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class OpenLibraryBuilderCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            const string commandTitle = "SAB Библиотека";

            try
            {
                UIApplication uiApplication = commandData != null ? commandData.Application : null;
                UIDocument uiDocument = uiApplication != null ? uiApplication.ActiveUIDocument : null;

                if (uiDocument == null)
                {
                    message = "Активный UIDocument недоступен.";
                    TaskDialog.Show(commandTitle, message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null)
                {
                    message = "Документ недоступен.";
                    TaskDialog.Show(commandTitle, message);
                    return Result.Failed;
                }

                View activeView = document.ActiveView;

                if (activeView == null)
                {
                    message = "Активный вид недоступен.";
                    TaskDialog.Show(commandTitle, message);
                    return Result.Failed;
                }

                int selectedElementsCount = uiDocument.Selection.GetElementIds().Count;
                bool isLegendView = activeView.ViewType == ViewType.Legend;
                bool isDraftingView = activeView.ViewType == ViewType.DraftingView;

                LibraryBuilderWindow window = new LibraryBuilderWindow(
                    document.Title,
                    activeView.Name,
                    selectedElementsCount,
                    isLegendView,
                    isDraftingView);

                AttachRevitOwner(window);

                bool? dialogResult = window.ShowDialog();

                if (dialogResult != true || !window.SelectedToolId.HasValue)
                {
                    return Result.Cancelled;
                }

                if (window.SelectedToolId.Value == LibraryToolId.GenerateDashboard)
                {
                    DashboardLaunchService dashboardLaunchService = new DashboardLaunchService();
                    DashboardLaunchResult dashboardResult = dashboardLaunchService.Launch(
                        commandData.Application,
                        window.DashboardRequest);

                    ShowDashboardWarnings(dashboardResult);
                    return Result.Succeeded;
                }

                // The window is closed before file dialogs, view switching, and Revit API work begin.
                LibraryCommandDispatcher dispatcher = new LibraryCommandDispatcher();
                return dispatcher.Execute(window.SelectedToolId.Value, commandData, ref message, elements);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show(commandTitle, exception.ToString());
                return Result.Failed;
            }
        }

        private static void ShowDashboardWarnings(DashboardLaunchResult result)
        {
            if (result == null || result.Warnings == null || result.Warnings.Count == 0)
            {
                return;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Просмотрщик открыт с предупреждениями по набору данных.");
            builder.AppendLine("Загружено CSV: " + result.LoadedCsvFilesCount);
            builder.AppendLine("Строк данных: " + result.LoadedRecordsCount);
            builder.AppendLine();

            for (int i = 0; i < result.Warnings.Count; i++)
            {
                builder.AppendLine("- " + result.Warnings[i]);
            }

            TaskDialog.Show("SAB Библиотека — просмотрщик", builder.ToString().Trim());
        }

        // Block responsible for making the Library Builder window owned by Revit.
        private static void AttachRevitOwner(Window window)
        {
            if (window == null)
            {
                return;
            }

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
