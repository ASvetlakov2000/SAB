using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SAB.ViewTemplateGraphics.Models;
using SAB.ViewTemplateGraphics.Services;
using SAB.ViewTemplateGraphics.ViewModels;
using SAB.ViewTemplateGraphics.Views;

namespace SAB.ViewTemplateGraphics.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class EditViewTemplateGraphicsCommand : IExternalCommand
    {
        private const string CommandTitle = "SAB Пакетное редактирование шаблонов видов";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string executionStep = "Инициализация команды";
            try
            {
                executionStep = "Проверка активного документа";
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

                executionStep = "Чтение списка шаблонов видов";
                ViewTemplateGraphicsDataService dataService = new ViewTemplateGraphicsDataService();
                List<TemplateSelectionItem> templates = dataService.GetViewTemplates(document);
                if (templates.Count == 0)
                {
                    TaskDialog.Show(CommandTitle, "В активном документе не найдено ни одного шаблона вида.");
                    return Result.Cancelled;
                }

                executionStep = "Подготовка данных окна";
                ViewTemplateGraphicsViewModel viewModel = new ViewTemplateGraphicsViewModel(document, templates, dataService);
                ViewTemplateGraphicsWindow window = new ViewTemplateGraphicsWindow(viewModel);
                PrepareWindowForRevit(window, uiApplication);

                executionStep = "Работа пользователя в окне";
                bool? dialogResult = window.ShowDialog();
                if (dialogResult != true)
                {
                    return Result.Cancelled;
                }

                executionStep = "Применение настроек к шаблонам";
                ViewTemplateGraphicsApplyService applyService = new ViewTemplateGraphicsApplyService();
                ApplyViewTemplateGraphicsResult result = applyService.Apply(
                    document,
                    window.GraphicsData,
                    window.TargetTemplateIdValues);

                executionStep = "Формирование итогового отчёта";
                ShowResult(result);
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (InvalidOperationException exception)
            {
                message = exception.Message;
                TaskDialog.Show(
                    CommandTitle,
                    "Шаг выполнения: " + executionStep + "\n\n" + exception.Message + "\n\nВсе изменения отменены.");
                return Result.Failed;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show(
                    CommandTitle,
                    "Шаг выполнения: " + executionStep + "\n\nПроизошла непредвиденная ошибка.\n\n" + exception + "\n\nВсе изменения отменены.");
                return Result.Failed;
            }
        }

        private static void PrepareWindowForRevit(ViewTemplateGraphicsWindow window, UIApplication uiApplication)
        {
            if (window == null)
            {
                return;
            }

            window.ShowInTaskbar = false;
            if (uiApplication != null && uiApplication.MainWindowHandle != IntPtr.Zero)
            {
                WindowInteropHelper helper = new WindowInteropHelper(window);
                helper.Owner = uiApplication.MainWindowHandle;
            }
        }

        private static void ShowResult(ApplyViewTemplateGraphicsResult result)
        {
            if (result == null)
            {
                return;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Настройки успешно применены.");
            builder.AppendLine();
            builder.AppendLine("Обработано шаблонов: " + result.ProcessedTemplateCount);
            builder.AppendLine("Изменено параметров: " + result.ChangedSettingCount);

            if (result.Warnings.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Предупреждения:");
                int warningCountToShow = Math.Min(result.Warnings.Count, 12);
                for (int i = 0; i < warningCountToShow; i++)
                {
                    builder.AppendLine("- " + result.Warnings[i]);
                }

                if (result.Warnings.Count > warningCountToShow)
                {
                    builder.AppendLine("- И ещё предупреждений: " + (result.Warnings.Count - warningCountToShow));
                }
            }

            TaskDialog.Show(CommandTitle, builder.ToString().TrimEnd());
        }

    }
}
