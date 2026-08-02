using System.Text;
using Helpers.Notifications.ToastNotifications;
using SAB.DoorWindowExplanations.Models;

namespace SAB.DoorWindowExplanations.Services.Reports
{
    public class DoorWindowCreationReportService
    {
        public void ShowFinalReport(
            int selectedElementsCount,
            DoorWindowBatchCreationResult creationResult)
        {
            int createdViewsCount = creationResult != null
                ? creationResult.ViewGroups.Count * 3
                : 0;

            StringBuilder reportBuilder = new StringBuilder();
            reportBuilder.AppendLine("Отчет SAB по созданию экспликаций дверей и окон");
            reportBuilder.AppendLine();
            reportBuilder.AppendLine("Выбрано элементов: " + selectedElementsCount);
            reportBuilder.AppendLine("Создано видов: " + createdViewsCount);

            if (creationResult != null && creationResult.Sheet != null)
            {
                reportBuilder.AppendLine(
                    "Создан лист: " + creationResult.Sheet.SheetNumber + " | " + creationResult.Sheet.Name);
                reportBuilder.AppendLine("Размещено видовых экранов: " + createdViewsCount);
            }
            else
            {
                reportBuilder.AppendLine("Лист: не создан");
                reportBuilder.AppendLine("Размещено видовых экранов: 0");
            }

            if (createdViewsCount > 0)
            {
                ToastNotifier.ShowSuccess(
                    "SAB Экспликации дверей и окон",
                    reportBuilder.ToString(),
                    15);
            }
            else
            {
                ToastNotifier.ShowWarning(
                    "SAB Экспликации дверей и окон",
                    reportBuilder.ToString(),
                    15);
            }
        }
    }
}
