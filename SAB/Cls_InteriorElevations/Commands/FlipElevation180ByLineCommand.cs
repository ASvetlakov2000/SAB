using System;
using System.Collections.Generic;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Elevations;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class FlipElevation180ByLineCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;
                if (uiDocument == null || uiDocument.Document == null)
                {
                    ToastNotifier.ShowError("SAB Развертки", "Не удалось получить активный документ Revit.");
                    return Result.Failed;
                }

                Document document = uiDocument.Document;
                View activeView = document.ActiveView;
                if (activeView == null || (activeView.ViewType != ViewType.FloorPlan && activeView.ViewType != ViewType.CeilingPlan))
                {
                    ToastNotifier.ShowWarning(
                        "SAB Развертки",
                        "Команда работает только с активного Плана этажа/Плана потолка. " +
                        "Откройте план, где видны обозначения фасадов, и запустите команду снова.");
                    return Result.Cancelled;
                }

                ElevationFlipService flipService = new ElevationFlipService();

                ToastNotifier.ShowInfo(
                    "SAB Развертки",
                    "Выберите некорректный фасад на активном плане (не на листе).");

                ViewSection targetElevationView;
                Viewport targetViewport;
                string viewSelectionError;
                if (!flipService.TryPickElevationTargetOnPlan(
                    uiDocument,
                    activeView,
                    out targetElevationView,
                    out targetViewport,
                    out viewSelectionError))
                {
                    if (!string.IsNullOrWhiteSpace(viewSelectionError))
                    {
                        ToastNotifier.ShowWarning("SAB Развертки", viewSelectionError);
                    }

                    return Result.Cancelled;
                }

                ToastNotifier.ShowInfo(
                    "SAB Развертки",
                    "Выберите линию детализации, по которой создавалась выбранная развертка.");

                DetailLine sourceDetailLine;
                string lineSelectionError;
                if (!flipService.TryPickSourceDetailLine(uiDocument, out sourceDetailLine, out lineSelectionError))
                {
                    if (!string.IsNullOrWhiteSpace(lineSelectionError))
                    {
                        ToastNotifier.ShowWarning("SAB Развертки", lineSelectionError);
                    }

                    return Result.Cancelled;
                }

                List<string> warnings = new List<string>();
                ElevationFlipResult flipResult;

                using (Transaction transaction = new Transaction(document, "SAB Разворот развертки на 180"))
                {
                    transaction.Start();

                    flipResult = flipService.FlipElevationBySourceLine(
                        document,
                        activeView,
                        targetElevationView,
                        sourceDetailLine,
                        targetViewport,
                        warnings);

                    if (flipResult != null && flipResult.IsSuccess)
                    {
                        transaction.Commit();
                    }
                    else
                    {
                        transaction.RollBack();
                    }
                }

                if (flipResult == null)
                {
                    ToastNotifier.ShowError("SAB Развертки", "Команда завершилась без результата.");
                    return Result.Failed;
                }

                if (!flipResult.IsSuccess)
                {
                    string failText = BuildReportText(flipResult, warnings);
                    ToastNotifier.ShowWarning("SAB Развертки", failText, 12);
                    return Result.Cancelled;
                }

                string successText = BuildReportText(flipResult, warnings);
                ToastNotifier.ShowSuccess("SAB Развертки", successText, 12);
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ToastNotifier.ShowError("SAB Развертки", "Ошибка разворота фасада: " + exception.Message);
                return Result.Failed;
            }
        }

        private string BuildReportText(ElevationFlipResult result, IList<string> warnings)
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("Отчет по развороту фасада");
            report.AppendLine();

            if (result.SourceViewId != null && result.SourceViewId != ElementId.InvalidElementId)
            {
                report.AppendLine("Исходный вид Id: " + RevitElementIdUtils.GetElementIdValue(result.SourceViewId));
            }

            if (result.ResultViewId != null && result.ResultViewId != ElementId.InvalidElementId)
            {
                report.AppendLine("Результирующий вид Id: " + RevitElementIdUtils.GetElementIdValue(result.ResultViewId));
            }

            if (!string.IsNullOrWhiteSpace(result.ResultViewName))
            {
                report.AppendLine("Имя результирующего вида: " + result.ResultViewName);
            }

            report.AppendLine("Угол разворота (град): " + result.RotationAngleDegrees.ToString("F2"));
            report.AppendLine("Исходный вид на листе: " + (result.IsSourcePlacedOnSheet ? "Да" : "Нет"));

            if (result.IsSourcePlacedOnSheet)
            {
                report.AppendLine("Лист: " + result.SheetNumber + " | " + result.SheetName);
            }

            report.AppendLine();
            report.AppendLine(result.Message ?? string.Empty);

            if (warnings != null && warnings.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("Предупреждения: " + warnings.Count);
                report.AppendLine("Подробности сохранены во внутреннем журнале выполнения команды.");
            }

            return report.ToString();
        }
    }
}
